"""Comparación de una clave alternativa contra el XML de la solución
exportada. Usa las muestras de `tests/muestras/clave/`: formas derivadas del
proyecto anterior, no exportaciones reales de CARP. El nombre visible se
compara en CADA idioma de `idiomas`, en los dos sentidos."""
import base64
import contextlib
import copy
import io
import json
import os
import sys
import tempfile
import unittest
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, os.path.dirname(os.path.dirname(_AQUI)))
sys.path.insert(0, _AQUI)

import credenciales  # noqa: E402
import muestra_clave as mc  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, escribir_playbook  # noqa: E402
from test_clave import BASE as K02, K01, K03  # noqa: E402

IDIOMAS = [1033, 3082]
MUESTRAS = os.path.join(_AQUI, "muestras", "clave")
IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="clave")


def entidad(tabla, claves_xml):
    return f'<Entity><Name>{tabla}</Name><EntityInfo><entity Name="{tabla}">{claves_xml}</entity></EntityInfo></Entity>'


def leer_muestra(archivo):
    with open(os.path.join(MUESTRAS, archivo), encoding="utf-8") as f:
        return f.read()


def customizations(*entidades):
    return "<ImportExportXml><Entities>" + "".join(entidades) + "</Entities></ImportExportXml>"


ARREGLO = entidad("sanic_carp_tbl_arreglo", leer_muestra("sanic_carp_tbl_arreglo.claves.solucion.xml"))
CONVERSACION = entidad("sanic_carp_tbl_conversacion", leer_muestra("sanic_carp_tbl_conversacion.claves.solucion.xml"))
XML = customizations(ARREGLO, CONVERSACION)


def cambiar(d, clave, valor):
    n = copy.deepcopy(d)
    n[clave] = valor
    return n


class ContraLaMuestra(unittest.TestCase):
    def comparar(self, xml, datos, idiomas=IDIOMAS):
        return mc.comparar_con_xml(xml, datos, idiomas)

    def test_coincide_aunque_el_xml_traiga_las_columnas_en_otro_orden(self):
        self.assertEqual(K02["columnas"], ["sanic_producto", "sanic_numeroproducto", "sanic_numeroarreglo"])  # el XML las trae en orden alfabético
        self.assertEqual(self.comparar(XML, K02), [])
        self.assertEqual(self.comparar(XML, K01), [])

    def test_no_esta(self):
        for nombre, datos in (("clave inexistente", cambiar(K02, "nombre", "sanic_carp_key_arreglo_inexistente")),
                              ("la clave de otra tabla", K03)):
            with self.subTest(nombre):
                difs = self.comparar(XML, datos)
                self.assertEqual(len(difs), 1, difs)
                self.assertIn("no está en la solución exportada", difs[0])

    def test_esta_pero_en_otra_tabla(self):
        difs = self.comparar(XML, cambiar(K02, "tabla", "sanic_carp_tbl_cuota"))
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])
        difs = self.comparar(XML, cambiar(K02, "tabla", "sanic_carp_tbl_conversacion"))
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia_de_columnas(self):
        casos = [cambiar(K02, "columnas", ["sanic_numeroproducto"]),
                 cambiar(K02, "columnas", K02["columnas"] + ["sanic_comentarios"])]
        for datos in casos:
            with self.subTest(str(datos["columnas"])):
                difs = self.comparar(XML, datos)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith("columnas: xml="), difs)

    def test_el_nombre_visible_se_compara_en_cada_idioma(self):
        for lcid in (1033, 3082):
            with self.subTest(lcid):
                datos = cambiar(K02, "displayname", dict(K02["displayname"], **{str(lcid): "KEY - CARP - Otro"}))
                difs = self.comparar(XML, datos)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith(f"displayname[{lcid}]: xml="), difs)
                self.assertIn("playbook='KEY - CARP - Otro'", difs[0])

    def test_un_idioma_menos_en_el_xml_es_una_diferencia_del_idioma_que_falta(self):
        linea = '<displayname description="KEY - CARP - Arreglo - Producto, numero de producto y numero de arreglo" languagecode="3082" />'
        self.assertIn(linea, XML)
        difs = self.comparar(XML.replace(linea, ""), K02)
        self.assertEqual(len(difs), 1, difs)
        self.assertTrue(difs[0].startswith("displayname[3082]: xml=None"), difs)

    def test_una_etiqueta_en_un_idioma_que_el_playbook_no_declara_es_una_diferencia(self):
        linea = '<displayname description="KEY - CARP - Arreglo - Product, product number and arrangement number" languagecode="1033" />'
        xml = XML.replace(linea, linea + '<displayname description="x" languagecode="2058" />')
        self.assertNotEqual(xml, XML)
        difs = self.comparar(xml, K02)
        self.assertEqual(len(difs), 1, difs)
        self.assertTrue(difs[0].startswith("displayname: tiene etiquetas en un idioma que el playbook no declara"), difs)
        self.assertIn("2058", difs[0])

    def test_con_un_solo_idioma_declarado_el_otro_del_xml_sobra(self):
        datos = cambiar(K01, "displayname", {"1033": "KEY - CARP - Conversacion - Huella del Message-ID"})
        difs = self.comparar(XML, datos, [1033])
        self.assertEqual(len(difs), 1, difs)
        self.assertIn("no declara", difs[0])
        self.assertIn("3082", difs[0])

    def test_un_nombre_visible_igual_en_los_dos_idiomas_tambien_coincide(self):
        self.assertEqual(K01["displayname"]["1033"], K01["displayname"]["3082"])
        self.assertEqual(self.comparar(XML, K01), [])


