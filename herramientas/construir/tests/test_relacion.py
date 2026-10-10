"""Herramienta `relacion.py`: una relación 1:N con su columna lookup, desde un
playbook. Ninguna prueba toca la red. Los datos de ejemplo son las relaciones
del diccionario de datos del proyecto (`diseno/02-diccionario-datos.md`, §6.2)."""
import copy
import os
import re
import sys
import tempfile
import unittest
from unittest import mock

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import relacion as rl  # noqa: E402
from _comun import dividir_secciones, leer_texto, obtener_componente, obtener_identidad  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from credenciales import RAIZ_PROYECTO, cargar_configuracion  # noqa: E402
from fixtures import (IDENTIDAD_VALIDA, SOLUTION_ID, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents, es_ruta_solutions, label, playbook_md,  # noqa: E402
                      por_idioma)

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
TODO_VERIFICADO = frozenset({"variante:autorreferida"})  # la variante A (autorreferida) como característica verificada
SIN_VERIFICAR = frozenset()  # ninguna característica de variante verificada: sirve para probar el mecanismo del gate «Ensayo»
# Lo que el ensayo del 2026-10-09 (Dev) observó de la variante B (hija estándar): la plataforma responde CanBeReferencing / CanBeReferenced por acción,
# y una relación nace con 1 fila en solutioncomponents (componenttype 10), su lookup con 1 fila (componenttype 2) y la tabla estándar con 1 fila
# (componenttype 1, rootcomponentbehavior 1 aunque ya estuviera por otra columna).
COMPONENTE_RELACION, COMPONENTE_LOOKUP, COMPONENTE_TABLA_SC = 10, 2, 1
FILA_SC = {"solutioncomponentid": "sc-1", "rootcomponentbehavior": 0}  # relación y lookup: rootcomponentbehavior 0
FILA_TABLA_ESTANDAR = {"solutioncomponentid": "sc-2", "rootcomponentbehavior": 1}
ACCIONES_DE_ELEGIBILIDAD = ("CanBeReferencing", "CanBeReferenced")

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
    """Clave primaria de la tabla: la de `systemuser` y la de `email` (una actividad: `activityid`) no siguen la regla `<tabla>id`."""
    return {"systemuser": "systemuserid", "email": "activityid"}.get(tabla_, tabla_ + "id")


def cuerpo_relacion(d):
    return {"MetadataId": META_REL, "SchemaName": d["nombre"], "ReferencedEntity": d["tabla_padre"], "ReferencedAttribute": id_de(d["tabla_padre"]),
            "ReferencingEntity": d["tabla_hija"], "ReferencingAttribute": d["lookup"]["nombre"], "IsCustomRelationship": True, "IsManaged": False,
            "IsHierarchical": False, "RelationshipType": "OneToManyRelationship", "CascadeConfiguration": dict(CASCADAS[d["comportamiento"]], Archive="NoCascade")}


def cuerpo_lookup(d, idiomas=None):
    k = d["lookup"]
    return {"MetadataId": "a-" + k["nombre"], "LogicalName": k["nombre"], "SchemaName": k["nombre"], "AttributeTypeName": {"Value": "LookupType"},
            "Targets": [d["tabla_padre"]], "RequiredLevel": rl_("ApplicationRequired" if k["requerida"] else "None"), "IsAuditEnabled": aud(k["auditoria"]),
            "IsSecured": False, "DisplayName": label(k["displayname"], idiomas), "Description": label(k["descripcion"], idiomas)}


def es_estandar(tabla_):
    return tabla_ in ESTANDAR


def armar(cliente, d, existe=True, relacion=None, lookup=None, componentes=None, tablas=None, ident=IDENT, pertenencia=None, elegibles=None,
          agrega=("relacion", "lookup", "tabla")):
    """Configura TODAS las rutas del camino feliz, como responde la plataforma.

    - `solutioncomponents` responde por el filtro EXACTO (`_solutionid_value`, `objectid` y `componenttype`): una consulta con otro objeto, otro tipo o
      otra solución devuelve la lista vacía, como la plataforma. Tabla propia: la tabla hija es componenttype 1 (`componentes` son sus filas) y la relación
      NO tiene fila propia. Variante B (hija estándar): la relación (componenttype 10), el lookup (componenttype 2) y la tabla (componenttype 1).
      `pertenencia` fija las filas de «relacion», «lookup» o «tabla» ANTES del alta (lista; vacía = ninguna). Al crear, la plataforma agrega lo que diga
      `agrega` donde no había filas (observado: la relación y el lookup con rootcomponentbehavior 0; la tabla estándar sin fila previa se asume agregada
      con 1, como pasa con una columna: NO se observó con la relación).
    - `elegibles`: respuesta de `CanBeReferencing` / `CanBeReferenced` por tabla (por omisión, todas verdaderas). Las acciones exigen `EntityName`.
    - La lectura del lookup con `Targets` exige el cast al tipo derivado: sin él la plataforma responde 400.
    """
    armar_cliente_precondiciones_ok(cliente, ident)
    estado = {"existe": existe}
    estandar = es_estandar(d["tabla_hija"])
    tablas = tablas if tablas is not None else {d["tabla_padre"]: {"PrimaryIdAttribute": id_de(d["tabla_padre"]), "IsCustomEntity": not es_estandar(d["tabla_padre"])},
                                                d["tabla_hija"]: {"PrimaryIdAttribute": id_de(d["tabla_hija"]), "IsCustomEntity": not estandar}}
    pertenencia = dict(pertenencia or {})
    llaves = {"relacion": (COMPONENTE_RELACION, META_REL), "lookup": (COMPONENTE_LOOKUP, "a-" + d["lookup"]["nombre"]), "tabla": (COMPONENTE_TABLA_SC, "t-" + d["tabla_hija"])}
    filas = {}
    if estandar:
        filas["tabla"] = [FILA_TABLA_ESTANDAR]  # la tabla estándar ya estaba en la solución por otra columna propia
        if existe:
            filas["relacion"], filas["lookup"] = [FILA_SC], [FILA_SC]
    else:
        # Una relación NO tiene fila propia: viaja dentro de la tabla hija (componenttype 1, con todos sus subcomponentes).
        filas["tabla"] = componentes if componentes is not None else [{"solutioncomponentid": "x", "rootcomponentbehavior": 0}]
    filas.update(pertenencia)

    def tabla_(ruta, *_):
        nombre = ruta.split("'")[1]
        if nombre in tablas:
            return (200, dict(tablas[nombre], LogicalName=nombre, MetadataId="t-" + nombre), {})
        return (404, {"error": "no existe"}, {})

    cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName=") and "/Attributes" not in r, tabla_)
    cliente.responder("GET", lambda r: r.startswith("RelationshipDefinitions(SchemaName="),
                      lambda *_: (200, relacion if relacion is not None else cuerpo_relacion(d), {}) if estado["existe"] else (404, {"error": "no existe"}, {}))

    def leer_lookup(ruta, *_):
        if "Targets" in ruta and "/Microsoft.Dynamics.CRM.LookupAttributeMetadata" not in ruta:
            return (400, {"error": {"message": "Could not find a property named 'Targets' on type 'Microsoft.Dynamics.CRM.AttributeMetadata'."}}, {})
        if not estado["existe"]:
            return (404, {"error": "no existe"}, {})
        if lookup is not None:
            return (200, lookup, {})
        c = cuerpo_lookup(d, ident["idiomas"])
        if estado.get("recien_creada") and not estado.get("auditoria_ajustada"):
            c["IsAuditEnabled"] = aud(False)  # verificado: la plataforma ignora la auditoría del lookup al crear
        return (200, c, {})

    cliente.responder("GET", lambda r: "/Attributes(LogicalName=" in r, leer_lookup)

    def leer_pertenencia(ruta, *_):
        m = re.search(r"_solutionid_value eq (\S+) and objectid eq (\S+) and componenttype eq (\d+)$", ruta)
        if m is None:
            return (400, {"error": "filtro de solutioncomponents inesperado"}, {})
        solucion_, objeto, tipo = m.group(1), m.group(2), int(m.group(3))
        if solucion_ != SOLUTION_ID:
            return (200, {"value": []}, {})
        return (200, {"value": [f for clave, (t, o) in llaves.items() if (t, o) == (tipo, objeto) for f in filas.get(clave, [])]}, {})

    cliente.responder("GET", es_ruta_solutioncomponents, leer_pertenencia)

    def al_crear(*_):
        estado["existe"] = True
        estado["recien_creada"] = True
        if estandar:
            for clave in agrega:
                if not filas.get(clave):  # lo que ya figuraba (p. ej. la estándar con behavior 0) no se toca
                    filas[clave] = [FILA_TABLA_ESTANDAR] if clave == "tabla" else [FILA_SC]
        return (204, None, {})

    def al_ajustar(*_):
        estado["auditoria_ajustada"] = True
        return (204, None, {})

    cliente.responder("PUT", lambda r: "/Attributes(LogicalName=" in r, al_ajustar)

    cliente.responder("POST", "RelationshipDefinitions", al_crear)
    cliente.responder("POST", "PublishXml", (204, None, {}))
    for accion in ACCIONES_DE_ELEGIBILIDAD:
        def eligible(ruta, cuerpo, solucion_, accion=accion):
            if not isinstance(cuerpo, dict) or set(cuerpo) != {"EntityName"}:
                return (400, {"error": {"message": f"The parameter 'EntityName' is required by {accion}."}}, {})
            return (200, {"@odata.context": f"{cliente.api}$metadata#Microsoft.Dynamics.CRM.{accion}Response", accion: (elegibles or {}).get(cuerpo["EntityName"], True)}, {})

        cliente.responder("POST", accion, eligible)
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


