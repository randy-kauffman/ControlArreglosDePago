"""Pruebas de la ampliación de `app.py` (brecha G-A1 de `diseno/07`): `<Privilege>` por entrada del sitemap y entrada de dashboard.

Formas (Learn, "SiteMap schema", SiteMap.xsd): `SubArea` admite, en este orden, `Titles`, `Descriptions` y `Privilege*`;
`<Privilege Entity="..." Privilege="Create" />` y el valor es una lista separada por comas de
Read, Write, Append, AppendTo, Create, Delete, Share, Assign, All (también hay tres de plataforma que aquí no se usan).
Un `SubArea` visible solo si el usuario tiene los privilegios que declara.
"""
import copy
import os
import sys
import unittest
from xml.etree import ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import app as ap  # noqa: E402
import test_app as ta  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402

DASH_ID = "dddd1111-2222-3333-4444-555566667777"
PRIV = [{"tabla": "sanic_carp_tbl_arreglo", "privilegio": "Create"}]


def con_entradas(*entradas):
    d = copy.deepcopy(ta.COMPONENTE)
    d["areas"][0]["grupos"][0]["entradas"] = list(copy.deepcopy(entradas))
    return d


def sub_con_privilegio(**extra):
    return dict({"id": "subarea_arreglos", "titulo": ta.t("Arreglos"), "tabla": "sanic_carp_tbl_arreglo",
                 "icono": "sanic_carp_wr_svg_arreglos", "privilegios": PRIV}, **extra)


DASH = {"id": "subarea_panel", "titulo": ta.t("Panel de supervision"), "dashboard": "DASH - CARP - Supervision",
        "icono": "sanic_carp_wr_svg_lista", "privilegios": PRIV}


def xml_de(datos):
    iconos = {e["icono"]: "/WebResources/" + e["icono"] for a in datos["areas"] for g in a["grupos"] for e in g["entradas"]}
    iconos[datos["icono"]] = "/WebResources/" + datos["icono"]
    vistas = {(e["tabla"], e["vista"]): ta.VISTA_ID for a in datos["areas"] for g in a["grupos"]
              for e in g["entradas"] if "vista" in e}
    dashboards = {e["dashboard"]: DASH_ID for a in datos["areas"] for g in a["grupos"] for e in g["entradas"]
                  if "dashboard" in e}
    return ap.sitemapxml(datos, iconos, vistas, ta.IDIOMAS, dashboards)


def subareas(xml):
    return {s.get("Id"): s for s in ET.fromstring(xml).iter("SubArea")}


class PrivilegiosEnElXml(unittest.TestCase):
    def test_el_privilegio_sale_como_hijo_de_la_subarea_despues_de_los_titulos(self):
        s = subareas(xml_de(con_entradas(sub_con_privilegio())))["subarea_arreglos"]
        self.assertEqual(["Titles", "Privilege"], [h.tag for h in s])
        self.assertEqual({"Entity": "sanic_carp_tbl_arreglo", "Privilege": "Create"}, s.find("Privilege").attrib)

    def test_varios_privilegios_de_una_tabla_van_separados_por_coma_sin_espacios(self):
        e = sub_con_privilegio(privilegios=[{"tabla": "sanic_carp_tbl_arreglo", "privilegio": "Read,Write"}])
        self.assertEqual("Read,Write", subareas(xml_de(con_entradas(e)))["subarea_arreglos"].find("Privilege").get("Privilege"))

    def test_varias_tablas_son_varios_elementos_en_el_orden_del_playbook(self):
        e = sub_con_privilegio(privilegios=PRIV + [{"tabla": "sanic_carp_tbl_cliente", "privilegio": "Read"}])
        ps = subareas(xml_de(con_entradas(e)))["subarea_arreglos"].findall("Privilege")
        self.assertEqual(["sanic_carp_tbl_arreglo", "sanic_carp_tbl_cliente"], [p.get("Entity") for p in ps])

    def test_una_entrada_sin_privilegios_no_lleva_ninguno(self):
        sin = {k: v for k, v in sub_con_privilegio().items() if k != "privilegios"}
        self.assertEqual(0, len(subareas(xml_de(con_entradas(sin)))["subarea_arreglos"].findall("Privilege")))

    def test_la_entrada_con_vista_tambien_lleva_privilegios(self):
        e = {"id": "subarea_sinrespuesta", "titulo": ta.t("Correos sin respuesta"), "tabla": "sanic_carp_tbl_conversacion",
             "vista": "Correos sin respuesta", "icono": "sanic_carp_wr_svg_sinrespuesta",
             "privilegios": [{"tabla": "sanic_carp_tbl_conversacion", "privilegio": "Write"}]}
        s = subareas(xml_de(con_entradas(e)))["subarea_sinrespuesta"]
        self.assertEqual("Write", s.find("Privilege").get("Privilege"))
        self.assertIn("viewid", s.get("Url"))


class DashboardEnElXml(unittest.TestCase):
    def test_la_entrada_de_dashboard_abre_por_url_con_su_id_y_sin_entity(self):
        s = subareas(xml_de(con_entradas(DASH)))["subarea_panel"]
        self.assertIsNone(s.get("Entity"))
        self.assertEqual(f"/main.aspx?pagetype=dashboard&id=%7b{DASH_ID}%7d", s.get("Url"))
        self.assertEqual("/WebResources/sanic_carp_wr_svg_lista", s.get("Icon"))

    def test_el_amp_de_la_url_del_dashboard_va_escapado_en_el_texto(self):
        self.assertIn("main.aspx?pagetype=dashboard&amp;id=", xml_de(con_entradas(DASH)))

    def test_el_dashboard_tambien_lleva_privilegio(self):
        self.assertEqual("Create", subareas(xml_de(con_entradas(DASH)))["subarea_panel"].find("Privilege").get("Privilege"))


