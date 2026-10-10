"""Herramienta `tabla.py`: una tabla con sus columnas propias, desde un
playbook. Ninguna prueba toca la red."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import tabla as tb  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA,
    SOLUTION_ID,
    armar_cliente_precondiciones_ok,
    es_ruta_solutioncomponents,
    es_ruta_solutions,
    label,
    playbook_md,
    por_idioma,
)

BORRAR = object()
TABLA = "sanic_carp_tbl_zzdemo"
META_TABLA = "44444444-4444-4444-4444-444444444444"
META_CHOICE = "55555555-5555-5555-5555-555555555555"
IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="tabla", inventario="3.1")


def col(nombre, tipo, **kw):
    base = {"nombre": nombre, "displayname": por_idioma(nombre.split("_")[1].title()), "descripcion": f"Descripción de {nombre}.",
            "tipo": tipo, "requerida": False, "protegida": False, "auditoria": False}
    base.update(kw)
    return base


# Un playbook con TODOS los tipos que admite el formato.
COMPLETO = {
    "tipo": "tabla",
    "nombre": TABLA,
    "displayname": por_idioma("Demo"),
    "displayname_plural": por_idioma("Demos"),
    "descripcion": "Tabla de demostración.",
    "propiedad": "usuario",
    "notas": False,
    "actividades": False,
    "auditoria": False,
    "primaria": {"nombre": "sanic_nombre", "displayname": por_idioma("Nombre"), "descripcion": "Nombre del registro.",
                 "largo": 200, "requerida": True, "autonumerico": ""},
    "columnas": [
        col("sanic_numeroproducto", "texto", largo=30, requerida=True),
        col("sanic_comentarios", "memo", largo=500),
        col("sanic_plazo", "entero", minimo=1, maximo=60),
        col("sanic_moneda", "choice", choice="sanic_carp_ch_moneda"),
        col("sanic_correonuevoenfinalizada", "sino", etiqueta_si=por_idioma("Si"), etiqueta_no=por_idioma("No"), defecto=False),
        col("sanic_fechainicio", "fecha"),
        col("sanic_fechacongelamiento", "fechahora"),
        col("sanic_archivo", "archivo", tamano_kb=5120),
        col("sanic_folio", "autonumerico", largo=100, formato="ZZ-{SEQNUM:8}"),
    ],
}
# El mínimo: solo texto (lo único verificado contra la plataforma al nacer la herramienta).
SIMPLE = dict(copy.deepcopy(COMPLETO), columnas=[col("sanic_numeroproducto", "texto", largo=30, requerida=True)])

TODO_VERIFICADO = frozenset(tb.CARACTERISTICAS)


def con_cambio(obj, ruta, valor):
    nuevo = copy.deepcopy(obj)
    if not ruta:
        return valor
    nodo = nuevo
    for paso in ruta[:-1]:
        nodo = nodo[paso]
    if valor is BORRAR:
        del nodo[ruta[-1]]
    else:
        nodo[ruta[-1]] = valor
    return nuevo


# ---------------------------------------------------------------------------
# Respuestas simuladas del entorno, derivadas del playbook
# ---------------------------------------------------------------------------
def rl(valor):
    return {"Value": valor, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}


def aud(valor):
    return {"Value": valor, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"}


TIPO_PLATAFORMA = {"texto": "StringType", "autonumerico": "StringType", "memo": "MemoType", "entero": "IntegerType",
                   "choice": "PicklistType", "sino": "BooleanType", "fecha": "DateTimeType", "fechahora": "DateTimeType",
                   "archivo": "FileType", "decimal": "DecimalType"}


def tipo_plataforma(c):
    """Una fórmula es del tipo de su resultado (SourceType la distingue), no de un tipo propio."""
    if c.get("tipo") == "formula":
        return "DecimalType" if c["resultado"] == "decimal" else "IntegerType"
    return TIPO_PLATAFORMA[c.get("tipo", "texto")]


def cuerpo_tabla(datos):
    return {"MetadataId": META_TABLA, "LogicalName": datos["nombre"], "SchemaName": datos["nombre"],
            "OwnershipType": "UserOwned", "HasNotes": datos["notas"], "HasActivities": datos["actividades"],
            "IsAuditEnabled": aud(datos["auditoria"]), "IsManaged": False, "IsCustomEntity": True,
            "PrimaryNameAttribute": datos["primaria"]["nombre"], "DisplayName": label(datos["displayname"]),
            "DisplayCollectionName": label(datos["displayname_plural"]), "Description": label(datos["descripcion"])}


def fila_generica(c, primaria=False):
    return {"LogicalName": c["nombre"], "SchemaName": c["nombre"], "AttributeOf": None, "IsCustomAttribute": True,
            "IsPrimaryName": primaria, "AttributeTypeName": {"Value": tipo_plataforma(c)},
            "RequiredLevel": rl("ApplicationRequired" if c["requerida"] else "None"),
            "IsSecured": c.get("protegida", False), "IsAuditEnabled": aud(c.get("auditoria", False)),
            "DisplayName": label(c["displayname"]), "Description": label(c["descripcion"])}


def genericas_de(datos):
    """Lo que devolvería `GET …/Attributes?$select=…` si el entorno tuviera exactamente lo que pide el playbook. La primaria hereda
    la auditoría de la tabla."""
    return ([fila_generica(dict(datos["primaria"], auditoria=datos["auditoria"]), True)] + [fila_generica(c) for c in datos["columnas"]] + SISTEMA)


def filas_por_tipo(datos):
    p = datos["primaria"]
    por = {"String": [{"LogicalName": p["nombre"], "MaxLength": p["largo"], "FormatName": {"Value": "Text"},
                       "AutoNumberFormat": p["autonumerico"] or None}]}
    for c in datos["columnas"]:
        n, t = c["nombre"], c["tipo"]
        if t in ("texto", "autonumerico"):
            por.setdefault("String", []).append({"LogicalName": n, "MaxLength": c["largo"], "FormatName": {"Value": "Text"},
                                                 "AutoNumberFormat": c.get("formato") or None})
        elif t == "memo":
            por.setdefault("Memo", []).append({"LogicalName": n, "MaxLength": c["largo"]})
        elif t == "entero":
            por.setdefault("Integer", []).append({"LogicalName": n, "MinValue": c["minimo"], "MaxValue": c["maximo"]})
        elif t == "decimal":
            por.setdefault("Decimal", []).append({"LogicalName": n, "Precision": c["precision"], "MinValue": float(c["minimo"]), "MaxValue": float(c["maximo"])})
        elif t == "choice":
            por.setdefault("Picklist", []).append({"LogicalName": n, "GlobalOptionSet": {"Name": c["choice"], "IsGlobal": True}})
        elif t == "sino":
            por.setdefault("Boolean", []).append({"LogicalName": n, "DefaultValue": c["defecto"], "OptionSet": {
                "DisplayName": label(c["displayname"]),  # la plataforma lo copia del nombre de la columna al crearla
                "TrueOption": {"Value": 1, "Label": label(c["etiqueta_si"])},
                "FalseOption": {"Value": 0, "Label": label(c["etiqueta_no"])}}})
        elif t in ("fecha", "fechahora"):
            por.setdefault("DateTime", []).append({"LogicalName": n, "Format": "DateOnly" if t == "fecha" else "DateAndTime",
                                                   "DateTimeBehavior": {"Value": "DateOnly" if t == "fecha" else "UserLocal"}})
        elif t == "archivo":
            por.setdefault("File", []).append({"LogicalName": n, "MaxSizeInKB": c["tamano_kb"]})
    return por


SISTEMA = [
    {"LogicalName": "createdon", "SchemaName": "CreatedOn", "AttributeOf": None, "IsCustomAttribute": False, "IsPrimaryName": False,
     "AttributeTypeName": {"Value": "DateTimeType"}, "RequiredLevel": rl("None"), "IsSecured": False, "IsAuditEnabled": aud(False),
     "DisplayName": label("Created On"), "Description": label("")},
    {"LogicalName": TABLA + "id", "SchemaName": TABLA + "Id", "AttributeOf": None, "IsCustomAttribute": True, "IsPrimaryName": False,
     "AttributeTypeName": {"Value": "UniqueidentifierType"}, "RequiredLevel": rl("SystemRequired"), "IsSecured": False,
     "IsAuditEnabled": aud(False), "DisplayName": label("Demo"), "Description": label("")},
]


def cuerpo_formula(c, **cambios):
    """Lo que devuelve `GET …/Attributes(LogicalName='<fórmula>')?$select=…` de una fórmula bien creada (observado en el spike 2:
    SourceType 3, SourceTypeMask 1, IsValidForCreate/Update false). La forma de FormulaDefinition de una columna creada en el
    editor de Power Apps (ensayo del 2026-10-09) es EXACTAMENTE la expresión tipeada, sin `=` ni envoltorio: el doble la repite."""
    cuerpo = {"LogicalName": c["nombre"], "AttributeTypeName": {"Value": tipo_plataforma(c)}, "SourceType": 3, "SourceTypeMask": 1,
              "FormulaDefinition": c["expresion"], "IsValidForCreate": False, "IsValidForUpdate": False, "IsSecured": False,
              "IsAuditEnabled": aud(False), "RequiredLevel": rl("None"), "DisplayName": label(c["displayname"]), "Description": label(c["descripcion"])}
    cuerpo.update(cambios)
    return cuerpo


def opciones_status(datos):
    """Opciones de `statuscode` tal como las dejaría el entorno si tuviera exactamente lo que pide el playbook."""
    motivos = datos.get("motivos_estado")
    if motivos is None:
        return [{"Value": 1, "State": 0, "Label": label("Active")}, {"Value": 2, "State": 1, "Label": label("Inactive")}]
    return [{"Value": m["valor"], "State": m["statecode"], "Label": label(m["etiqueta"])} for m in motivos]


def cuerpo_status(datos, opciones=None):
    return {"LogicalName": "statuscode", "OptionSet": {"Options": opciones_status(datos) if opciones is None else opciones}}


def cuerpo_state(opciones=None):
    return {"LogicalName": "statecode", "OptionSet": {"Options": [{"Value": 0, "Label": label("Active")}, {"Value": 1, "Label": label("Inactive")}]
                                                       if opciones is None else opciones}}


def armar(cliente, datos, existe=True, genericas=None, por_tipo=None, tabla=None, componentes=None, formulas=None, status=None, state=None):
    """Configura TODAS las rutas del camino feliz (si no, el error del doble
    por 'ruta sin regla' haría pasar en falso las pruebas que esperan error).
    `formulas`: {columna: cuerpo} para pisar lo que devuelve la lectura de una fórmula;
    `status` / `state`: cuerpos completos de las lecturas de `statuscode` / `statecode`."""
    armar_cliente_precondiciones_ok(cliente, IDENT)
    cliente.responder("GET", lambda r: r.startswith("GlobalOptionSetDefinitions(Name="),
                      (200, {"MetadataId": META_CHOICE, "Name": "sanic_carp_ch_moneda", "IsGlobal": True}, {}))
    estado = {"existe": existe}
    cliente.responder("GET", lambda r: r.startswith(f"EntityDefinitions(LogicalName='{datos['nombre']}')?"),
                      lambda *_: (200, tabla if tabla is not None else cuerpo_tabla(datos), {}) if estado["existe"] else (404, {"error": {"message": "no existe"}}, {}))
    gen = genericas if genericas is not None else genericas_de(datos)
    cliente.responder("GET", lambda r: "/Attributes?" in r, (200, {"value": gen}, {}))
    pt = por_tipo if por_tipo is not None else filas_por_tipo(datos)
    for t in ("String", "Memo", "Integer", "Decimal", "Picklist", "Boolean", "DateTime", "File"):
        cliente.responder("GET", lambda r, t=t: f"/Attributes/Microsoft.Dynamics.CRM.{t}AttributeMetadata" in r, (200, {"value": pt.get(t, [])}, {}))
    for c in datos["columnas"]:
        if c["tipo"] != "formula":
            continue
        ruta_f = f"EntityDefinitions(LogicalName='{datos['nombre']}')/Attributes(LogicalName='{c['nombre']}')"
        # Como la plataforma (Dev, 2026-10-09): SourceTypeMask solo existe en el tipo concreto; sin el cast, 400.
        cast_f = {"decimal": "Decimal", "entero": "Integer"}[c["resultado"]]
        cliente.responder("GET", lambda r, ruta_f=ruta_f: r.startswith(ruta_f + "?") and "SourceTypeMask" in r,
                          (400, {"error": "Could not find a property named 'SourceTypeMask' on type 'Microsoft.Dynamics.CRM.AttributeMetadata'."}, {}))
        cliente.responder("GET", lambda r, ruta_f=ruta_f, cast_f=cast_f: r.startswith(f"{ruta_f}/Microsoft.Dynamics.CRM.{cast_f}AttributeMetadata?") and "SourceTypeMask" in r,
                          (200, (formulas or {}).get(c["nombre"], cuerpo_formula(c)), {}))
        cliente.responder("GET", lambda r, ruta_f=ruta_f: r.startswith(ruta_f + "/Microsoft.Dynamics.CRM.DecimalAttributeMetadata"),
                          (200, {"LogicalName": c["nombre"], "Precision": c.get("precision")}, {}))
    ruta_t = f"EntityDefinitions(LogicalName='{datos['nombre']}')/Attributes"
    cliente.responder("GET", lambda r: r.startswith(ruta_t + "(LogicalName='statuscode')/Microsoft.Dynamics.CRM.StatusAttributeMetadata"),
                      (200, status if status is not None else cuerpo_status(datos), {}))
    cliente.responder("GET", lambda r: r.startswith(ruta_t + "(LogicalName='statecode')/Microsoft.Dynamics.CRM.StateAttributeMetadata"),
                      (200, state if state is not None else cuerpo_state(), {}))
    cliente.responder("POST", "InsertStatusValue", lambda ruta, cuerpo, solucion: (200, {"NewOptionValue": cuerpo["Value"]}, {}))
    cliente.responder("POST", "UpdateOptionValue", (204, None, {}))
    cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": componentes if componentes is not None else [{"solutioncomponentid": "x"}]}, {}))

    def al_crear(ruta, cuerpo, solucion):
        estado["existe"] = True
        return (204, None, {})

    cliente.responder("POST", "EntityDefinitions", al_crear)
    cliente.responder("POST", f"EntityDefinitions(LogicalName='{datos['nombre']}')/Attributes", (204, None, {}))
    ruta_prim = f"EntityDefinitions(LogicalName='{datos['nombre']}')/Attributes(LogicalName='{datos['primaria']['nombre']}')"
    # La plataforma crea la primaria con SUS valores (850, opcional, auditada) y devuelve el cast sin '@odata.type'.
    cliente.responder("GET", ruta_prim + "/Microsoft.Dynamics.CRM.StringAttributeMetadata",
                      (200, {"LogicalName": datos["primaria"]["nombre"], "SchemaName": datos["primaria"]["nombre"], "MaxLength": 850,
                             "RequiredLevel": rl("None"), "FormatName": {"Value": "Text"}, "IsAuditEnabled": aud(True),
                             "DisplayName": label(datos["primaria"]["displayname"])}, {}))
    cliente.responder("PUT", ruta_prim, (204, None, {}))
    cliente.responder("POST", "PublishXml", (204, None, {}))
    return cliente


class Base(unittest.TestCase):
    def correr(self, fabrica, datos, solo_verificar=False, verificadas=TODO_VERIFICADO, permitir=False, corregir_primaria=False, publicar=False, corregir_nombres=False, **otros):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "playbook.md")
            open(ruta, "w", encoding="utf-8").write(playbook_md(IDENT, datos))
            return tb.construir(ruta, solo_verificar, fabrica, verificadas=verificadas, permitir_no_verificadas=permitir,
                                corregir_primaria=corregir_primaria, publicar=publicar, corregir_nombres=corregir_nombres, **otros)

    def con_cliente(self, cliente, datos, **kw):
        return self.correr(lambda: cliente, datos, **kw)


# ---------------------------------------------------------------------------
class ValidacionSinRed(Base):
    CASOS = [
        (["tipo"], "choice-global", "tipo"),
        (["nombre"], "sanic_carp_ch_zzdemo", "nombre"),
        (["nombre"], "Sanic_carp_tbl_Demo", "nombre"),
        (["nombre"], "sanic_carp_tbl_" + "a" * 90, "95"),
        (["displayname"], "", "displayname"),
        (["displayname_plural"], BORRAR, "displayname_plural"),
        (["descripcion"], "  ", "descripcion"),
        (["propiedad"], "organizacional", "propiedad"),
        (["notas"], "no", "notas"),
        (["auditoria"], 1, "auditoria"),
        (["sobra"], True, "sobra"),
        (["primaria", "nombre"], "sanic_Nombre", "primaria.nombre"),
        (["primaria", "largo"], 0, "primaria.largo"),
        (["primaria", "largo"], "200", "primaria.largo"),
        (["primaria", "largo"], True, "primaria.largo"),
        (["primaria", "requerida"], "si", "primaria.requerida"),
        (["primaria", "autonumerico"], None, "primaria.autonumerico"),
        (["primaria", "autonumerico"], "sin marcador", "primaria.autonumerico"),
        (["columnas"], {}, "columnas"),
        (["columnas", 0], "texto", "columnas[0]"),
        (["columnas", 0, "nombre"], "otro_codigo", "columnas[0].nombre"),
        (["columnas", 0, "nombre"], "sanic_nombre", "repetid"),
        (["columnas", 0, "tipo"], "lookup", "columnas[0].tipo"),
        (["columnas", 0, "largo"], 4001, "columnas[0].largo"),
        (["columnas", 0, "largo"], BORRAR, "columnas[0]"),
        (["columnas", 0, "minimo"], 1, "columnas[0]"),
        (["columnas", 0, "requerida"], None, "columnas[0].requerida"),
        (["columnas", 0, "protegida"], "no", "columnas[0].protegida"),
        (["columnas", 2, "minimo"], 61, "columnas[2]"),
        (["columnas", 2, "maximo"], 2147483648, "columnas[2].maximo"),
        (["columnas", 3, "choice"], "sanic_otro", "columnas[3].choice"),
        (["columnas", 4, "defecto"], "false", "columnas[4].defecto"),
        (["columnas", 4, "etiqueta_si"], "", "columnas[4].etiqueta_si"),
        (["columnas", 7, "tamano_kb"], 0, "columnas[7].tamano_kb"),
        (["columnas", 7, "requerida"], True, "columnas[7]"),
        (["columnas", 8, "formato"], "ZZ-SEQNUM", "columnas[8].formato"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta} <- {'BORRAR' if valor is BORRAR else repr(valor)}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(COMPLETO, ruta, valor))
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)

    def test_el_playbook_completo_es_valido(self):
        cliente = armar(ClienteSimulado(), COMPLETO)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "ya_existia", detalle)


class NombresPorIdioma(Base):
    """Skill 0.2.0: todo nombre visible se escribe UNA VEZ POR CADA idioma de la Identidad (nunca como cadena única), sin tildes ni ñ."""

    CAMPOS = [
        (["displayname"], "'displayname'"),
        (["displayname_plural"], "'displayname_plural'"),
        (["primaria", "displayname"], "primaria.displayname"),
        (["columnas", 0, "displayname"], "columnas[0].displayname"),
        (["columnas", 4, "etiqueta_si"], "columnas[4].etiqueta_si"),
        (["columnas", 4, "etiqueta_no"], "columnas[4].etiqueta_no"),
    ]

    def test_cada_nombre_visible_rechaza_la_cadena_unica_el_idioma_que_falta_el_que_sobra_el_vacio_y_la_tilde(self):
        formas = [("cadena unica", "Demo", "objeto"), ("falta el 3082", {"1033": "Demo"}, "3082"), ("falta el 1033", {"3082": "Demo"}, "1033"),
                  ("sobra el 1046", {"1033": "Demo", "3082": "Demo", "1046": "Demo"}, "1046"), ("vacio en 3082", {"1033": "Demo", "3082": ""}, "3082"),
                  ("tilde en 3082", {"1033": "Demo", "3082": "Demó"}, "BP-PP-197"), ("tilde en 1033", {"1033": "Sí", "3082": "Si"}, "BP-PP-197")]
        for ruta, donde in self.CAMPOS:
            for nombre, valor, esperado in formas:
                with self.subTest(f"{ruta} {nombre}"):
                    centinela = FabricaCentinela()
                    estado, _, detalle = self.correr(centinela, con_cambio(COMPLETO, ruta, valor))
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn(donde, detalle)
                    self.assertIn(esperado, detalle)
                    self.assertFalse(centinela.llamada)

    def test_los_nombres_pueden_ser_distintos_en_cada_idioma(self):
        datos = con_cambio(COMPLETO, ["displayname"], {"1033": "Installment", "3082": "Cuota"})
        cliente = armar(ClienteSimulado(), datos, existe=False)
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "creado", detalle)
        post = next(l for l in cliente.llamadas if l["metodo"] == "POST" and l["ruta"] == "EntityDefinitions")
        self.assertEqual([(1033, "Installment"), (3082, "Cuota")], [(l["LanguageCode"], l["Label"]) for l in post["cuerpo"]["DisplayName"]["LocalizedLabels"]])

    def test_el_entorno_con_la_etiqueta_en_un_solo_idioma_difiere_y_nombra_el_idioma(self):
        t = tt_tabla = cuerpo_tabla(COMPLETO)
        casos = [("DisplayName[3082]", dict(t, DisplayName=label("Demo", idiomas=[1033]))),
                 ("DisplayCollectionName[1033]", dict(t, DisplayCollectionName=label("Demos", idiomas=[3082]))),
                 ("Description[3082]", dict(t, Description=label("Tabla de demostración.", idiomas=[1033]))),
                 ("DisplayName tiene etiquetas en un idioma que el playbook no declara", dict(t, DisplayName=label("Demo", otros_idiomas=[(1046, "Demo")])))]
        for esperado, tabla in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), COMPLETO, tabla=tabla)
                estado, _, detalle = self.con_cliente(cliente, COMPLETO)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_las_etiquetas_de_la_columna_y_del_si_no_se_comprueban_en_cada_idioma(self):
        gen = genericas_de(COMPLETO)
        pt = filas_por_tipo(COMPLETO)
        sino = next(i for i, f in enumerate(pt["Boolean"]) if f["LogicalName"] == "sanic_correonuevoenfinalizada")
        casos = [
            ("sanic_numeroproducto.DisplayName[3082]", dict(genericas=con_cambio(gen, [1, "DisplayName"], label("Numeroproducto", idiomas=[1033])))),
            ("sanic_numeroproducto.Description[3082]", dict(genericas=con_cambio(gen, [1, "Description"], label("Descripción de sanic_numeroproducto.", idiomas=[1033])))),
            ("sanic_correonuevoenfinalizada.etiqueta_no[3082]", dict(por_tipo=con_cambio(pt, ["Boolean", sino, "OptionSet", "FalseOption", "Label"], label("No", idiomas=[1033])))),
            ("sanic_correonuevoenfinalizada.OptionSet.DisplayName[3082]", dict(por_tipo=con_cambio(pt, ["Boolean", sino, "OptionSet", "DisplayName"], label("Correonuevoenfinalizada", idiomas=[1033])))),
        ]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), COMPLETO, **kw)
                estado, _, detalle = self.con_cliente(cliente, COMPLETO)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)

    def test_un_idioma_del_playbook_sin_provisionar_es_bloqueado_y_no_escribe(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": [1033]}, {}))
        armar(cliente, COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("3082", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_playbook_con_la_clave_lcid_de_la_version_vieja_es_error_antes_de_la_red(self):
        import json, re
        centinela = FabricaCentinela()
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "p.md")
            texto = playbook_md(IDENT, COMPLETO)
            texto = texto.replace('"idiomas": [\n    1033,\n    3082\n  ]', '"lcid": 1033')
            self.assertIn('"lcid": 1033', texto)
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(texto)
            estado, _, detalle = tb.construir(ruta, False, centinela)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("idiomas", detalle)
        self.assertFalse(centinela.llamada)


class CaracteristicasNoVerificadas(Base):
    def test_usar_algo_no_verificado_contra_la_plataforma_bloquea_sin_red(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, COMPLETO, verificadas=frozenset({"tipo:texto"}))
        self.assertEqual(estado, "bloqueado", detalle)
        for c in ("tipo:memo", "tipo:archivo", "tipo:fecha"):
            self.assertIn(c, detalle)
        self.assertFalse(centinela.llamada)

    def test_solo_texto_pasa_con_lo_minimo_verificado(self):
        cliente = armar(ClienteSimulado(), SIMPLE)
        estado, _, detalle = self.con_cliente(cliente, SIMPLE, verificadas=frozenset({"tipo:texto"}))
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_ensayo_solo_se_permite_sobre_una_tabla_descartable(self):
        real = dict(copy.deepcopy(COMPLETO), nombre="sanic_carp_tbl_cuota")
        estado, _, detalle = self.correr(FabricaCentinela(), real, verificadas=frozenset(), permitir=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("_tbl_zz", detalle)
        cliente = armar(ClienteSimulado(), COMPLETO)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO, verificadas=frozenset(), permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_las_caracteristicas_que_usa_un_playbook(self):
        usadas = tb.caracteristicas_usadas(dict(copy.deepcopy(COMPLETO), auditoria=True))
        self.assertIn("tabla:auditoria", usadas)
        self.assertIn("tipo:autonumerico", usadas)
        self.assertNotIn("columna:protegida", usadas)
        prot = con_cambio(COMPLETO, ["columnas", 0, "protegida"], True)
        self.assertIn("columna:protegida", tb.caracteristicas_usadas(prot))
        auto = con_cambio(COMPLETO, ["primaria", "autonumerico"], "ZZ-{SEQNUM:8}")
        self.assertIn("primaria:autonumerica", tb.caracteristicas_usadas(auto))


class Payload(unittest.TestCase):
    def setUp(self):
        self.p = tb.construir_payload(COMPLETO, IDENT, {"sanic_carp_ch_moneda": META_CHOICE})
        self.cols = {a["SchemaName"]: a for a in self.p["Attributes"]}

    def test_tabla(self):
        p = self.p
        self.assertEqual(p["@odata.type"], "Microsoft.Dynamics.CRM.EntityMetadata")
        self.assertEqual(p["SchemaName"], TABLA)
        self.assertEqual(p["OwnershipType"], "UserOwned")
        self.assertIs(p["HasNotes"], False)
        self.assertIs(p["HasActivities"], False)
        self.assertIs(p["IsActivity"], False)
        self.assertEqual(p["IsAuditEnabled"]["Value"], False)
        self.assertEqual(p["DisplayCollectionName"]["LocalizedLabels"], [{"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": "Demos", "LanguageCode": l} for l in (1033, 3082)])
        self.assertEqual([(l["LanguageCode"], l["Label"]) for l in p["DisplayName"]["LocalizedLabels"]], [(1033, "Demo"), (3082, "Demo")])
        self.assertEqual([l["LanguageCode"] for l in p["Description"]["LocalizedLabels"]], [1033, 3082])  # la descripcion va en cada idioma
        self.assertEqual(len(p["Attributes"]), 10)

    def test_primaria(self):
        a = self.p["Attributes"][0]
        self.assertEqual(a["SchemaName"], "sanic_nombre")
        self.assertIs(a["IsPrimaryName"], True)
        self.assertEqual(a["@odata.type"], "Microsoft.Dynamics.CRM.StringAttributeMetadata")
        self.assertEqual(a["MaxLength"], 200)
        self.assertEqual(a["FormatName"], {"Value": "Text"})
        self.assertEqual(a["RequiredLevel"]["Value"], "ApplicationRequired")
        self.assertNotIn("AutoNumberFormat", a)

    def test_cada_tipo(self):
        c = self.cols
        self.assertEqual((c["sanic_numeroproducto"]["@odata.type"], c["sanic_numeroproducto"]["MaxLength"]), ("Microsoft.Dynamics.CRM.StringAttributeMetadata", 30))
        self.assertEqual((c["sanic_comentarios"]["@odata.type"], c["sanic_comentarios"]["Format"], c["sanic_comentarios"]["MaxLength"]), ("Microsoft.Dynamics.CRM.MemoAttributeMetadata", "TextArea", 500))
        self.assertEqual((c["sanic_plazo"]["MinValue"], c["sanic_plazo"]["MaxValue"], c["sanic_plazo"]["Format"]), (1, 60, "None"))
        self.assertEqual(c["sanic_moneda"]["GlobalOptionSet@odata.bind"], f"/GlobalOptionSetDefinitions({META_CHOICE})")
        self.assertNotIn("OptionSet", c["sanic_moneda"])
        b = c["sanic_correonuevoenfinalizada"]
        self.assertIs(b["DefaultValue"], False)
        self.assertEqual([(l["LanguageCode"], l["Label"]) for l in b["OptionSet"]["TrueOption"]["Label"]["LocalizedLabels"]], [(1033, "Si"), (3082, "Si")])
        self.assertEqual(b["OptionSet"]["FalseOption"]["Value"], 0)
        self.assertEqual((c["sanic_fechainicio"]["Format"], c["sanic_fechainicio"]["DateTimeBehavior"]), ("DateOnly", {"Value": "DateOnly"}))
        self.assertEqual((c["sanic_fechacongelamiento"]["Format"], c["sanic_fechacongelamiento"]["DateTimeBehavior"]), ("DateAndTime", {"Value": "UserLocal"}))
        self.assertEqual((c["sanic_archivo"]["@odata.type"], c["sanic_archivo"]["MaxSizeInKB"]), ("Microsoft.Dynamics.CRM.FileAttributeMetadata", 5120))
        self.assertEqual(c["sanic_folio"]["AutoNumberFormat"], "ZZ-{SEQNUM:8}")

    def test_comunes(self):
        for a in self.p["Attributes"]:
            self.assertIn("RequiredLevel", a)
            self.assertIn("DisplayName", a)
            self.assertIn("Description", a)
            self.assertEqual(a["IsAuditEnabled"]["Value"], False)
            self.assertIs(a["IsSecured"], False)
        prot = con_cambio(COMPLETO, ["columnas", 0, "protegida"], True)
        p = tb.construir_payload(prot, IDENT, {"sanic_carp_ch_moneda": META_CHOICE})
        self.assertNotIn("sanic_numeroproducto", [a["SchemaName"] for a in p["Attributes"]])
        despues = tb.columnas_protegidas_payload(prot, IDENT, {"sanic_carp_ch_moneda": META_CHOICE})
        self.assertEqual([a["SchemaName"] for a in despues], ["sanic_numeroproducto"])
        self.assertIs(despues[0]["IsSecured"], True)


class Caminos(Base):
    def test_no_existe_se_crea_con_un_solo_post_y_se_verifica(self):
        cliente = armar(ClienteSimulado(), COMPLETO, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, TABLA)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([p["ruta"] for p in posts], ["EntityDefinitions", "PublishXml"])
        self.assertEqual(posts[0]["solucion"], IDENT["solucion"])
        self.assertEqual(len(posts[0]["cuerpo"]["Attributes"]), 10)
        self.assertIn(META_TABLA, detalle)

    def test_las_columnas_protegidas_se_agregan_despues_de_crear_la_tabla(self):
        """La plataforma rechaza (400) una columna con IsSecured=true dentro del
        POST que crea la tabla; sí la acepta agregada después (verificado)."""
        prot = con_cambio(con_cambio(COMPLETO, ["columnas", 0, "protegida"], True), ["columnas", 2, "protegida"], True)
        cliente = armar(ClienteSimulado(), prot, existe=False)
        estado, _, detalle = self.con_cliente(cliente, prot)
        self.assertEqual(estado, "creado", detalle)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([p["ruta"] for p in posts], ["EntityDefinitions", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes",
                                                      f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", "PublishXml"])
        en_tabla = [a["SchemaName"] for a in posts[0]["cuerpo"]["Attributes"]]
        self.assertNotIn("sanic_numeroproducto", en_tabla)
        self.assertNotIn("sanic_plazo", en_tabla)
        self.assertEqual(len(en_tabla), 8)
        self.assertEqual([p["cuerpo"]["SchemaName"] for p in posts[1:3]], ["sanic_numeroproducto", "sanic_plazo"])
        for p in posts[:3]:
            self.assertEqual(p["solucion"], IDENT["solucion"])
        self.assertIs(posts[1]["cuerpo"]["IsSecured"], True)

    def test_si_falla_agregar_una_protegida_dice_que_la_tabla_quedo_incompleta(self):
        prot = con_cambio(COMPLETO, ["columnas", 0, "protegida"], True)
        cliente = ClienteSimulado()
        cliente.responder("POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", (400, {"error": "boom"}, {}))
        armar(cliente, prot, existe=False)
        estado, _, detalle = self.con_cliente(cliente, prot)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("sanic_numeroproducto", detalle)
        self.assertIn("HTTP 400", detalle)
        self.assertIn("incompleta", detalle)

    def test_la_primaria_se_ajusta_despues_de_crear_porque_la_plataforma_ignora_su_largo_y_su_requerida(self):
        """Verificado el 2026-09-20: al crear la tabla la plataforma deja la
        primaria en 850 y opcional, diga lo que diga el POST (salvo que sea
        autonumérica). Hay que reenviar su definición completa con PUT."""
        cliente = armar(ClienteSimulado(), COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "creado", detalle)
        puts = [l for l in cliente.llamadas if l["metodo"] == "PUT"]
        self.assertEqual(len(puts), 1)
        self.assertEqual(puts[0]["ruta"], f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_nombre')")
        c = puts[0]["cuerpo"]
        self.assertEqual(c["@odata.type"], "Microsoft.Dynamics.CRM.StringAttributeMetadata")
        self.assertEqual(c["MaxLength"], 200)
        self.assertEqual(c["RequiredLevel"]["Value"], "ApplicationRequired")
        # Visto el 2026-09-20: la plataforma también ignora IsAuditEnabled=false de la primaria y la deja auditada.
        self.assertEqual(c["IsAuditEnabled"], aud(COMPLETO["auditoria"]))
        self.assertFalse(COMPLETO["auditoria"])
        self.assertEqual([(l["LanguageCode"], l["Label"]) for l in c["DisplayName"]["LocalizedLabels"]], [(1033, "Nombre"), (3082, "Nombre")])
        self.assertEqual(c["FormatName"], {"Value": "Text"})  # el resto de la definición se conserva
        self.assertEqual(puts[0]["cabeceras"], {"MSCRM.MergeLabels": "true"})
        self.assertEqual(puts[0]["solucion"], IDENT["solucion"])
        orden = [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] in ("POST", "PUT")]
        self.assertEqual(orden[-1][1], "PublishXml")
        self.assertIn(TABLA, [l for l in cliente.llamadas if l["ruta"] == "PublishXml"][0]["cuerpo"]["ParameterXml"])

    def test_una_primaria_autonumerica_no_se_ajusta(self):
        auto = con_cambio(COMPLETO, ["primaria", "autonumerico"], "ZZ-{SEQNUM:8}")
        cliente = armar(ClienteSimulado(), auto, existe=False)
        estado, _, detalle = self.con_cliente(cliente, auto)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual([l for l in cliente.llamadas if l["metodo"] == "PUT"], [])

    def test_si_falla_el_ajuste_de_la_primaria_dice_que_quedo_incompleta(self):
        cliente = ClienteSimulado()
        cliente.responder("PUT", lambda r: True, (400, {"error": "boom"}, {}))
        armar(cliente, COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("incompleta", detalle)
        self.assertIn("sanic_nombre", detalle)
        self.assertIn("HTTP 400", detalle)

    def test_corregir_primaria_repara_solo_ese_caso(self):
        pt = filas_por_tipo(COMPLETO)
        pt["String"][0]["MaxLength"] = 850
        gen = [fila_generica(dict(COMPLETO["primaria"], requerida=False), True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        # sin el flag: difiere y no escribe
        cliente = armar(ClienteSimulado(), COMPLETO, por_tipo=pt, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())
        # con el flag: hace el PUT y publica (la relectura simulada sigue difiriendo: acá importa que lo intentó)
        cliente = armar(ClienteSimulado(), COMPLETO, por_tipo=pt, genericas=gen)
        self.con_cliente(cliente, COMPLETO, corregir_primaria=True)
        self.assertEqual([l["metodo"] for l in cliente.llamadas if l["metodo"] in ("PUT", "POST")], ["PUT", "POST"])
        # con el flag pero con OTRA diferencia además: no toca nada
        otra = con_cambio(pt, ["Integer", 0, "MaxValue"], 5)
        cliente = armar(ClienteSimulado(), COMPLETO, por_tipo=otra, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO, corregir_primaria=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_corregir_primaria_tambien_le_quita_la_auditoria_que_puso_la_plataforma(self):
        """Visto el 2026-09-20: la plataforma deja auditada la primaria de una tabla sin auditoría."""
        gen = [dict(fila_generica(COMPLETO["primaria"], True), IsAuditEnabled=aud(True))] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        cliente = armar(ClienteSimulado(), COMPLETO, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_nombre.IsAuditEnabled", detalle)
        self.assertFalse(cliente.hubo_escritura())
        cliente = armar(ClienteSimulado(), COMPLETO, genericas=gen)
        self.con_cliente(cliente, COMPLETO, corregir_primaria=True)
        puts = [l for l in cliente.llamadas if l["metodo"] == "PUT"]
        self.assertEqual(len(puts), 1)
        self.assertEqual(puts[0]["cuerpo"]["IsAuditEnabled"], aud(False))

    def test_publicar_vuelve_a_publicar_una_tabla_que_coincide_y_nada_mas(self):
        cliente = armar(ClienteSimulado(), COMPLETO)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO, publicar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn("publicó", detalle)
        escrituras = [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]
        self.assertEqual(escrituras, [("POST", "PublishXml")])
        # si difiere, no publica ni toca nada
        pt = con_cambio(filas_por_tipo(COMPLETO), ["Integer", 0, "MaxValue"], 5)
        cliente = armar(ClienteSimulado(), COMPLETO, por_tipo=pt)
        estado, _, _ = self.con_cliente(cliente, COMPLETO, publicar=True)
        self.assertEqual(estado, "difiere")
        self.assertFalse(cliente.hubo_escritura())

    def test_corregir_primaria_tambien_la_vuelve_autonumerica(self):
        """Decisión del aprobador (2026-09-20): la primaria de una tabla que ya
        existe pasa de texto a autonumérica y opcional. Es el mismo PUT."""
        auto = con_cambio(con_cambio(COMPLETO, ["primaria", "autonumerico"], "REG-{SEQNUM:4}"), ["primaria", "requerida"], False)
        # el entorno todavía la tiene como texto común, requerida
        pt = filas_por_tipo(COMPLETO)
        gen = [fila_generica(COMPLETO["primaria"], True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        cliente = armar(ClienteSimulado(), auto, por_tipo=pt, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, auto)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_nombre.AutoNumberFormat", detalle)
        self.assertIn("sanic_nombre.RequiredLevel", detalle)
        self.assertFalse(cliente.hubo_escritura())
        cliente = armar(ClienteSimulado(), auto, por_tipo=pt, genericas=gen)
        self.con_cliente(cliente, auto, corregir_primaria=True)
        puts = [l for l in cliente.llamadas if l["metodo"] == "PUT"]
        self.assertEqual(len(puts), 1)
        self.assertEqual(puts[0]["cuerpo"]["AutoNumberFormat"], "REG-{SEQNUM:4}")
        self.assertEqual(puts[0]["cuerpo"]["RequiredLevel"]["Value"], "None")
        self.assertEqual(puts[0]["cuerpo"]["MaxLength"], 200)
        # y al revés no: quitarle el formato a una autonumérica NO lo hace la herramienta
        entorno_auto = con_cambio(pt, ["String", 0, "AutoNumberFormat"], "X-{SEQNUM:4}")
        cliente = armar(ClienteSimulado(), COMPLETO, por_tipo=entorno_auto)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO, corregir_primaria=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_corregir_primaria_tambien_cambia_su_nombre_visible_y_su_descripcion(self):
        """Al volverla autonumérica cambia su papel (de nombre a número): el
        nombre visible y la descripción van en el mismo PUT. Lo detectó el
        constructor el 2026-09-20: sin esto la reparación no alcanzaba."""
        nuevo = con_cambio(con_cambio(con_cambio(COMPLETO, ["primaria", "autonumerico"], "REG-{SEQNUM:4}"), ["primaria", "displayname"], por_idioma("Numero")),
                           ["primaria", "descripcion"], "Número que la plataforma asigna sola.")
        gen = [fila_generica(COMPLETO["primaria"], True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        cliente = armar(ClienteSimulado(), nuevo, por_tipo=filas_por_tipo(COMPLETO), genericas=gen)
        self.con_cliente(cliente, nuevo, corregir_primaria=True)
        puts = [l for l in cliente.llamadas if l["metodo"] == "PUT"]
        self.assertEqual(len(puts), 1)
        self.assertEqual(puts[0]["cuerpo"]["DisplayName"], tb.etiqueta_web_api(por_idioma("Numero"), [1033, 3082]))
        self.assertEqual(puts[0]["cuerpo"]["Description"], tb.etiqueta_uniforme("Número que la plataforma asigna sola.", [1033, 3082]))
        # pero el nombre visible de OTRA columna no lo repara
        otra = con_cambio(gen, [1, "DisplayName"], label("Otro"))
        cliente = armar(ClienteSimulado(), nuevo, por_tipo=filas_por_tipo(COMPLETO), genericas=otra)
        estado, _, _ = self.con_cliente(cliente, nuevo, corregir_primaria=True)
        self.assertEqual(estado, "difiere")
        self.assertFalse(cliente.hubo_escritura())

    def test_ya_existia_no_escribe(self):
        cliente = armar(ClienteSimulado(), COMPLETO)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn(META_TABLA, detalle)  # quien verifica necesita el identificador para cruzar contra el entorno
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_sin_tabla_es_error_y_no_crea(self):
        cliente = armar(ClienteSimulado(), COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO, solo_verificar=True)
        self.assertEqual(estado, "error")
        self.assertIn("no existe", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_choice_de_una_columna_no_existe_bloquea_sin_crear(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("GlobalOptionSetDefinitions(Name="), (404, {"error": {}}, {}))
        armar(cliente, COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("sanic_carp_ch_moneda", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_creacion_falla(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "EntityDefinitions", (400, {"error": {"message": "nombre inválido"}}, {}))
        armar(cliente, COMPLETO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "error")
        self.assertIn("HTTP 400", detalle)


class Diferencias(Base):
    def difiere(self, esperado, **kw):
        cliente = armar(ClienteSimulado(), COMPLETO, **kw)
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn(esperado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_de_la_tabla(self):
        for clave, valor, esperado in [("OwnershipType", "OrganizationOwned", "OwnershipType"), ("HasNotes", True, "HasNotes"),
                                       ("IsManaged", True, "IsManaged"), ("PrimaryNameAttribute", "sanic_otro", "PrimaryNameAttribute"),
                                       ("DisplayCollectionName", label("Otros"), "DisplayCollectionName"),
                                       ("IsAuditEnabled", aud(True), "IsAuditEnabled"),
                                       ("DisplayName", label("Demo", otros_idiomas=[(1046, "Demo")]), "no declara")]:
            with self.subTest(clave):
                self.difiere(esperado, tabla=con_cambio(cuerpo_tabla(COMPLETO), [clave], valor))

    def test_de_las_columnas(self):
        gen = [fila_generica(COMPLETO["primaria"], True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        pt = filas_por_tipo(COMPLETO)
        casos = [
            ("falta la columna sanic_plazo", dict(genericas=[g for g in gen if g["LogicalName"] != "sanic_plazo"])),
            ("sanic_numeroproducto.RequiredLevel", dict(genericas=con_cambio(gen, [1, "RequiredLevel"], rl("None")))),
            ("sanic_numeroproducto.IsSecured", dict(genericas=con_cambio(gen, [1, "IsSecured"], True))),
            ("sanic_numeroproducto.tipo", dict(genericas=con_cambio(gen, [1, "AttributeTypeName"], {"Value": "MemoType"}))),
            ("sanic_numeroproducto.DisplayName", dict(genericas=con_cambio(gen, [1, "DisplayName"], label("Otro")))),
            ("sanic_numeroproducto.MaxLength", dict(por_tipo=con_cambio(pt, ["String", 1, "MaxLength"], 10))),
            ("sanic_nombre.MaxLength", dict(por_tipo=con_cambio(pt, ["String", 0, "MaxLength"], 100))),
            ("sanic_folio.AutoNumberFormat", dict(por_tipo=con_cambio(pt, ["String", 2, "AutoNumberFormat"], None))),
            ("sanic_plazo.MaxValue", dict(por_tipo=con_cambio(pt, ["Integer", 0, "MaxValue"], 5))),
            ("sanic_moneda.choice", dict(por_tipo=con_cambio(pt, ["Picklist", 0, "GlobalOptionSet", "Name"], "sanic_carp_ch_tipoarreglo"))),
            ("sanic_correonuevoenfinalizada.DefaultValue", dict(por_tipo=con_cambio(pt, ["Boolean", 0, "DefaultValue"], True))),
            ("sanic_correonuevoenfinalizada.etiqueta_si", dict(por_tipo=con_cambio(pt, ["Boolean", 0, "OptionSet", "TrueOption", "Label"], label("Yes")))),
            ("sanic_fechainicio.DateTimeBehavior", dict(por_tipo=con_cambio(pt, ["DateTime", 0, "DateTimeBehavior"], {"Value": "UserLocal"}))),
            ("sanic_fechacongelamiento.Format", dict(por_tipo=con_cambio(pt, ["DateTime", 1, "Format"], "DateOnly"))),
            ("sanic_archivo.MaxSizeInKB", dict(por_tipo=con_cambio(pt, ["File", 0, "MaxSizeInKB"], 32768))),
            ("pertenencia a la solución", dict(componentes=[])),
        ]
        for esperado, kw in casos:
            with self.subTest(esperado):
                self.difiere(esperado, **kw)

    def test_una_columna_propia_de_mas_difiere_pero_un_lookup_no(self):
        gen = [fila_generica(COMPLETO["primaria"], True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        intrusa = fila_generica(col("sanic_intrusa", "texto", largo=5))
        self.difiere("sanic_intrusa", genericas=gen + [intrusa])
        lookup = dict(fila_generica(col("sanic_clienteid", "texto", largo=5)), AttributeTypeName={"Value": "LookupType"})
        sombra = dict(fila_generica(col("sanic_monedaname", "texto", largo=5)), AttributeOf="sanic_moneda", AttributeTypeName={"Value": "VirtualType"})
        cliente = armar(ClienteSimulado(), COMPLETO, genericas=gen + [lookup, sombra])
        estado, _, detalle = self.con_cliente(cliente, COMPLETO)
        self.assertEqual(estado, "ya_existia", detalle)


class FormaYHttpInesperados(Base):
    def test_http_inesperado_es_error_y_nombra_la_consulta(self):
        for matcher, nombre in [(lambda r: r.startswith("GlobalOptionSetDefinitions(Name="), "GlobalOptionSetDefinitions"),
                                (lambda r: r.startswith("EntityDefinitions(LogicalName=") and "/Attributes" not in r, "EntityDefinitions"),
                                (lambda r: "/Attributes?" in r, "Attributes"),
                                (lambda r: "StringAttributeMetadata" in r, "StringAttributeMetadata"),
                                (es_ruta_solutioncomponents, "solutioncomponents"),
                                (es_ruta_solutions, "solutions")]:
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder("GET", matcher, (500, {"error": {"message": "boom"}}, {}))
                armar(cliente, COMPLETO)
                estado, _, detalle = self.con_cliente(cliente, COMPLETO)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertIn(nombre, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_es_error_y_nombra_el_campo(self):
        gen = [fila_generica(COMPLETO["primaria"], True)] + [fila_generica(c) for c in COMPLETO["columnas"]] + SISTEMA
        pt = filas_por_tipo(COMPLETO)
        t = cuerpo_tabla(COMPLETO)
        casos = [
            ("MetadataId", dict(tabla=con_cambio(t, ["MetadataId"], None))),
            ("OwnershipType", dict(tabla=con_cambio(t, ["OwnershipType"], 1))),
            ("HasNotes", dict(tabla=con_cambio(t, ["HasNotes"], "false"))),
            ("IsAuditEnabled.Value", dict(tabla=con_cambio(t, ["IsAuditEnabled", "Value"], "false"))),
            ("DisplayName", dict(tabla=con_cambio(t, ["DisplayName"], None))),
            ("value[1].LogicalName", dict(genericas=con_cambio(gen, [1, "LogicalName"], None))),
            ("value[1].RequiredLevel.Value", dict(genericas=con_cambio(gen, [1, "RequiredLevel", "Value"], 0))),
            ("value[1].IsSecured", dict(genericas=con_cambio(gen, [1, "IsSecured"], None))),
            ("value[2]", dict(genericas=con_cambio(gen, [2], "texto"))),
            ("value[1].MaxLength", dict(por_tipo=con_cambio(pt, ["String", 1, "MaxLength"], "30"))),
            ("value[0].MaxValue", dict(por_tipo=con_cambio(pt, ["Integer", 0, "MaxValue"], None))),
            ("value[0].GlobalOptionSet", dict(por_tipo=con_cambio(pt, ["Picklist", 0, "GlobalOptionSet"], None))),
            ("value[0].DateTimeBehavior.Value", dict(por_tipo=con_cambio(pt, ["DateTime", 0, "DateTimeBehavior", "Value"], None))),
            ("value[0].MaxSizeInKB", dict(por_tipo=con_cambio(pt, ["File", 0, "MaxSizeInKB"], True))),
            ("value[0].DefaultValue", dict(por_tipo=con_cambio(pt, ["Boolean", 0, "DefaultValue"], 0))),
        ]
        for campo, kw in casos:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), COMPLETO, **kw)
                estado, _, detalle = self.con_cliente(cliente, COMPLETO)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
