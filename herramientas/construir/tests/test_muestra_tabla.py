"""Comparación de una tabla contra el XML de la solución exportada. Usa las muestras de `tests/muestras/tabla/`: formas de exportación derivadas
de las del proyecto anterior (ver su LEEME.txt), con una etiqueta por cada idioma 1033 y 3082; se reemplazan por la exportación real de este
proyecto en el primer ciclo."""
import copy
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
MUESTRAS = os.path.join(_AQUI, "muestras", "tabla")
IDIOMAS = [1033, 3082]
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import muestra_tabla as mt  # noqa: E402
from _comun import dividir_secciones, leer_texto, obtener_componente  # noqa: E402
from fixtures import por_idioma  # noqa: E402


def datos_de(nombre):
    return obtener_componente(dividir_secciones(leer_texto(os.path.join(MUESTRAS, nombre))), "tabla")


def xml_de(nombre):
    with open(os.path.join(MUESTRAS, nombre), encoding="utf-8") as f:
        return "<ImportExportXml><Entities>" + f.read() + "</Entities></ImportExportXml>"


CLIENTE = datos_de("sanic_carp_tbl_cliente.md")
ENSAYO = datos_de("sanic_carp_tbl_zzensayo.md")
XML_CLIENTE = xml_de("sanic_carp_tbl_cliente.solucion.xml")
XML_ENSAYO = xml_de("sanic_carp_tbl_zzensayo.solucion.xml")


def cambiar(datos, ruta, valor):
    d = copy.deepcopy(datos)
    nodo = d
    for p in ruta[:-1]:
        nodo = nodo[p]
    nodo[ruta[-1]] = valor
    return d