def escrituras(cliente):
    """Lo que el cliente ESCRIBIÓ: todo lo que no es GET, salvo las dos acciones de elegibilidad (`CanBeReferencing` / `CanBeReferenced`), que son POST
    pero solo preguntan."""
    return [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET" and l["ruta"] not in ACCIONES_DE_ELEGIBILIDAD]


def acciones(cliente):
    """Las preguntas de elegibilidad, en el orden en que se hicieron: `[(acción, cuerpo)]`."""
    return [(l["ruta"], l["cuerpo"]) for l in cliente.llamadas if l["ruta"] in ACCIONES_DE_ELEGIBILIDAD]


# Variante B sobre una lookup descartable: el ensayo con la hija estándar solo vale con un lookup `_zz`.
DESCARTABLE_B = con_cambio(con_cambio(HIJA_ESTANDAR, ["nombre"], "sanic_carp_systemuser_email_zzrespondidoporid"), ["lookup", "nombre"], "sanic_zzrespondidoporid")


class GateEnsayo(Base):
    """El mecanismo del gate «Ensayo»: `bloqueado` si el playbook usa algo sin verificar, y `--permitir-no-verificadas` solo sobre algo descartable
    (una tabla `_tbl_zz…` o, con una hija estándar, un lookup `_zz…`). Las variantes A y B se verificaron el 2026-10-09, así que acá se prueba con
    `verificadas=SIN_VERIFICAR`, como si una característica volviera a estar sin ensayar."""
    PENDIENTE = {rl.AUTORREFERIDA: "TEXTO DE LO QUE FALTA DE LA AUTORREFERIDA", rl.HIJA_ESTANDAR: "TEXTO DE LO QUE FALTA DE LA HIJA ESTANDAR"}

    def correr(self, fabrica, datos, **kw):
        kw.setdefault("verificadas", SIN_VERIFICAR)
        return super().correr(fabrica, datos, **kw)

    def test_sin_el_flag_es_bloqueado_y_explica_que_falta(self):
        for datos, caracteristica in ((AUTORREFERIDA, rl.AUTORREFERIDA), (HIJA_ESTANDAR, rl.HIJA_ESTANDAR)):
            with self.subTest(caracteristica), mock.patch.dict(rl.PENDIENTE_DE_VERIFICAR, self.PENDIENTE):
                centinela = FabricaCentinela()
                estado, comp, detalle = self.correr(centinela, datos)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertEqual(comp, datos["nombre"])
                self.assertFalse(centinela.llamada)  # ni siquiera se construye el cliente de Dataverse
                self.assertIn(f"({caracteristica})", detalle)
                self.assertIn(self.PENDIENTE[caracteristica], detalle)
                self.assertIn("--permitir-no-verificadas", detalle)

    def test_el_motivo_pendiente_sale_del_diccionario_de_la_herramienta(self):
        """Sin texto cargado para la característica el bloqueo sigue siendo `bloqueado` y nombra la característica (no un KeyError)."""
        with mock.patch.dict(rl.PENDIENTE_DE_VERIFICAR, clear=True):
            estado, _, detalle = self.correr(FabricaCentinela(), AUTORREFERIDA)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("variante:autorreferida", detalle)

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

    def test_con_una_hija_estandar_lo_descartable_es_el_lookup(self):
        """Una tabla estándar no puede ser descartable: lo que se ensaya y se borra después es el lookup (`_zz`), como las columnas de `columnas-estandar`."""
        for datos in (HIJA_ESTANDAR, con_cambio(HIJA_ESTANDAR, ["nombre"], "sanic_carp_systemuser_email_zz")):
            with self.subTest(datos["nombre"]):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, datos, permitir_no_verificadas=True)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertIn("solo vale sobre un lookup descartable", detalle)
                self.assertIn("'_zz'", detalle)
                self.assertIn("sanic_respondidoporid", detalle)
                self.assertFalse(centinela.llamada)

    def test_el_flag_no_salta_la_validacion_del_playbook(self):
        for cambio in ([["comportamiento"], "parental"], [["lookup", "requerida"], True], [["jerarquica"], True]):
            with self.subTest(cambio):
                estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(DESCARTABLE, *cambio), permitir_no_verificadas=True)
                self.assertEqual(estado, "error", detalle)
        estado, _, detalle = self.correr(FabricaCentinela(), con_cambio(DESCARTABLE_B, ["comportamiento"], "parental"), permitir_no_verificadas=True)
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

    def test_con_el_flag_sobre_un_lookup_descartable_de_una_hija_estandar_corre_la_receta_completa(self):
        cliente = armar(ClienteSimulado(), DESCARTABLE_B, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, DESCARTABLE_B, permitir_no_verificadas=True)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, DESCARTABLE_B["nombre"])
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])

    def test_la_relacion_comun_no_necesita_el_flag(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE)  # ninguna variante verificada: la relación común no usa ninguna
        self.assertEqual(estado, "ya_existia", detalle)

    def test_solo_verificar_tambien_respeta_el_bloqueo(self):
        for datos in (AUTORREFERIDA, HIJA_ESTANDAR):
            with self.subTest(datos["nombre"]):
                estado, _, detalle = self.correr(FabricaCentinela(), datos, solo_verificar=True)
                self.assertEqual(estado, "bloqueado", detalle)

    def test_la_auditoria_del_lookup_en_una_hija_estandar_esta_sin_ensayar_aunque_la_variante_este_verificada(self):
        """El ensayo del 2026-10-09 fue con auditoria false. Ajustar la auditoría (PUT del lookup) sobre una tabla estándar no se ensayó: es una
        característica propia que NO está verificada."""
        self.assertIn(rl.HIJA_ESTANDAR, rl.VERIFICADAS_EN_PLATAFORMA)
        self.assertNotIn(rl.HIJA_ESTANDAR_AUDITORIA, rl.VERIFICADAS_EN_PLATAFORMA)
        datos = con_cambio(HIJA_ESTANDAR, ["lookup", "auditoria"], True)
        centinela = FabricaCentinela()
        estado, comp, detalle = Base.correr(self, centinela, datos)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertEqual(comp, datos["nombre"])
        self.assertFalse(centinela.llamada)
        self.assertEqual(detalle, f"el playbook usa algo todavía no verificado contra la plataforma (variante:hija-estandar-auditoria): "
                                  f"{rl.PENDIENTE_DE_VERIFICAR[rl.HIJA_ESTANDAR_AUDITORIA]}. Primero se ensaya sobre un lookup descartable ('…_zz…') con --permitir-no-verificadas")
        # Con el lookup descartable y el flag corre, incluido el ajuste de la auditoría.
        descartable = con_cambio(DESCARTABLE_B, ["lookup", "auditoria"], True)
        cliente = armar(ClienteSimulado(), descartable, existe=False)
        estado, _, detalle = Base.correr(self, lambda: cliente, descartable, permitir_no_verificadas=True)
        self.assertEqual(estado, "creado", detalle)
        ruta_lookup = "EntityDefinitions(LogicalName='email')/Attributes(LogicalName='sanic_zzrespondidoporid')"
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"), ("PUT", ruta_lookup), ("POST", "PublishXml")])