class ValidacionAmpliada(ta.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = ap.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_tabla_de_rechazos(self):
        def p(**kw):
            return con_entradas(sub_con_privilegio(privilegios=[dict({"tabla": "sanic_carp_tbl_arreglo", "privilegio": "Create"}, **kw)]))
        casos = [
            ("privilegios no es lista", con_entradas(sub_con_privilegio(privilegios={})), "privilegios"),
            ("privilegios vacia", con_entradas(sub_con_privilegio(privilegios=[])), "privilegios"),
            ("privilegio que no es objeto", con_entradas(sub_con_privilegio(privilegios=["Create"])), "privilegios[0]"),
            ("privilegio sin tabla", con_entradas(sub_con_privilegio(privilegios=[{"privilegio": "Create"}])), "faltan ['tabla']"),
            ("privilegio sin privilegio", con_entradas(sub_con_privilegio(
                privilegios=[{"tabla": "sanic_carp_tbl_arreglo"}])), "faltan ['privilegio']"),
            ("privilegio con clave de mas", p(x=1), "sobran ['x']"),
            ("tabla que no es texto", p(tabla=3), "tabla"),
            ("privilegio que no es texto", p(privilegio=3), "privilegio"),
            ("privilegio inexistente", p(privilegio="Crear"), "no admitido"),
            ("privilegio con espacio", p(privilegio="Read, Write"), "no admitido"),
            ("privilegio con coma de mas", p(privilegio="Read,"), "no admitido"),
            ("privilegio repetido", p(privilegio="Read,Read"), "repetido"),
            ("tabla fuera de la app", p(tabla="sanic_carp_tbl_otra"), "no está en la lista de tablas"),
            ("la misma tabla dos veces", con_entradas(sub_con_privilegio(privilegios=PRIV + PRIV)), "ya tiene un privilegio"),
            ("dashboard con tabla", con_entradas(dict(DASH, tabla="sanic_carp_tbl_arreglo")), "exactamente uno"),
            ("dashboard con vista", con_entradas(dict(DASH, vista="x")), "sobran"),
            ("dashboard vacio", con_entradas(dict(DASH, dashboard="")), "dashboard"),
            ("dashboard que no es texto", con_entradas(dict(DASH, dashboard=3)), "dashboard"),
            ("entrada sin tabla ni dashboard", con_entradas({k: v for k, v in DASH.items() if k != "dashboard"}),
             "exactamente uno"),
            ("dashboard con tilde", con_entradas(dict(DASH, dashboard="DASH - Supervisión")), "fuera de ASCII"),
        ]
        for nombre, componente, fragmento in casos:
            with self.subTest(nombre):
                self.rechaza(componente, fragmento)

    def test_los_playbooks_validos_pasan(self):
        ap.validar_playbook(con_entradas(sub_con_privilegio(), DASH), ta.IDENT)


class DashboardContraElEntorno(ta.Base):
    def armar_dashboard(self, filas):
        cliente = self.armar(ClienteSimulado())
        self.filas = filas
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("systemforms?"),
                                   lambda r, c, s: (200, {"value": self.filas}, {})))
        return cliente

    def datos(self):
        return con_entradas(sub_con_privilegio(), DASH)

    def correr(self, cliente, solo_verificar=True):
        return ap.construir(self.escribir(self.datos()), solo_verificar, lambda: cliente, dormir=lambda s: None)

    def test_el_dashboard_se_busca_por_nombre_y_tipo_0(self):
        cliente = self.armar_dashboard([{"formid": DASH_ID}])
        self.correr(cliente)
        ruta = next(l["ruta"] for l in cliente.llamadas if l["ruta"].startswith("systemforms?"))
        self.assertIn("type eq 0", ruta)
        self.assertIn("name eq 'DASH - CARP - Supervision'", ruta)

    def test_un_dashboard_que_no_existe_bloquea_sin_escribir(self):
        cliente = self.armar_dashboard([])
        estado, _, detalle = self.correr(cliente, solo_verificar=False)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("DASH - CARP - Supervision", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_dos_dashboards_con_el_mismo_nombre_bloquean(self):
        cliente = self.armar_dashboard([{"formid": DASH_ID}, {"formid": "otro"}])
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("hay 2", detalle)

    def test_un_http_inesperado_al_buscar_el_dashboard_es_error(self):
        cliente = self.armar_dashboard([])
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("systemforms?"), (500, {}, {})))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("500", detalle)

    def test_un_formid_ausente_es_forma_inesperada(self):
        cliente = self.armar_dashboard([{"nombre": "x"}])
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("formid", detalle)

    def test_el_sitemap_existente_sin_los_privilegios_difiere_y_no_escribe(self):
        # Los tres caminos comparan el mismo XML: un sitemap publicado sin <Privilege> no coincide.
        cliente = self.armar_dashboard([{"formid": DASH_ID}])
        viejo = con_entradas(*[{k: v for k, v in e.items() if k != "privilegios"} for e in (sub_con_privilegio(), DASH)])
        self.sitemap = {"sitemapid": ta.SITEMAP_ID, "sitemapxml": xml_de(viejo), "isappaware": True}
        self.app = {"appmoduleid": ta.APP_ID, "ismanaged": False}
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sitemapxml no coincide", detalle)
        self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
