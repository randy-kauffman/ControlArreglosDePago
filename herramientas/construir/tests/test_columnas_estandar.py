"""`tabla.py` con un playbook `columnas-estandar` (skill power-platform-especificar 0.2.0, `tabla.md` sección 6): columnas PROPIAS sobre una
tabla estándar que ya existe, sin crear ni modificar la tabla.

Dominio: `sanic_tiemporespuesta` (entero) sobre `email` (diccionario de datos, sección 5). Variante SIN ENSAYAR (0 ciclos): la herramienta
devuelve `bloqueado` salvo con `--permitir-no-verificadas`, y ese flag solo vale si TODAS las columnas son descartables (`_zz`). La
pertenencia a la solución es un pendiente de la skill: la herramienta la OBSERVA y la informa, y solo la exige cuando alguien marca
`variante:pertenencia` como verificada. Ninguna prueba toca la red."""
import copy
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import columnas_estandar as ce  # noqa: E402
import tabla as tb  # noqa: E402
import test_tabla as tt  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import armar_cliente_precondiciones_ok, es_ruta_solutioncomponents, label, playbook_md, por_idioma  # noqa: E402
from test_tabla import BORRAR, aud, col, con_cambio, rl  # noqa: E402

META_EMAIL = "66666666-6666-6666-6666-666666666666"
IDENT = dict(tt.IDENT, tipo_playbook="columnas-estandar")
RUTA_ATRIBUTOS = "EntityDefinitions(LogicalName='email')/Attributes"
# Descartable (`_zz`) para poder ensayar; la columna real del diccionario es `sanic_tiemporespuesta`.
EMAIL = {
    "tipo": "columnas-estandar",
    "tabla": "email",
    "columnas": [
        col("sanic_zztiemporespuesta", "entero", minimo=0, maximo=2147483647),
        col("sanic_zzmonto", "decimal", precision=2, minimo=0, maximo=999999999.99),
        col("sanic_zzmoneda", "choice", choice="sanic_carp_ch_moneda"),
    ],
}
REAL = dict(copy.deepcopy(EMAIL), columnas=[col("sanic_tiemporespuesta", "entero", minimo=0, maximo=2147483647)])
VERIFICADAS_TODO = frozenset(tb.CARACTERISTICAS) | ce.CARACTERISTICAS_DE_LA_VARIANTE
SOLO_TABLA = tb.VERIFICADAS_EN_PLATAFORMA  # lo que hoy tiene la herramienta como verificado: ningún rastro de la variante
PERMITIR = dict(verificadas=SOLO_TABLA, permitir=True)
SISTEMA_EMAIL = [
    {"LogicalName": "subject", "SchemaName": "Subject", "AttributeOf": None, "IsCustomAttribute": False, "IsPrimaryName": True,
     "AttributeTypeName": {"Value": "StringType"}, "RequiredLevel": rl("None"), "IsSecured": False, "IsAuditEnabled": aud(False),
     "DisplayName": label("Subject"), "Description": label(""), "MetadataId": "77777777-0000-0000-0000-000000000001"},
    {"LogicalName": "new_externa", "SchemaName": "new_externa", "AttributeOf": None, "IsCustomAttribute": True, "IsPrimaryName": False,
     "AttributeTypeName": {"Value": "StringType"}, "RequiredLevel": rl("None"), "IsSecured": False, "IsAuditEnabled": aud(False),
     "DisplayName": label("Externa"), "Description": label(""), "MetadataId": "77777777-0000-0000-0000-000000000002"},
]


def tabla_estandar(**cambios):
    cuerpo = {"MetadataId": META_EMAIL, "LogicalName": "email", "IsCustomEntity": False,
              "CanCreateAttributes": {"Value": True, "CanBeChanged": False, "ManagedPropertyLogicalName": "cancreateattributes"},
              "IsAuditEnabled": aud(False)}
    cuerpo.update(cambios)
    return cuerpo


def genericas_variante(datos, sin=()):
    filas = [dict(tt.fila_generica(c), MetadataId=f"88888888-0000-0000-0000-{i:012d}") for i, c in enumerate(datos["columnas"]) if c["nombre"] not in sin]
    return filas + copy.deepcopy(SISTEMA_EMAIL)


