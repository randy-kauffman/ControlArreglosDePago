"""Comparación de un choice contra el XML de la solución exportada: la
comprobación independiente del ciclo (otro camino que el Web API).

Cada nombre visible y cada descripción se compara en CADA idioma de
`idiomas`, en los dos sentidos. Las muestras de `tests/muestras/choice-global/`
son formas derivadas del proyecto anterior, no exportaciones reales de CARP."""
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
from xml.sax.saxutils import quoteattr

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, os.path.dirname(os.path.dirname(_AQUI)))
sys.path.insert(0, _AQUI)

import credenciales  # noqa: E402
import muestra_choice as mc  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from fixtures import COMPONENTE_VALIDO, IDENTIDAD_VALIDA, componente, escribir_playbook, por_idioma  # noqa: E402

IDIOMAS = [1033, 3082]
MUESTRAS = os.path.join(_AQUI, "muestras", "choice-global")

# Tipo de arreglo (diccionario, 3.1) con un texto distinto en cada idioma y una opción sin descripción.
DATOS = componente(
    nombre="sanic_carp_ch_tipoarreglo",
    displayname={"1033": "CH - CARP - Arrangement type", "3082": "CH - CARP - Tipo de arreglo"},
    descripcion="Tipo de arreglo según el formato F-NIC-0000052.",
    opciones=[
        {"valor": 159460001, "etiqueta": {"1033": "Temporary", "3082": "Temporal"}, "descripcion": "Arreglo temporal"},
        {"valor": 159460002, "etiqueta": {"1033": "Cancellation", "3082": "Cancelacion"}, "descripcion": ""},
    ],
)


def modelo(datos):
    """El XML que la plataforma exportaría para `datos`, como estructura editable: {lcid: texto} por cada campo."""
    return {
        "nombre": datos["nombre"], "is_global": "1", "tipo": "picklist", "localized": datos["displayname"]["1033"],
        "display": {int(k): v for k, v in datos["displayname"].items()},
        "desc": {int(k): datos["descripcion"] for k in datos["displayname"]},
        "opciones": [{"valor": str(op["valor"]), "etiqueta": {int(k): v for k, v in op["etiqueta"].items()},
                      "desc": ({int(k): op["descripcion"] for k in op["etiqueta"]} if op["descripcion"] else None)} for op in datos["opciones"]],
    }


def render(m):
    def bloque(contenedor, hijo, textos):
        return f"<{contenedor}>" + "".join(f"<{hijo} description={quoteattr(t)} languagecode=\"{c}\" />" for c, t in textos.items()) + f"</{contenedor}>"

    opciones = "".join(
        f"<option value=\"{o['valor']}\" ExternalValue=\"\" IsHidden=\"0\">{bloque('labels', 'label', o['etiqueta'])}"
        + (bloque("Descriptions", "Description", o["desc"]) if o["desc"] is not None else "") + "</option>"
        for o in m["opciones"]
    )
    return (
        f"<ImportExportXml><optionsets><optionset Name=\"{m['nombre']}\" localizedName={quoteattr(m['localized'])} description=\"x\">"
        f"<OptionSetType>{m['tipo']}</OptionSetType><IsGlobal>{m['is_global']}</IsGlobal>"
        f"{bloque('displaynames', 'displayname', m['display'])}{bloque('Descriptions', 'Description', m['desc'])}"
        f"<options>{opciones}</options></optionset></optionsets></ImportExportXml>"
    )


