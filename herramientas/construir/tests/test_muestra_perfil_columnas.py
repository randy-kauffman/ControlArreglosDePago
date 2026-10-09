"""Comparación de un perfil de seguridad de columna contra el XML de la solución exportada, y de dónde sale la
carpeta de playbooks. Usa la copia de `tests/muestras/perfil-columnas/` (forma de exportación derivada del
proyecto anterior, ver su LEEME.txt)."""
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

import muestra_perfil_columnas as mp  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from credenciales import ARCHIVO_CONFIGURACION, RAIZ_PROYECTO  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, playbook_md  # noqa: E402

MUESTRAS = os.path.join(_AQUI, "muestras", "perfil-columnas")
with open(os.path.join(MUESTRAS, "zzensayo.perfil.solucion.xml"), encoding="utf-8") as _f:
    XML = "<ImportExportXml>" + _f.read() + "</ImportExportXml>"
A = "sanic_carp_tbl_arreglo"
# Lo que tenía el perfil descartable del que salió la muestra: lectura sola sobre las dos columnas.
MUESTRA = {"tipo": "perfil-columnas", "nombre": "CSP - CARP - ZZ Ensayo", "descripcion": "Perfil descartable de ensayo.",
           "permisos": [{"tabla": A, "columna": "sanic_cuentadebitar", "leer": True, "crear": False, "actualizar": False},
                        {"tabla": A, "columna": "sanic_instantaneacedula", "leer": True, "crear": False, "actualizar": False}]}


def cambiar(d, ruta, valor):
    n = copy.deepcopy(d)
    nodo = n
    for p in ruta[:-1]:
        nodo = nodo[p]
    nodo[ruta[-1]] = valor
    return n


class ContraLaMuestra(unittest.TestCase):
    def test_coincide(self):
        self.assertEqual(mp.comparar_con_xml(XML, MUESTRA), [])

    def test_no_esta(self):
        difs = mp.comparar_con_xml(XML, cambiar(MUESTRA, ["nombre"], "CSP - CARP - ZZ Inexistente"))
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia(self):
        casos = [("descripcion", cambiar(MUESTRA, ["descripcion"], "Otra.")),
                 (f"{A}.sanic_cuentadebitar.CanUpdate: xml='0' playbook='4'", cambiar(MUESTRA, ["permisos", 0, "actualizar"], True)),
                 (f"{A}.sanic_cuentadebitar.CanCreate: xml='0' playbook='4'", cambiar(MUESTRA, ["permisos", 0, "crear"], True)),
                 (f"{A}.sanic_cuentadebitar.CanRead: xml='4' playbook='0'", cambiar(MUESTRA, ["permisos", 0, "leer"], False)),
                 (f"falta el permiso sobre {A}.sanic_otra", cambiar(MUESTRA, ["permisos", 1, "columna"], "sanic_otra")),
                 (f"sobra el permiso sobre {A}.sanic_instantaneacedula", cambiar(MUESTRA, ["permisos"], MUESTRA["permisos"][:1]))]
        for esperado, datos in casos:
            with self.subTest(esperado):
                difs = mp.comparar_con_xml(XML, datos)
                self.assertTrue(any(esperado in d for d in difs), difs)

    def test_el_mismo_nombre_de_columna_en_otra_tabla_no_es_el_permiso_declarado(self):
        difs = mp.comparar_con_xml(XML, cambiar(MUESTRA, ["permisos", 0, "tabla"], "sanic_carp_tbl_cliente"))
        self.assertTrue(any("falta el permiso sobre sanic_carp_tbl_cliente.sanic_cuentadebitar" in d for d in difs), difs)
        self.assertTrue(any(f"sobra el permiso sobre {A}.sanic_cuentadebitar" in d for d in difs), difs)

    def test_leer_sin_enmascarar_distinto_de_cero_es_una_diferencia(self):
        xml = XML.replace("<CanReadUnmasked>0</CanReadUnmasked>", "<CanReadUnmasked>3</CanReadUnmasked>", 1)
        self.assertNotEqual(xml, XML)
        self.assertTrue(any("CanReadUnmasked" in d for d in mp.comparar_con_xml(xml, MUESTRA)))


class CarpetaDePlaybooks(unittest.TestCase):
    """La carpeta de playbooks sale de `proyecto.json` (clave `playbooks`), no está escrita en la herramienta."""

    def test_sale_de_la_configuracion_recibida(self):
        carpeta = mp.carpeta_de_playbooks({"playbooks": "otra/ruta"}, raiz="/raiz")
        self.assertEqual(carpeta, os.path.join("/raiz", "otra/ruta", "perfil-columnas"))

    def test_sin_configuracion_explicita_lee_proyecto_json(self):
        with open(ARCHIVO_CONFIGURACION, encoding="utf-8") as f:
            esperado = json.load(f)["playbooks"]
        self.assertEqual(mp.carpeta_de_playbooks(), os.path.join(RAIZ_PROYECTO, esperado, "perfil-columnas"))

    def test_si_la_clave_falta_o_no_es_texto_dice_cual_es(self):
        for config in ({}, {"playbooks": ""}, {"playbooks": 3}, {"playbooks": None}):
            with self.subTest(repr(config)):
                with self.assertRaises(ValueError) as e:
                    mp.carpeta_de_playbooks(config, raiz="/raiz")
                self.assertIn("playbooks", str(e.exception))


