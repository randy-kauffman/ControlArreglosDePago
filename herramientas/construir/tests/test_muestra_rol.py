"""Comparación de un rol contra el XML de la solución exportada, y de dónde sale la carpeta de playbooks.
Usa la copia de `tests/muestras/rol/` (forma de exportación derivada del proyecto anterior, ver su LEEME.txt)."""
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

import muestra_rol as mr  # noqa: E402
from _comun import dividir_secciones, leer_texto, obtener_componente, obtener_identidad  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from credenciales import ARCHIVO_CONFIGURACION, RAIZ_PROYECTO  # noqa: E402

MUESTRAS = os.path.join(_AQUI, "muestras", "rol")
with open(os.path.join(MUESTRAS, "zzensayo.rol.solucion.xml"), encoding="utf-8") as _f:
    XML = "<ImportExportXml>" + _f.read() + "</ImportExportXml>"
RUTA_PLAYBOOK = os.path.join(MUESTRAS, "zz_nuevo_mas.md")
_SEC = dividir_secciones(leer_texto(RUTA_PLAYBOOK))
NUEVO, IDENT = obtener_componente(_SEC, "rol"), obtener_identidad(_SEC)
C, F = "sanic_carp_tbl_cliente", "sanic_carp_tbl_cuota"


def cambiar(d, ruta, valor, borrar=False):
    n = copy.deepcopy(d)
    nodo = n
    for p in ruta[:-1]:
        nodo = nodo[p]
    if borrar:
        del nodo[ruta[-1]]
    else:
        nodo[ruta[-1]] = valor
    return n


class ContraLaMuestra(unittest.TestCase):
    def test_coincide(self):
        self.assertEqual(mr.comparar_con_xml(XML, NUEVO, IDENT), [])

    def test_no_esta(self):
        difs = mr.comparar_con_xml(XML, cambiar(NUEVO, ["nombre"], "SR - CARP - ZZ Inexistente"), IDENT)
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia(self):
        casos = [(f"falta prvDelete{C}", cambiar(NUEVO, ["tablas", C, "borrar"], "organizacion")),
                 (f"sobra prvAssign{C}", cambiar(NUEVO, ["tablas", C, "asignar"], None, borrar=True)),
                 (f"sobra prvAppend{F}", cambiar(NUEVO, ["tablas", F, "anexar"], None, borrar=True)),
                 (f"prvAppendTo{F}: xml='Local' playbook='Global'", cambiar(NUEVO, ["tablas", F, "anexar_a"], "organizacion")),
                 ("falta prvReadAccount", cambiar(NUEVO, ["otros_privilegios", "prvReadAccount"], "organizacion")),
                 ("prvBulkDelete: xml='Global' playbook='Deep'", cambiar(NUEVO, ["otros_privilegios", "prvBulkDelete"], "unidad_e_hijas")),
                 ("descripcion", cambiar(NUEVO, ["descripcion"], "Otra."))]
        for esperado, datos in casos:
            with self.subTest(esperado):
                difs = mr.comparar_con_xml(XML, datos, IDENT)
                self.assertTrue(any(esperado in d for d in difs), difs)

    def test_los_privilegios_del_rol_base_no_son_sobrantes(self):
        """El XML no sabe cuáles vienen de la base: eso lo comprueba `rol.py` contra el entorno."""
        self.assertFalse([d for d in mr.comparar_con_xml(XML, NUEVO, IDENT) if "prvReadAppModule" in d])

    def test_una_tabla_entera_de_la_solucion_que_el_playbook_no_nombra_es_sobrante(self):
        difs = mr.comparar_con_xml(XML, cambiar(NUEVO, ["tablas", F], None, borrar=True), IDENT)
        self.assertEqual(len([d for d in difs if d.startswith("sobra ") and d.split()[1].endswith(F)]), 4, difs)

    def test_lo_sobrante_se_reconoce_por_el_prefijo_y_la_abreviatura_de_la_identidad(self):
        """Con otra abreviatura, las tablas de la muestra ya no son 'de la solución': nada de lo que sobre se denuncia."""
        otra = dict(IDENT, abrev="otra")
        difs = mr.comparar_con_xml(XML, cambiar(NUEVO, ["tablas", F], None, borrar=True), otra)
        self.assertFalse([d for d in difs if d.startswith("sobra ")], difs)