class CompararContraXml(unittest.TestCase):
    def comparar(self, m, datos=DATOS):
        return mc.comparar_con_xml(render(m), datos, IDIOMAS)

    def test_coincide(self):
        self.assertEqual(self.comparar(modelo(DATOS)), [])

    def test_una_descripcion_de_opcion_vacia_puede_faltar_en_el_xml_o_venir_vacia(self):
        m = modelo(DATOS)
        self.assertIsNone(m["opciones"][1]["desc"])
        self.assertEqual(self.comparar(m), [])
        m["opciones"][1]["desc"] = {1033: "", 3082: ""}
        self.assertEqual(self.comparar(m), [])

    def test_el_orden_de_los_idiomas_dentro_del_xml_no_importa(self):
        m = modelo(DATOS)
        m["display"] = dict(reversed(list(m["display"].items())))
        m["opciones"][0]["etiqueta"] = dict(reversed(list(m["opciones"][0]["etiqueta"].items())))
        self.assertEqual(self.comparar(m), [])

    def test_no_esta_en_la_solucion(self):
        difs = mc.comparar_con_xml("<ImportExportXml><optionsets /></ImportExportXml>", DATOS, IDIOMAS)
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia_en_cada_idioma(self):
        def en(campo, lcid, texto):
            def mutar(m):
                campo(m)[lcid] = texto
            return mutar

        casos = [
            ("displayname[1033]", en(lambda m: m["display"], 1033, "Otro")),
            ("displayname[3082]", en(lambda m: m["display"], 3082, "Otro")),
            ("descripcion[1033]", en(lambda m: m["desc"], 1033, "Otra")),
            ("descripcion[3082]", en(lambda m: m["desc"], 3082, "Otra")),
            ("opciones[0].etiqueta[1033]", en(lambda m: m["opciones"][0]["etiqueta"], 1033, "OTRO")),
            ("opciones[1].etiqueta[3082]", en(lambda m: m["opciones"][1]["etiqueta"], 3082, "OTRO")),
            ("opciones[0].descripcion[1033]", en(lambda m: m["opciones"][0]["desc"], 1033, "Otra")),
            ("opciones[0].descripcion[3082]", en(lambda m: m["opciones"][0]["desc"], 3082, "Otra")),
            ("opciones[1].descripcion[3082]", lambda m: m["opciones"][1].update(desc={1033: "", 3082: "algo"})),
        ]
        for esperado, mutar in casos:
            with self.subTest(esperado):
                m = modelo(DATOS)
                mutar(m)
                difs = self.comparar(m)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith(esperado + ": xml="), difs)

    def test_un_solo_idioma_en_el_xml_es_una_diferencia_del_idioma_que_falta(self):
        quitar = {
            "displayname[3082]": lambda m: m["display"].pop(3082),
            "descripcion[1033]": lambda m: m["desc"].pop(1033),
            "opciones[0].etiqueta[3082]": lambda m: m["opciones"][0]["etiqueta"].pop(3082),
            "opciones[0].descripcion[1033]": lambda m: m["opciones"][0]["desc"].pop(1033),
        }
        for esperado, mutar in quitar.items():
            with self.subTest(esperado):
                m = modelo(DATOS)
                mutar(m)
                difs = self.comparar(m)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith(esperado + ": xml=None"), difs)

    def test_una_etiqueta_en_un_idioma_que_el_playbook_no_declara_es_una_diferencia(self):
        casos = {
            "displayname": lambda m: m["display"].update({2058: "x"}),
            "descripcion": lambda m: m["desc"].update({2058: "x"}),
            "opciones[0].etiqueta": lambda m: m["opciones"][0]["etiqueta"].update({2058: "x"}),
            "opciones[0].descripcion": lambda m: m["opciones"][0]["desc"].update({2058: "x"}),
        }
        for esperado, mutar in casos.items():
            with self.subTest(esperado):
                m = modelo(DATOS)
                mutar(m)
                difs = self.comparar(m)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith(esperado + ": tiene etiquetas en un idioma que el playbook no declara"), difs)
                self.assertIn("2058", difs[0])

    def test_detecta_el_resto_de_las_diferencias(self):
        casos = {
            "IsGlobal": lambda m: m.update(is_global="0"),
            "OptionSetType": lambda m: m.update(tipo="boolean"),
            "opciones[0].valor": lambda m: m["opciones"][0].update(valor="159460009"),
            "cantidad de opciones": lambda m: m.update(opciones=m["opciones"][:1]),
            "opciones[0].valor ": lambda m: m.update(opciones=list(reversed(m["opciones"]))),  # el orden es parte de lo que se verifica
            "atributo localizedName": lambda m: m.update(localized="CH - CARP - Otra cosa"),
        }
        for esperado, mutar in casos.items():
            with self.subTest(esperado):
                m = modelo(DATOS)
                mutar(m)
                difs = self.comparar(m)
                self.assertTrue(any(d.startswith(esperado.strip()) for d in difs), difs)

    def test_el_atributo_localizedname_acepta_el_displayname_de_cualquier_idioma_declarado(self):
        """La plataforma lo copia del idioma base del entorno de origen (1033 o 3082), que la herramienta no conoce."""
        for lcid in IDIOMAS:
            with self.subTest(lcid):
                m = modelo(DATOS)
                m["localized"] = m["display"][lcid]
                self.assertEqual(self.comparar(m), [])