def exportacion(customizations_xml):
    """Lo que devuelve `ExportSolution`: un .zip con `customizations.xml`, en base64."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("customizations.xml", customizations_xml)
    return (200, {"ExportSolutionFile": base64.b64encode(buf.getvalue()).decode("ascii")}, {})


class Principal(unittest.TestCase):
    """`main` con un cliente simulado y una raíz temporal: nunca toca el entorno ni el repo."""

    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.config = {"playbooks": os.path.join("carpeta", "de-prueba")}
        self.carpeta = os.path.join(self.dir.name, "carpeta", "de-prueba", "perfil-columnas")
        os.makedirs(self.carpeta)
        self.identidad = dict(IDENTIDAD_VALIDA, tipo_playbook="perfil-columnas", solucion="sanic_carp_sol_zzensayo")

    def poner_playbook(self, nombre="zz_ensayo.md", identidad=None):
        ruta = os.path.join(self.carpeta, nombre)
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(playbook_md(identidad or self.identidad, MUESTRA))
        return ruta

    def correr(self, argv, xml=XML):
        cliente = ClienteSimulado().responder("POST", "ExportSolution", exportacion(xml))
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mp.main(argv, fabrica_cliente=lambda: cliente, config=self.config, raiz=self.dir.name)
        return codigo, salida.getvalue(), cliente

    def test_sin_argumentos_compara_los_playbooks_de_la_carpeta_de_la_configuracion(self):
        self.poner_playbook()
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 0, texto)
        self.assertIn("OK       CSP - CARP - ZZ Ensayo", texto)
        self.assertIn('"coinciden": 1', texto)
        # Pide la exportación de la solución de la Identidad del playbook, sin modificarla.
        self.assertEqual(cliente.llamadas[0]["cuerpo"], {"SolutionName": "sanic_carp_sol_zzensayo", "Managed": False})

    def test_una_diferencia_devuelve_1_y_la_nombra(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr([], xml=XML.replace("<CanRead>4</CanRead>", "<CanRead>0</CanRead>", 1))
        self.assertEqual(codigo, 1, texto)
        self.assertIn("DIFIERE  CSP - CARP - ZZ Ensayo", texto)
        self.assertIn(".CanRead: xml='0' playbook='4'", texto)

    def test_sin_guardar_no_escribe_ninguna_muestra(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr([])
        self.assertEqual(codigo, 0, texto)
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))

    def test_guardar_escribe_la_muestra_dentro_de_la_carpeta_de_la_configuracion(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr(["--guardar"])
        self.assertEqual(codigo, 0, texto)
        guardada = os.path.join(self.carpeta, "muestras", "csp_carp_zz_ensayo.solucion.xml")
        self.assertTrue(os.path.isfile(guardada), os.listdir(self.carpeta))
        with open(guardada, encoding="utf-8") as f:
            self.assertIn('name="CSP - CARP - ZZ Ensayo"', f.read())
        self.assertFalse(os.path.exists(os.path.join(self.dir.name, "playbooks")))

    def test_sin_playbooks_en_la_carpeta_lo_dice_y_no_pide_la_exportacion(self):
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 1, texto)
        self.assertIn(self.carpeta, texto)
        self.assertEqual(cliente.llamadas, [])

    def test_con_una_ruta_explicita_no_mira_la_carpeta_de_la_configuracion(self):
        ruta = os.path.join(self.dir.name, "suelto.md")
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(playbook_md(self.identidad, MUESTRA))
        codigo, texto, _ = self.correr([ruta])
        self.assertEqual(codigo, 0, texto)
        self.assertIn('"playbooks": 1', texto)

    def test_playbooks_de_soluciones_distintas_no_se_comparan_juntos(self):
        self.poner_playbook()
        self.poner_playbook("otro.md", dict(self.identidad, solucion="sanic_carp_sol_otra"))
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 1, texto)
        self.assertIn("2 soluciones", texto)
        self.assertEqual(cliente.llamadas, [])

    def test_una_exportacion_con_forma_inesperada_devuelve_1(self):
        self.poner_playbook()
        cliente = ClienteSimulado().responder("POST", "ExportSolution", (200, {"ExportSolutionFile": 5}, {}))
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mp.main([], fabrica_cliente=lambda: cliente, config=self.config, raiz=self.dir.name)
        self.assertEqual(codigo, 1)
        self.assertIn("forma inesperada", salida.getvalue())


if __name__ == "__main__":
    unittest.main()