class VariantesVerificadas(Base):
    """Las dos variantes se verificaron contra la plataforma el 2026-10-09 (Dev): corren sin `--permitir-no-verificadas`."""

    def test_por_omision_las_dos_variantes_estan_verificadas(self):
        self.assertEqual((rl.AUTORREFERIDA, rl.HIJA_ESTANDAR), ("variante:autorreferida", "variante:hija-estandar"))
        self.assertEqual(rl.VERIFICADAS_EN_PLATAFORMA, frozenset({rl.AUTORREFERIDA, rl.HIJA_ESTANDAR}))
        self.assertLessEqual(rl.VERIFICADAS_EN_PLATAFORMA, rl.CARACTERISTICAS)

    def test_lo_que_se_usa_se_deduce_del_playbook(self):
        casos = [(BASE, set()), (DE_SISTEMA, set()), (PARENTAL, set()), (AUTORREFERIDA, {rl.AUTORREFERIDA}), (HIJA_ESTANDAR, {rl.HIJA_ESTANDAR}),
                 (con_cambio(HIJA_ESTANDAR, ["lookup", "auditoria"], True), {rl.HIJA_ESTANDAR, rl.HIJA_ESTANDAR_AUDITORIA})]
        for datos, esperado in casos:
            with self.subTest(datos["nombre"], auditoria=datos["lookup"]["auditoria"]):
                self.assertEqual(rl.caracteristicas_usadas(datos, IDENT), esperado)

    def test_la_autorreferida_corre_sin_el_flag(self):
        cliente = armar(ClienteSimulado(), AUTORREFERIDA)
        estado, _, detalle = self.con_cliente(cliente, AUTORREFERIDA)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_hija_estandar_corre_sin_el_flag(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(escrituras(cliente), [])

    def test_el_flag_sobre_algo_verificado_no_cambia_nada(self):
        cliente = armar(ClienteSimulado(), AUTORREFERIDA)
        estado, _, detalle = self.con_cliente(cliente, AUTORREFERIDA, permitir_no_verificadas=True)
        self.assertEqual(estado, "ya_existia", detalle)


class Autorreferida(Base):
    """Variante A, VERIFICADA el 2026-10-09: payload, verificación en los tres caminos y diferencias."""

    def test_payload(self):
        p = rl.construir_payload(DESCARTABLE, IDENT, ZZ_TABLA + "id")
        self.assertIs(p["IsHierarchical"], False)
        self.assertEqual((p["SchemaName"], p["ReferencedEntity"], p["ReferencingEntity"], p["ReferencedAttribute"]),
                         ("sanic_carp_zzconv_zzconv", ZZ_TABLA, ZZ_TABLA, ZZ_TABLA + "id"))
        self.assertEqual(p["CascadeConfiguration"], CASCADAS["quitar_vinculo"])

    def test_el_payload_de_una_relacion_comun_no_manda_la_jerarquia(self):
        self.assertNotIn("IsHierarchical", rl.construir_payload(BASE, IDENT, "x"))

    def test_creada_se_publica_la_tabla_una_sola_vez(self):
        cliente = armar(ClienteSimulado(), DESCARTABLE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, DESCARTABLE)
        self.assertEqual(estado, "creado", detalle)
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
        self.assertEqual(self.con_cliente(cliente, DESCARTABLE)[0], "creado")
        del cliente.llamadas[:]
        estado, _, detalle = self.con_cliente(cliente, DESCARTABLE)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn(META_REL, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_lookup_sin_auditoria_no_necesita_ajuste(self):
        datos = con_cambio(DESCARTABLE, ["lookup", "auditoria"], False)
        cliente = armar(ClienteSimulado(), datos, existe=False)
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(sin_escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])

    def test_diferencias_propias_de_la_variante(self):
        """El detalle EXACTO de cada diferencia: `IsHierarchical` falso, padre e hija la misma tabla del playbook, y el destino del lookup."""
        r, k = cuerpo_relacion(DESCARTABLE), cuerpo_lookup(DESCARTABLE)
        lk = DESCARTABLE["lookup"]["nombre"]
        casos = [
            ("IsHierarchical", dict(relacion=con_cambio(r, ["IsHierarchical"], True)), "IsHierarchical: entorno=True playbook=False"),
            # padre e hija tienen que ser la tabla del playbook: cada lado por separado, y los dos corridos juntos
            ("ReferencedEntity", dict(relacion=con_cambio(r, ["ReferencedEntity"], "sanic_carp_tbl_otra")),
             f"ReferencedEntity: entorno='sanic_carp_tbl_otra' playbook='{ZZ_TABLA}'"),
            ("ReferencingEntity", dict(relacion=con_cambio(r, ["ReferencingEntity"], "sanic_carp_tbl_otra")),
             f"ReferencingEntity: entorno='sanic_carp_tbl_otra' playbook='{ZZ_TABLA}'"),
            ("las dos", dict(relacion=con_cambio(con_cambio(r, ["ReferencingEntity"], "sanic_carp_tbl_otra"), ["ReferencedEntity"], "sanic_carp_tbl_otra")),
             f"ReferencedEntity: entorno='sanic_carp_tbl_otra' playbook='{ZZ_TABLA}'; ReferencingEntity: entorno='sanic_carp_tbl_otra' playbook='{ZZ_TABLA}'"),
            ("ReferencedAttribute", dict(relacion=con_cambio(r, ["ReferencedAttribute"], "otroid")), f"ReferencedAttribute: entorno='otroid' playbook='{ZZ_TABLA}id'"),
            ("Delete", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Delete"], "Cascade")),
             "CascadeConfiguration.Delete: entorno='Cascade' playbook='RemoveLink' (comportamiento 'quitar_vinculo')"),
            ("Reparent", dict(relacion=con_cambio(r, ["CascadeConfiguration", "Reparent"], "Cascade")),
             "CascadeConfiguration.Reparent: entorno='Cascade' playbook='NoCascade' (comportamiento 'quitar_vinculo')"),
            ("Targets", dict(lookup=con_cambio(k, ["Targets"], ["sanic_carp_tbl_otra"])), f"{lk}.Targets: entorno=['sanic_carp_tbl_otra'] playbook=['{ZZ_TABLA}']"),
            ("RequiredLevel", dict(lookup=con_cambio(k, ["RequiredLevel"], rl_("ApplicationRequired"))),
             f"{lk}.RequiredLevel: entorno='ApplicationRequired' playbook='None'"),
            ("tabla sin fila", dict(componentes=[]),
             f"pertenencia a la solución: la tabla hija '{ZZ_TABLA}' tiene 0 filas en solutioncomponents, se esperaba 1 (la relación viaja dentro de ella)"),
            ("tabla sin todos sus subcomponentes", dict(componentes=[{"solutioncomponentid": "x", "rootcomponentbehavior": 1}]),
             f"la tabla hija '{ZZ_TABLA}' está en la solución pero no incluye todos sus subcomponentes (rootcomponentbehavior=1): la relación no viajaría con ella"),
        ]
        for nombre, kw, esperado in casos:
            with self.subTest(nombre):
                cliente = armar(ClienteSimulado(), DESCARTABLE, **kw)
                estado, _, detalle = self.con_cliente(cliente, DESCARTABLE, solo_verificar=True)
                self.assertEqual(estado, "difiere", detalle)
                self.assertEqual(detalle, esperado)
                self.assertFalse(cliente.hubo_escritura())

    def test_ya_no_es_una_variante_bloqueada_ninguna_diferencia_lo_vuelve_a_bloquear(self):
        for kw in (dict(), dict(solo_verificar=True), dict(publicar=True), dict(corregir_lookup=True)):
            with self.subTest(kw):
                cliente = armar(ClienteSimulado(), DESCARTABLE)
                estado, _, detalle = self.con_cliente(cliente, DESCARTABLE, **kw)
                self.assertEqual(estado, "ya_existia", detalle)

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


class Ensayo20261009VarianteA(Base):
    """Lo que la plataforma hizo en el ensayo de la variante A (tabla descartable `sanic_carp_tbl_zzrel`, 2026-10-09, Dev): la herramienta la construyó
    con `--permitir-no-verificadas` → `creado` y la verificación propia dio OK; releída, `IsHierarchical` false, ReferencedEntity = ReferencingEntity y
    Delete = RemoveLink; CascadeConfiguration trae además `Archive: RemoveLink`, propiedad que la plataforma pone sola. Con datos, la plataforma PERMITE un
    registro que se apunta a sí mismo, un ciclo B→C y C→B, y borrar con RemoveLink deja el lookup vacío; también ACEPTÓ una autorreferida parental
    (la prohibición del formato la frena la validación offline, no la plataforma)."""
    ZZREL = dict(AUTORREFERIDA, nombre="sanic_carp_zzrel_zzrel", tabla_padre="sanic_carp_tbl_zzrel", tabla_hija="sanic_carp_tbl_zzrel")

    def test_la_relacion_tal_como_la_devolvio_la_plataforma_coincide(self):
        r = con_cambio(cuerpo_relacion(self.ZZREL), ["CascadeConfiguration", "Archive"], "RemoveLink")
        cliente = armar(ClienteSimulado(), self.ZZREL, relacion=r)
        estado, _, detalle = self.con_cliente(cliente, self.ZZREL, solo_verificar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(r["CascadeConfiguration"]["Archive"], "RemoveLink")  # la propiedad nueva no es una cascada del formato: no se compara

    def test_la_prohibicion_de_la_parental_la_pone_el_formato_no_la_plataforma(self):
        """La plataforma aceptó (204) una autorreferida parental; el formato la rechaza offline, sin tocar la red, y lo dice en el mensaje."""
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, con_cambio(self.ZZREL, ["comportamiento"], "parental"))
        self.assertEqual(estado, "error", detalle)
        self.assertFalse(centinela.llamada)
        self.assertIn("'comportamiento' = 'parental' no se admite en una relación autorreferida", detalle)
        self.assertIn("la plataforma la acepta", detalle)

    def test_el_comentario_de_lo_verificado_esta_fechado(self):
        with open(os.path.join(os.path.dirname(_AQUI), "relacion.py"), encoding="utf-8") as f:
            texto = f.read()
        for parte in ("2026-10-09", "se apunte a sí mismo", "ciclos", "RemoveLink", "Archive"):
            self.assertIn(parte, texto)


class PlaybooksReales(Base):
    """Los playbooks reales del proyecto (`playbooks/relacion/`), leídos del disco: ninguno se bloquea por una variante sin ensayar."""

    @staticmethod
    def leer(nombre):
        ruta = os.path.join(RAIZ_PROYECTO, cargar_configuracion()["playbooks"], "relacion", nombre)
        secciones = dividir_secciones(leer_texto(ruta))
        return ruta, obtener_componente(secciones, "relacion"), obtener_identidad(secciones)

    def correr_real(self, nombre, existe, **armado):
        ruta, datos, ident = self.leer(nombre)
        cliente = armar(ClienteSimulado(), datos, existe=existe, ident=ident, **armado)
        return cliente, datos, rl.construir(ruta, False, lambda: cliente)

    def test_la_autorreferida_real_valida_y_no_se_bloquea(self):
        ruta, datos, ident = self.leer("sanic_carp_conversacion_conversacion.md")
        rl.validar_playbook(datos, ident)
        self.assertEqual((datos["tabla_padre"], datos["tabla_hija"], datos["jerarquica"]), ("sanic_carp_tbl_conversacion", "sanic_carp_tbl_conversacion", False))
        self.assertEqual(rl.caracteristicas_usadas(datos, ident) - set(rl.VERIFICADAS_EN_PLATAFORMA), set())
        cliente, datos, (estado, comp, detalle) = self.correr_real("sanic_carp_conversacion_conversacion.md", existe=True)
        self.assertEqual((estado, comp), ("ya_existia", "sanic_carp_conversacion_conversacion"), detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_autorreferida_real_se_crea_y_publica_la_tabla_una_vez(self):
        cliente, datos, (estado, _, detalle) = self.correr_real("sanic_carp_conversacion_conversacion.md", existe=False)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"),
                                              ("PUT", "EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Attributes(LogicalName='sanic_unidaaid')"),
                                              ("POST", "PublishXml")])  # el lookup va auditado: la plataforma ignora la auditoría al crear
        self.assertIs([l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]["IsHierarchical"], False)

    def test_la_verificacion_de_la_autorreferida_real_exige_jerarquia_falsa_y_la_misma_tabla(self):
        ruta, datos, ident = self.leer("sanic_carp_conversacion_conversacion.md")
        tabla_ = datos["tabla_hija"]
        r = cuerpo_relacion(datos)
        casos = [("IsHierarchical", con_cambio(r, ["IsHierarchical"], True), "IsHierarchical: entorno=True playbook=False"),
                 ("ReferencedEntity", con_cambio(r, ["ReferencedEntity"], "systemuser"), f"ReferencedEntity: entorno='systemuser' playbook='{tabla_}'"),
                 ("ReferencingEntity", con_cambio(r, ["ReferencingEntity"], "systemuser"), f"ReferencingEntity: entorno='systemuser' playbook='{tabla_}'")]
        for nombre, relacion, esperado in casos:
            with self.subTest(nombre):
                cliente = armar(ClienteSimulado(), datos, ident=ident, relacion=relacion)
                estado, _, detalle = rl.construir(ruta, True, lambda: cliente)
                self.assertEqual((estado, detalle), ("difiere", esperado))
                self.assertFalse(cliente.hubo_escritura())

    def test_la_hija_estandar_real_valida_y_no_se_bloquea(self):
        ruta, datos, ident = self.leer("sanic_carp_systemuser_email.md")
        rl.validar_playbook(datos, ident)
        self.assertEqual((datos["tabla_padre"], datos["tabla_hija"], datos["comportamiento"]), ("systemuser", "email", "restringido"))
        self.assertEqual(rl.caracteristicas_usadas(datos, ident), {rl.HIJA_ESTANDAR})
        cliente, datos, (estado, comp, detalle) = self.correr_real("sanic_carp_systemuser_email.md", existe=True)
        self.assertEqual((estado, comp), ("ya_existia", "sanic_carp_systemuser_email"), detalle)
        self.assertEqual(escrituras(cliente), [])

    def test_la_hija_estandar_real_se_crea_con_el_cuerpo_del_ensayo(self):
        cliente, datos, (estado, _, detalle) = self.correr_real("sanic_carp_systemuser_email.md", existe=False)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(detalle, f"MetadataId {META_REL}, con su lookup email.sanic_respondidoporid, en la solución 'sanic_carp_sol_controlarreglospago'")
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])  # auditoria false: sin ajuste del lookup
        cuerpo = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]["cuerpo"]
        self.assertEqual(cuerpo["Lookup"]["SchemaName"], "sanic_respondidoporid")
        self.assertEqual((cuerpo["ReferencedEntity"], cuerpo["ReferencingEntity"]), ("systemuser", "email"))