class ContraMuestrasReales(unittest.TestCase):
    def test_las_dos_muestras_reales_coinciden_con_su_playbook(self):
        self.assertEqual(mt.comparar_con_xml(XML_CLIENTE, CLIENTE, IDIOMAS), [])
        self.assertEqual(mt.comparar_con_xml(XML_ENSAYO, ENSAYO, IDIOMAS), [])

    def test_la_tabla_no_esta(self):
        difs = mt.comparar_con_xml(XML_CLIENTE, ENSAYO, IDIOMAS)
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia(self):
        i = {c["nombre"]: n for n, c in enumerate(ENSAYO["columnas"])}
        casos = [
            ("displayname_plural[1033]", cambiar(ENSAYO, ["displayname_plural"], por_idioma("Otros"))),
            ("descripcion", cambiar(ENSAYO, ["descripcion"], "Otra.")),
            ("auditoria de la tabla", cambiar(ENSAYO, ["auditoria"], False)),
            ("propiedad", cambiar(ENSAYO, ["propiedad"], "organizacion")),
            ("sanic_nombre.largo", cambiar(ENSAYO, ["primaria", "largo"], 50)),
            ("sanic_nombre.requerida", cambiar(ENSAYO, ["primaria", "requerida"], False)),
            ("sanic_nombre.autonumerico", cambiar(ENSAYO, ["primaria", "autonumerico"], "")),
            ("sanic_numeroproducto.largo", cambiar(ENSAYO, ["columnas", i["sanic_numeroproducto"], "largo"], 10)),
            ("sanic_numeroproducto.requerida", cambiar(ENSAYO, ["columnas", i["sanic_numeroproducto"], "requerida"], False)),
            ("sanic_numeroproducto.displayname[3082]", cambiar(ENSAYO, ["columnas", i["sanic_numeroproducto"], "displayname"], {"1033": "Numero", "3082": "Otro"})),
            ("sanic_cuentadebitar.protegida", cambiar(ENSAYO, ["columnas", i["sanic_cuentadebitar"], "protegida"], False)),
            ("sanic_archivo.auditoria", cambiar(ENSAYO, ["columnas", i["sanic_archivo"], "auditoria"], True)),
            ("sanic_archivo.tamano_kb", cambiar(ENSAYO, ["columnas", i["sanic_archivo"], "tamano_kb"], 5)),
            ("sanic_plazo.maximo", cambiar(ENSAYO, ["columnas", i["sanic_plazo"], "maximo"], 5)),
            ("sanic_moneda.choice", cambiar(ENSAYO, ["columnas", i["sanic_moneda"], "choice"], "sanic_carp_ch_tipoarreglo")),
            ("sanic_correonuevoenfinalizada.defecto", cambiar(ENSAYO, ["columnas", i["sanic_correonuevoenfinalizada"], "defecto"], True)),
            ("sanic_fechainicio.tipo", cambiar(ENSAYO, ["columnas", i["sanic_fechainicio"], "tipo"], "fechahora")),
            ("sanic_folio.formato", cambiar(ENSAYO, ["columnas", i["sanic_folio"], "formato"], "X-{SEQNUM:3}")),
            ("sanic_comentarios.tipo", cambiar(cambiar(ENSAYO, ["columnas", i["sanic_comentarios"], "tipo"], "texto"), ["columnas", i["sanic_comentarios"], "largo"], 2000)),
        ]
        for esperado, datos in casos:
            with self.subTest(esperado):
                difs = mt.comparar_con_xml(XML_ENSAYO, datos, IDIOMAS)
                self.assertTrue(any(esperado in d for d in difs), difs)

    def test_la_etiqueta_de_un_idioma_que_falta_o_sobra_en_el_xml_difiere_en_los_dos_sentidos(self):
        # falta la gemela 3082 del nombre de la tabla
        sin_3082 = XML_ENSAYO.replace('<LocalizedName description="Ensayo" languagecode="3082" />', "", 1)
        self.assertNotEqual(sin_3082, XML_ENSAYO)
        difs = mt.comparar_con_xml(sin_3082, ENSAYO, IDIOMAS)
        self.assertTrue(any("displayname[3082]" in d for d in difs), difs)
        # sobra una etiqueta en un idioma que el playbook no declara
        con_1046 = XML_ENSAYO.replace('<LocalizedName description="Ensayo" languagecode="3082" />',
                                      '<LocalizedName description="Ensayo" languagecode="3082" /><LocalizedName description="Ensaio" languagecode="1046" />', 1)
        difs = mt.comparar_con_xml(con_1046, ENSAYO, IDIOMAS)
        self.assertTrue(any("1046" in d and "no declara" in d for d in difs), difs)
        # y la descripcion se exige igual en cada idioma
        otra = XML_ENSAYO.replace('languagecode="3082" />', 'languagecode="3082" />', 1)
        self.assertEqual([], mt.comparar_con_xml(otra, ENSAYO, IDIOMAS))

    def test_columna_que_falta_y_columna_de_mas(self):
        falta = copy.deepcopy(CLIENTE)
        falta["columnas"].append(dict(CLIENTE["columnas"][0], nombre="sanic_inexistente"))
        self.assertTrue(any("falta la columna sanic_inexistente" in d for d in mt.comparar_con_xml(XML_CLIENTE, falta, IDIOMAS)))
        sobra = copy.deepcopy(CLIENTE)
        sobra["columnas"].pop()
        self.assertTrue(any("sanic_celular" in d and "no declara" in d for d in mt.comparar_con_xml(XML_CLIENTE, sobra, IDIOMAS)))