def armar(cliente, datos, genericas=None, por_tipo=None, tabla=None, tabla_filas=None, columna_filas=None, formulas=None):
    """Todas las rutas del camino feliz. `por_tipo`/`genericas` pisan lo que devuelve el entorno."""
    armar_cliente_precondiciones_ok(cliente, IDENT)
    cliente.responder("GET", lambda r: r.startswith("GlobalOptionSetDefinitions(Name="), (200, {"MetadataId": tt.META_CHOICE, "Name": "sanic_carp_ch_moneda", "IsGlobal": True}, {}))
    cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName='email')?"), (200, tabla if tabla is not None else tabla_estandar(), {}))
    cliente.responder("GET", lambda r: "/Attributes?" in r, (200, {"value": genericas if genericas is not None else genericas_variante(datos)}, {}))
    pt = por_tipo if por_tipo is not None else tt.filas_por_tipo(dict(datos, primaria={"nombre": "x", "largo": 1, "autonumerico": ""}))
    pt = {k: [f for f in v if f["LogicalName"] != "x"] for k, v in pt.items()}
    for t in ("String", "Memo", "Integer", "Decimal", "Picklist", "Boolean", "DateTime", "File"):
        cliente.responder("GET", lambda r, t=t: f"/Attributes/Microsoft.Dynamics.CRM.{t}AttributeMetadata" in r, (200, {"value": pt.get(t, [])}, {}))
    for c in datos["columnas"]:
        if c["tipo"] == "formula":
            ruta_f = f"{RUTA_ATRIBUTOS}(LogicalName='{c['nombre']}')"
            cliente.responder("GET", lambda r, ruta_f=ruta_f: r.startswith(ruta_f + "?"), (200, (formulas or {}).get(c["nombre"], tt.cuerpo_formula(c)), {}))
            cliente.responder("GET", lambda r, ruta_f=ruta_f: r.startswith(ruta_f + "/Microsoft.Dynamics.CRM.DecimalAttributeMetadata"),
                              (200, {"LogicalName": c["nombre"], "Precision": c.get("precision")}, {}))
    cliente.responder("GET", lambda r: es_ruta_solutioncomponents(r) and "componenttype eq 1" in r and META_EMAIL in r,
                      (200, {"value": tabla_filas if tabla_filas is not None else [{"solutioncomponentid": "t", "rootcomponentbehavior": 1}]}, {}))
    cliente.responder("GET", lambda r: es_ruta_solutioncomponents(r) and "componenttype eq 2" in r,
                      (200, {"value": columna_filas if columna_filas is not None else [{"solutioncomponentid": "c", "rootcomponentbehavior": None}]}, {}))
    cliente.responder("POST", RUTA_ATRIBUTOS, (204, None, {}))
    cliente.responder("POST", "PublishXml", (204, None, {}))
    return cliente


class Base(unittest.TestCase):
    def correr(self, fabrica, datos, solo_verificar=False, verificadas=SOLO_TABLA, permitir=False, **otros):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "playbook.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(IDENT, datos))
            return tb.construir(ruta, solo_verificar, fabrica, verificadas=verificadas, permitir_no_verificadas=permitir, **otros)

    def con_cliente(self, cliente, datos, **kw):
        return self.correr(lambda: cliente, datos, **kw)


