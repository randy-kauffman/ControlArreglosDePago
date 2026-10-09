"""Herramienta `relacion.py`: una relación 1:N con su columna lookup, desde un
playbook. Ninguna prueba toca la red. Los datos de ejemplo son las relaciones
del diccionario de datos del proyecto (`diseno/02-diccionario-datos.md`, §6.2)."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import relacion as rl  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents, es_ruta_solutions, label, playbook_md, por_idioma  # noqa: E402

BORRAR = object()
IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="relacion", inventario="4.1")
META_REL = "66666666-6666-6666-6666-666666666666"

# Diccionario §6.2: (id, nombre de la relación, padre, hija, lookup, comportamiento, requerida, auditoría).
# `systemuser` y `email` son tablas estándar; las demás se escriben con el nombre corto.
DICCIONARIO = [
    ("REL-01", "sanic_carp_cliente_conversacion", "cliente", "conversacion", "sanic_clienteid", "restringido", False, False),
    ("REL-02", "sanic_carp_cliente_arreglo", "cliente", "arreglo", "sanic_clienteid", "restringido", True, False),
    ("REL-03", "sanic_carp_conversacion_arreglo", "conversacion", "arreglo", "sanic_conversacionid", "restringido", False, False),
    ("REL-04", "sanic_carp_versionplantilla_arreglo", "versionplantilla", "arreglo", "sanic_versionplantillaid", "restringido", True, True),
    ("REL-05", "sanic_carp_arreglo_cuota", "arreglo", "cuota", "sanic_arregloid", "parental", True, False),
    ("REL-06", "sanic_carp_arreglo_generacionformato", "arreglo", "generacionformato", "sanic_arregloid", "parental", True, True),
    ("REL-07", "sanic_carp_versionplantilla_generacionformato", "versionplantilla", "generacionformato", "sanic_versionplantillaid", "restringido", True, True),
    ("REL-08", "sanic_carp_versionplantilla_mapeocelda", "versionplantilla", "mapeocelda", "sanic_versionplantillaid", "parental", True, True),
    ("REL-09", "sanic_carp_conversacion_conversacion", "conversacion", "conversacion", "sanic_unidaaid", "quitar_vinculo", False, True),
    ("REL-10", "sanic_carp_systemuser_conversacion_oficialasignadoid", "systemuser", "conversacion", "sanic_oficialasignadoid", "restringido", False, True),
    ("REL-11", "sanic_carp_systemuser_conversacion_finalizadoporid", "systemuser", "conversacion", "sanic_finalizadoporid", "restringido", False, True),
    ("REL-12", "sanic_carp_systemuser_arreglo_oficialid", "systemuser", "arreglo", "sanic_oficialid", "restringido", True, False),
    ("REL-13", "sanic_carp_systemuser_arreglo_formalizadoporid", "systemuser", "arreglo", "sanic_formalizadoporid", "restringido", False, True),
    ("REL-14", "sanic_carp_systemuser_arreglo_descartadoporid", "systemuser", "arreglo", "sanic_descartadoporid", "restringido", False, True),
    ("REL-15", "sanic_carp_systemuser_arreglo_anuladoporid", "systemuser", "arreglo", "sanic_anuladoporid", "restringido", False, True),
    ("REL-16", "sanic_carp_systemuser_arreglo_reabiertoporid", "systemuser", "arreglo", "sanic_reabiertoporid", "restringido", False, True),
    ("REL-17", "sanic_carp_systemuser_versionplantilla", "systemuser", "versionplantilla", "sanic_aprobadoporid", "restringido", False, True),
    ("REL-18", "sanic_carp_systemuser_generacionformato", "systemuser", "generacionformato", "sanic_generadoporid", "restringido", True, True),
    ("REL-19", "sanic_carp_systemuser_email", "systemuser", "email", "sanic_respondidoporid", "restringido", False, False),
]
VISIBLES = {"sanic_clienteid": "Cliente", "sanic_conversacionid": "Conversacion", "sanic_versionplantillaid": "Version de plantilla", "sanic_arregloid": "Arreglo",
            "sanic_unidaaid": "Unida a", "sanic_oficialasignadoid": "Oficial asignado", "sanic_finalizadoporid": "Finalizado por", "sanic_oficialid": "Oficial",
            "sanic_formalizadoporid": "Formalizado por", "sanic_descartadoporid": "Descartado por", "sanic_anuladoporid": "Anulado por",
            "sanic_reabiertoporid": "Reabierto por", "sanic_aprobadoporid": "Aprobado por", "sanic_generadoporid": "Generado por",
            "sanic_respondidoporid": "Respondido por"}
ESTANDAR = {"systemuser", "email"}  # tablas que ya existen en la plataforma


def tabla(corto):
    return corto if corto in ESTANDAR else f"sanic_carp_tbl_{corto}"


def desde_diccionario(fila):
    """El bloque `## 2. Qué se crea` de una fila del diccionario."""
    _, nombre, padre, hija, lookup, comportamiento, requerida, auditoria = fila
    d = {"tipo": "relacion", "nombre": nombre, "tabla_padre": tabla(padre), "tabla_hija": tabla(hija), "comportamiento": comportamiento,
         "lookup": {"nombre": lookup, "displayname": por_idioma(VISIBLES[lookup]), "descripcion": f"Dato de la relación {nombre}.",
                    "requerida": requerida, "auditoria": auditoria}}
    if padre == hija:
        d["jerarquica"] = False  # variante A: la clave está SOLO cuando padre y hija son la misma tabla
    return d


def rel(id_):
    return desde_diccionario(next(f for f in DICCIONARIO if f[0] == id_))


# BASE: REL-04 (restringido, lookup requerido y auditado), con un texto distinto en cada idioma para probar que se compara idioma por idioma.
BASE = copy.deepcopy(rel("REL-04"))
BASE["lookup"]["displayname"] = {"1033": "Template version", "3082": "Version de plantilla"}
PADRE, HIJA, REL, LOOKUP = BASE["tabla_padre"], BASE["tabla_hija"], BASE["nombre"], BASE["lookup"]["nombre"]
PARENTAL = rel("REL-05")  # arreglo -> cuota, lookup requerido y sin auditoría
DE_SISTEMA = rel("REL-15")  # systemuser -> arreglo, con sufijo `anuladoporid` y auditado
AUTORREFERIDA = rel("REL-09")  # conversacion -> conversacion (variante A), quitar vínculo, lookup opcional y auditado
HIJA_ESTANDAR = rel("REL-19")  # systemuser -> email (variante B)
# La misma relación sobre una tabla descartable (`_tbl_zz`): lo único que la herramienta deja ensayar mientras la variante A no esté verificada.
ZZ_TABLA = "sanic_carp_tbl_zzconv"
DESCARTABLE = dict(copy.deepcopy(AUTORREFERIDA), nombre="sanic_carp_zzconv_zzconv", tabla_padre=ZZ_TABLA, tabla_hija=ZZ_TABLA)
TODO_VERIFICADO = frozenset({"variante:autorreferida"})  # la característica que la herramienta declara como verificada

CASCADAS = {
    "parental": {"Assign": "Cascade", "Delete": "Cascade", "Merge": "NoCascade", "Reparent": "Cascade", "Share": "Cascade", "Unshare": "Cascade", "RollupView": "NoCascade"},
    "restringido": {"Assign": "NoCascade", "Delete": "Restrict", "Merge": "NoCascade", "Reparent": "NoCascade", "Share": "NoCascade", "Unshare": "NoCascade", "RollupView": "NoCascade"},
    "quitar_vinculo": {"Assign": "NoCascade", "Delete": "RemoveLink", "Merge": "NoCascade", "Reparent": "NoCascade", "Share": "NoCascade", "Unshare": "NoCascade", "RollupView": "NoCascade"},
}


def con_cambio(obj, ruta, valor):
    nuevo = copy.deepcopy(obj)
    nodo = nuevo
    for paso in ruta[:-1]:
        nodo = nodo[paso]
    if valor is BORRAR:
        del nodo[ruta[-1]]
    else:
        nodo[ruta[-1]] = valor
    return nuevo


def rl_(valor):
    return {"Value": valor, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}


def aud(valor):
    return {"Value": valor, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"}


def id_de(tabla_):
    return "systemuserid" if tabla_ == "systemuser" else tabla_ + "id"


def cuerpo_relacion(d):
    return {"MetadataId": META_REL, "SchemaName": d["nombre"], "ReferencedEntity": d["tabla_padre"], "ReferencedAttribute": id_de(d["tabla_padre"]),
            "ReferencingEntity": d["tabla_hija"], "ReferencingAttribute": d["lookup"]["nombre"], "IsCustomRelationship": True, "IsManaged": False,
            "IsHierarchical": False, "RelationshipType": "OneToManyRelationship", "CascadeConfiguration": dict(CASCADAS[d["comportamiento"]], Archive="NoCascade")}


def cuerpo_lookup(d, idiomas=None):
    k = d["lookup"]
    return {"LogicalName": k["nombre"], "SchemaName": k["nombre"], "AttributeTypeName": {"Value": "LookupType"}, "Targets": [d["tabla_padre"]],
            "RequiredLevel": rl_("ApplicationRequired" if k["requerida"] else "None"), "IsAuditEnabled": aud(k["auditoria"]), "IsSecured": False,
            "DisplayName": label(k["displayname"], idiomas), "Description": label(k["descripcion"], idiomas)}


def armar(cliente, d, existe=True, relacion=None, lookup=None, componentes=None, tablas=None, ident=IDENT):
    """Configura TODAS las rutas del camino feliz."""
    armar_cliente_precondiciones_ok(cliente, ident)
    estado = {"existe": existe}
    tablas = tablas if tablas is not None else {d["tabla_padre"]: {"PrimaryIdAttribute": id_de(d["tabla_padre"]), "IsCustomEntity": d["tabla_padre"] != "systemuser"},
                                                d["tabla_hija"]: {"PrimaryIdAttribute": id_de(d["tabla_hija"]), "IsCustomEntity": True}}

    def tabla_(ruta, *_):
        nombre = ruta.split("'")[1]
        if nombre in tablas:
            return (200, dict(tablas[nombre], LogicalName=nombre, MetadataId="t-" + nombre), {})
        return (404, {"error": "no existe"}, {})

    cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName=") and "/Attributes" not in r, tabla_)
    cliente.responder("GET", lambda r: r.startswith("RelationshipDefinitions(SchemaName="),
                      lambda *_: (200, relacion if relacion is not None else cuerpo_relacion(d), {}) if estado["existe"] else (404, {"error": "no existe"}, {}))

    def leer_lookup(*_):
        if not estado["existe"]:
            return (404, {"error": "no existe"}, {})
        if lookup is not None:
            return (200, lookup, {})
        c = cuerpo_lookup(d, ident["idiomas"])
        if estado.get("recien_creada") and not estado.get("auditoria_ajustada"):
            c["IsAuditEnabled"] = aud(False)  # verificado: la plataforma ignora la auditoría del lookup al crear
        return (200, c, {})

    cliente.responder("GET", lambda r: "/Attributes(LogicalName=" in r, leer_lookup)
    # Una relación NO tiene fila propia en solutioncomponents: viaja dentro de la tabla hija (componenttype 1, con todos sus subcomponentes).
    cliente.responder("GET", es_ruta_solutioncomponents,
                      (200, {"value": componentes if componentes is not None else [{"solutioncomponentid": "x", "rootcomponentbehavior": 0}]}, {}))

    def al_crear(*_):
        estado["existe"] = True
        estado["recien_creada"] = True
        return (204, None, {})

    def al_ajustar(*_):
        estado["auditoria_ajustada"] = True
        return (204, None, {})

    cliente.responder("PUT", lambda r: "/Attributes(LogicalName=" in r, al_ajustar)

    cliente.responder("POST", "RelationshipDefinitions", al_crear)
    cliente.responder("POST", "PublishXml", (204, None, {}))
    return cliente


class Base(unittest.TestCase):
    def correr(self, fabrica, datos, solo_verificar=False, ident=IDENT, **kw):
        with tempfile.TemporaryDirectory() as dd:
            ruta = os.path.join(dd, "playbook.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(ident, datos))
            return rl.construir(ruta, solo_verificar, fabrica, **kw)

    def con_cliente(self, cliente, datos, **kw):
        return self.correr(lambda: cliente, datos, **kw)


class ValidacionSinRed(Base):
    CASOS = [
        (["tipo"], "tabla", "tipo"),
        (["nombre"], "sanic_carp_rel_Cliente", "nombre"),
        (["nombre"], "sanic_cliente_arreglo", "nombre"),
        (["nombre"], "sanic_carp_versionplantilla_arreglo_" + "a" * 90, "100"),
        (["tabla_padre"], "versionplantilla", "tabla_padre"),
        (["tabla_padre"], "email", "tabla_padre"),  # el padre es una tabla de la solución o `systemuser`, no cualquier estándar
        (["tabla_hija"], "sanic_carp_arreglo", "tabla_hija"),  # lleva el prefijo del proyecto pero no es `<prefijo>_<abrev>_tbl_<nombre>`
        (["tabla_hija"], "sanic_carp_tbl_Arreglo", "tabla_hija"),
        (["tabla_hija"], "Email", "tabla_hija"),
        (["tabla_hija"], "", "tabla_hija"),
        (["tabla_hija"], BORRAR, "tabla_hija"),
        (["comportamiento"], "cascada", "comportamiento"),
        (["sobra"], 1, "sobra"),
        (["lookup"], "sanic_versionplantillaid", "lookup"),
        (["lookup", "nombre"], "sanic_VersionPlantillaId", "lookup.nombre"),
        (["lookup", "nombre"], "otro_versionplantillaid", "lookup.nombre"),
        (["lookup", "requerida"], "si", "lookup.requerida"),
        (["lookup", "auditoria"], None, "lookup.auditoria"),
        (["lookup", "largo"], 10, "largo"),
        # displayname: un objeto con un texto por CADA idioma de `idiomas`, nunca una cadena única
        (["lookup", "displayname"], "", "lookup.displayname"),
        (["lookup", "displayname"], "Version de plantilla", "lookup.displayname"),
        (["lookup", "displayname"], None, "lookup.displayname"),
        (["lookup", "displayname"], {"1033": "Template version"}, "3082"),
        (["lookup", "displayname"], {"3082": "Version de plantilla"}, "1033"),
        (["lookup", "displayname", "1034"], "Plantilla", "1034"),
        (["lookup", "displayname", "1033"], "", "lookup.displayname[1033]"),
        (["lookup", "displayname", "3082"], 5, "lookup.displayname[3082]"),
        # descripcion: prosa (cadena), el mismo texto en cada idioma
        (["lookup", "descripcion"], "", "lookup.descripcion"),
        (["lookup", "descripcion"], por_idioma("Version"), "lookup.descripcion"),
        (["lookup", "descripcion"], BORRAR, "descripcion"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta} <- {'BORRAR' if valor is BORRAR else repr(valor)}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(BASE, ruta, valor))
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)

    def test_el_nombre_visible_no_lleva_tildes_en_ningun_idioma(self):
        """BP-PP-197. `excepciones_nombres.json` está vacío: no hay excepciones heredadas, todo nombre visible con tilde se rechaza."""
        for idioma in ("1033", "3082"):
            for malo in ("Versión", "Año", "Último", "Ñandú", "Cónyuge", "Ü"):
                with self.subTest(f"{idioma} {malo}"):
                    centinela = FabricaCentinela()
                    estado, _, detalle = self.correr(centinela, con_cambio(BASE, ["lookup", "displayname", idioma], malo))
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn(f"lookup.displayname[{idioma}]", detalle)
                    self.assertIn("BP-PP-197", detalle)
                    self.assertFalse(centinela.llamada)

    def test_la_descripcion_es_prosa_y_puede_llevar_tildes(self):
        cliente = armar(ClienteSimulado(), con_cambio(BASE, ["lookup", "descripcion"], "Versión de la plantilla con la que se armó el arreglo."))
        estado, _, detalle = self.con_cliente(cliente, con_cambio(BASE, ["lookup", "descripcion"], "Versión de la plantilla con la que se armó el arreglo."))
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_nombre_tiene_que_nombrar_a_las_dos_tablas(self):
        """BP-PP-188: `<prefijo>_<abrev>_<padre>_<hijo>`, con un sufijo libre
        cuando hay más de una relación entre el mismo par de tablas."""
        estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(BASE, ["nombre"], "sanic_carp_arreglo_versionplantilla"))
        self.assertEqual(estado, "error", detalle)
        self.assertIn("sanic_carp_versionplantilla_arreglo", detalle)
        cliente = armar(ClienteSimulado(), DE_SISTEMA)
        estado, _, detalle = self.con_cliente(cliente, DE_SISTEMA)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_las_relaciones_comunes_del_diccionario_pasan_la_validacion(self):
        """REL-01 a REL-08 y REL-10 a REL-18: incluidos los nombres con sufijo `…porid` y la padre `systemuser`."""
        for fila in DICCIONARIO:
            if fila[0] in ("REL-09", "REL-19"):
                continue  # la autorreferida y la de hija estándar tienen sus propias pruebas
            with self.subTest(fila[0]):
                datos = desde_diccionario(fila)
                rl.validar_playbook(datos, IDENT)
                cliente = armar(ClienteSimulado(), datos)
                estado, _, detalle = self.con_cliente(cliente, datos)
                self.assertEqual(estado, "ya_existia", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_se_usan_los_idiomas_del_playbook_no_unos_fijos(self):
        con_tres = dict(IDENT, idiomas=[1033, 3082, 1034])
        datos = con_cambio(BASE, ["lookup", "displayname"], {"1033": "Template version", "3082": "Version de plantilla", "1034": "Plantilla"})
        cliente = armar(ClienteSimulado(), datos, existe=False, ident=con_tres)
        estado, _, detalle = self.con_cliente(cliente, datos, ident=con_tres)
        self.assertEqual(estado, "creado", detalle)
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]["Lookup"]
        self.assertEqual([x["LanguageCode"] for x in post["DisplayName"]["LocalizedLabels"]], [1033, 3082, 1034])
        self.assertEqual([x["LanguageCode"] for x in post["Description"]["LocalizedLabels"]], [1033, 3082, 1034])
        # Con tres idiomas declarados, un displayname de dos es un error de playbook.
        estado, _, detalle = self.correr(FabricaCentinela(), BASE, ident=con_tres)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("1034", detalle)
        # Con un solo idioma, el displayname lleva solo ese.
        solo = dict(IDENT, idiomas=[1033])
        unico = con_cambio(BASE, ["lookup", "displayname"], {"1033": "Template version"})
        cliente = armar(ClienteSimulado(), unico, ident=solo)
        estado, _, detalle = self.con_cliente(cliente, unico, ident=solo)
        self.assertEqual(estado, "ya_existia", detalle)


class SinDatosDelProyectoAnterior(unittest.TestCase):
    def test_el_codigo_no_trae_datos_de_ningun_proyecto(self):
        """Prefijo, abreviatura, publisher y demás salen de la Identidad del playbook, nunca del código."""
        for archivo in ("relacion.py", "muestra_relacion.py"):
            with open(os.path.join(os.path.dirname(_AQUI), archivo), encoding="utf-8") as f:
                texto = f.read().lower()
            for prohibido in ("mppp", "sanic", "sistemas_abiertos", "15946", '["lcid"]'):
                self.assertFalse(prohibido in texto, f"{archivo} lleva '{prohibido}' escrito")


class Payload(unittest.TestCase):
    def test_restringido(self):
        p = rl.construir_payload(BASE, IDENT, id_de(PADRE))
        self.assertEqual(p["@odata.type"], "Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata")
        self.assertEqual((p["SchemaName"], p["ReferencedEntity"], p["ReferencedAttribute"], p["ReferencingEntity"]), (REL, PADRE, PADRE + "id", HIJA))
        self.assertEqual(p["CascadeConfiguration"], CASCADAS["restringido"])
        k = p["Lookup"]
        self.assertEqual(k["@odata.type"], "Microsoft.Dynamics.CRM.LookupAttributeMetadata")
        self.assertEqual((k["SchemaName"], k["AttributeType"], k["AttributeTypeName"]), (LOOKUP, "Lookup", {"Value": "LookupType"}))
        self.assertEqual(k["RequiredLevel"]["Value"], "ApplicationRequired")
        self.assertEqual(k["IsAuditEnabled"]["Value"], True)
        self.assertEqual(p["AssociatedMenuConfiguration"]["Behavior"], "UseCollectionName")

    def test_una_etiqueta_por_idioma_en_el_nombre_y_el_mismo_texto_en_la_descripcion(self):
        k = rl.construir_payload(BASE, IDENT, id_de(PADRE))["Lookup"]
        self.assertEqual([(x["LanguageCode"], x["Label"]) for x in k["DisplayName"]["LocalizedLabels"]], [(1033, "Template version"), (3082, "Version de plantilla")])
        self.assertEqual([(x["LanguageCode"], x["Label"]) for x in k["Description"]["LocalizedLabels"]],
                         [(1033, BASE["lookup"]["descripcion"]), (3082, BASE["lookup"]["descripcion"])])

    def test_cada_comportamiento(self):
        for c, esperado in CASCADAS.items():
            with self.subTest(c):
                self.assertEqual(rl.construir_payload(con_cambio(BASE, ["comportamiento"], c), IDENT, "x")["CascadeConfiguration"], esperado)


class Caminos(Base):
    def test_no_existe_se_crea_se_publica_y_se_verifica(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, REL)
        self.assertIn(META_REL, detalle)
        escrituras = [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]
        ruta_lookup = f"EntityDefinitions(LogicalName='{HIJA}')/Attributes(LogicalName='{LOOKUP}')"
        self.assertEqual(escrituras, [("POST", "RelationshipDefinitions"), ("PUT", ruta_lookup), ("POST", "PublishXml")])
        put = [l for l in cliente.llamadas if l["metodo"] == "PUT"][0]
        self.assertEqual(put["cuerpo"]["@odata.type"], "Microsoft.Dynamics.CRM.LookupAttributeMetadata")
        self.assertEqual(put["cuerpo"]["IsAuditEnabled"]["Value"], True)
        self.assertEqual(put["cuerpo"]["Targets"], [PADRE])  # el resto de la definición se conserva
        self.assertEqual(put["cabeceras"], {"MSCRM.MergeLabels": "true"})
        self.assertEqual(put["solucion"], IDENT["solucion"])
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]
        self.assertEqual(post["solucion"], IDENT["solucion"])
        self.assertEqual(post["cuerpo"]["ReferencedAttribute"], PADRE + "id")
        xml = [l for l in cliente.llamadas if l["ruta"] == "PublishXml"][0]["cuerpo"]["ParameterXml"]
        self.assertIn(f"<entity>{PADRE}</entity>", xml)
        self.assertIn(f"<entity>{HIJA}</entity>", xml)

    def test_un_lookup_sin_auditoria_no_necesita_ajuste(self):
        cliente = armar(ClienteSimulado(), PARENTAL, existe=False)
        estado, _, detalle = self.con_cliente(cliente, PARENTAL)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual([l for l in cliente.llamadas if l["metodo"] == "PUT"], [])
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]
        self.assertEqual(post["CascadeConfiguration"], CASCADAS["parental"])

    def test_si_falla_el_ajuste_de_la_auditoria_dice_que_quedo_incompleta(self):
        cliente = ClienteSimulado()
        cliente.responder("PUT", lambda r: True, (400, {"error": "boom"}, {}))
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("incompleta", detalle)
        self.assertIn("--corregir-lookup", detalle)

    def test_corregir_lookup_repara_solo_la_auditoria(self):
        sin_aud = con_cambio(cuerpo_lookup(BASE), ["IsAuditEnabled"], aud(False))
        with tempfile.TemporaryDirectory() as dd:
            ruta = os.path.join(dd, "p.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(IDENT, BASE))
            cliente = armar(ClienteSimulado(), BASE, lookup=sin_aud)
            rl.construir(ruta, False, lambda: cliente, corregir_lookup=True)
            self.assertEqual([l["metodo"] for l in cliente.llamadas if l["metodo"] != "GET"], ["PUT", "POST"])
            otra = con_cambio(sin_aud, ["RequiredLevel"], rl_("None"))
            cliente = armar(ClienteSimulado(), BASE, lookup=otra)
            estado, _, detalle = rl.construir(ruta, False, lambda: cliente, corregir_lookup=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("RequiredLevel", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_hacia_una_tabla_del_sistema_solo_publica_la_hija(self):
        cliente = armar(ClienteSimulado(), DE_SISTEMA, existe=False)
        estado, _, detalle = self.con_cliente(cliente, DE_SISTEMA)
        self.assertEqual(estado, "creado", detalle)
        xml = [l for l in cliente.llamadas if l["ruta"] == "PublishXml"][0]["cuerpo"]["ParameterXml"]
        self.assertNotIn("systemuser", xml)
        self.assertEqual([l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]["ReferencedAttribute"], "systemuserid")

    def test_ya_existia_no_escribe(self):
        for d in (BASE, PARENTAL, DE_SISTEMA):
            with self.subTest(d["nombre"]):
                cliente = armar(ClienteSimulado(), d)
                estado, _, detalle = self.con_cliente(cliente, d)
                self.assertEqual(estado, "ya_existia", detalle)
                self.assertIn(META_REL, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_sin_relacion_es_error_y_no_crea(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE, solo_verificar=True)
        self.assertEqual(estado, "error")
        self.assertIn("no existe", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_falta_una_de_las_tablas_bloquea_sin_crear(self):
        for falta in (PADRE, HIJA):
            with self.subTest(falta):
                tablas = {t: {"PrimaryIdAttribute": t + "id", "IsCustomEntity": True} for t in (PADRE, HIJA) if t != falta}
                cliente = armar(ClienteSimulado(), BASE, existe=False, tablas=tablas)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertIn(falta, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_la_creacion_falla(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "RelationshipDefinitions", (400, {"error": "ya hay una relación parental"}, {}))
        armar(cliente, PARENTAL, existe=False)
        estado, _, detalle = self.con_cliente(cliente, PARENTAL)
        self.assertEqual(estado, "error")
        self.assertIn("HTTP 400", detalle)

    def test_si_falla_publicar_lo_dice(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "PublishXml", (500, {"error": "boom"}, {}))
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("publicar", detalle)
        self.assertIn("--publicar", detalle)

    def test_publicar_republica_una_relacion_que_coincide_y_nada_mas(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE, publicar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual([(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"], [("POST", "PublishXml")])


class Diferencias(Base):
    def test_cada_diferencia(self):
        r, k = cuerpo_relacion(BASE), cuerpo_lookup(BASE)
        casos = [
            ("ReferencedEntity", dict(relacion=con_cambio(r, ["ReferencedEntity"], "sanic_carp_tbl_otra"))),
            ("ReferencingEntity", dict(relacion=con_cambio(r, ["ReferencingEntity"], "sanic_carp_tbl_otra"))),
            ("ReferencingAttribute", dict(relacion=con_cambio(r, ["ReferencingAttribute"], "sanic_otroid"))),
            ("IsManaged", dict(relacion=con_cambio(r, ["IsManaged"], True))),
            # El formato nunca cubre una relación jerárquica: es una diferencia real aunque en una no autorreferida la plataforma no la admita.
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], True))),
            ("CascadeConfiguration.Delete", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Delete"], "RemoveLink"))),
            ("CascadeConfiguration.Assign", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Assign"], "Cascade"))),
            (f"{LOOKUP}.RequiredLevel", dict(lookup=con_cambio(k, ["RequiredLevel"], rl_("None")))),
            (f"{LOOKUP}.IsAuditEnabled", dict(lookup=con_cambio(k, ["IsAuditEnabled"], aud(False)))),
            (f"{LOOKUP}.IsSecured", dict(lookup=con_cambio(k, ["IsSecured"], True))),
            (f"{LOOKUP}.DisplayName[1033]", dict(lookup=con_cambio(k, ["DisplayName"], label({"1033": "Otro", "3082": "Version de plantilla"})))),
            (f"{LOOKUP}.DisplayName[3082]", dict(lookup=con_cambio(k, ["DisplayName"], label({"1033": "Template version", "3082": "Otro"})))),
            (f"{LOOKUP}.DisplayName[3082]", dict(lookup=con_cambio(k, ["DisplayName"], label({"1033": "Template version"})))),  # falta el idioma
            (f"{LOOKUP}.DisplayName tiene etiquetas en un idioma que el playbook no declara", dict(lookup=con_cambio(k, ["DisplayName"], label(BASE["lookup"]["displayname"], otros_idiomas=[(1034, "Plantilla")])))),
            (f"{LOOKUP}.Description[1033]", dict(lookup=con_cambio(k, ["Description"], label({"1033": "Otra.", "3082": BASE["lookup"]["descripcion"]})))),
            (f"{LOOKUP}.Description[3082]", dict(lookup=con_cambio(k, ["Description"], label({"1033": BASE["lookup"]["descripcion"], "3082": "Otra."})))),
            (f"{LOOKUP}.Description tiene etiquetas en un idioma que el playbook no declara", dict(lookup=con_cambio(k, ["Description"], label(BASE["lookup"]["descripcion"], otros_idiomas=[(1034, "x")])))),
            (f"{LOOKUP}.Targets", dict(lookup=con_cambio(k, ["Targets"], ["account"]))),
            (f"{LOOKUP}.tipo", dict(lookup=con_cambio(k, ["AttributeTypeName"], {"Value": "StringType"}))),
            ("pertenencia a la solución", dict(componentes=[])),
            ("no incluye todos sus subcomponentes", dict(componentes=[{"solutioncomponentid": "x", "rootcomponentbehavior": 1}])),
        ]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_una_descripcion_ausente_en_el_entorno_se_lee_como_vacia_y_difiere(self):
        cliente = armar(ClienteSimulado(), BASE, lookup=con_cambio(cuerpo_lookup(BASE), ["Description"], None))
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn(f"{LOOKUP}.Description[1033]", detalle)


class FormaYHttpInesperados(Base):
    def test_http_inesperado_es_error_y_nombra_la_consulta(self):
        for matcher, nombre in [(lambda r: r.startswith("EntityDefinitions(LogicalName=") and "/Attributes" not in r, "EntityDefinitions"),
                                (lambda r: r.startswith("RelationshipDefinitions(SchemaName="), "RelationshipDefinitions"),
                                (lambda r: "/Attributes(LogicalName=" in r, "LookupAttributeMetadata"),
                                (es_ruta_solutioncomponents, "solutioncomponents"), (es_ruta_solutions, "solutions")]:
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder("GET", matcher, (500, {"error": "boom"}, {}))
                armar(cliente, BASE)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertIn(nombre, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_es_error_y_nombra_el_campo(self):
        r, k = cuerpo_relacion(BASE), cuerpo_lookup(BASE)
        casos = [
            ("MetadataId", dict(relacion=con_cambio(r, ["MetadataId"], None))),
            ("ReferencedEntity", dict(relacion=con_cambio(r, ["ReferencedEntity"], 5))),
            ("IsManaged", dict(relacion=con_cambio(r, ["IsManaged"], "false"))),
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], None))),
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], "false"))),
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], 0))),
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], BORRAR))),
            ("CascadeConfiguration", dict(relacion=con_cambio(r, ["CascadeConfiguration"], None))),
            ("CascadeConfiguration.Delete", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Delete"], None))),
            ("RequiredLevel.Value", dict(lookup=con_cambio(k, ["RequiredLevel", "Value"], 0))),
            ("IsAuditEnabled", dict(lookup=con_cambio(k, ["IsAuditEnabled"], True))),
            ("Targets", dict(lookup=con_cambio(k, ["Targets"], PADRE))),
            ("Targets[0]", dict(lookup=con_cambio(k, ["Targets"], [None]))),
            ("DisplayName", dict(lookup=con_cambio(k, ["DisplayName"], None))),
            ("DisplayName.LocalizedLabels[0].LanguageCode", dict(lookup=con_cambio(k, ["DisplayName"], {"LocalizedLabels": [{"Label": "x", "LanguageCode": "1033"}]}))),
            ("Description.LocalizedLabels", dict(lookup=con_cambio(k, ["Description"], {"LocalizedLabels": "x"}))),
            ("value[0].rootcomponentbehavior", dict(componentes=[{"solutioncomponentid": "x", "rootcomponentbehavior": None}])),
            ("PrimaryIdAttribute", dict(tablas={PADRE: {"PrimaryIdAttribute": None, "IsCustomEntity": True}, HIJA: {"PrimaryIdAttribute": HIJA + "id", "IsCustomEntity": True}})),
        ]
        for campo, kw in casos:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())


def sin_escrituras(cliente):
    return [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]


class Jerarquica(Base):
    """Variante A, validación previa: la clave `jerarquica` está SOLO cuando `tabla_padre` es igual a `tabla_hija`, y vale `false`."""

    def _error(self, datos, esperado):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, datos, verificadas=TODO_VERIFICADO, permitir_no_verificadas=True)
        self.assertEqual(estado, "error", detalle)
        self.assertIn(esperado, detalle)
        self.assertNotIn("fallo inesperado", detalle)
        self.assertFalse(centinela.llamada)

    def test_en_una_relacion_comun_la_clave_no_esta(self):
        for valor in (False, True, "false", None, 0):
            with self.subTest(repr(valor)):
                self._error(dict(BASE, jerarquica=valor), "jerarquica")

    def test_una_autorreferida_sin_la_clave_es_error(self):
        self._error(con_cambio(AUTORREFERIDA, ["jerarquica"], BORRAR), "jerarquica")

    def test_una_autorreferida_solo_admite_false(self):
        for valor in (True, "false", "true", 0, 1, None, [], {}):
            with self.subTest(repr(valor)):
                self._error(con_cambio(AUTORREFERIDA, ["jerarquica"], valor), "jerarquica")

    def test_una_jerarquica_dice_que_es_otro_tipo_de_playbook(self):
        self._error(con_cambio(AUTORREFERIDA, ["jerarquica"], True), "otro tipo de playbook")

    def test_una_autorreferida_nunca_es_parental(self):
        self._error(con_cambio(AUTORREFERIDA, ["comportamiento"], "parental"), "parental")

    def test_el_lookup_de_una_autorreferida_no_es_requerido(self):
        self._error(con_cambio(AUTORREFERIDA, ["lookup", "requerida"], True), "lookup.requerida")

    def test_la_autorreferida_valida_su_nombre_con_el_padre_igual_al_hijo(self):
        for nombre in ("sanic_carp_conversacion_arreglo", "sanic_carp_conversacion", "sanic_carp_conversacion_conversacion_Unida"):
            with self.subTest(nombre):
                self._error(con_cambio(AUTORREFERIDA, ["nombre"], nombre), "sanic_carp_conversacion_conversacion")

    def test_los_comportamientos_que_admite_una_autorreferida(self):
        for comportamiento in ("restringido", "quitar_vinculo"):
            with self.subTest(comportamiento):
                rl.validar_playbook(con_cambio(AUTORREFERIDA, ["comportamiento"], comportamiento), IDENT)

    def test_el_nombre_admite_el_sufijo_del_lookup_cuando_hay_mas_de_una(self):
        for nombre in ("sanic_carp_conversacion_conversacion", "sanic_carp_conversacion_conversacion_unidaaid", "sanic_carp_conversacion_conversacion_duplicadaid"):
            with self.subTest(nombre):
                rl.validar_playbook(con_cambio(AUTORREFERIDA, ["nombre"], nombre), IDENT)


class SufijosPorId(Base):
    """REL-10 a REL-16 (`systemuser`): el lookup termina en `…porid` y la relación lleva ese nombre como sufijo."""

    def test_los_nombres_de_lookup_y_de_relacion_con_porid_se_aceptan(self):
        for fila in DICCIONARIO:
            if fila[2] != "systemuser":
                continue
            with self.subTest(fila[0]):
                datos = desde_diccionario(fila)
                rl.validar_playbook(datos, IDENT)

    def test_la_padre_systemuser_no_puede_ser_parental(self):
        """`relacion.md` §4. La herramienta no lo impone (lo hace la plataforma y el diseño); el playbook de `systemuser` sigue siendo restringido o quitar vínculo."""
        cliente = armar(ClienteSimulado(), DE_SISTEMA, existe=False)
        estado, _, detalle = self.con_cliente(cliente, DE_SISTEMA)
        self.assertEqual(estado, "creado", detalle)
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]
        self.assertEqual(post["CascadeConfiguration"], CASCADAS["restringido"])
        self.assertEqual(post["Lookup"]["SchemaName"], "sanic_anuladoporid")

    def test_el_sufijo_tiene_que_ser_el_del_diccionario(self):
        estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(DE_SISTEMA, ["nombre"], "sanic_carp_systemuser_arreglo_anuladopor_id"))
        self.assertEqual(estado, "error", detalle)
        self.assertIn("nombre", detalle)

    def test_rel_05_parental_con_cuota_requerida(self):
        d = rel("REL-05")
        self.assertEqual((d["comportamiento"], d["lookup"]["requerida"], d["lookup"]["nombre"]), ("parental", True, "sanic_arregloid"))
        cliente = armar(ClienteSimulado(), d, existe=False)
        estado, _, detalle = self.con_cliente(cliente, d)
        self.assertEqual(estado, "creado", detalle)
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]
        self.assertEqual(post["CascadeConfiguration"], CASCADAS["parental"])
        self.assertEqual(post["Lookup"]["RequiredLevel"]["Value"], "ApplicationRequired")
        self.assertNotIn("IsHierarchical", post)  # la clave solo se manda en la variante A


class VariantesNoVerificadas(Base):
    """Variante A (autorreferida no jerárquica) mientras no esté verificada contra la plataforma: `bloqueado`, y solo se ensaya
    sobre una tabla descartable con `--permitir-no-verificadas`."""

    def test_sin_el_flag_es_bloqueado_y_explica_que_falta(self):
        centinela = FabricaCentinela()
        estado, comp, detalle = self.correr(centinela, AUTORREFERIDA)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertEqual(comp, AUTORREFERIDA["nombre"])
        self.assertFalse(centinela.llamada)  # ni siquiera se construye el cliente de Dataverse
        for parte in ("no verificad", "--permitir-no-verificadas", "_tbl_zz", "tabla descartable", "IsHierarchical", "parental", "ciclos",
                      "apunte a sí mismo", "0 ciclos"):
            self.assertIn(parte, detalle)

    def test_con_el_flag_sobre_una_tabla_que_no_es_descartable_sigue_bloqueado(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, AUTORREFERIDA, permitir_no_verificadas=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("solo vale sobre una tabla descartable", detalle)
        self.assertIn(AUTORREFERIDA["tabla_hija"], detalle)
        self.assertFalse(centinela.llamada)

    def test_un_sufijo_zz_en_el_nombre_de_la_relacion_no_vuelve_descartable_a_una_tabla_real(self):
        """El ensayo crea la columna lookup en la tabla: lo que tiene que ser descartable es la tabla, no el nombre de la relación."""
        datos = con_cambio(AUTORREFERIDA, ["nombre"], "sanic_carp_conversacion_conversacion_zzensayo")
        estado, _, detalle = self.correr(FabricaCentinela(), datos, permitir_no_verificadas=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("solo vale sobre una tabla descartable", detalle)

    def test_el_flag_no_salta_la_validacion_del_playbook(self):
        for cambio in ([["comportamiento"], "parental"], [["lookup", "requerida"], True], [["jerarquica"], True]):
            with self.subTest(cambio):
                estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(DESCARTABLE, *cambio), permitir_no_verificadas=True)
                self.assertEqual(estado, "error", detalle)

    def test_la_validacion_va_antes_que_el_bloqueo(self):
        estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(AUTORREFERIDA, ["comportamiento"], "parental"))
        self.assertEqual(estado, "error", detalle)
        self.assertIn("parental", detalle)

    def test_con_el_flag_sobre_una_tabla_descartable_corre_la_receta_completa(self):
        cliente = armar(ClienteSimulado(), DESCARTABLE, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, DESCARTABLE, permitir_no_verificadas=True)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, DESCARTABLE["nombre"])
        ruta_lookup = f"EntityDefinitions(LogicalName='{ZZ_TABLA}')/Attributes(LogicalName='sanic_unidaaid')"
        # alta, ajuste de la auditoría del lookup (va auditado) y una sola publicación
        self.assertEqual(sin_escrituras(cliente), [("POST", "RelationshipDefinitions"), ("PUT", ruta_lookup), ("POST", "PublishXml")])
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]
        self.assertEqual(post["solucion"], IDENT["solucion"])
        c = post["cuerpo"]
        self.assertIs(c["IsHierarchical"], False)  # EXPLÍCITO
        self.assertEqual((c["ReferencedEntity"], c["ReferencingEntity"], c["ReferencedAttribute"]), (ZZ_TABLA, ZZ_TABLA, ZZ_TABLA + "id"))
        self.assertEqual(c["CascadeConfiguration"], CASCADAS["quitar_vinculo"])
        self.assertEqual(c["Lookup"]["RequiredLevel"]["Value"], "None")
        xml = [l for l in cliente.llamadas if l["ruta"] == "PublishXml"][0]["cuerpo"]["ParameterXml"]
        self.assertEqual(xml.count("<entity>"), 1)  # la tabla se publica UNA vez, no dos
        self.assertIn(f"<entity>{ZZ_TABLA}</entity>", xml)

    def test_segunda_ejecucion_es_ya_existia_y_no_escribe(self):
        cliente = armar(ClienteSimulado(), DESCARTABLE, existe=False)
        self.assertEqual(self.con_cliente(cliente, DESCARTABLE, permitir_no_verificadas=True)[0], "creado")
        del cliente.llamadas[:]
        estado, _, detalle = self.con_cliente(cliente, DESCARTABLE, permitir_no_verificadas=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn(META_REL, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_lookup_sin_auditoria_no_necesita_ajuste(self):
        datos = con_cambio(DESCARTABLE, ["lookup", "auditoria"], False)
        cliente = armar(ClienteSimulado(), datos, existe=False)
        estado, _, detalle = self.con_cliente(cliente, datos, permitir_no_verificadas=True)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(sin_escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])

    def test_una_vez_verificada_corre_sin_el_flag(self):
        cliente = armar(ClienteSimulado(), AUTORREFERIDA)
        estado, _, detalle = self.con_cliente(cliente, AUTORREFERIDA, verificadas=TODO_VERIFICADO)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_la_relacion_comun_no_necesita_el_flag(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE)  # verificadas por omisión: la variante A no está
        self.assertEqual(estado, "ya_existia", detalle)

    def test_por_omision_la_variante_a_no_esta_verificada(self):
        self.assertEqual(rl.AUTORREFERIDA, "variante:autorreferida")
        self.assertNotIn(rl.AUTORREFERIDA, rl.VERIFICADAS_EN_PLATAFORMA)

    def test_solo_verificar_tambien_respeta_el_bloqueo(self):
        estado, _, detalle = self.correr(FabricaCentinela(), AUTORREFERIDA, solo_verificar=True)
        self.assertEqual(estado, "bloqueado", detalle)


class Autorreferida(Base):
    """Variante A, ya admitida (verificadas = todo): payload, verificación en los tres caminos y diferencias."""

    def correr(self, fabrica, datos, **kw):
        return super().correr(fabrica, datos, verificadas=TODO_VERIFICADO, **kw)

    def test_payload(self):
        p = rl.construir_payload(DESCARTABLE, IDENT, ZZ_TABLA + "id")
        self.assertIs(p["IsHierarchical"], False)
        self.assertEqual((p["SchemaName"], p["ReferencedEntity"], p["ReferencingEntity"], p["ReferencedAttribute"]),
                         ("sanic_carp_zzconv_zzconv", ZZ_TABLA, ZZ_TABLA, ZZ_TABLA + "id"))
        self.assertEqual(p["CascadeConfiguration"], CASCADAS["quitar_vinculo"])

    def test_el_payload_de_una_relacion_comun_no_manda_la_jerarquia(self):
        self.assertNotIn("IsHierarchical", rl.construir_payload(BASE, IDENT, "x"))

    def test_diferencias_propias_de_la_variante(self):
        r, k = cuerpo_relacion(DESCARTABLE), cuerpo_lookup(DESCARTABLE)
        casos = [
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], True))),
            ("ReferencedEntity", dict(relacion=con_cambio(r, ["ReferencedEntity"], "sanic_carp_tbl_otra"))),
            ("ReferencingEntity", dict(relacion=con_cambio(r, ["ReferencingEntity"], "sanic_carp_tbl_otra"))),
            ("ReferencedAttribute", dict(relacion=con_cambio(r, ["ReferencedAttribute"], "otroid"))),
            ("CascadeConfiguration.Delete", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Delete"], "Cascade"))),
            ("CascadeConfiguration.Reparent", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Reparent"], "Cascade"))),
            (f"{DESCARTABLE['lookup']['nombre']}.Targets", dict(lookup=con_cambio(k, ["Targets"], ["sanic_carp_tbl_otra"]))),
            (f"{DESCARTABLE['lookup']['nombre']}.RequiredLevel", dict(lookup=con_cambio(k, ["RequiredLevel"], rl_("ApplicationRequired")))),
            ("pertenencia a la solución", dict(componentes=[])),
            ("no incluye todos sus subcomponentes", dict(componentes=[{"solutioncomponentid": "x", "rootcomponentbehavior": 1}])),
        ]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), DESCARTABLE, **kw)
                estado, _, detalle = self.con_cliente(cliente, DESCARTABLE, solo_verificar=True)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_de_is_hierarchical(self):
        for valor in (None, "false", 0, BORRAR):
            with self.subTest(repr(valor)):
                cliente = armar(ClienteSimulado(), DESCARTABLE, relacion=con_cambio(cuerpo_relacion(DESCARTABLE), ["IsHierarchical"], valor))
                estado, _, detalle = self.con_cliente(cliente, DESCARTABLE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn("'IsHierarchical'", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_los_tres_caminos_usan_la_misma_verificacion(self):
        """Una diferencia en lo propio de la variante se ve igual con --solo-verificar, con --publicar y al releer tras crear."""
        mala = con_cambio(cuerpo_relacion(DESCARTABLE), ["IsHierarchical"], True)
        for kw in (dict(solo_verificar=True), dict(publicar=True), dict()):
            with self.subTest(kw):
                cliente = armar(ClienteSimulado(), DESCARTABLE, relacion=mala)
                estado, _, detalle = self.con_cliente(cliente, DESCARTABLE, **kw)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn("IsHierarchical", detalle)
                self.assertFalse(cliente.hubo_escritura())
        # al releer tras crear: la plataforma devuelve la relación como jerárquica y la herramienta lo reporta como error
        cliente = armar(ClienteSimulado(), DESCARTABLE, existe=False, relacion=mala)
        estado, _, detalle = self.con_cliente(cliente, DESCARTABLE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("no coincide con el playbook al releer", detalle)
        self.assertIn("IsHierarchical", detalle)


class HijaEstandar(Base):
    """Variante B (la tabla hija es estándar, por ejemplo `email`): sin ensayar, siempre `bloqueado`. NO se implementa la creación.
    La padre `systemuser` NO es este caso: el padre estándar ya estaba soportado (se referencia, no se modifica)."""

    @staticmethod
    def tablas(hija_propia, padre="systemuser", hija="email"):
        return {padre: {"PrimaryIdAttribute": id_de(padre), "IsCustomEntity": padre != "systemuser"},
                hija: {"PrimaryIdAttribute": "activityid", "IsCustomEntity": hija_propia}}

    def test_rel_19_pasa_la_validacion_previa(self):
        rl.validar_playbook(HIJA_ESTANDAR, IDENT)

    def test_rel_19_es_bloqueado_con_el_motivo_exacto_y_no_escribe(self):
        for existe in (False, True):
            for kw in (dict(), dict(solo_verificar=True), dict(permitir_no_verificadas=True), dict(publicar=True), dict(corregir_lookup=True)):
                with self.subTest(existe=existe, **kw):
                    cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=existe, tablas=self.tablas(False))
                    estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR, **kw)
                    self.assertEqual(estado, "bloqueado", detalle)
                    for parte in ("'email'", "IsCustomEntity", "variante B", "sin ensayar", "CanBeReferencing", "CanBeReferenced", "pertenencia a la solución", "publicación"):
                        self.assertIn(parte, detalle)
                    self.assertFalse(cliente.hubo_escritura())

    def test_una_tabla_de_la_solucion_que_no_es_propia_tambien_bloquea(self):
        """Protege la relación común de crear por error un lookup sobre una tabla estándar."""
        cliente = armar(ClienteSimulado(), BASE, existe=False, tablas=self.tablas(False, PADRE, HIJA))
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("IsCustomEntity", detalle)
        self.assertIn(HIJA, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_tabla_propia_de_otro_publisher_no_es_de_la_solucion(self):
        datos = dict(rel("REL-12"), nombre="sanic_carp_systemuser_otro_tabla", tabla_hija="otro_tabla")
        cliente = armar(ClienteSimulado(), datos, existe=False, tablas=self.tablas(True, "systemuser", "otro_tabla"))
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("otro_tabla", detalle)
        self.assertIn("no es una tabla de la solución", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_forma_de_is_custom_entity_se_valida_por_tipo(self):
        for valor in (None, "false", "true", 0, 1, [], {}, BORRAR):
            for datos, tablas in ((HIJA_ESTANDAR, self.tablas(valor)), (BASE, None)):
                with self.subTest(repr(valor), nombre=datos["nombre"]):
                    if tablas is None:
                        tablas = {PADRE: {"PrimaryIdAttribute": PADRE + "id", "IsCustomEntity": True}, HIJA: {"PrimaryIdAttribute": HIJA + "id", "IsCustomEntity": valor}}
                    if valor is BORRAR:
                        tablas[datos["tabla_hija"]].pop("IsCustomEntity")
                    cliente = armar(ClienteSimulado(), datos, existe=False, tablas=tablas)
                    estado, _, detalle = self.con_cliente(cliente, datos)
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn("forma inesperada", detalle)
                    self.assertIn("'IsCustomEntity'", detalle)
                    self.assertNotIn("fallo inesperado", detalle)
                    self.assertFalse(cliente.hubo_escritura())

    def test_el_padre_estandar_sigue_soportado(self):
        """`systemuser` es la tabla padre (se referencia, no se modifica): IsCustomEntity = false NO la bloquea."""
        cliente = armar(ClienteSimulado(), DE_SISTEMA, existe=False)
        self.assertFalse(cliente.llamadas)
        estado, _, detalle = self.con_cliente(cliente, DE_SISTEMA)
        self.assertEqual(estado, "creado", detalle)

    def test_la_hija_estandar_pasa_la_validacion_pero_un_nombre_con_el_prefijo_del_proyecto_no(self):
        for hija in ("sanic_carp_arreglo", "sanic_carp_tbl_Arreglo", "Email"):
            with self.subTest(hija):
                datos = dict(HIJA_ESTANDAR, tabla_hija=hija, nombre=f"sanic_carp_systemuser_{hija}")
                estado, _, detalle = self.correr(FabricaCentinela(), datos)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("tabla_hija", detalle)


class Banderas(unittest.TestCase):
    def _correr(self, *argv):
        vistos = {}
        original_argv, original_salida, original_construir = sys.argv, rl.salida, rl.construir
        sys.argv = ["relacion.py", *argv]
        rl.salida = lambda estado, comp, det: vistos.update(estado=estado, detalle=det) or 1

        def falso(ruta, solo_verificar, fabrica, **kw):
            vistos["kw"] = dict(kw, solo_verificar=solo_verificar)
            return "ya_existia", "x", "y"

        rl.construir = falso
        try:
            rl.main()
        finally:
            sys.argv, rl.salida, rl.construir = original_argv, original_salida, original_construir
        return vistos

    def test_una_bandera_que_no_existe_es_error(self):
        r = self._correr("p.md", "--completar", "--forzar")
        self.assertEqual(r["estado"], "error")
        self.assertIn("--completar", r["detalle"])
        self.assertIn("--forzar", r["detalle"])
        self.assertNotIn("kw", r)  # ni se llegó a construir

    def test_cada_bandera_llega_a_construir(self):
        esperadas = {"--solo-verificar": "solo_verificar", "--publicar": "publicar", "--corregir-lookup": "corregir_lookup",
                     "--permitir-no-verificadas": "permitir_no_verificadas"}
        self.assertEqual(set(esperadas), set(rl.BANDERAS))
        for bandera, nombre in esperadas.items():
            with self.subTest(bandera):
                con = self._correr("p.md", bandera)["kw"]
                sin = self._correr("p.md")["kw"]
                self.assertTrue(con[nombre], con)
                self.assertFalse(sin[nombre], sin)
                self.assertEqual({k for k, v in con.items() if v is True} - {nombre}, set())

    def test_sin_playbook_o_con_dos_es_uso_incorrecto(self):
        for argv in ((), ("a.md", "b.md")):
            r = self._correr(*argv)
            self.assertEqual(r["estado"], "error")
            self.assertIn("uso incorrecto", r["detalle"])


if __name__ == "__main__":
    unittest.main()