class CarpetaDePlaybooks(unittest.TestCase):
    def test_sale_de_la_clave_playbooks_de_la_configuracion(self):
        self.assertEqual(mc.carpeta_playbooks("clave", {"playbooks": "otra/carpeta"}), os.path.join(credenciales.RAIZ_PROYECTO, "otra", "carpeta", "clave"))

    def test_una_ruta_absoluta_se_respeta(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(mc.carpeta_playbooks("clave", {"playbooks": d}), os.path.join(d, "clave"))

    def test_sin_configuracion_usa_proyecto_json(self):
        esperado = os.path.join(credenciales.RAIZ_PROYECTO, credenciales.cargar_configuracion()["playbooks"], "clave")
        self.assertEqual(mc.carpeta_playbooks("clave"), esperado)


def respuesta_export(custom_xml):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("customizations.xml", custom_xml)
    return (200, {"ExportSolutionFile": base64.b64encode(buf.getvalue()).decode("ascii")}, {})


class Principal(unittest.TestCase):
    """`main()` de punta a punta, con un cliente simulado y una carpeta temporal de playbooks."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.config = {"playbooks": self.tmp.name}
        self.carpeta = os.path.join(self.tmp.name, "clave")
        os.makedirs(self.carpeta)

    def playbook(self, nombre_archivo, datos, ident=None):
        ruta = os.path.join(self.carpeta, nombre_archivo)
        escribir_playbook(ruta, IDENT if ident is None else ident, datos)
        return ruta

    def correr(self, respuesta, argv=()):
        cliente = ClienteSimulado()
        cliente.responder("POST", "ExportSolution", respuesta)
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mc.main(list(argv), lambda: cliente, self.config)
        return codigo, salida.getvalue(), cliente

    def test_todo_coincide(self):
        self.playbook("k02.md", K02)
        self.playbook("k01.md", K01)
        codigo, texto, cliente = self.correr(respuesta_export(XML))
        self.assertEqual(codigo, 0, texto)
        self.assertIn(f"OK       {K02['nombre']}", texto)
        self.assertIn(f"OK       {K01['nombre']}", texto)
        self.assertEqual(json.loads(texto.strip().splitlines()[-1]), {"playbooks": 2, "coinciden": 2, "difieren": 0})
        llamada = cliente.llamadas[0]
        self.assertEqual((llamada["metodo"], llamada["ruta"], llamada["cuerpo"]), ("POST", "ExportSolution", {"SolutionName": IDENTIDAD_VALIDA["solucion"], "Managed": False}))

    def test_un_idioma_que_difiere_se_informa_y_da_codigo_1(self):
        self.playbook("k02.md", cambiar(K02, "displayname", dict(K02["displayname"], **{"3082": "KEY - CARP - Arreglo - Otro"})))
        codigo, texto, _ = self.correr(respuesta_export(XML))
        self.assertEqual(codigo, 1)
        self.assertIn(f"DIFIERE  {K02['nombre']}: displayname[3082]", texto)
        self.assertEqual(json.loads(texto.strip().splitlines()[-1]), {"playbooks": 1, "coinciden": 0, "difieren": 1})

    def test_una_clave_que_no_esta_en_la_solucion_difiere(self):
        self.playbook("k03.md", K03)
        codigo, texto, _ = self.correr(respuesta_export(XML))
        self.assertEqual(codigo, 1)
        self.assertIn("no está en la solución exportada", texto)

    def test_se_puede_pasar_el_playbook_por_argumento(self):
        ruta = self.playbook("k01.md", K01)
        self.playbook("k02.md", cambiar(K02, "nombre", "sanic_carp_key_arreglo_inexistente"))  # no se compara: no se nombra
        codigo, texto, _ = self.correr(respuesta_export(XML), argv=[ruta])
        self.assertEqual(codigo, 0, texto)
        self.assertEqual(json.loads(texto.strip().splitlines()[-1]), {"playbooks": 1, "coinciden": 1, "difieren": 0})

    def test_exportsolution_con_http_inesperado_o_forma_inesperada_da_codigo_1(self):
        self.playbook("k01.md", K01)
        for respuesta, esperado in (((500, {"error": "boom"}, {}), "ExportSolution devolvió HTTP 500"), ((200, {"ExportSolutionFile": 5}, {}), "forma inesperada")):
            with self.subTest(esperado):
                codigo, texto, _ = self.correr(respuesta)
                self.assertEqual(codigo, 1)
                self.assertIn(esperado, texto)

    def test_sin_playbooks_en_la_carpeta_configurada_lo_dice(self):
        codigo, texto, cliente = self.correr(respuesta_export(XML))
        self.assertEqual(codigo, 1)
        self.assertIn("no hay playbooks", texto)
        self.assertIn(self.carpeta, texto)
        self.assertEqual(cliente.llamadas, [])

    def test_playbooks_de_dos_soluciones_no_se_comparan(self):
        self.playbook("k01.md", K01)
        self.playbook("k02.md", K02, ident=dict(IDENT, solucion="sanic_carp_sol_otra"))
        codigo, texto, cliente = self.correr(respuesta_export(XML))
        self.assertEqual(codigo, 1)
        self.assertIn("nombran 2 soluciones", texto)
        self.assertEqual(cliente.llamadas, [])

    def test_guardar_escribe_la_clave_en_muestras_aunque_la_carpeta_no_exista(self):
        self.playbook("k02.md", K02)
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))
        codigo, texto, _ = self.correr(respuesta_export(XML), argv=["--guardar"])
        self.assertEqual(codigo, 0, texto)
        with open(os.path.join(self.carpeta, "muestras", f"{K02['nombre']}.solucion.xml"), encoding="utf-8") as f:
            contenido = f.read()
        self.assertIn(f"<LogicalName>{K02['nombre']}</LogicalName>", contenido)
        self.assertNotIn(K01["nombre"], contenido)  # solo la clave de ese playbook, no las demás de la tabla

    def test_sin_guardar_no_escribe_nada(self):
        self.playbook("k02.md", K02)
        self.correr(respuesta_export(XML))
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))


if __name__ == "__main__":
    unittest.main()