class CarpetaDePlaybooks(unittest.TestCase):
    """La carpeta de playbooks sale de `proyecto.json` (clave `playbooks`), no está escrita en la herramienta."""

    def test_sale_de_la_configuracion_recibida(self):
        carpeta = mr.carpeta_de_playbooks({"playbooks": "otra/ruta"}, raiz="/raiz")
        self.assertEqual(carpeta, os.path.join("/raiz", "otra/ruta", "rol"))

    def test_sin_configuracion_explicita_lee_proyecto_json(self):
        with open(ARCHIVO_CONFIGURACION, encoding="utf-8") as f:
            esperado = json.load(f)["playbooks"]
        self.assertEqual(mr.carpeta_de_playbooks(), os.path.join(RAIZ_PROYECTO, esperado, "rol"))

    def test_si_la_clave_falta_o_no_es_texto_dice_cual_es(self):
        for config in ({}, {"playbooks": ""}, {"playbooks": 3}, {"playbooks": None}):
            with self.subTest(repr(config)):
                with self.assertRaises(ValueError) as e:
                    mr.carpeta_de_playbooks(config, raiz="/raiz")
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
        self.carpeta = os.path.join(self.dir.name, "carpeta", "de-prueba", "rol")
        os.makedirs(self.carpeta)
        with open(RUTA_PLAYBOOK, encoding="utf-8") as f:
            self.texto = f.read()

    def poner_playbook(self, nombre="zz_nuevo_mas.md"):
        ruta = os.path.join(self.carpeta, nombre)
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(self.texto)
        return ruta

    def correr(self, argv, xml=XML):
        cliente = ClienteSimulado().responder("POST", "ExportSolution", exportacion(xml))
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mr.main(argv, fabrica_cliente=lambda: cliente, config=self.config, raiz=self.dir.name)
        return codigo, salida.getvalue(), cliente

    def test_sin_argumentos_compara_los_playbooks_de_la_carpeta_de_la_configuracion(self):
        self.poner_playbook()
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 0, texto)
        self.assertIn("OK       SR - CARP - ZZ Nuevo", texto)
        self.assertIn('"coinciden": 1', texto)
        # Pide la exportación de la solución de la Identidad del playbook, sin modificarla.
        cuerpo = cliente.llamadas[0]["cuerpo"]
        self.assertEqual(cuerpo, {"SolutionName": IDENT["solucion"], "Managed": False})

    def test_una_diferencia_devuelve_1_y_la_nombra(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr([], xml=XML.replace("prvAssign" + C, "prvShare" + C))
        self.assertEqual(codigo, 1, texto)
        self.assertIn("DIFIERE  SR - CARP - ZZ Nuevo", texto)
        self.assertIn("falta prvAssign" + C, texto)

    def test_sin_guardar_no_escribe_ninguna_muestra(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr([])
        self.assertEqual(codigo, 0, texto)
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))

    def test_guardar_escribe_la_muestra_dentro_de_la_carpeta_de_la_configuracion(self):
        self.poner_playbook()
        codigo, texto, _ = self.correr(["--guardar"])
        self.assertEqual(codigo, 0, texto)
        guardada = os.path.join(self.carpeta, "muestras", "sr_carp_zz_nuevo.solucion.xml")
        self.assertTrue(os.path.isfile(guardada), os.listdir(self.carpeta))
        with open(guardada, encoding="utf-8") as f:
            self.assertIn('name="SR - CARP - ZZ Nuevo"', f.read())
        self.assertFalse(os.path.exists(os.path.join(self.dir.name, "playbooks")))

    def test_sin_playbooks_en_la_carpeta_lo_dice_y_no_pide_la_exportacion(self):
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 1, texto)
        self.assertIn(self.carpeta, texto)
        self.assertEqual(cliente.llamadas, [])

    def test_con_una_ruta_explicita_no_mira_la_carpeta_de_la_configuracion(self):
        ruta = os.path.join(self.dir.name, "suelto.md")
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(self.texto)
        codigo, texto, _ = self.correr([ruta])
        self.assertEqual(codigo, 0, texto)
        self.assertIn('"playbooks": 1', texto)

    def test_playbooks_de_soluciones_distintas_no_se_comparan_juntos(self):
        self.poner_playbook()
        otro = os.path.join(self.carpeta, "otro.md")
        with open(otro, "w", encoding="utf-8") as f:
            f.write(self.texto.replace(IDENT["solucion"], "sanic_carp_sol_otra"))
        codigo, texto, cliente = self.correr([])
        self.assertEqual(codigo, 1, texto)
        self.assertIn("2 soluciones", texto)
        self.assertEqual(cliente.llamadas, [])

    def test_una_exportacion_con_forma_inesperada_devuelve_1(self):
        self.poner_playbook()
        cliente = ClienteSimulado().responder("POST", "ExportSolution", (200, {"ExportSolutionFile": 5}, {}))
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mr.main([], fabrica_cliente=lambda: cliente, config=self.config, raiz=self.dir.name)
        self.assertEqual(codigo, 1)
        self.assertIn("forma inesperada", salida.getvalue())


if __name__ == "__main__":
    unittest.main()