class ContraLaMuestra(unittest.TestCase):
    """Las muestras del directorio `tests/muestras/choice-global/` (forma de exportación, dos idiomas)."""

    @staticmethod
    def xml(nombre_archivo):
        with open(os.path.join(MUESTRAS, nombre_archivo), encoding="utf-8") as f:
            return f"<ImportExportXml><optionsets>{f.read()}</optionsets></ImportExportXml>"

    RESULTADO = componente(
        nombre="sanic_carp_ch_resultadoconversacion",
        displayname=por_idioma("CH - CARP - Resultado de conversacion"),
        descripcion="Resultado con el que el oficial finaliza una conversación. Lo usa la columna sanic_resultado de Conversación.",
        opciones=[
            {"valor": 159460001, "etiqueta": por_idioma("Con arreglo de pago"), "descripcion": "La conversación terminó en un arreglo de pago"},
            {"valor": 159460002, "etiqueta": por_idioma("Sin arreglo de pago"), "descripcion": ""},
            {"valor": 159460003, "etiqueta": por_idioma("Sin respuesta"), "descripcion": "No hubo conversación real: exige motivo y detalle"},
        ],
    )

    def test_las_muestras_coinciden_con_su_playbook(self):
        for archivo, datos in (("sanic_carp_ch_moneda.solucion.xml", COMPONENTE_VALIDO), ("sanic_carp_ch_resultadoconversacion.solucion.xml", self.RESULTADO)):
            with self.subTest(archivo):
                self.assertEqual(mc.comparar_con_xml(self.xml(archivo), datos, IDIOMAS), [])

    def test_la_descripcion_vacia_de_la_muestra_es_un_elemento_con_texto_vacio(self):
        """Visto en la muestra real del proyecto anterior: la plataforma no omite la descripción vacía de una opción."""
        xml = self.xml("sanic_carp_ch_resultadoconversacion.solucion.xml")
        self.assertIn('<Description description="" languagecode="3082" />', xml)

    def test_la_muestra_con_un_texto_cambiado_en_un_idioma_difiere(self):
        xml = self.xml("sanic_carp_ch_moneda.solucion.xml")
        cambiado = xml.replace('<displayname description="CH - CARP - Moneda" languagecode="3082" />', '<displayname description="CH - CARP - Currency" languagecode="3082" />')
        self.assertNotEqual(cambiado, xml)
        difs = mc.comparar_con_xml(cambiado, COMPONENTE_VALIDO, IDIOMAS)
        self.assertEqual(len(difs), 1, difs)
        self.assertTrue(difs[0].startswith("displayname[3082]: xml='CH - CARP - Currency'"), difs)

    def test_la_muestra_con_un_idioma_menos_o_de_mas_difiere(self):
        xml = self.xml("sanic_carp_ch_moneda.solucion.xml")
        sin_3082 = xml.replace('<label description="Dolares" languagecode="3082" />', "")
        de_mas = xml.replace('<label description="Dolares" languagecode="3082" />', '<label description="Dolares" languagecode="3082" /><label description="Dollars" languagecode="2058" />')
        for nombre, x, esperado in (("menos", sin_3082, "opciones[1].etiqueta[3082]: xml=None"), ("de más", de_mas, "opciones[1].etiqueta: tiene etiquetas en un idioma que el playbook no declara")):
            with self.subTest(nombre):
                self.assertNotEqual(x, xml)
                difs = mc.comparar_con_xml(x, COMPONENTE_VALIDO, IDIOMAS)
                self.assertEqual(len(difs), 1, difs)
                self.assertTrue(difs[0].startswith(esperado), difs)

    def test_con_un_solo_idioma_declarado_los_dos_del_xml_sobran(self):
        xml = self.xml("sanic_carp_ch_moneda.solucion.xml")
        datos = componente(displayname={"1033": "CH - CARP - Moneda"}, opciones=[
            {"valor": 159460001, "etiqueta": {"1033": "Cordobas"}, "descripcion": "Córdobas nicaragüenses"},
            {"valor": 159460002, "etiqueta": {"1033": "Dolares"}, "descripcion": "Dólares estadounidenses"}])
        difs = mc.comparar_con_xml(xml, datos, [1033])
        self.assertTrue(all("no declara" in d and "3082" in d for d in difs), difs)
        self.assertEqual(len(difs), 6, difs)  # displayname, descripcion y 2 × (etiqueta + descripcion)