class LoQueNoSePuedeCompararTodavia(unittest.TestCase):
    """La skill 0.2.0 agregó tipos de columna (decimal, fórmula) y los motivos de estado sin muestra real de cómo se ven en el XML exportado: la
    herramienta no inventa esa forma. Los nombra como «sin comparar» y el resultado no es OK."""

    def test_una_tabla_con_los_tipos_viejos_no_tiene_nada_sin_comparar(self):
        self.assertEqual([], mt.no_comparables(ENSAYO))
        self.assertEqual([], mt.no_comparables(dict(ENSAYO, motivos_estado=None)))

    def test_decimal_formula_y_motivos_se_nombran(self):
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"] += [
            {"nombre": "sanic_monto", "tipo": "decimal", "displayname": por_idioma("Monto"), "descripcion": "x", "requerida": False, "protegida": False,
             "auditoria": False, "precision": 2, "minimo": 0, "maximo": 10},
            {"nombre": "sanic_antiguedad", "tipo": "formula", "displayname": por_idioma("Antiguedad"), "descripcion": "x", "requerida": False, "protegida": False,
             "auditoria": False, "expresion": "1", "resultado": "entero", "depende_de": ["sanic_plazo"]},
        ]
        datos["motivos_estado"] = [{"statecode": 0, "valor": 1, "origen": "defecto", "etiqueta": por_idioma("Abierta"), "descripcion": ""}]
        sin_comparar = mt.no_comparables(datos)
        self.assertEqual(3, len(sin_comparar), sin_comparar)
        for pieza, texto in (("sanic_monto", sin_comparar[0]), ("sanic_antiguedad", sin_comparar[1]), ("motivos_estado", sin_comparar[2])):
            self.assertIn(pieza, texto)
        self.assertTrue(all("sin muestra real" in t for t in sin_comparar))

    def test_evaluar_solo_da_ok_si_no_hay_diferencias_ni_nada_sin_comparar(self):
        raiz = "sanic_carp_tbl_zzensayo"
        self.assertEqual(("OK", []), mt.evaluar(XML_ENSAYO, {raiz}, ENSAYO, IDIOMAS))
        estado, textos = mt.evaluar(XML_ENSAYO, set(), ENSAYO, IDIOMAS)
        self.assertEqual("DIFIERE", estado)
        self.assertTrue(any("RootComponent" in t for t in textos), textos)
        estado, textos = mt.evaluar(XML_ENSAYO, {raiz}, cambiar(ENSAYO, ["descripcion"], "Otra."), IDIOMAS)
        self.assertEqual("DIFIERE", estado)
        con_motivos = dict(ENSAYO, motivos_estado=[{"statecode": 0, "valor": 1, "origen": "defecto", "etiqueta": por_idioma("Abierta"), "descripcion": ""}])
        estado, textos = mt.evaluar(XML_ENSAYO, {raiz}, con_motivos, IDIOMAS)
        self.assertEqual(("SIN COMPARAR", 1), (estado, len(textos)))
        # una diferencia real manda sobre lo que no se pudo comparar
        estado, _ = mt.evaluar(XML_ENSAYO, {raiz}, cambiar(con_motivos, ["descripcion"], "Otra."), IDIOMAS)
        self.assertEqual("DIFIERE", estado)

    def test_sin_comparar_cuenta_como_no_coincidencia_para_el_codigo_de_salida(self):
        self.assertEqual(0, mt.no_coinciden(["OK", "OK"]))
        self.assertEqual(1, mt.no_coinciden(["OK", "SIN COMPARAR"]))
        self.assertEqual(2, mt.no_coinciden(["DIFIERE", "SIN COMPARAR", "OK"]))

    def test_una_columna_decimal_o_formula_presente_en_el_xml_no_se_compara_contra_una_forma_inventada(self):
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"].append({"nombre": "sanic_monto", "tipo": "decimal", "displayname": por_idioma("Monto"), "descripcion": "x", "requerida": False,
                                  "protegida": False, "auditoria": False, "precision": 2, "minimo": 0, "maximo": 10})
        atributo = ('<attribute PhysicalName="sanic_monto"><Type>decimal</Type><Name>sanic_monto</Name><LogicalName>sanic_monto</LogicalName>'
                    '<IsCustomField>1</IsCustomField></attribute>')
        con_la_columna = XML_ENSAYO.replace("<attributes>", "<attributes>" + atributo, 1)
        self.assertNotEqual(con_la_columna, XML_ENSAYO)
        self.assertEqual([], mt.comparar_con_xml(con_la_columna, datos, IDIOMAS))  # sin KeyError, sin diferencias inventadas
        estado, textos = mt.evaluar(con_la_columna, {"sanic_carp_tbl_zzensayo"}, datos, IDIOMAS)
        self.assertEqual("SIN COMPARAR", estado)
        self.assertIn("sanic_monto", textos[0])

    def test_comparar_con_xml_no_falla_ni_da_por_buenas_las_columnas_sin_comparar(self):
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"].append({"nombre": "sanic_monto", "tipo": "decimal", "displayname": por_idioma("Monto"), "descripcion": "x", "requerida": False,
                                  "protegida": False, "auditoria": False, "precision": 2, "minimo": 0, "maximo": 10})
        difs = mt.comparar_con_xml(XML_ENSAYO, datos, IDIOMAS)
        # la columna no está en el XML de la muestra: se informa que falta, y NO se compara su tipo contra una forma inventada
        self.assertEqual(["falta la columna sanic_monto"], difs)


if __name__ == "__main__":
    unittest.main()