class HijaEstandar(Base):
    """Variante B (la tabla hija es estándar, por ejemplo `email`): VERIFICADA el 2026-10-09 (Dev) con un lookup sobre `email`. La herramienta pregunta
    `CanBeReferencing` (hija) y `CanBeReferenced` (padre) antes de crear, solo admite `restringido` o `quitar_vinculo`, crea con el mismo cuerpo que la
    relación común y verifica la pertenencia así: la relación 1 fila (componenttype 10), el lookup 1 fila (componenttype 2) y la tabla estándar exactamente
    1 fila con rootcomponentbehavior distinto de 0 (componenttype 1)."""
    REL = HIJA_ESTANDAR["nombre"]
    LK = HIJA_ESTANDAR["lookup"]["nombre"]

    @staticmethod
    def tablas(hija_propia, padre="systemuser", hija="email"):
        return {padre: {"PrimaryIdAttribute": id_de(padre), "IsCustomEntity": padre != "systemuser"},
                hija: {"PrimaryIdAttribute": "activityid", "IsCustomEntity": hija_propia}}

    def test_rel_19_pasa_la_validacion_previa(self):
        rl.validar_playbook(HIJA_ESTANDAR, IDENT)

    # -- Validación sin red -------------------------------------------------------------------------------------------------------------------
    def test_parental_con_una_hija_estandar_es_un_error_de_playbook_offline(self):
        centinela = FabricaCentinela()
        estado, comp, detalle = self.correr(centinela, con_cambio(HIJA_ESTANDAR, ["comportamiento"], "parental"))
        self.assertEqual(estado, "error")
        self.assertEqual(comp, self.REL)
        self.assertEqual(detalle, "'comportamiento' = 'parental' no se admite cuando 'tabla_hija' es una tabla estándar ('email', variante B de relacion.md, sección 6): "
                                  "una tabla estándar ya trae sus relaciones parentales y no se pueden agregar más cascadas: usar 'restringido' o 'quitar_vinculo'")
        self.assertFalse(centinela.llamada)

    def test_restringido_y_quitar_vinculo_si_se_admiten(self):
        for comportamiento in ("restringido", "quitar_vinculo"):
            with self.subTest(comportamiento):
                rl.validar_playbook(con_cambio(HIJA_ESTANDAR, ["comportamiento"], comportamiento), IDENT)

    def test_una_autorreferida_sobre_una_estandar_no_esta_cubierta(self):
        datos = dict(HIJA_ESTANDAR, nombre="sanic_carp_systemuser_systemuser", tabla_hija="systemuser", jerarquica=False)
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, datos)
        self.assertEqual(estado, "error", detalle)
        self.assertEqual(detalle, "'tabla_padre' y 'tabla_hija' son la misma tabla estándar ('systemuser'): una relación autorreferida sobre una tabla estándar "
                                  "no está cubierta (la variante A es solo para una tabla de la solución)")
        self.assertFalse(centinela.llamada)

    def test_corregir_lookup_no_aplica_a_una_hija_estandar(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, HIJA_ESTANDAR, corregir_lookup=True)
        self.assertEqual(estado, "error", detalle)
        self.assertEqual(detalle, "--corregir-lookup no aplica a una relación con tabla hija estándar ('email'): ajustar el lookup con PUT sobre una tabla estándar "
                                  "no se ensayó; una diferencia se informa y no se toca")
        self.assertFalse(centinela.llamada)

    def test_la_hija_estandar_pasa_la_validacion_pero_un_nombre_con_el_prefijo_del_proyecto_no(self):
        for hija in ("sanic_carp_arreglo", "sanic_carp_tbl_Arreglo", "Email"):
            with self.subTest(hija):
                datos = dict(HIJA_ESTANDAR, tabla_hija=hija, nombre=f"sanic_carp_systemuser_{hija}")
                estado, _, detalle = self.correr(FabricaCentinela(), datos)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("tabla_hija", detalle)

    # -- Precondiciones contra el entorno -----------------------------------------------------------------------------------------------------
    def test_una_tabla_de_la_solucion_que_no_es_propia_tambien_bloquea(self):
        """Protege la relación común de crear por error un lookup sobre una tabla estándar."""
        cliente = armar(ClienteSimulado(), BASE, existe=False, tablas=self.tablas(False, PADRE, HIJA))
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertEqual(detalle, f"la tabla hija '{HIJA}' lleva el nombre de una tabla de la solución pero no es una tabla propia (IsCustomEntity = false): "
                                  "una tabla estándar solo se admite como hija en la variante B de relacion.md (sección 6), con su nombre lógico sin el prefijo del proyecto")
        self.assertFalse(cliente.hubo_escritura())
        self.assertEqual(acciones(cliente), [])

    def test_una_tabla_propia_de_otro_publisher_no_es_de_la_solucion(self):
        datos = dict(rel("REL-12"), nombre="sanic_carp_systemuser_otro_tabla", tabla_hija="otro_tabla")
        cliente = armar(ClienteSimulado(), datos, existe=False, tablas=self.tablas(True, "systemuser", "otro_tabla"))
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("otro_tabla", detalle)
        self.assertIn("no es una tabla de la solución", detalle)
        self.assertFalse(cliente.hubo_escritura())
        self.assertEqual(acciones(cliente), [])

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

    def test_falta_una_de_las_tablas_bloquea_antes_de_preguntar_la_elegibilidad(self):
        for falta, nombre in (("systemuser", "padre"), ("email", "hija")):
            with self.subTest(falta):
                tablas = {t: v for t, v in self.tablas(False).items() if t != falta}
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, tablas=tablas)
                estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertEqual(detalle, f"la tabla '{falta}' (tabla_{nombre}) no existe en el entorno: se construye antes que sus relaciones")
                self.assertEqual(acciones(cliente), [])
                self.assertEqual(escrituras(cliente), [])

    def test_can_be_referencing_false_bloquea_y_no_escribe(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, elegibles={"email": False})
        estado, comp, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual((estado, comp), ("bloqueado", self.REL))
        self.assertEqual(detalle, "la tabla hija 'email' no puede ser la referenciante de una relación 1:N (CanBeReferencing = false): "
                                  "la plataforma no admite crear un lookup en ella (variante B de relacion.md, sección 6)")
        self.assertEqual(escrituras(cliente), [])

    def test_can_be_referenced_false_bloquea_y_no_escribe(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, elegibles={"systemuser": False})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertEqual(detalle, "la tabla padre 'systemuser' no puede ser referenciada por una relación 1:N (CanBeReferenced = false): "
                                  "la plataforma no admite que sea el lado 1 (variante B de relacion.md, sección 6)")
        self.assertEqual(escrituras(cliente), [])

    def test_si_las_dos_fallan_se_informa_primero_la_hija(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, elegibles={"email": False, "systemuser": False})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("CanBeReferencing = false", detalle)
        self.assertNotIn("CanBeReferenced", detalle)

    def test_la_elegibilidad_bloquea_tambien_con_solo_verificar(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, elegibles={"email": False})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR, solo_verificar=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("CanBeReferencing = false", detalle)

    def test_las_acciones_se_llaman_con_el_nombre_logico_en_entity_name_y_la_hija_primero(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False)
        self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(acciones(cliente), [("CanBeReferencing", {"EntityName": "email"}), ("CanBeReferenced", {"EntityName": "systemuser"})])
        # las dos preguntas se hacen ANTES del alta
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(rutas, ["CanBeReferencing", "CanBeReferenced", "RelationshipDefinitions", "PublishXml"])

    def test_la_relacion_comun_y_la_autorreferida_nunca_preguntan_la_elegibilidad(self):
        for datos in (BASE, DE_SISTEMA, AUTORREFERIDA):
            with self.subTest(datos["nombre"]):
                for existe in (False, True):
                    cliente = armar(ClienteSimulado(), datos, existe=existe)
                    estado, _, detalle = self.con_cliente(cliente, datos)
                    self.assertIn(estado, ("creado", "ya_existia"), detalle)
                    self.assertEqual(acciones(cliente), [])

    def test_si_la_relacion_ya_existe_no_se_pregunta_la_elegibilidad(self):
        """Es una precondición de la CREACIÓN: una relación que ya existe prueba que se pudo crear."""
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=True, elegibles={"email": False, "systemuser": False})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(acciones(cliente), [])

    def test_http_inesperado_de_una_accion_es_error_y_la_nombra(self):
        for accion in ACCIONES_DE_ELEGIBILIDAD:
            for estado_http in (400, 404, 500):
                with self.subTest(f"{accion} HTTP {estado_http}"):
                    cliente = ClienteSimulado()
                    cliente.responder("POST", accion, (estado_http, {"error": "boom"}, {}))
                    armar(cliente, HIJA_ESTANDAR, existe=False)
                    estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn(f"HTTP {estado_http}", detalle)
                    self.assertIn(accion, detalle)
                    self.assertNotIn("fallo inesperado", detalle)
                    self.assertEqual(escrituras(cliente), [])

    def test_forma_inesperada_de_una_accion_es_error_y_nombra_el_campo(self):
        for accion in ACCIONES_DE_ELEGIBILIDAD:
            for cuerpo in ({accion: None}, {accion: "true"}, {accion: 1}, {accion: [True]}, {}, {"Otra": True}):
                with self.subTest(f"{accion} {cuerpo}"):
                    cliente = ClienteSimulado()
                    cliente.responder("POST", accion, (200, dict(cuerpo, **{"@odata.context": "x"}), {}))
                    armar(cliente, HIJA_ESTANDAR, existe=False)
                    estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn("forma inesperada", detalle)
                    self.assertIn(f"'{accion}'", detalle)
                    self.assertNotIn("fallo inesperado", detalle)
                    self.assertEqual(escrituras(cliente), [])

    # -- Creación -----------------------------------------------------------------------------------------------------------------------------
    def test_el_cuerpo_del_alta_es_el_del_ensayo_del_2026_10_09(self):
        p = rl.construir_payload(HIJA_ESTANDAR, IDENT, "systemuserid")
        self.assertEqual(p, {
            "@odata.type": "Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata",
            "SchemaName": "sanic_carp_systemuser_email",
            "ReferencedEntity": "systemuser",
            "ReferencedAttribute": "systemuserid",
            "ReferencingEntity": "email",
            "CascadeConfiguration": CASCADAS["restringido"],
            "AssociatedMenuConfiguration": {"Behavior": "UseCollectionName", "Group": "Details", "Order": 10000},
            "Lookup": {
                "@odata.type": "Microsoft.Dynamics.CRM.LookupAttributeMetadata",
                "AttributeType": "Lookup",
                "AttributeTypeName": {"Value": "LookupType"},
                "SchemaName": "sanic_respondidoporid",
                "DisplayName": {"@odata.type": "Microsoft.Dynamics.CRM.Label", "LocalizedLabels": [
                    {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": "Respondido por", "LanguageCode": 1033},
                    {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": "Respondido por", "LanguageCode": 3082}]},
                "Description": {"@odata.type": "Microsoft.Dynamics.CRM.Label", "LocalizedLabels": [
                    {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": HIJA_ESTANDAR["lookup"]["descripcion"], "LanguageCode": 1033},
                    {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": HIJA_ESTANDAR["lookup"]["descripcion"], "LanguageCode": 3082}]},
                "RequiredLevel": {"Value": "None", "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"},
                "IsAuditEnabled": {"Value": False, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"},
            },
        })

    def test_quitar_vinculo_sobre_una_hija_estandar_manda_remove_link(self):
        p = rl.construir_payload(con_cambio(HIJA_ESTANDAR, ["comportamiento"], "quitar_vinculo"), IDENT, "systemuserid")
        self.assertEqual(p["CascadeConfiguration"], CASCADAS["quitar_vinculo"])

    def test_no_existe_se_crea_con_la_cabecera_de_la_solucion_y_se_publica_la_estandar(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual((estado, comp), ("creado", self.REL))
        self.assertEqual(detalle, f"MetadataId {META_REL}, con su lookup email.{self.LK}, en la solución '{IDENT['solucion']}'")
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])
        post = [l for l in cliente.llamadas if l["ruta"] == "RelationshipDefinitions"][0]
        self.assertEqual(post["solucion"], IDENT["solucion"])
        self.assertEqual(post["cuerpo"]["ReferencedAttribute"], "systemuserid")
        xml = [l for l in cliente.llamadas if l["ruta"] == "PublishXml"][0]["cuerpo"]["ParameterXml"]
        self.assertEqual(xml, "<importexportxml><entities><entity>email</entity></entities></importexportxml>")  # systemuser no se publica

    def test_creada_la_segunda_corrida_es_ya_existia_y_no_escribe(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False)
        self.assertEqual(self.con_cliente(cliente, HIJA_ESTANDAR)[0], "creado")
        del cliente.llamadas[:]
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(escrituras(cliente), [])

    def test_primera_corrida_la_estandar_todavia_no_figura_en_la_solucion(self):
        """Antes del alta la tabla estándar puede no estar en la solución: eso NO impide crear (no es «otra diferencia»); la pertenencia se exige al releer, ya
        con la relación. Que la plataforma agregue la tabla con la relación es un supuesto tomado de las columnas estándar: con una relación no se observó
        (en el ensayo `email` ya estaba por otra columna)."""
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, pertenencia={"tabla": []})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(escrituras(cliente), [("POST", "RelationshipDefinitions"), ("POST", "PublishXml")])

    def test_si_al_releer_algo_de_la_pertenencia_no_aparece_es_error_con_el_detalle_exacto(self):
        P = "se creó pero no coincide con el playbook al releer: pertenencia a la solución: "
        casos = [
            ("la plataforma no agrega la tabla", dict(pertenencia={"tabla": []}, agrega=("relacion", "lookup")),
             P + "la tabla estándar 'email' tiene 0 filas en solutioncomponents (componenttype 1), se esperaba 1"),
            ("la plataforma no agrega el lookup", dict(agrega=("relacion", "tabla")),
             P + f"el lookup '{self.LK}' tiene 0 filas en solutioncomponents (componenttype 2), se esperaba 1"),
            ("la plataforma no agrega la relación", dict(agrega=("lookup", "tabla")),
             P + f"la relación '{self.REL}' tiene 0 filas en solutioncomponents (componenttype 10), se esperaba 1"),
        ]
        for nombre, kw, esperado in casos:
            with self.subTest(nombre):
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, **kw)
                estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                self.assertEqual((estado, detalle), ("error", esperado))

    def test_la_estandar_con_rootcomponentbehavior_0_antes_del_alta_no_impide_crear_pero_se_exige_al_releer(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False, pertenencia={"tabla": [{"solutioncomponentid": "x", "rootcomponentbehavior": 0}]})
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "error", detalle)
        self.assertEqual(detalle, "se creó pero no coincide con el playbook al releer: pertenencia a la solución: la tabla estándar 'email' figura con "
                                  "rootcomponentbehavior=0, que la arrastraría entera a la solución: tiene que ser distinto de 0")
        self.assertEqual([e for e in escrituras(cliente) if e[1] == "RelationshipDefinitions"], [("POST", "RelationshipDefinitions")])

    def test_si_falla_la_creacion_o_publicar_lo_dice(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "RelationshipDefinitions", (400, {"error": "boom"}, {}))
        armar(cliente, HIJA_ESTANDAR, existe=False)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual((estado, detalle), ("error", 'la creación falló: HTTP 400 {\'error\': \'boom\'}'))
        cliente = ClienteSimulado()
        cliente.responder("POST", "PublishXml", (500, {"error": "boom"}, {}))
        armar(cliente, HIJA_ESTANDAR, existe=False)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("falló publicar", detalle)

    # -- Verificación: ya existe --------------------------------------------------------------------------------------------------------------
    def test_ya_existia_con_el_detalle_exacto_y_sin_escribir(self):
        for kw in (dict(), dict(solo_verificar=True)):
            with self.subTest(kw):
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
                estado, comp, detalle = self.con_cliente(cliente, HIJA_ESTANDAR, **kw)
                self.assertEqual((estado, comp), ("ya_existia", self.REL))
                self.assertEqual(detalle, f"MetadataId {META_REL}; coincide en todo lo que exige la receta y pertenece a '{IDENT['solucion']}'; no se modificó nada")
                self.assertEqual(escrituras(cliente), [])

    def test_publicar_republica_solo_la_estandar(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR, publicar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(escrituras(cliente), [("POST", "PublishXml")])

    def test_la_estandar_con_rootcomponentbehavior_distinto_de_0_pertenece(self):
        for comportamiento in (1, 2):
            with self.subTest(comportamiento):
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, pertenencia={"tabla": [{"solutioncomponentid": "x", "rootcomponentbehavior": comportamiento}]})
                estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                self.assertEqual(estado, "ya_existia", detalle)

    def test_cada_diferencia_de_pertenencia_con_el_detalle_exacto(self):
        P = "pertenencia a la solución: "
        casos = [
            ("relación sin fila", dict(relacion=[]), P + f"la relación '{self.REL}' tiene 0 filas en solutioncomponents (componenttype 10), se esperaba 1"),
            ("relación con dos filas", dict(relacion=[FILA_SC, FILA_SC]), P + f"la relación '{self.REL}' tiene 2 filas en solutioncomponents (componenttype 10), se esperaba 1"),
            ("lookup sin fila", dict(lookup=[]), P + f"el lookup '{self.LK}' tiene 0 filas en solutioncomponents (componenttype 2), se esperaba 1"),
            ("lookup con dos filas", dict(lookup=[FILA_SC, FILA_SC]), P + f"el lookup '{self.LK}' tiene 2 filas en solutioncomponents (componenttype 2), se esperaba 1"),
            ("estándar sin fila", dict(tabla=[]), P + "la tabla estándar 'email' tiene 0 filas en solutioncomponents (componenttype 1), se esperaba 1"),
            ("estándar con dos filas", dict(tabla=[FILA_TABLA_ESTANDAR, FILA_TABLA_ESTANDAR]),
             P + "la tabla estándar 'email' tiene 2 filas en solutioncomponents (componenttype 1), se esperaba 1"),
            ("estándar con dos filas, una con behavior 0", dict(tabla=[FILA_TABLA_ESTANDAR, {"solutioncomponentid": "y", "rootcomponentbehavior": 0}]),
             P + "la tabla estándar 'email' tiene 2 filas en solutioncomponents (componenttype 1), se esperaba 1"),
            ("estándar con behavior 0", dict(tabla=[{"solutioncomponentid": "x", "rootcomponentbehavior": 0}]),
             P + "la tabla estándar 'email' figura con rootcomponentbehavior=0, que la arrastraría entera a la solución: tiene que ser distinto de 0"),
            ("lookup y estándar sin fila", dict(lookup=[], tabla=[]),
             P + f"el lookup '{self.LK}' tiene 0 filas en solutioncomponents (componenttype 2), se esperaba 1; "
             + P + "la tabla estándar 'email' tiene 0 filas en solutioncomponents (componenttype 1), se esperaba 1"),
            ("las tres", dict(relacion=[], lookup=[], tabla=[{"solutioncomponentid": "x", "rootcomponentbehavior": 0}]),
             P + f"la relación '{self.REL}' tiene 0 filas en solutioncomponents (componenttype 10), se esperaba 1; "
             + P + f"el lookup '{self.LK}' tiene 0 filas en solutioncomponents (componenttype 2), se esperaba 1; "
             + P + "la tabla estándar 'email' figura con rootcomponentbehavior=0, que la arrastraría entera a la solución: tiene que ser distinto de 0"),
        ]
        for nombre, pertenencia, esperado in casos:
            for kw in (dict(solo_verificar=True), dict(publicar=True), dict()):
                with self.subTest(nombre, **kw):
                    cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, pertenencia=pertenencia)
                    estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR, **kw)
                    self.assertEqual((estado, detalle), ("difiere", esperado))
                    self.assertEqual(escrituras(cliente), [])

    def test_la_pertenencia_de_la_variante_no_pide_lo_de_una_tabla_propia(self):
        """Una tabla estándar con rootcomponentbehavior 1 NO es «no incluye todos sus subcomponentes» (eso es de la tabla propia, behavior 0)."""
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertNotIn("subcomponentes", detalle)

    def test_cada_consulta_de_pertenencia_usa_su_objeto_su_tipo_y_la_solucion(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        self.con_cliente(cliente, HIJA_ESTANDAR)
        consultas = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith("solutioncomponents?")]
        base = f"solutioncomponents?$select=rootcomponentbehavior&$filter=_solutionid_value eq {SOLUTION_ID} and objectid eq "
        self.assertEqual(consultas, [base + f"{META_REL} and componenttype eq 10", base + f"a-{self.LK} and componenttype eq 2", base + "t-email and componenttype eq 1"])

    def test_cada_diferencia_de_la_relacion_comun_tambien_aplica(self):
        r, k = cuerpo_relacion(HIJA_ESTANDAR), cuerpo_lookup(HIJA_ESTANDAR)
        casos = [
            (dict(relacion=con_cambio(r, ["ReferencedEntity"], "team")), "ReferencedEntity: entorno='team' playbook='systemuser'"),
            (dict(relacion=con_cambio(r, ["ReferencingEntity"], "task")), "ReferencingEntity: entorno='task' playbook='email'"),
            (dict(relacion=con_cambio(r, ["ReferencedAttribute"], "teamid")), "ReferencedAttribute: entorno='teamid' playbook='systemuserid'"),
            (dict(relacion=con_cambio(r, ["IsHierarchical"], True)), "IsHierarchical: entorno=True playbook=False"),
            (dict(relacion=con_cambio(r, ["CascadeConfiguration", "Delete"], "Cascade")),
             "CascadeConfiguration.Delete: entorno='Cascade' playbook='Restrict' (comportamiento 'restringido')"),
            (dict(relacion=con_cambio(r, ["CascadeConfiguration", "Assign"], "Cascade")),
             "CascadeConfiguration.Assign: entorno='Cascade' playbook='NoCascade' (comportamiento 'restringido')"),
            (dict(lookup=con_cambio(k, ["Targets"], ["team"])), f"{self.LK}.Targets: entorno=['team'] playbook=['systemuser']"),
            (dict(lookup=con_cambio(k, ["IsAuditEnabled"], aud(True))), f"{self.LK}.IsAuditEnabled: entorno=True playbook=False"),
            (dict(lookup=con_cambio(k, ["RequiredLevel"], rl_("ApplicationRequired"))), f"{self.LK}.RequiredLevel: entorno='ApplicationRequired' playbook='None'"),
            (dict(lookup=con_cambio(k, ["DisplayName"], label({"1033": "Otro", "3082": "Respondido por"}))),
             f"{self.LK}.DisplayName[1033]: entorno='Otro' playbook='Respondido por'"),
        ]
        for kw, esperado in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, **kw)
                estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                self.assertEqual((estado, detalle), ("difiere", esperado))
                self.assertEqual(escrituras(cliente), [])

    def test_forma_inesperada_en_la_pertenencia_de_la_variante(self):
        k = cuerpo_lookup(HIJA_ESTANDAR)
        casos = [("value[0].rootcomponentbehavior", dict(pertenencia={"tabla": [{"solutioncomponentid": "x", "rootcomponentbehavior": None}]})),
                 ("value[0].rootcomponentbehavior", dict(pertenencia={"tabla": [{"solutioncomponentid": "x", "rootcomponentbehavior": "1"}]})),
                 ("MetadataId", dict(lookup=con_cambio(k, ["MetadataId"], None))),
                 ("MetadataId", dict(lookup=con_cambio(k, ["MetadataId"], BORRAR)))]
        for campo, kw in casos:
            with self.subTest(campo, kw=str(kw)[:60]):
                cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, **kw)
                estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertEqual(escrituras(cliente), [])

    def test_http_inesperado_de_la_pertenencia_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", es_ruta_solutioncomponents, (500, {"error": "boom"}, {}))
        armar(cliente, HIJA_ESTANDAR)
        estado, _, detalle = self.con_cliente(cliente, HIJA_ESTANDAR)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("HTTP 500", detalle)
        self.assertIn("solutioncomponents", detalle)


class DobleDeLaPlataforma(unittest.TestCase):
    """El doble de `armar` responde como la plataforma, errores incluidos. Si el doble dejara de hacerlo, las pruebas de arriba dejarían de probar algo."""

    def test_las_acciones_de_elegibilidad_exigen_entity_name(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        for accion in ACCIONES_DE_ELEGIBILIDAD:
            for cuerpo in (None, {}, {"LogicalName": "email"}, {"EntityName": "email", "Otro": 1}):
                with self.subTest(f"{accion} {cuerpo}"):
                    self.assertEqual(cliente.call("POST", accion, cuerpo)[0], 400)
            estado, cuerpo, _ = cliente.call("POST", accion, {"EntityName": "email"})
            self.assertEqual((estado, cuerpo[accion]), (200, True))

    def test_las_acciones_responden_por_tabla(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, elegibles={"email": False})
        self.assertIs(cliente.call("POST", "CanBeReferencing", {"EntityName": "email"})[1]["CanBeReferencing"], False)
        self.assertIs(cliente.call("POST", "CanBeReferenced", {"EntityName": "systemuser"})[1]["CanBeReferenced"], True)

    def test_solutioncomponents_responde_solo_al_filtro_exacto(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        base = "solutioncomponents?$select=rootcomponentbehavior&$filter=_solutionid_value eq {s} and objectid eq {o} and componenttype eq {t}"
        filas = lambda **kw: cliente.call("GET", base.format(**kw))[1]["value"]  # noqa: E731
        self.assertEqual(len(filas(s=SOLUTION_ID, o="t-email", t=1)), 1)
        self.assertEqual(filas(s=SOLUTION_ID, o="t-email", t=2), [])  # otro tipo
        self.assertEqual(filas(s=SOLUTION_ID, o="t-otra", t=1), [])  # otro objeto
        self.assertEqual(filas(s="otra", o="t-email", t=1), [])  # otra solución
        self.assertEqual(len(filas(s=SOLUTION_ID, o=META_REL, t=10)), 1)
        self.assertEqual(len(filas(s=SOLUTION_ID, o="a-sanic_respondidoporid", t=2)), 1)

    def test_antes_del_alta_la_relacion_y_el_lookup_no_tienen_fila(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR, existe=False)
        base = "solutioncomponents?$select=rootcomponentbehavior&$filter=_solutionid_value eq " + SOLUTION_ID + " and objectid eq {o} and componenttype eq {t}"
        self.assertEqual(cliente.call("GET", base.format(o=META_REL, t=10))[1]["value"], [])
        self.assertEqual(len(cliente.call("GET", base.format(o="t-email", t=1))[1]["value"]), 1)
        cliente.call("POST", "RelationshipDefinitions", {})
        self.assertEqual(len(cliente.call("GET", base.format(o=META_REL, t=10))[1]["value"]), 1)

    def test_leer_targets_sin_el_cast_al_tipo_derivado_es_400(self):
        cliente = armar(ClienteSimulado(), HIJA_ESTANDAR)
        ruta = "EntityDefinitions(LogicalName='email')/Attributes(LogicalName='sanic_respondidoporid')"
        self.assertEqual(cliente.call("GET", ruta + "?$select=LogicalName,Targets")[0], 400)
        self.assertEqual(cliente.call("GET", ruta + "/Microsoft.Dynamics.CRM.LookupAttributeMetadata?$select=LogicalName,Targets")[0], 200)


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