class ValidacionSinRed(Base):
    CASOS = [
        (["sobra"], True, "sobra"),
        (["tabla"], BORRAR, "tabla"),
        (["tabla"], "", "tabla"),
        (["tabla"], 5, "tabla"),
        (["tabla"], "Email", "tabla"),
        (["tabla"], "sanic_carp_tbl_cuota", "propia"),       # una tabla con el prefijo del publisher es propia: va con el playbook de tabla
        (["columnas"], {}, "columnas"),
        (["columnas"], [], "columnas"),
        (["columnas", 0], "texto", "columnas[0]"),
        (["columnas", 0, "tipo"], "lookup", "columnas[0].tipo"),                    # un lookup nace con su relación
        (["columnas", 0, "tipo"], "archivo", "archivo"),                              # no se ensayó sobre una estándar
        (["columnas", 0, "tipo"], "autonumerico", "autonumerico"),
        (["columnas", 0, "nombre"], "tiemporespuesta", "columnas[0].nombre"),         # sin prefijo del publisher
        (["columnas", 0, "nombre"], "sanic_zzmonto", "repetid"),
        (["columnas", 0, "nombre"], "Sanic_ZzTiempo", "columnas[0].nombre"),
        (["columnas", 0, "displayname"], "Tiempo", "columnas[0].displayname"),         # cadena única: formato viejo
        (["columnas", 0, "displayname"], {"1033": "Tiempo"}, "3082"),
        (["columnas", 0, "displayname"], por_idioma("Tiempo de respuesta (minutos) – año"), "BP-PP-197"),
        (["columnas", 0, "maximo"], -1, "maximo"),
        (["columnas", 0, "requerida"], "no", "columnas[0].requerida"),
        (["columnas", 1, "precision"], 11, "columnas[1]"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta} <- {'BORRAR' if valor is BORRAR else repr(valor)[:50]}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(EMAIL, ruta, valor), **PERMITIR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)

    def test_no_hay_columna_primaria_ni_propiedades_de_tabla(self):
        for sobra in ("primaria", "propiedad", "notas", "actividades", "auditoria", "displayname", "motivos_estado"):
            with self.subTest(sobra):
                estado, _, detalle = self.correr(FabricaCentinela(), dict(EMAIL, **{sobra: "x"}), **PERMITIR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn(sobra, detalle)

    def test_la_formula_de_la_variante_se_valida_como_la_de_una_tabla_propia(self):
        formula = col("sanic_zzantiguedad", "formula", expresion="DateDiff(createdon, Now(), TimeUnit.Minutes) / 60", resultado="decimal", precision=2,
                      depende_de=["createdon"])
        ok = dict(copy.deepcopy(EMAIL), columnas=EMAIL["columnas"] + [formula])
        for ruta, valor, esperado in [(["columnas", 3, "expresion"], "DateDiff(createdon, UTCNow(), TimeUnit.Minutes)", "UTCNow"),
                                      (["columnas", 3, "depende_de"], [], "depende_de"), (["columnas", 3, "requerida"], True, "requerida")]:
            with self.subTest(esperado):
                estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(ok, ruta, valor), **PERMITIR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)


