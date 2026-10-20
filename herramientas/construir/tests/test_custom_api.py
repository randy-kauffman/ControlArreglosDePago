"""Pruebas de `herramientas/construir/custom_api.py`. Nunca tocan la red.

Lo que más se prueba acá es la desconfianza: en una Custom API casi todo es
inmutable, así que la herramienta tiene que rechazar un playbook mal armado
SIN tocar el entorno, y tiene que negarse a "arreglar" lo que no se puede
arreglar con un PATCH.
"""
import copy
import json
import os
import re
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import custom_api as ca  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA,
    SOLUTION_ID,
    armar_cliente_precondiciones_ok,
    es_ruta_solutioncomponents,
)

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="custom-api", inventario="8.1")

API_ID = "55555555-5555-5555-5555-555555555555"
TIPO_ID = "66666666-6666-6666-6666-666666666666"
PAQUETE_ID = "77777777-7777-7777-7777-777777777777"
ASM_ID = "88888888-8888-8888-8888-888888888888"
TYPENAME = "Sanic.Carp.Plugins.Api.GenerarFormatoApi"
PAQUETE = "sanic_carp_pkg_plugins"

# Dominio de este proyecto (convenciones, seccion 2): la Custom API que genera el formato de un arreglo.
COMPONENTE = {
    "tipo": "custom-api",
    "nombre": "sanic_carp_capi_generarformato",
    "displayname": "CAPI - CARP - Generar formato",
    "descripcion": "Genera el formato del arreglo de pago a partir de la version vigente de la plantilla y deja registrada la generacion.",
    "paquete": PAQUETE,
    "plugintype": TYPENAME,
    "esfuncion": False,
    "esprivada": True,
    "binding": "Global",
    "pasosdeterceros": "None",
    "habilitadaenflujos": False,
    "privilegio": "prvCreatesanic_carp_tbl_generacionformato",
    "entrada": [
        {"nombre": "arregloid", "displayname": "CAPI_IP - CARP - Arreglo id",
         "descripcion": "Identificador del arreglo del que se genera el formato.", "tipo": "Guid", "opcional": False},
    ],
    "salida": [
        {"nombre": "archivo", "displayname": "CAPI_OP - CARP - Archivo",
         "descripcion": "Contenido del archivo generado, en Base64.", "tipo": "String"},
        {"nombre": "tamanobytes", "displayname": "CAPI_OP - CARP - Tamano en bytes",
         "descripcion": "Tamano del archivo generado, en bytes.", "tipo": "Integer"},
    ],
}


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None, identidad=None):
    componente = COMPONENTE if componente is None else componente
    identidad = IDENT if identidad is None else identidad
    return (
        "# Playbook: custom-api · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(identidad, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def es_ruta_apis(ruta):
    return ruta.startswith("customapis?")


def fila_parametro(p, es_salida, datos=None):
    datos = COMPONENTE if datos is None else datos
    fila = {
        "uniquename": p["nombre"],
        "name": ca.nombre_largo(datos["nombre"], IDENT, es_salida, p["nombre"]),
        "displayname": p["displayname"],
        "description": p["descripcion"],
        "type": ca.TIPOS_DE_PARAMETRO[p["tipo"]],
    }
    fila["customapiresponsepropertyid" if es_salida else "customapirequestparameterid"] = "p-" + p["nombre"]
    if not es_salida:
        fila["isoptional"] = p["opcional"]
    return fila


def fila_api(datos=None, entrada=None, salida=None, **cambios):
    """La fila que devolvería el entorno si tuviera exactamente lo declarado."""
    datos = COMPONENTE if datos is None else datos
    entrada = datos["entrada"] if entrada is None else entrada
    salida = datos["salida"] if salida is None else salida
    fila = {
        "customapiid": API_ID,
        "uniquename": datos["nombre"],
        "name": datos["nombre"],
        "displayname": datos["displayname"],
        "description": datos["descripcion"],
        "executeprivilegename": datos["privilegio"],
        "isfunction": datos["esfuncion"],
        "isprivate": datos["esprivada"],
        "bindingtype": ca.BINDING[datos["binding"]],
        "allowedcustomprocessingsteptype": ca.PASOS_DE_TERCEROS[datos["pasosdeterceros"]],
        "workflowsdkstepenabled": datos["habilitadaenflujos"],
        "ismanaged": False,
        "PluginTypeId": {"plugintypeid": TIPO_ID, "typename": datos["plugintype"]},
        "CustomAPIRequestParameters": [fila_parametro(p, False, datos) for p in entrada],
        "CustomAPIResponseProperties": [fila_parametro(p, True, datos) for p in salida],
    }
    fila.update(cambios)
    return fila


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None, identidad=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente, identidad))
        return self.ruta

    def responder_paquete(self, cliente, tipos=None):
        """El paquete del playbook, su único ensamblado y los tipos que trajo (`tipos`: filas de `plugintypes`). Los tipos
        se piden por el ensamblado del paquete: un tipo de OTRO paquete con el mismo nombre no aparece acá."""
        if tipos is None:
            tipos = [{"plugintypeid": TIPO_ID, "typename": TYPENAME}]
        cliente.responder("GET", lambda r: r.startswith("pluginpackages?") and f"uniquename eq '{PAQUETE}'" in r,
                          (200, {"value": [{"pluginpackageid": PAQUETE_ID, "uniquename": PAQUETE}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("pluginassemblies?") and f"_packageid_value eq {PAQUETE_ID}" in r,
                          (200, {"value": [{"pluginassemblyid": ASM_ID}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("plugintypes?") and f"_pluginassemblyid_value eq {ASM_ID}" in r,
                          (200, {"value": tipos}, {}))

    def armar(self, cliente, apis, tipos=None, componentes=None):
        """`apis` es la SECUENCIA de respuestas de `GET customapis` (una por
        lectura): así se simula el antes y el después de una escritura."""
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.responder_paquete(cliente, tipos)
        pendientes = list(apis)

        def responder_api(ruta, cuerpo, solucion):
            fila = pendientes.pop(0) if len(pendientes) > 1 else pendientes[0]
            return (200, {"value": [] if fila is None else [fila]}, {})

        cliente.responder("GET", es_ruta_apis, responder_api)
        if componentes is None:
            componentes = [{"componenttype": 10101, "objectid": API_ID}]
        cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": componentes}, {}))
        return cliente


# ---------------------------------------------------------------------------
# Validación offline
# ---------------------------------------------------------------------------
class ValidacionOffline(Base):
    def acepta(self, componente):
        """Pasa la validación offline. No se mira el estado final —sin entorno no hay entorno que
        mirar—, sino que NO se haya rechazado por forma del playbook."""
        centinela = FabricaCentinela()
        estado, _, detalle = ca.construir(self.escribir(componente), False, centinela)
        self.assertTrue(centinela.llamada,
                        f"se rechazó en la validación offline y no tenía que rechazarse: {detalle}")

    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = ca.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        # Un dato mal formado del PLAYBOOK no es "una respuesta del entorno" ni un fallo imprevisto.
        self.assertNotIn("fallo inesperado", detalle)
        self.assertNotIn("devolvió 200", detalle)
        self.assertFalse(centinela.llamada, "no tenía que intentar construir el cliente de Dataverse")

    def test_una_clave_de_mas_o_de_menos_se_rechaza(self):
        sin_salida = {k: v for k, v in COMPONENTE.items() if k != "salida"}
        self.rechaza(sin_salida, "faltan ['salida']")
        self.rechaza(comp(sobrante=1), "sobran ['sobrante']")

    def test_el_nombre_sigue_el_patron_de_la_convencion(self):
        self.rechaza(comp(nombre="generarformato"), "no sigue el patrón sanic_carp_capi_")
        self.rechaza(comp(nombre="sanic_otra_capi_generarformato"), "no sigue el patrón sanic_carp_capi_")
        self.rechaza(comp(nombre="sanic_carp_capi_"), "solo van minúsculas ASCII y dígitos")
        self.rechaza(comp(nombre="sanic_carp_capi_Generar"), "solo van minúsculas ASCII y dígitos")
        self.rechaza(comp(nombre="sanic_carp_capi_generar_formato"), "solo van minúsculas ASCII y dígitos")
        self.rechaza(comp(nombre="sanic_carp_capi_generar" + "x" * 120), "el máximo de la columna es 128")

    def test_el_nombre_de_un_parametro_es_inmutable_asi_que_se_valida_duro(self):
        malo = comp(entrada=[dict(COMPONENTE["entrada"][0], nombre="arregloId")])
        self.rechaza(malo, "solo puede llevar minúsculas ASCII y dígitos")
        malo = comp(entrada=[dict(COMPONENTE["entrada"][0], nombre="arreglo_id")])
        self.rechaza(malo, "solo puede llevar minúsculas ASCII y dígitos")

    def test_un_nombre_repetido_DENTRO_de_la_entrada_se_rechaza(self):
        # El nombre es el identificador del parámetro al llamar la API: dos iguales en la misma
        # lista son ambiguos.
        uno = COMPONENTE["entrada"][0]
        malo = comp(entrada=[uno, dict(uno, displayname="Otro")])
        self.rechaza(malo, "está repetido")

    def test_el_mismo_nombre_en_entrada_y_en_salida_SI_se_admite(self):
        """En Dataverse son DOS TABLAS distintas —`customapirequestparameters` y
        `customapiresponseproperties`— y la plataforma no las choca. Prohibirlo bloquearía un diseño
        legítimo: una API que recibe `arregloid` y lo devuelve en su respuesta. El plugin lee y
        escribe por ese nombre exacto, así que la herramienta no puede ser más estricta que la
        plataforma.
        """
        bueno = comp(salida=[dict(COMPONENTE["salida"][0], nombre="arregloid")])
        self.acepta(bueno)

    def test_privilegio_VACIO_se_admite_pero_hay_que_declararlo(self):
        """`ExecutePrivilegeName` vacío se admite cuando la puerta es el plugin y no un privilegio de
        la plataforma; **omitir** la clave no, para que nadie se lo olvide sin querer."""
        self.acepta(comp(privilegio=""))
        faltante = comp()
        del faltante["privilegio"]
        self.rechaza(faltante, "privilegio")

    def test_un_tipo_que_no_existe_se_rechaza_nombrando_los_validos(self):
        malo = comp(salida=[dict(COMPONENTE["salida"][0], tipo="Int32")])
        self.rechaza(malo, "no es un tipo de Custom API")

    def test_un_tipo_que_necesita_tabla_se_rechaza_en_vez_de_declararlo_a_medias(self):
        # `logicalentityname` también es inmutable: mejor no crear nada.
        malo = comp(salida=[dict(COMPONENTE["salida"][0], tipo="Entity")])
        self.rechaza(malo, "necesita `logicalentityname`")

    def test_una_api_atada_a_una_tabla_todavia_no_se_sabe_crear(self):
        self.rechaza(comp(binding="Entity"), "solo sabe crear API unbound")
        self.rechaza(comp(binding="Otro"), "no es válido")

    def test_una_function_sin_salidas_se_rechaza(self):
        self.rechaza(comp(esfuncion=True, salida=[]), "una Function tiene que devolver algo")

    def test_los_nombres_visibles_van_sin_tildes(self):
        self.rechaza(comp(displayname="CAPI - CARP - Generación"), "fuera de ASCII")
        malo = comp(entrada=[dict(COMPONENTE["entrada"][0], displayname="CAPI_IP - CARP - Arreglo ñ")])
        self.rechaza(malo, "fuera de ASCII")

    def test_las_tablas_de_valores_son_las_de_learn(self):
        self.assertEqual(ca.TIPOS_DE_PARAMETRO, {"Boolean": 0, "DateTime": 1, "Decimal": 2, "Entity": 3, "EntityCollection": 4, "EntityReference": 5,
                                                 "Float": 6, "Integer": 7, "Money": 8, "Picklist": 9, "String": 10, "StringArray": 11, "Guid": 12})
        self.assertEqual(ca.BINDING, {"Global": 0, "Entity": 1, "EntityCollection": 2})
        self.assertEqual(ca.PASOS_DE_TERCEROS, {"None": 0, "AsyncOnly": 1, "SyncAndAsync": 2})

    def test_los_tipos_que_piden_tabla_se_rechazan_en_la_entrada_y_en_la_salida_segun_corresponda(self):
        entrada = COMPONENTE["entrada"][0]
        salida = COMPONENTE["salida"][0]
        for tipo in ("Entity", "EntityCollection", "EntityReference"):
            with self.subTest(f"entrada {tipo}"):
                self.rechaza(comp(entrada=[dict(entrada, tipo=tipo)]), "necesita `logicalentityname`")
        for tipo in ("Entity", "EntityReference"):
            with self.subTest(f"salida {tipo}"):
                self.rechaza(comp(salida=[dict(salida, tipo=tipo)]), "necesita `logicalentityname`")
        # Una salida EntityCollection no admite `logicalentityname` (Learn): se declara sin tabla.
        self.acepta(comp(salida=[dict(salida, tipo="EntityCollection")]))

    def test_el_nombre_de_la_api_respeta_el_maximo_de_la_columna(self):
        justo = "sanic_carp_capi_" + "a" * (128 - len("sanic_carp_capi_"))
        self.assertEqual(len(justo), 128)
        self.acepta(comp(nombre=justo))
        self.rechaza(comp(nombre=justo + "a"), "el máximo de la columna es 128")

    def test_un_texto_vacio_se_rechaza_en_cada_campo_menos_el_privilegio(self):
        for clave in ("nombre", "displayname", "descripcion", "paquete", "plugintype", "binding", "pasosdeterceros"):
            with self.subTest(clave):
                self.rechaza(comp(**{clave: ""}), clave)
        for clave in ("nombre", "displayname", "descripcion", "tipo"):
            with self.subTest("entrada." + clave):
                self.rechaza(comp(entrada=[dict(COMPONENTE["entrada"][0], **{clave: ""})]), clave)
            with self.subTest("salida." + clave):
                self.rechaza(comp(salida=[dict(COMPONENTE["salida"][0], **{clave: ""})]), clave)

    def test_cada_clave_tiene_que_ser_del_tipo_que_corresponde(self):
        for clave, valor in (("esfuncion", "no"), ("esprivada", 1), ("habilitadaenflujos", None), ("entrada", {}), ("salida", "x"), ("privilegio", 3),
                             ("paquete", 7), ("paquete", None), ("plugintype", 7)):
            with self.subTest(clave):
                self.rechaza(comp(**{clave: valor}), clave)
        self.rechaza(comp(entrada=[dict(COMPONENTE["entrada"][0], opcional="no")]), "opcional")
        self.rechaza(comp(entrada=["arregloid"]), "entrada[0]")
        self.rechaza(comp(salida=["archivo"]), "salida[0]")

    def test_validar_playbook_tambien_rechaza_un_tipo_que_no_es_custom_api(self):
        with self.assertRaises(ca.ErrorPlaybook) as e:
            ca.validar_playbook(comp(tipo="steps"), IDENT)
        self.assertIn("se esperaba 'custom-api'", str(e.exception))

    def test_el_prefijo_del_nombre_sale_de_la_identidad(self):
        otra = dict(IDENT, prefijo="zeta", abrev="otra")
        ca.validar_playbook(comp(nombre="zeta_otra_capi_generarformato"), otra)
        with self.assertRaises(ca.ErrorPlaybook) as e:
            ca.validar_playbook(comp(), otra)
        self.assertIn("zeta_otra_capi_", str(e.exception))

    def test_el_tipo_del_plugin_tiene_que_ser_un_nombre_dotnet_completo(self):
        self.rechaza(comp(plugintype="GenerarFormatoApi"), "no parece un nombre de tipo .NET completo")

    def test_el_bloque_exige_el_paquete_del_plugin(self):
        sin_paquete = {k: v for k, v in COMPONENTE.items() if k != "paquete"}
        self.rechaza(sin_paquete, "faltan ['paquete']")

    def test_el_paquete_solo_admite_caracteres_de_un_unique_name(self):
        # El `paquete` viaja dentro de un `$filter`: una comilla lo rompería (o lo reescribiría).
        for malo in ("sanic_carp_pkg' or uniquename ne '", "sanic carp", "sanic_carp_pkg-plugins", "sanic_ñ", "a" * 129):
            with self.subTest(malo):
                self.rechaza(comp(paquete=malo), "paquete = ")
        for bueno in ("sanic_carp_pkg_plugins", "sanic_Carp.Pkg_2"):
            ca.validar_playbook(comp(paquete=bueno), IDENT)

    def test_pasos_de_terceros_se_nombra_no_se_numera(self):
        self.rechaza(comp(pasosdeterceros="0"), "no es válido")

    def test_la_descripcion_de_una_salida_respeta_el_tope_de_100_caracteres(self):
        # `customapiresponseproperty.description` tiene tope de 100 (Learn): de no validarlo offline,
        # el largo de más llegaría como HTTP 400 DESPUÉS de crear la API, dejándola a medias
        # (creada, con sus entradas, sin ninguna salida).
        larga = "a" * 101
        malo = comp(salida=[dict(COMPONENTE["salida"][0], descripcion=larga)])
        self.rechaza(malo, "el máximo de la columna customapiresponseproperty.description es 100")

    def test_la_descripcion_de_una_salida_de_exactamente_100_caracteres_se_acepta(self):
        justo = "a" * 100
        bueno = comp(salida=[dict(COMPONENTE["salida"][0], descripcion=justo)])
        self.acepta(bueno)

    def test_la_descripcion_de_una_entrada_no_tiene_tope_de_longitud(self):
        # A diferencia de la salida, `customapirequestparameter.description` NO
        # tiene tope documentado en Learn. No hay que inventarle uno.
        larga = "a" * 500
        bueno = comp(entrada=[dict(COMPONENTE["entrada"][0], descripcion=larga)])
        self.acepta(bueno)

    def test_el_nombre_largo_del_parametro_se_calcula_no_se_escribe(self):
        self.assertEqual(
            "sanic_carp_capiip_generarformato_arregloid",
            ca.nombre_largo("sanic_carp_capi_generarformato", IDENT, False, "arregloid"),
        )
        self.assertEqual(
            "sanic_carp_capiop_generarformato_archivo",
            ca.nombre_largo("sanic_carp_capi_generarformato", IDENT, True, "archivo"),
        )
        # El prefijo y la abreviatura salen de la Identidad, no están escritos en la herramienta.
        self.assertEqual(
            "otro_xyz_capiop_demo_resultado",
            ca.nombre_largo("otro_xyz_capi_demo", dict(IDENT, prefijo="otro", abrev="xyz"), True, "resultado"),
        )


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
class ContraElEntorno(Base):
    def test_las_consultas_filtran_por_lo_que_el_playbook_declara(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()])
        ca.construir(self.escribir(), False, lambda: cliente)
        rutas = [l["ruta"] for l in cliente.llamadas]
        self.assertTrue([r for r in rutas if r.startswith("pluginpackages?") and f"uniquename eq '{PAQUETE}'" in r])
        self.assertTrue([r for r in rutas if r.startswith("pluginassemblies?") and f"_packageid_value eq {PAQUETE_ID}" in r])
        self.assertTrue([r for r in rutas if r.startswith("plugintypes?") and f"_pluginassemblyid_value eq {ASM_ID}" in r])
        self.assertFalse([r for r in rutas if r.startswith("plugintypes?") and "typename eq" in r], "el tipo se resuelve dentro del paquete, no por su nombre")
        self.assertTrue([r for r in rutas if r.startswith("customapis?") and f"uniquename eq '{COMPONENTE['nombre']}'" in r])
        pertenencia = [r for r in rutas if r.startswith("solutioncomponents?")]
        self.assertEqual(len(pertenencia), 1)
        self.assertIn(f"_solutionid_value eq {SOLUTION_ID}", pertenencia[0])
        self.assertIn(f"objectid eq {API_ID}", pertenencia[0])

    def test_dos_tipos_de_plugin_o_dos_apis_con_el_mismo_nombre_es_error(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()], tipos=[{"plugintypeid": TIPO_ID, "typename": TYPENAME}, {"plugintypeid": "otro", "typename": TYPENAME}])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("devolvió 2 filas", detalle)
        cliente = ClienteSimulado()
        cliente.responder("GET", es_ruta_apis, (200, {"value": [fila_api(), fila_api()]}, {}))
        self.armar(cliente, [fila_api()])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("devolvió 2 filas", detalle)
        self.assertIn(COMPONENTE["nombre"], detalle)

    def test_el_cuerpo_completo_del_alta_de_la_api_y_de_cada_parametro(self):
        cliente = self.armar(ClienteSimulado(), [None, fila_api(entrada=[], salida=[]), fila_api()])
        for ruta in ("customapis", "customapirequestparameters", "customapiresponseproperties"):
            cliente.responder("POST", ruta, (204, {}, {}))
        ca.construir(self.escribir(), False, lambda: cliente)
        cuerpos = {}
        for l in cliente.llamadas:
            if l["metodo"] == "POST":
                cuerpos.setdefault(l["ruta"], []).append(l["cuerpo"])
        self.assertEqual(cuerpos["customapis"], [{
            "uniquename": COMPONENTE["nombre"], "name": COMPONENTE["nombre"], "displayname": COMPONENTE["displayname"],
            "description": COMPONENTE["descripcion"], "bindingtype": 0, "isfunction": False, "isprivate": True,
            "allowedcustomprocessingsteptype": 0, "workflowsdkstepenabled": False,
            "executeprivilegename": COMPONENTE["privilegio"], "PluginTypeId@odata.bind": f"/plugintypes({TIPO_ID})"}])
        self.assertEqual(cuerpos["customapirequestparameters"], [{
            "uniquename": "arregloid", "name": "sanic_carp_capiip_generarformato_arregloid", "displayname": "CAPI_IP - CARP - Arreglo id",
            "description": "Identificador del arreglo del que se genera el formato.", "type": 12, "isoptional": False,
            "CustomAPIId@odata.bind": f"/customapis({API_ID})"}])
        self.assertEqual(cuerpos["customapiresponseproperties"][1], {
            "uniquename": "tamanobytes", "name": "sanic_carp_capiop_generarformato_tamanobytes", "displayname": "CAPI_OP - CARP - Tamano en bytes",
            "description": "Tamano del archivo generado, en bytes.", "type": 7, "CustomAPIId@odata.bind": f"/customapis({API_ID})"})

    def test_cada_campo_inmutable_de_la_api_que_difiere_dice_que_hay_que_borrar_y_rehacer(self):
        casos = {"uniquename": "otro", "bindingtype": 1, "isfunction": True, "allowedcustomprocessingsteptype": 2, "workflowsdkstepenabled": True}
        for campo, valor in casos.items():
            for completar in (False, True):
                with self.subTest(f"{campo} completar={completar}"):
                    cliente = self.armar(ClienteSimulado(), [fila_api(**{campo: valor})])
                    estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=completar)
                    self.assertEqual("difiere", estado, detalle)
                    self.assertIn(f"{campo} es {valor!r}", detalle)
                    self.assertIn("INMUTABLE", detalle)
                    self.assertFalse(cliente.hubo_escritura())

    def test_cada_diferencia_de_un_parametro_se_nombra(self):
        for lista, tipo_fila in (("entrada", False), ("salida", True)):
            for campo in ("name", "displayname", "description"):
                with self.subTest(f"{lista}.{campo}"):
                    filas = [fila_parametro(p, tipo_fila) for p in COMPONENTE[lista]]
                    filas[0][campo] = "otro"
                    api = fila_api(**{"CustomAPIRequestParameters" if not tipo_fila else "CustomAPIResponseProperties": filas})
                    cliente = self.armar(ClienteSimulado(), [api])
                    estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
                    self.assertEqual("difiere", estado, detalle)
                    self.assertIn(f"{lista} '{COMPONENTE[lista][0]['nombre']}': {campo} es 'otro'", detalle)
                    self.assertFalse(cliente.hubo_escritura())

    def test_una_entrada_de_mas_se_denuncia_igual_que_una_salida_de_mas(self):
        extra = dict(COMPONENTE["entrada"][0], nombre="colada")
        cliente = self.armar(ClienteSimulado(), [fila_api(entrada=COMPONENTE["entrada"] + [extra])])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("entradas que el playbook no declara: ['colada']", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_corrige_cada_campo_modificable_y_solo_ese(self):
        casos = [("name", "otro"), ("displayname", "otro"), ("description", "otra"), ("executeprivilegename", "prvOtro"), ("isprivate", False)]
        for campo, valor in casos:
            with self.subTest(campo):
                cliente = self.armar(ClienteSimulado(), [fila_api(**{campo: valor}), fila_api()])
                cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))
                estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
                self.assertEqual("actualizado", estado, detalle)
                esperado = {"name": COMPONENTE["nombre"], "displayname": COMPONENTE["displayname"], "description": COMPONENTE["descripcion"],
                            "executeprivilegename": COMPONENTE["privilegio"], "isprivate": COMPONENTE["esprivada"]}[campo]
                patches = [l["cuerpo"] for l in cliente.llamadas if l["metodo"] == "PATCH"]
                self.assertEqual(patches, [{campo: esperado}])

    def test_completar_reapunta_el_plugin_asociado(self):
        otro = fila_api(PluginTypeId={"plugintypeid": "viejo", "typename": "Otro.Tipo"})
        cliente = self.armar(ClienteSimulado(), [otro, fila_api()])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        self.assertEqual([l["cuerpo"] for l in cliente.llamadas if l["metodo"] == "PATCH"], [{"PluginTypeId@odata.bind": f"/plugintypes({TIPO_ID})"}])

    def test_un_plugin_asociado_con_el_mismo_nombre_pero_de_otro_paquete_difiere(self):
        # El caso de los spikes: la Custom API quedó apuntando a la clase homónima de otro paquete. El nombre coincide, el tipo no.
        del_spike = fila_api(PluginTypeId={"plugintypeid": "t-del-spike", "typename": TYPENAME})
        cliente = self.armar(ClienteSimulado(), [del_spike])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(f"el plugin asociado es el tipo '{TYPENAME}' de otro paquete (plugintypeid t-del-spike)", detalle)
        self.assertIn(f"el del paquete '{PAQUETE}' (plugintypeid {TIPO_ID})", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_reapunta_un_plugin_homonimo_de_otro_paquete_al_del_paquete(self):
        del_spike = fila_api(PluginTypeId={"plugintypeid": "t-del-spike", "typename": TYPENAME})
        cliente = self.armar(ClienteSimulado(), [del_spike, fila_api()])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        self.assertEqual([l["cuerpo"] for l in cliente.llamadas if l["metodo"] == "PATCH"], [{"PluginTypeId@odata.bind": f"/plugintypes({TIPO_ID})"}])

    def test_el_plugin_asociado_ausente_o_mal_tipado_en_la_api_es_forma_inesperada(self):
        for valor in ({"plugintypeid": None, "typename": TYPENAME}, {"plugintypeid": TIPO_ID, "typename": 5}, "texto", []):
            with self.subTest(valor):
                cliente = self.armar(ClienteSimulado(), [fila_api(PluginTypeId=valor)])
                estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
                self.assertEqual("error", estado, detalle)
                self.assertIn("la consulta de la Custom API (GET customapis) devolvió 200 con forma inesperada", detalle)
                self.assertIn("PluginTypeId", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_un_tipo_que_solo_existe_en_otro_paquete_bloquea_y_no_se_usa(self):
        cliente = self.armar(ClienteSimulado(), [None], tipos=[])
        cliente.responder("GET", lambda r: r.startswith("plugintypes?") and "typename eq" in r,
                          (200, {"value": [{"plugintypeid": "t-del-spike", "typename": TYPENAME}]}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn(f"no está registrado en el paquete '{PAQUETE}'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_paquete_que_no_existe_bloquea_antes_de_escribir(self):
        cliente = self.armar(ClienteSimulado(), [None])
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("pluginpackages?"), (200, {"value": []}, {})))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn(f"el paquete de plugins '{PAQUETE}' no está registrado", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_http_no_previsto_al_resolver_el_paquete_es_error_y_no_bloqueado(self):
        for prefijo in ("pluginpackages?", "pluginassemblies?", "plugintypes?"):
            with self.subTest(prefijo):
                cliente = self.armar(ClienteSimulado(), [None])
                cliente._reglas.insert(0, ("GET", lambda r, p=prefijo: r.startswith(p), (500, {"error": "boom"}, {})))
                estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
                self.assertEqual("error", estado, detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_una_api_sin_plugin_asociado_difiere_en_vez_de_romper(self):
        cliente = self.armar(ClienteSimulado(), [fila_api(PluginTypeId=None)])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(f"el plugin asociado es None y el playbook dice {TYPENAME!r}", detalle)

    def test_el_tipo_se_resuelve_una_sola_vez_por_ejecucion(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()])
        ca.construir(self.escribir(), False, lambda: cliente)
        rutas = [l["ruta"].split("?")[0] for l in cliente.llamadas]
        self.assertEqual((1, 1, 1), (rutas.count("pluginpackages"), rutas.count("pluginassemblies"), rutas.count("plugintypes")))

    def test_una_api_fuera_de_la_solucion_dice_que_solucion_y_que_la_herramienta_no_la_agrega(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()], componentes=[])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("la Custom API no figura como componente de la solución", detalle)
        self.assertIn(f"'{IDENT['solucion']}'", detalle)
        self.assertIn(f"solutionid {SOLUTION_ID}", detalle)
        self.assertIn("AddSolutionComponent", detalle)
        self.assertIn("a mano", detalle)
        self.assertFalse(cliente.hubo_escritura(), "no agrega el componente a la solución: está pendiente de ensayar en Dev")

    def test_sin_completar_o_con_solo_verificar_una_diferencia_modificable_no_se_toca(self):
        for completar, solo_verificar in ((False, False), (True, True), (False, True)):
            with self.subTest(f"completar={completar} solo_verificar={solo_verificar}"):
                cliente = self.armar(ClienteSimulado(), [fila_api(displayname="otro")])
                estado, _, detalle = ca.construir(self.escribir(), solo_verificar, lambda: cliente, completar=completar)
                self.assertEqual("difiere", estado, detalle)
                self.assertIn("displayname es 'otro'", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_errores_al_escribir_cuando_se_completa(self):
        sin_una = fila_api(salida=COMPONENTE["salida"][:1])
        cliente = self.armar(ClienteSimulado(), [sin_una])
        cliente.responder("POST", "customapiresponseproperties", (400, {"error": "boom"}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("tamanobytes", detalle)
        self.assertIn("HTTP 400", detalle)
        cliente = self.armar(ClienteSimulado(), [fila_api(displayname="otro")])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (400, {"error": "boom"}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("la corrección devolvió HTTP 400", detalle)

    def test_si_tras_escribir_el_entorno_no_coincide_es_error_y_no_difiere(self):
        # El PATCH responde 204 pero la relectura sigue mostrando el valor viejo.
        cliente = self.armar(ClienteSimulado(), [fila_api(displayname="otro")])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("error", estado, detalle)
        self.assertTrue(detalle.startswith("se escribió pero no quedó bien: "), detalle)
        self.assertIn("displayname es 'otro'", detalle)

    def test_si_tras_el_alta_la_api_no_aparece_al_releer_es_error(self):
        cliente = self.armar(ClienteSimulado(), [None])
        cliente.responder("POST", "customapis", (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("no aparece al releer", detalle)

    def test_si_tras_el_alta_el_entorno_no_coincide_es_error(self):
        cliente = self.armar(ClienteSimulado(), [None, fila_api(entrada=[], salida=[])])
        for ruta in ("customapis", "customapirequestparameters", "customapiresponseproperties"):
            cliente.responder("POST", ruta, (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertTrue(detalle.startswith("se escribió pero no quedó bien: "), detalle)
        self.assertIn("falta el parámetro 'arregloid'", detalle)
        self.assertIn("falta la salida 'archivo'", detalle)

    def test_si_la_api_desaparece_al_releer_despues_de_corregir_es_error(self):
        cliente = self.armar(ClienteSimulado(), [fila_api(displayname="otro"), None])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("error", estado, detalle)
        self.assertIn("desapareció al releer", detalle)

    def test_el_alta_no_pide_la_exportacion_ni_consulta_nada_de_mas(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()])
        ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual({l["metodo"] for l in cliente.llamadas}, {"GET"})

    def test_sin_el_paquete_registrado_queda_bloqueado_y_no_escribe(self):
        cliente = self.armar(ClienteSimulado(), [None], tipos=[])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("paquete_plugins.py", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_idioma_del_playbook_que_el_entorno_no_provisiono_bloquea_sin_escribir(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": [1033]}, {}))
        self.armar(cliente, [None])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("3082", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_alta_crea_la_api_y_un_post_por_parametro(self):
        cliente = self.armar(ClienteSimulado(), [None, fila_api(entrada=[], salida=[]), fila_api()])
        cliente.responder("POST", "customapis", (204, {}, {}))
        cliente.responder("POST", "customapirequestparameters", (204, {}, {}))
        cliente.responder("POST", "customapiresponseproperties", (204, {}, {}))

        estado, componente, detalle = ca.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("creado", estado, detalle)
        self.assertEqual(COMPONENTE["nombre"], componente)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(["customapis", "customapirequestparameters",
                          "customapiresponseproperties", "customapiresponseproperties"],
                         [p["ruta"] for p in posts])
        # Todo entra en la solución en la misma llamada que lo crea.
        for p in posts:
            self.assertEqual(IDENT["solucion"], p["solucion"])

    def test_el_cuerpo_del_alta_lleva_lo_que_fija_el_diseno(self):
        cliente = self.armar(ClienteSimulado(), [None, fila_api(entrada=[], salida=[]), fila_api()])
        for ruta in ("customapis", "customapirequestparameters", "customapiresponseproperties"):
            cliente.responder("POST", ruta, (204, {}, {}))
        ca.construir(self.escribir(), False, lambda: cliente)

        cuerpo = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "customapis")
        self.assertEqual(0, cuerpo["bindingtype"])                      # Global (unbound)
        self.assertEqual(0, cuerpo["allowedcustomprocessingsteptype"])  # None: nadie le cuelga pasos
        self.assertIs(True, cuerpo["isprivate"])
        self.assertIs(False, cuerpo["isfunction"])
        self.assertEqual("prvCreatesanic_carp_tbl_generacionformato", cuerpo["executeprivilegename"])
        self.assertEqual(f"/plugintypes({TIPO_ID})", cuerpo["PluginTypeId@odata.bind"])

        entrada = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "customapirequestparameters")
        self.assertEqual("arregloid", entrada["uniquename"])
        self.assertEqual("sanic_carp_capiip_generarformato_arregloid", entrada["name"])
        self.assertEqual(12, entrada["type"])  # Guid
        self.assertIs(False, entrada["isoptional"])
        self.assertEqual(f"/customapis({API_ID})", entrada["CustomAPIId@odata.bind"])

        salidas = [l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "customapiresponseproperties"]
        self.assertEqual(10, salidas[0]["type"])  # String
        self.assertEqual(7, salidas[1]["type"])   # Integer
        self.assertNotIn("isoptional", salidas[0])  # una salida no es opcional ni obligatoria

    def test_si_ya_esta_bien_no_se_escribe_nada(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_privilegio_vacio_que_la_plataforma_devuelve_como_nulo_no_es_una_diferencia(self):
        """Dataverse guarda el texto vacío como `null`: sin esta equivalencia la API quedaría en `difiere` para siempre."""
        sin_puerta = comp(privilegio="")
        cliente = self.armar(ClienteSimulado(), [fila_api(datos=sin_puerta, executeprivilegename=None)])
        estado, _, detalle = ca.construir(self.escribir(sin_puerta), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_valor_nulo_en_el_entorno_solo_equivale_al_texto_vacio_del_playbook(self):
        for campo in ("executeprivilegename", "description"):
            with self.subTest(campo):
                cliente = self.armar(ClienteSimulado(), [fila_api(**{campo: None})])
                estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
                self.assertEqual("difiere", estado, detalle)
                self.assertIn(f"{campo} es None", detalle)

    def test_un_parametro_de_entrada_opcional_se_da_de_alta_como_opcional(self):
        opcional = comp(entrada=[dict(COMPONENTE["entrada"][0], opcional=True)])
        cliente = self.armar(ClienteSimulado(), [None, fila_api(datos=opcional, entrada=[], salida=[]), fila_api(datos=opcional)])
        for ruta in ("customapis", "customapirequestparameters", "customapiresponseproperties"):
            cliente.responder("POST", ruta, (204, {}, {}))
        estado, _, detalle = ca.construir(self.escribir(opcional), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        entrada = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "customapirequestparameters")
        self.assertIs(True, entrada["isoptional"])

    def test_un_privilegio_que_el_playbook_no_declara_pero_el_entorno_si_tiene_es_una_diferencia(self):
        sin_puerta = comp(privilegio="")
        cliente = self.armar(ClienteSimulado(), [fila_api(datos=sin_puerta, executeprivilegename="prvReadAccount")])
        estado, _, detalle = ca.construir(self.escribir(sin_puerta), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("executeprivilegename es 'prvReadAccount'", detalle)

    def test_el_componenttype_leido_se_informa_y_la_consulta_no_lo_filtra(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()], componentes=[{"componenttype": 10101, "objectid": API_ID}])
        _, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertIn("componenttype 10101", detalle)
        for l in cliente.llamadas:
            if l["ruta"].startswith("solutioncomponents?"):
                self.assertNotIn("componenttype eq", l["ruta"])

    def test_un_tipo_de_parametro_distinto_dice_que_hay_que_borrar_y_rehacer(self):
        otra = copy.deepcopy(COMPONENTE)
        otra["entrada"][0]["tipo"] = "String"
        cliente = self.armar(ClienteSimulado(), [fila_api(datos=otra)])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)
        self.assertIn("borrar la Custom API entera", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_isoptional_distinto_tambien_es_inmutable(self):
        otra = copy.deepcopy(COMPONENTE)
        otra["entrada"][0]["opcional"] = True
        cliente = self.armar(ClienteSimulado(), [fila_api(datos=otra)])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("isoptional", detalle)
        self.assertIn("INMUTABLE", detalle)

    def test_completar_no_intenta_arreglar_un_campo_inmutable(self):
        # Lo peor que podría hacer: un PATCH que la plataforma acepta callada
        # sin cambiar nada, y quedarnos creyendo que está corregido.
        cliente = self.armar(ClienteSimulado(), [fila_api(isfunction=True)])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_no_corrige_nada_si_ademas_hay_un_campo_inmutable_distinto(self):
        """Una API con un campo inmutable mal puesto hay que rehacerla: corregirle lo demás sería trabajo tirado."""
        api = fila_api(isfunction=True, displayname="otro", salida=COMPONENTE["salida"][:1])
        cliente = self.armar(ClienteSimulado(), [api])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)
        self.assertIn("displayname es 'otro'", detalle)
        self.assertIn("falta la salida 'tamanobytes'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_corrige_lo_que_si_se_puede_y_nada_mas(self):
        viejo = fila_api(displayname="CAPI - CARP - Viejo", executeprivilegename="prvOtro")
        cliente = self.armar(ClienteSimulado(), [viejo, fila_api()])
        cliente.responder("PATCH", lambda r: r.startswith("customapis("), (204, {}, {}))

        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)

        self.assertEqual("actualizado", estado, detalle)
        patches = [l for l in cliente.llamadas if l["metodo"] == "PATCH"]
        self.assertEqual(1, len(patches))
        self.assertEqual({"displayname", "executeprivilegename"}, set(patches[0]["cuerpo"]))
        self.assertEqual(f"customapis({API_ID})", patches[0]["ruta"])

    def test_completar_agrega_una_salida_que_falta(self):
        sin_una = fila_api(salida=COMPONENTE["salida"][:1])
        cliente = self.armar(ClienteSimulado(), [sin_una, fila_api()])
        cliente.responder("POST", "customapiresponseproperties", (204, {}, {}))

        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)

        self.assertEqual("actualizado", estado, detalle)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(1, len(posts))
        self.assertEqual("tamanobytes", posts[0]["cuerpo"]["uniquename"])

    def test_sin_completar_una_salida_que_falta_solo_se_denuncia(self):
        cliente = self.armar(ClienteSimulado(), [fila_api(salida=COMPONENTE["salida"][:1])])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("falta la salida 'tamanobytes'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_parametro_de_mas_se_denuncia_y_nunca_se_borra(self):
        extra = dict(COMPONENTE["salida"][0], nombre="colado")
        cliente = self.armar(ClienteSimulado(), [fila_api(salida=COMPONENTE["salida"] + [extra])])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("'colado'", detalle)
        self.assertIn("nunca borra", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_api_managed_no_se_toca(self):
        cliente = self.armar(ClienteSimulado(), [fila_api(ismanaged=True, displayname="otro")])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("managed", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_nunca_escribe(self):
        cliente = self.armar(ClienteSimulado(), [None])
        estado, _, detalle = ca.construir(self.escribir(), True, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no existe en el entorno", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_api_fuera_de_la_solucion_es_difiere(self):
        cliente = self.armar(ClienteSimulado(), [fila_api()], componentes=[])
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(SOLUTION_ID, detalle)

    def test_un_http_inesperado_en_el_alta_no_se_confunde_con_exito(self):
        cliente = self.armar(ClienteSimulado(), [None])
        cliente.responder("POST", "customapis", (400, {"error": "unique name en uso"}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HTTP 400", detalle)

    def test_si_un_parametro_no_se_pudo_crear_no_se_informa_creado(self):
        cliente = self.armar(ClienteSimulado(), [None, fila_api(entrada=[], salida=[])])
        cliente.responder("POST", "customapis", (204, {}, {}))
        cliente.responder("POST", "customapirequestparameters", (400, {"error": "tipo inválido"}, {}))
        estado, _, detalle = ca.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("la Custom API se creó pero", detalle)
        self.assertIn("arregloid", detalle)


# ---------------------------------------------------------------------------
# Higiene de los archivos de este paquete de herramientas
# ---------------------------------------------------------------------------
HERRAMIENTAS = os.path.dirname(os.path.dirname(_AQUI))
FUENTES = [os.path.join(HERRAMIENTAS, "construir", n) for n in (
    "rol.py", "perfil_columnas.py", "muestra_rol.py", "muestra_perfil_columnas.py", "custom_api.py", "step.py", "paquete_plugins.py")
] + [os.path.join(HERRAMIENTAS, "codigo", "generar_enums.py")]
PRUEBAS = [os.path.join(_AQUI, n) for n in (
    "test_rol.py", "test_rol_sin_privilegios.py", "test_perfil_columnas.py", "test_muestra_rol.py", "test_muestra_perfil_columnas.py",
    "test_custom_api.py", "test_step.py", "test_paquete_plugins.py")
] + [os.path.join(HERRAMIENTAS, "codigo", "tests", "test_generar_enums.py")]
# Los marcadores se arman en pedazos para que este archivo no se detecte a sí mismo.
DEL_PROYECTO_ANTERIOR = re.compile("|".join([
    "mp" + "pp", "mantenimiento" + "ppp", "puerta" + "delrp" + "a", "registrarresultado" + "rp" + "a", r"\brp" + r"a\b", r"\bD-\d+",
    "03-" + "contratos",  # documento de diseno del proyecto anterior que este repo no tiene
]), re.IGNORECASE)
DE_ESTE_PROYECTO = re.compile(r"\bsanic|(?<![a-z])carp(?![a-z])|15946|nicaragua", re.IGNORECASE)
VOSEO = re.compile(r"\b(" + "|".join([
    "agreg" + "á", "borr" + "á", "volv" + "é", "corr" + "é", "mir" + "á", "us" + "á", "ten" + "és", "pod" + "és", "sab" + "és",
    "quer" + "és", "eleg" + "í", "escrib" + "í", "ped" + "í", "fijat" + "e", "ponet" + "e", "vo" + "s",
]) + r")\b", re.IGNORECASE)


def _lineas_con(patron, ruta):
    with open(ruta, encoding="utf-8") as f:
        return [f"{os.path.basename(ruta)}:{n}: {linea.strip()[:100]}" for n, linea in enumerate(f, 1) if patron.search(linea)]


class HigieneDeLosArchivos(unittest.TestCase):
    def test_ni_las_herramientas_ni_las_pruebas_nombran_el_proyecto_anterior_ni_sus_decisiones(self):
        for ruta in FUENTES + PRUEBAS:
            with self.subTest(os.path.basename(ruta)):
                self.assertEqual(_lineas_con(DEL_PROYECTO_ANTERIOR, ruta), [])

    def test_las_herramientas_no_llevan_escritos_los_datos_de_este_proyecto(self):
        """Prefijo, abreviatura, prefijo de opciones y publisher salen de la Identidad del playbook (o de proyecto.json)."""
        for ruta in FUENTES:
            with self.subTest(os.path.basename(ruta)):
                self.assertEqual(_lineas_con(DE_ESTE_PROYECTO, ruta), [])

    def test_los_textos_van_en_espanol_neutro_sin_voseo(self):
        for ruta in FUENTES + PRUEBAS:
            with self.subTest(os.path.basename(ruta)):
                self.assertEqual(_lineas_con(VOSEO, ruta), [])


class Uso(Base):
    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original, capturado = sys.argv, []
        real = ca.salida
        try:
            sys.argv = ["custom_api.py", self.escribir(), "--forzar"]
            ca.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            ca.main()
        finally:
            sys.argv, ca.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("--forzar", capturado[0][2])


if __name__ == "__main__":
    unittest.main()
