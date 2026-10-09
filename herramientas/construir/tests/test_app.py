"""Pruebas de `herramientas/construir/app.py`. Nunca tocan la red.

Lo que más importa: que cada título lleve UNA etiqueta POR IDIOMA (ahí la
plataforma no sustituye), y que una entrada que nombra una vista NO se arme con
`Entity=` — con cuatro entradas sobre la misma tabla, las cuatro abrirían la
vista por defecto y la navegación del diseño se cae.
"""
import copy
import json
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import app as ap  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="app", inventario="12.7+12.8")
IDIOMAS = IDENT["idiomas"]

APP_ID = "aaaa1111-2222-3333-4444-555566667777"
SITEMAP_ID = "bbbb1111-2222-3333-4444-555566667777"
VISTA_ID = "cccc1111-2222-3333-4444-555566667777"

def t(texto):
    return {"1033": texto, "3082": texto}

COMPONENTE = {
    "tipo": "app",
    "nombre": "MDA - CARP - Control de Arreglos de Pago",
    "uniquename": "sanic_carp_mda_controlarreglospago",
    "descripcion": "Una sola app para los cuatro roles del area.",
    # Sin `idiomas` propio: los titulos cubren los idiomas de la Identidad.
    "icono": "sanic_carp_wr_svg_app",
    "sitemap": "sanic_carp_sm_controlarreglospago",
    "roles": ["SR - CARP - Oficial"],
    "tablas": ["sanic_carp_tbl_conversacion", "sanic_carp_tbl_arreglo", "sanic_carp_tbl_cliente"],
    "areas": [
        {"titulo": t("Trabajo"), "grupos": [
            {"titulo": t("Trabajo"), "entradas": [
                {"id": "subarea_sinrespuesta", "titulo": t("Correos sin respuesta"), "tabla": "sanic_carp_tbl_conversacion",
                 "vista": "Correos sin respuesta", "icono": "sanic_carp_wr_svg_sinrespuesta"},
                {"id": "subarea_arreglos", "titulo": t("Arreglos"), "tabla": "sanic_carp_tbl_arreglo",
                 "icono": "sanic_carp_wr_svg_arreglos"},
                {"id": "subarea_clientes", "titulo": t("Clientes"), "tabla": "sanic_carp_tbl_cliente",
                 "icono": "sanic_carp_wr_svg_clientes"},
            ]},
        ]},
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
        "# Playbook: app · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(identidad, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def xml_de(datos=None, idiomas=None):
    datos = COMPONENTE if datos is None else datos
    idiomas = IDIOMAS if idiomas is None else idiomas
    iconos = {}
    for a in datos["areas"]:
        for g in a["grupos"]:
            for e in g["entradas"]:
                iconos[e["icono"]] = "/WebResources/" + e["icono"]
    iconos[datos["icono"]] = "/WebResources/" + datos["icono"]
    vistas = {(e["tabla"], e["vista"]): VISTA_ID
              for a in datos["areas"] for g in a["grupos"] for e in g["entradas"] if "vista" in e}
    return ap.sitemapxml(datos, iconos, vistas, idiomas)


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None, identidad=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente, identidad))
        return self.ruta

    def armar(self, cliente, sitemap=None, app=None, sin_icono=False, problemas=None, puestas=None, identidad=None):
        armar_cliente_precondiciones_ok(cliente, IDENT if identidad is None else identidad)
        self.sitemap, self.app = sitemap, app
        cliente.responder("GET", lambda r: r.startswith("webresourceset?"),
                          (200, {"value": [] if sin_icono else [{"name": "x", "webresourceid": "wr-1"}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("savedqueries?"),
                          (200, {"value": [{"savedqueryid": VISTA_ID}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("roles?"),
                          (200, {"value": [{"roleid": "rol-1", "name": "SR - CARP - Oficial"}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("sitemaps?"),
                          lambda r, c, s: (200, {"value": [] if self.sitemap is None else [self.sitemap]}, {}))
        # La plataforma tiene DOS capas. `GET appmodules?` devuelve solo la
        # publicada; `RetrieveUnpublishedMultiple` devuelve las dos. El doble
        # respeta esa diferencia: si no, una app sin publicar se ve igual que
        # una publicada y la prueba no distingue nada.
        cliente.responder("GET", lambda r: r.startswith("appmodules?"),
                          lambda r, c, s: (200, {"value": [] if self.app is None
                                                 or self.app.get("componentstate") == 1 else [self.app]}, {}))
        cliente.responder("GET", lambda r: r.startswith("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple"),
                          lambda r, c, s: (200, {"value": [] if self.app is None else [self.app]}, {}))
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName="),
                          lambda r, c, s: (200, {"MetadataId": "md-" + r.split("'")[1]}, {}))
        # `AddAppComponents` devuelve 204 aunque no agregue nada: la herramienta
        # tiene que releer lo que QUEDÓ, no contar lo que pidió el playbook.
        self.puestas = list(COMPONENTE["tablas"]) if puestas is None else puestas
        cliente.responder("GET", lambda r: r.startswith("RetrieveAppComponents("),
                          lambda r, c, s: (200, {"value": [{"componenttype": 1, "objectid": "md-" + t}
                                                           for t in self.puestas]}, {}))
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(md-"),
                          lambda r, c, s: (200, {"LogicalName": r.split("(md-")[1].split(")")[0]}, {}))
        lista = problemas or []
        cliente.responder("GET", lambda r: r.startswith("ValidateApp("),
                          (200, {"AppValidationResponse": {"ValidationIssueList": lista}}, {}))
        return cliente


class XmlGenerado(unittest.TestCase):
    def test_cada_titulo_lleva_una_etiqueta_por_idioma(self):
        # `01-convenciones.md`: en el XML del sitemap no hay sustitucion.
        x = xml_de()
        self.assertIn('<Title LCID="1033" Title="Correos sin respuesta" />', x)
        self.assertIn('<Title LCID="3082" Title="Correos sin respuesta" />', x)
        self.assertEqual(x.count('LCID="1033"'), x.count('LCID="3082"'))

    def test_los_titulos_salen_en_los_idiomas_de_la_identidad_y_en_ese_orden(self):
        # Los idiomas ya no vienen del bloque de la app sino de la Identidad.
        datos = copy.deepcopy(COMPONENTE)
        for a in datos["areas"]:
            a["titulo"] = {"1033": "A", "3082": "B", "1034": "C"}
            for g in a["grupos"]:
                g["titulo"] = {"1033": "A", "3082": "B", "1034": "C"}
                for e in g["entradas"]:
                    e["titulo"] = {"1033": "A", "3082": "B", "1034": "C"}
        x = xml_de(datos, [3082, 1033])
        self.assertNotIn('LCID="1034"', x)
        self.assertIn('<Titles><Title LCID="3082" Title="B" /><Title LCID="1033" Title="A" /></Titles>', x)

    def test_una_entrada_con_vista_no_usa_Entity(self):
        # Con Entity=, cuatro entradas sobre la misma tabla abren lo mismo.
        x = xml_de()
        sinrespuesta = x[x.index("subarea_sinrespuesta"):x.index("</SubArea>")]
        self.assertNotIn("Entity=", sinrespuesta)
        self.assertIn("pagetype=entitylist", sinrespuesta)
        self.assertIn(f"viewid=%7b{VISTA_ID}%7d", sinrespuesta)
        self.assertIn(f"viewtype={ap.VISTA_LISTA}", sinrespuesta)

    def test_una_entrada_sin_vista_usa_Entity(self):
        x = xml_de()
        i = x.index("subarea_arreglos")
        arreglos = x[i:x.index("</SubArea>", i)]
        self.assertIn('Entity="sanic_carp_tbl_arreglo"', arreglos)
        self.assertNotIn("Url=", arreglos)

    def test_el_amp_de_la_url_va_escapado(self):
        # Una URL con & crudo rompe el XML del sitemap entero.
        x = xml_de()
        self.assertIn("&amp;pagetype", x)
        self.assertNotIn("?etn=sanic_carp_tbl_conversacion&pagetype", x)

    def test_el_icono_se_referencia_como_webresources(self):
        self.assertIn('VectorIcon="/WebResources/sanic_carp_wr_svg_sinrespuesta"', xml_de())


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = ap.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_un_titulo_sin_todos_los_idiomas_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["areas"][0]["titulo"] = {"1033": "Trabajo"}
        self.rechaza(malo, "un texto por cada idioma")

    def test_un_titulo_con_un_idioma_de_mas_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["areas"][0]["titulo"] = {"1033": "T", "3082": "T", "1034": "T"}
        self.rechaza(malo, "sobran")

    def test_un_titulo_con_tilde_se_rechaza_en_area_grupo_y_entrada(self):
        # Los titulos del sitemap son nombres visibles: BP-PP-197, sin tildes ni enie, en cada idioma.
        for donde in ("area", "grupo", "entrada"):
            for lcid in ("1033", "3082"):
                with self.subTest(donde=donde, idioma=lcid):
                    malo = copy.deepcopy(COMPONENTE)
                    area = malo["areas"][0]
                    objeto = {"area": area, "grupo": area["grupos"][0], "entrada": area["grupos"][0]["entradas"][0]}[donde]
                    objeto["titulo"] = dict(objeto["titulo"], **{lcid: "Gestión"})
                    self.rechaza(malo, "fuera de ASCII")

    def test_el_bloque_de_la_app_ya_no_trae_idiomas_propios(self):
        # Los idiomas del proyecto son los de la Identidad: una segunda lista en el bloque podria contradecirla.
        self.rechaza(comp(idiomas=[1033, 3082]), "sobran ['idiomas']")

    def rechaza_con_identidad(self, componente, idiomas, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = ap.construir(
            self.escribir(componente, dict(IDENT, idiomas=idiomas)), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_los_titulos_tienen_que_cubrir_todos_los_idiomas_de_la_identidad(self):
        # La Identidad declara tres idiomas y los titulos solo traen dos: falta el 1034.
        self.rechaza_con_identidad(COMPONENTE, [1033, 3082, 1034], "faltan ['1034']")

    def test_los_titulos_no_pueden_traer_un_idioma_que_la_identidad_no_declara(self):
        # La Identidad declara solo el 1033 y los titulos traen ademas el 3082.
        self.rechaza_con_identidad(COMPONENTE, [1033], "sobran ['3082']")

    def test_una_entrada_sobre_una_tabla_que_la_app_no_incluye_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["areas"][0]["grupos"][0]["entradas"][0]["tabla"] = "sanic_carp_tbl_cuota"
        self.rechaza(malo, "no está en la lista de tablas de la app")

    def test_dos_entradas_con_el_mismo_id_se_rechazan(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["areas"][0]["grupos"][0]["entradas"][1]["id"] = "subarea_sinrespuesta"
        self.rechaza(malo, "está repetido")

    def test_los_patrones_de_nombre_salen_de_la_identidad_y_no_del_codigo(self):
        # Con otra Identidad, el patron es el de ESA Identidad: el del proyecto de las demas pruebas ya no vale.
        otra = dict(IDENT, prefijo="zeta", abrev="qq", solucion="zeta_qq_sol_prueba", publisher="Otro_Publisher", prefijo_opciones=12345)
        centinela = FabricaCentinela()
        estado, _, detalle = ap.construir(self.escribir(COMPONENTE, otra), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn("no sigue el patrón zeta_qq_mda_", detalle)
        propio = comp(uniquename="zeta_qq_mda_x", sitemap="zeta_qq_sm_x")
        estado, _, detalle = ap.construir(self.escribir(propio, otra), False, centinela)
        self.assertNotIn("no sigue el patrón", detalle)

    def test_los_nombres_siguen_la_convencion(self):
        self.rechaza(comp(uniquename="controlarreglospago"), "no sigue el patrón sanic_carp_mda_")
        self.rechaza(comp(sitemap="mapa"), "no sigue el patrón sanic_carp_sm_")


class ContraElEntorno(Base):
    def test_un_idioma_de_la_identidad_sin_provisionar_bloquea_sin_escribir(self):
        # `comprobar_solucion_e_idioma` exige cada idioma de `idiomas` provisionado: si falta el 3082 la creacion
        # responderia 204 y el componente quedaria sin etiqueta en ese idioma.
        cliente = self.armar(ClienteSimulado())
        cliente._reglas.insert(0, ("GET", "RetrieveProvisionedLanguages",
                                   (200, {"RetrieveProvisionedLanguages": [1033]}, {})))
        estado, _, detalle = ap.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("[3082]", detalle)
        self.assertIn("no están provisionados", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_icono_que_no_existe_queda_bloqueado_sin_escribir(self):
        cliente = self.armar(ClienteSimulado(), sin_icono=True)
        estado, _, detalle = ap.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("hay que crearlo primero", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_alta_crea_sitemap_y_app_y_asocia_todo(self):
        cliente = self.armar(ClienteSimulado())

        def crear_sitemap(ruta, cuerpo, solucion):
            self.sitemap = {"sitemapid": SITEMAP_ID, "sitemapnameunique": COMPONENTE["sitemap"],
                            "sitemapxml": xml_de(), "isappaware": True}
            return (204, {}, {})

        def crear_app(ruta, cuerpo, solucion):
            self.app = {"appmoduleid": APP_ID, "uniquename": COMPONENTE["uniquename"],
                        "name": COMPONENTE["nombre"], "description": COMPONENTE["descripcion"],
                        "ismanaged": False}
            return (204, {}, {})

        cliente.responder("POST", "sitemaps", crear_sitemap)
        cliente.responder("POST", "appmodules", crear_app)
        cliente.responder("POST", "AddAppComponents", (204, {}, {}))
        cliente.responder("POST", lambda r: "appmoduleroles_association" in r, (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))

        estado, _, detalle = ap.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("creado", estado, detalle)
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertIn("sitemaps", rutas)
        self.assertIn("appmodules", rutas)
        # Sin `clienttype` la plataforma lo pone en 2 y la app abre con el cartel
        # "designed for the legacy web client" (visto en Dev el 2026-09-23).
        alta = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "appmodules")
        self.assertEqual(4, alta["clienttype"], "4 = Unified Interface")
        # Un AddAppComponents por el sitemap y uno por cada tabla.
        self.assertEqual(1 + len(COMPONENTE["tablas"]), rutas.count("AddAppComponents"))
        self.assertTrue(any("appmoduleroles_association" in r for r in rutas))
        self.assertIn("PublishXml", rutas)
        # OData exige un URI ABSOLUTO en el `@odata.id` de un `$ref`.
        rol = next(l["cuerpo"] for l in cliente.llamadas if "appmoduleroles_association" in l["ruta"])
        self.assertTrue(rol["@odata.id"].startswith("https://"), rol["@odata.id"])
        self.assertTrue(rol["@odata.id"].endswith("roles(rol-1)"), rol["@odata.id"])
        sm = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "sitemaps")
        self.assertIs(True, sm["isappaware"])
        # El tipo `entity` lleva `entityid`, no `MetadataId`: con `MetadataId`
        # la plataforma responde 400 (verificado en Dev el 2026-09-22).
        tablas = [l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "AddAppComponents"
                  and l["cuerpo"]["Components"][0]["@odata.type"].endswith(".entity")]
        self.assertEqual(len(COMPONENTE["tablas"]), len(tablas))
        self.assertEqual(["md-" + t for t in COMPONENTE["tablas"]],
                         [c["Components"][0]["entityid"] for c in tablas])
        for cuerpo in tablas:
            self.assertNotIn("MetadataId", cuerpo["Components"][0])

    def test_el_sitemap_se_escribe_en_los_idiomas_de_la_identidad(self):
        idiomas = [1033, 3082, 1034]
        ident = dict(IDENT, idiomas=idiomas)
        datos = copy.deepcopy(COMPONENTE)
        for a in datos["areas"]:
            a["titulo"] = {str(i): "T" for i in idiomas}
            for g in a["grupos"]:
                g["titulo"] = {str(i): "G" for i in idiomas}
                for e in g["entradas"]:
                    e["titulo"] = {str(i): "E" for i in idiomas}
        cliente = self.armar(ClienteSimulado(), identidad=ident)
        cliente.responder("POST", "sitemaps", (204, {}, {}))
        cliente.responder("POST", "appmodules", (204, {}, {}))
        cliente.responder("POST", "AddAppComponents", (204, {}, {}))
        cliente.responder("POST", lambda r: "appmoduleroles_association" in r, (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))

        ap.construir(self.escribir(datos, ident), False, lambda: cliente)

        xml = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "sitemaps" and l["metodo"] == "POST")["sitemapxml"]
        self.assertEqual(xml_de(datos, idiomas), xml)
        self.assertIn('<Title LCID="1034" Title="E" />', xml)

    def test_una_app_sin_publicar_se_encuentra_y_no_se_intenta_crear(self):
        # `componentstate = 1` (sin publicar): un `GET appmodules?` NO la
        # devuelve. Si la herramienta no la ve, la intenta crear y la
        # plataforma responde 400 con `-2147155681` (unique name duplicado),
        # que no dice "duplicado" por ningún lado. Verificado en Dev el
        # 2026-09-22: el primer alta había funcionado y quedó invisible.
        sitemap = {"sitemapid": SITEMAP_ID, "sitemapnameunique": COMPONENTE["sitemap"],
                   "sitemapxml": xml_de(), "isappaware": True}
        app = {"appmoduleid": APP_ID, "uniquename": COMPONENTE["uniquename"],
               "name": COMPONENTE["nombre"], "description": COMPONENTE["descripcion"],
               "ismanaged": False, "componentstate": 1}
        cliente = self.armar(ClienteSimulado(), sitemap=sitemap, app=app)
        cliente.responder("POST", "AddAppComponents", (204, {}, {}))
        cliente.responder("POST", lambda r: "appmoduleroles_association" in r, (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))

        estado, _, detalle = ap.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("actualizado", estado, detalle)
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertNotIn("appmodules", rutas)
        self.assertIn("PublishXml", rutas)

    def test_una_tabla_que_no_quedo_en_la_app_no_se_informa_como_puesta(self):
        # `AddAppComponents` responde 204 aunque no agregue nada, y
        # `ValidateApp` tampoco se queja (verificado en Dev el 2026-09-22:
        # diez altas, diez 204, cero tablas). Si la herramienta cuenta lo que
        # pidió el playbook en vez de lo que quedó, informa un éxito falso.
        sitemap = {"sitemapid": SITEMAP_ID, "sitemapnameunique": COMPONENTE["sitemap"],
                   "sitemapxml": xml_de(), "isappaware": True}
        app = {"appmoduleid": APP_ID, "uniquename": COMPONENTE["uniquename"],
               "name": COMPONENTE["nombre"], "description": COMPONENTE["descripcion"], "ismanaged": False}
        cliente = self.armar(ClienteSimulado(), sitemap=sitemap, app=app,
                             puestas=[COMPONENTE["tablas"][0]])
        cliente.responder("POST", "AddAppComponents", (204, {}, {}))
        cliente.responder("POST", lambda r: "appmoduleroles_association" in r, (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))

        estado, _, detalle = ap.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("error", estado, detalle)
        self.assertIn("no incluye 2 de las 3 tablas", detalle)
        self.assertIn(COMPONENTE["tablas"][1], detalle)
        self.assertIn(COMPONENTE["tablas"][2], detalle)

    def test_si_ValidateApp_devuelve_errores_no_se_informa_exito(self):
        cliente = self.armar(ClienteSimulado(), problemas=[{"ErrorType": "Error", "Message": "falta una vista"}],
                             sitemap={"sitemapid": SITEMAP_ID, "sitemapxml": xml_de(), "isappaware": True},
                             app={"appmoduleid": APP_ID, "ismanaged": False})
        estado, _, detalle = ap.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("falta una vista", detalle)

    def test_un_aviso_de_ValidateApp_no_frena(self):
        cliente = self.armar(ClienteSimulado(), problemas=[{"ErrorType": "Warning", "Message": "aviso"}],
                             sitemap={"sitemapid": SITEMAP_ID, "sitemapxml": xml_de(), "isappaware": True},
                             app={"appmoduleid": APP_ID, "ismanaged": False})
        estado, _, detalle = ap.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_un_sitemap_sin_isappaware_se_denuncia(self):
        cliente = self.armar(ClienteSimulado(),
                             sitemap={"sitemapid": SITEMAP_ID, "sitemapxml": xml_de(), "isappaware": False},
                             app={"appmoduleid": APP_ID, "ismanaged": False})
        estado, _, detalle = ap.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("isappaware", detalle)

    def test_una_app_managed_no_se_toca(self):
        cliente = self.armar(ClienteSimulado(),
                             sitemap={"sitemapid": SITEMAP_ID, "sitemapxml": xml_de(), "isappaware": True},
                             app={"appmoduleid": APP_ID, "ismanaged": True})
        estado, _, detalle = ap.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("managed", detalle)

    def test_solo_verificar_nunca_escribe(self):
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = ap.construir(self.escribir(), True, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())


class Uso(Base):
    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original, capturado = sys.argv, []
        real = ap.salida
        try:
            sys.argv = ["app.py", self.escribir(), "--forzar"]
            ap.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            ap.main()
        finally:
            sys.argv, ap.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("--forzar", capturado[0][2])


if __name__ == "__main__":
    unittest.main()