class CarpetaDePlaybooks(unittest.TestCase):
    def test_sale_de_la_clave_playbooks_de_la_configuracion(self):
        self.assertEqual(mc.carpeta_playbooks("choice-global", {"playbooks": "otra/carpeta"}), os.path.join(credenciales.RAIZ_PROYECTO, "otra", "carpeta", "choice-global"))

    def test_una_ruta_absoluta_se_respeta(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(mc.carpeta_playbooks("choice-global", {"playbooks": d}), os.path.join(d, "choice-global"))

    def test_sin_configuracion_usa_proyecto_json(self):
        esperado = os.path.join(credenciales.RAIZ_PROYECTO, credenciales.cargar_configuracion()["playbooks"], "choice-global")
        self.assertEqual(mc.carpeta_playbooks("choice-global"), esperado)


def zip_solucion(modelos, raices=None):
    """Respuesta de ExportSolution: un zip con `customizations.xml` (los optionsets de `modelos`) y `solution.xml`."""
    custom = "<ImportExportXml><optionsets>" + "".join(render(m).split("<optionsets>")[1].split("</optionsets>")[0] for m in modelos) + "</optionsets></ImportExportXml>"
    raices = [m["nombre"] for m in modelos] if raices is None else raices
    solucion = "<ImportExportXml><SolutionManifest><RootComponents>" + "".join(f'<RootComponent type="9" schemaName="{r}" behavior="0" />' for r in raices) + "</RootComponents></SolutionManifest></ImportExportXml>"
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("customizations.xml", custom)
        z.writestr("solution.xml", solucion)
    return base64.b64encode(buf.getvalue()).decode("ascii")


class Principal(unittest.TestCase):
    """`main()` de punta a punta, con un cliente simulado y una carpeta temporal de playbooks."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.config = {"playbooks": self.tmp.name}
        self.carpeta = os.path.join(self.tmp.name, "choice-global")
        os.makedirs(self.carpeta)

    def playbook(self, nombre_archivo, datos, ident=None):
        escribir_playbook(os.path.join(self.carpeta, nombre_archivo), ident, datos)

    def correr(self, respuesta, argv=()):
        cliente = ClienteSimulado()
        cliente.responder("POST", "ExportSolution", respuesta)
        salida = io.StringIO()
        with contextlib.redirect_stdout(salida):
            codigo = mc.main(list(argv), lambda: cliente, self.config)
        return codigo, salida.getvalue(), cliente

    def test_todo_coincide(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        codigo, texto, cliente = self.correr((200, {"ExportSolutionFile": zip_solucion([modelo(COMPONENTE_VALIDO)])}, {}))
        self.assertEqual(codigo, 0, texto)
        self.assertIn("OK       sanic_carp_ch_moneda", texto)
        self.assertEqual(json.loads(texto.strip().splitlines()[-1]), {"playbooks": 1, "coinciden": 1, "difieren": 0, "sin_playbook": 0})
        llamada = cliente.llamadas[0]
        self.assertEqual((llamada["metodo"], llamada["ruta"], llamada["cuerpo"]), ("POST", "ExportSolution", {"SolutionName": IDENTIDAD_VALIDA["solucion"], "Managed": False}))

    def test_un_idioma_que_difiere_se_informa_y_da_codigo_1(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        m = modelo(COMPONENTE_VALIDO)
        m["display"][3082] = "CH - CARP - Currency"
        codigo, texto, _ = self.correr((200, {"ExportSolutionFile": zip_solucion([m])}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("DIFIERE  sanic_carp_ch_moneda: displayname[3082]", texto)

    def test_si_no_figura_como_componente_raiz_difiere(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        codigo, texto, _ = self.correr((200, {"ExportSolutionFile": zip_solucion([modelo(COMPONENTE_VALIDO)], raices=[])}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("no figura como RootComponent type=9", texto)

    def test_un_choice_de_la_solucion_sin_playbook_se_avisa(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        codigo, texto, _ = self.correr((200, {"ExportSolutionFile": zip_solucion([modelo(COMPONENTE_VALIDO), modelo(DATOS)])}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("AVISO: choices en la solución sin playbook: ['sanic_carp_ch_tipoarreglo']", texto)

    def test_exportsolution_con_http_inesperado_da_codigo_1(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        codigo, texto, _ = self.correr((500, {"error": "boom"}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("ExportSolution devolvió HTTP 500", texto)

    def test_exportsolution_200_con_forma_inesperada_da_codigo_1(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        codigo, texto, _ = self.correr((200, {"ExportSolutionFile": 5}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("forma inesperada", texto)

    def test_sin_playbooks_en_la_carpeta_configurada_lo_dice(self):
        codigo, texto, cliente = self.correr((200, {"ExportSolutionFile": ""}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("no hay playbooks", texto)
        self.assertIn(self.carpeta, texto)
        self.assertEqual(cliente.llamadas, [])

    def test_playbooks_de_dos_soluciones_no_se_comparan(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        self.playbook("tipo.md", DATOS, ident=dict(IDENTIDAD_VALIDA, solucion="sanic_carp_sol_otra"))
        codigo, texto, cliente = self.correr((200, {"ExportSolutionFile": ""}, {}))
        self.assertEqual(codigo, 1)
        self.assertIn("más de una solución", texto)
        self.assertEqual(cliente.llamadas, [])

    def test_guardar_escribe_el_fragmento_en_muestras_aunque_la_carpeta_no_exista(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))
        codigo, texto, _ = self.correr((200, {"ExportSolutionFile": zip_solucion([modelo(COMPONENTE_VALIDO)])}, {}), argv=["--guardar"])
        self.assertEqual(codigo, 0, texto)
        destino = os.path.join(self.carpeta, "muestras", "sanic_carp_ch_moneda.solucion.xml")
        with open(destino, encoding="utf-8") as f:
            self.assertIn('Name="sanic_carp_ch_moneda"', f.read())

    def test_sin_guardar_no_escribe_nada(self):
        self.playbook("moneda.md", COMPONENTE_VALIDO)
        self.correr((200, {"ExportSolutionFile": zip_solucion([modelo(COMPONENTE_VALIDO)])}, {}))
        self.assertFalse(os.path.exists(os.path.join(self.carpeta, "muestras")))


if __name__ == "__main__":
    unittest.main()