class SinEnsayar(Base):
    def test_sin_el_flag_es_bloqueado_sin_tocar_la_red_y_nombra_lo_pendiente(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, EMAIL)
        self.assertEqual(estado, "bloqueado", detalle)
        for pieza in ("variante:columnas-estandar", "variante:pertenencia", "--permitir-no-verificadas", "CanCreateAttributes", "rootcomponentbehavior"):
            self.assertIn(pieza, detalle)
        self.assertFalse(centinela.llamada)

    def test_el_playbook_real_del_diccionario_tampoco_pasa_hasta_que_un_ciclo_lo_verifique(self):
        estado, _, detalle = self.correr(FabricaCentinela(), REAL, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("_zz", detalle)
        self.assertIn("sanic_tiemporespuesta", detalle)

    def test_el_ensayo_exige_que_todas_las_columnas_sean_descartables_no_alcanza_con_una(self):
        mixto = dict(copy.deepcopy(EMAIL), columnas=EMAIL["columnas"] + REAL["columnas"])
        estado, _, detalle = self.correr(FabricaCentinela(), mixto, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("sanic_tiemporespuesta", detalle)
        self.assertNotIn("sanic_zzmonto", detalle)

    def test_las_caracteristicas_de_la_variante_no_estan_en_las_verificadas_de_la_herramienta(self):
        self.assertEqual({"variante:columnas-estandar", "variante:pertenencia"}, set(ce.CARACTERISTICAS_DE_LA_VARIANTE))
        self.assertFalse(set(ce.CARACTERISTICAS_DE_LA_VARIANTE) & set(tb.VERIFICADAS_EN_PLATAFORMA))

    def test_el_gate_tambien_incluye_lo_sin_ensayar_de_cada_columna(self):
        # Todo lo de la variante verificado, salvo el decimal: sigue bloqueado por el decimal.
        verificadas = VERIFICADAS_TODO - {"tipo:decimal"}
        estado, _, detalle = self.correr(FabricaCentinela(), EMAIL, verificadas=verificadas, permitir=False)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("tipo:decimal", detalle)

    def test_las_banderas_de_reparacion_de_la_tabla_no_aplican(self):
        for bandera in ("corregir_primaria", "publicar", "corregir_nombres", "agregar_columnas"):
            with self.subTest(bandera):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, EMAIL, **PERMITIR, **{bandera: True})
                self.assertEqual(estado, "error", detalle)
                self.assertIn("columnas-estandar", detalle)
                self.assertIn("no aplica", detalle)
                self.assertFalse(centinela.llamada)


class Precondiciones(Base):
    def test_la_tabla_no_existe_es_bloqueado(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName='email')?"), (404, {"error": {"message": "no existe"}}, {}))
        armar(cliente, EMAIL)
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("email", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_tabla_es_propia_es_bloqueado_y_manda_al_playbook_de_tabla(self):
        cliente = armar(ClienteSimulado(), EMAIL, tabla=tabla_estandar(IsCustomEntity=True))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("propia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_tabla_no_acepta_columnas_propias_es_bloqueado(self):
        cliente = armar(ClienteSimulado(), EMAIL, tabla=tabla_estandar(CanCreateAttributes={"Value": False, "CanBeChanged": False, "ManagedPropertyLogicalName": "cancreateattributes"}))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("CanCreateAttributes", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_pide_auditoria_y_la_tabla_no_esta_auditada_es_un_hueco_del_diseno_no_se_decide_aca(self):
        datos = con_cambio(EMAIL, ["columnas", 0, "auditoria"], True)
        cliente = armar(ClienteSimulado(), datos)
        estado, _, detalle = self.con_cliente(cliente, datos, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("auditor", detalle)
        self.assertIn("hueco", detalle)
        self.assertFalse(cliente.hubo_escritura())
        auditada = armar(ClienteSimulado(), datos, tabla=tabla_estandar(IsAuditEnabled=aud(True)),
                         genericas=genericas_variante(datos))
        estado, _, detalle = self.con_cliente(auditada, datos, **PERMITIR)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_choice_de_una_columna_no_existe_es_bloqueado_sin_escribir(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("GlobalOptionSetDefinitions(Name="), (404, {"error": {}}, {}))
        armar(cliente, EMAIL, genericas=genericas_variante(EMAIL, sin=("sanic_zzmoneda",)))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("sanic_carp_ch_moneda", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_o_http_inesperado_es_error_y_nombra_la_consulta(self):
        casos = [
            ("IsCustomEntity", tabla_estandar(IsCustomEntity="false")),
            ("IsCustomEntity", tabla_estandar(IsCustomEntity=None)),
            ("CanCreateAttributes", tabla_estandar(CanCreateAttributes=None)),
            ("CanCreateAttributes.Value", tabla_estandar(CanCreateAttributes={"Value": "true"})),
            ("IsAuditEnabled.Value", tabla_estandar(IsAuditEnabled=aud(None))),
            ("MetadataId", tabla_estandar(MetadataId=None)),
        ]
        for campo, tabla in casos:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), EMAIL, tabla=tabla)
                estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertIn("email", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName='email')?"), (500, {"error": {"message": "boom"}}, {}))
        armar(cliente, EMAIL)
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("HTTP 500", detalle)
        self.assertIn("email", detalle)


class Construccion(Base):
    def test_agrega_las_columnas_en_el_orden_del_playbook_con_la_cabecera_de_solucion_publica_una_vez_y_no_toca_la_tabla(self):
        cliente = armar(ClienteSimulado(), EMAIL, genericas=genericas_variante(EMAIL, sin=[c["nombre"] for c in EMAIL["columnas"]]))
        estado = {"hay": False}

        def al_agregar(ruta, cuerpo, solucion):
            return (204, None, {})

        cliente.responder("POST", RUTA_ATRIBUTOS, al_agregar)
        # el entorno "gana" las columnas a medida que se agregan: el primer GET no las tiene, el segundo sí
        lecturas = {"n": 0}

        def listado(ruta, cuerpo, solucion):
            lecturas["n"] += 1
            return (200, {"value": genericas_variante(EMAIL, sin=[c["nombre"] for c in EMAIL["columnas"]] if lecturas["n"] == 1 else ())}, {})

        cliente._reglas.insert(0, ("GET", lambda r: "/Attributes?" in r, listado))
        resultado, comp, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(resultado, "creado", detalle)
        self.assertEqual("email", comp)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([RUTA_ATRIBUTOS] * 3 + ["PublishXml"], [p["ruta"] for p in posts])
        self.assertEqual(["sanic_zztiemporespuesta", "sanic_zzmonto", "sanic_zzmoneda"], [p["cuerpo"]["SchemaName"] for p in posts[:3]])
        for p in posts[:3]:
            self.assertEqual(IDENT["solucion"], p["solucion"])
        # EL MISMO cuerpo que la creación de una tabla propia: una sola receta por tipo de columna
        esperado = {c["nombre"]: tb._columna_payload(c, IDENT["idiomas"], {"sanic_carp_ch_moneda": tt.META_CHOICE}) for c in EMAIL["columnas"]}
        for p in posts[:3]:
            self.assertEqual(esperado[p["cuerpo"]["SchemaName"]], p["cuerpo"])
        self.assertIn("email", posts[3]["cuerpo"]["ParameterXml"])
        # ni se crea ni se toca la tabla estándar
        self.assertNotIn(("POST", "EntityDefinitions"), [(l["metodo"], l["ruta"]) for l in cliente.llamadas])
        self.assertEqual([], [l for l in cliente.llamadas if l["metodo"] in ("PUT", "PATCH", "DELETE")])

    def test_si_una_alta_falla_dice_cual_y_cuales_ya_quedaron_y_no_publica(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", RUTA_ATRIBUTOS, lambda ruta, cuerpo, sol: (400, {"error": "boom"}, {}) if cuerpo["SchemaName"] == "sanic_zzmonto" else (204, None, {}))
        armar(cliente, EMAIL, genericas=genericas_variante(EMAIL, sin=[c["nombre"] for c in EMAIL["columnas"]]))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("sanic_zzmonto", detalle)
        self.assertIn("HTTP 400", detalle)
        self.assertIn("ya se agregaron: sanic_zztiemporespuesta", detalle)
        self.assertNotIn(("POST", "PublishXml"), [(l["metodo"], l["ruta"]) for l in cliente.llamadas])

    def test_si_al_releer_no_aparecen_las_columnas_es_error_no_creado(self):
        cliente = armar(ClienteSimulado(), EMAIL, genericas=genericas_variante(EMAIL, sin=[c["nombre"] for c in EMAIL["columnas"]]))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)  # el 204 de las altas no prueba nada
        self.assertEqual(estado, "error", detalle)
        self.assertIn("falta la columna sanic_zztiemporespuesta", detalle)

    def test_con_columnas_que_ya_existen_solo_agrega_las_que_faltan(self):
        sin_una = genericas_variante(EMAIL, sin=("sanic_zzmonto",))
        cliente = armar(ClienteSimulado(), EMAIL, genericas=sin_una)
        self.con_cliente(cliente, EMAIL, **PERMITIR)
        posts = [l["cuerpo"]["SchemaName"] for l in cliente.llamadas if l["metodo"] == "POST" and l["ruta"] == RUTA_ATRIBUTOS]
        self.assertEqual(["sanic_zzmonto"], posts)

    def test_solo_verificar_no_escribe_y_con_columnas_que_faltan_difiere(self):
        cliente = armar(ClienteSimulado(), EMAIL, genericas=genericas_variante(EMAIL, sin=("sanic_zzmonto",)))
        estado, _, detalle = self.con_cliente(cliente, EMAIL, solo_verificar=True, **PERMITIR)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("falta la columna sanic_zzmonto", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_columna_que_difiere_no_se_toca_y_no_se_agrega_nada(self):
        pt = con_cambio(tt.filas_por_tipo(dict(EMAIL, primaria={"nombre": "x", "largo": 1, "autonumerico": ""})), ["Integer", 0, "MaxValue"], 5)
        gen = genericas_variante(EMAIL, sin=("sanic_zzmonto",))
        cliente = armar(ClienteSimulado(), EMAIL, genericas=gen, por_tipo=pt)
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_zztiemporespuesta.MaxValue", detalle)
        self.assertFalse(cliente.hubo_escritura())


class Verificacion(Base):
    def test_segunda_ejecucion_ya_existia_no_escribe_y_lee_la_pertenencia_sin_darla_por_verificada(self):
        cliente = armar(ClienteSimulado(), EMAIL)
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())
        self.assertIn("pertenencia a la solución NO verificada", detalle)
        self.assertIn("rootcomponentbehavior", detalle)
        self.assertEqual(4, len([l for l in cliente.llamadas if es_ruta_solutioncomponents(l["ruta"])]))  # la tabla y cada columna

    def test_cada_diferencia_difiere_y_nombra_la_columna_y_la_propiedad(self):
        sombra = dict(tt.fila_generica(col("sanic_zzmonedaname", "texto", largo=5)), AttributeOf="sanic_zzmoneda", AttributeTypeName={"Value": "VirtualType"},
                      MetadataId="99999999-0000-0000-0000-000000000001")
        base = tt.filas_por_tipo(dict(EMAIL, primaria={"nombre": "x", "largo": 1, "autonumerico": ""}))
        gen = genericas_variante(EMAIL)
        casos = [
            ("sanic_zztiemporespuesta.MaxValue", dict(por_tipo=con_cambio(base, ["Integer", 0, "MaxValue"], 5))),
            ("sanic_zzmonto.Precision", dict(por_tipo=con_cambio(base, ["Decimal", 0, "Precision"], 3))),
            ("sanic_zzmoneda.choice", dict(por_tipo=con_cambio(base, ["Picklist", 0, "GlobalOptionSet", "Name"], "sanic_carp_ch_tipoarreglo"))),
            ("sanic_zztiemporespuesta.RequiredLevel", dict(genericas=con_cambio(gen, [0, "RequiredLevel"], rl("ApplicationRequired")))),
            ("sanic_zztiemporespuesta.IsSecured", dict(genericas=con_cambio(gen, [0, "IsSecured"], True))),
            ("sanic_zztiemporespuesta.DisplayName[3082]", dict(genericas=con_cambio(gen, [0, "DisplayName"], label("Otro", idiomas=[1033])))),
            ("sanic_zztiemporespuesta.tipo", dict(genericas=con_cambio(gen, [0, "AttributeTypeName"], {"Value": "MemoType"}))),
            ("columna propia que el playbook no declara: sanic_zzintrusa", dict(genericas=gen + [dict(tt.fila_generica(col("sanic_zzintrusa", "texto", largo=5)), MetadataId="x")])),
        ]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), EMAIL, **kw)
                estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())
        # los lookups, las sombras y las columnas de OTRO publisher o del sistema no son columnas de más
        lookup = dict(tt.fila_generica(col("sanic_respondidoporid", "texto", largo=5)), AttributeTypeName={"Value": "LookupType"}, MetadataId="y")
        cliente = armar(ClienteSimulado(), EMAIL, genericas=gen + [lookup, sombra])
        estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_forma_inesperada_de_la_pertenencia_es_error(self):
        for campo, tabla_filas in [("value[0]", ["texto"]), ("value[0].rootcomponentbehavior", [{"rootcomponentbehavior": "1"}])]:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), EMAIL, tabla_filas=tabla_filas)
                estado, _, detalle = self.con_cliente(cliente, EMAIL, **PERMITIR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(campo, detalle)

    def test_la_formula_ausente_en_la_variante_es_paso_manual_bloqueado(self):
        formula = col("sanic_zzantiguedad", "formula", expresion="DateDiff(createdon, Now(), TimeUnit.Minutes) / 60", resultado="decimal", precision=2,
                      depende_de=["createdon"])
        datos = dict(copy.deepcopy(EMAIL), columnas=EMAIL["columnas"] + [formula])
        gen = genericas_variante(EMAIL) + [dict(tt.fila_generica(col("sanic_createdon", "fechahora")), LogicalName="createdon", SchemaName="CreatedOn", IsCustomAttribute=False, MetadataId="z")]
        cliente = armar(ClienteSimulado(), datos, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, datos, **PERMITIR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("paso manual", detalle)
        self.assertIn("sanic_zzantiguedad", detalle)
        self.assertIn("email", detalle)

    def test_cuando_la_pertenencia_esta_verificada_se_exige_la_tabla_en_la_solucion_con_comportamiento_distinto_de_0(self):
        verificadas = VERIFICADAS_TODO
        casos = [
            ("pertenencia a la solución", dict(tabla_filas=[])),
            ("pertenencia a la solución", dict(tabla_filas=[{"rootcomponentbehavior": 0}])),
            ("pertenencia a la solución", dict(tabla_filas=[{"rootcomponentbehavior": None}])),
            ("pertenencia a la solución", dict(tabla_filas=[{"rootcomponentbehavior": 1}, {"rootcomponentbehavior": 1}])),
        ]
        for esperado, kw in casos:
            with self.subTest(kw):
                cliente = armar(ClienteSimulado(), EMAIL, **kw)
                estado, _, detalle = self.con_cliente(cliente, EMAIL, verificadas=verificadas)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
        for bueno in (1, 2):
            cliente = armar(ClienteSimulado(), EMAIL, tabla_filas=[{"rootcomponentbehavior": bueno}])
            estado, _, detalle = self.con_cliente(cliente, EMAIL, verificadas=verificadas)
            self.assertEqual(estado, "ya_existia", detalle)
            self.assertNotIn("NO verificada", detalle)

    def test_con_la_pertenencia_sin_verificar_nunca_se_da_por_buena_aunque_el_resto_este_verificado(self):
        # El resto de la variante verificado, pero no la pertenencia: sigue bloqueado.
        verificadas = VERIFICADAS_TODO - {"variante:pertenencia"}
        estado, _, detalle = self.correr(FabricaCentinela(), EMAIL, verificadas=verificadas)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("variante:pertenencia", detalle)


if __name__ == "__main__":
    unittest.main()
