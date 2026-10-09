"""Comparación de una tabla contra el XML de la solución exportada. Usa las muestras de `tests/muestras/tabla/`: formas de exportación derivadas
de las del proyecto anterior (ver su LEEME.txt), con una etiqueta por cada idioma 1033 y 3082; se reemplazan por la exportación real de este
proyecto en el primer ciclo. Más la muestra REAL (exportación del 2026-10-09) de la tabla de ensayo con columnas decimal y motivos de estado:
`sanic_carp_tbl_zzensayo_decimal_motivos.*` (ver su entrada en el LEEME.txt)."""
import copy
import decimal
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
# La exportación REAL de la tabla de ensayo con decimales y motivos de estado (ExportSolution, 2026-10-09, Dev) y el playbook que la creó.
DECIMAL_MOTIVOS = datos_de("sanic_carp_tbl_zzensayo_decimal_motivos.md")
XML_DECIMAL_MOTIVOS = xml_de("sanic_carp_tbl_zzensayo_decimal_motivos.solucion.xml")
RAIZ_DECIMAL_MOTIVOS = "sanic_carp_tbl_zzensayo"


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


FORMULA = {"nombre": "sanic_antiguedad", "tipo": "formula", "displayname": por_idioma("Antiguedad"), "descripcion": "x", "requerida": False,
           "protegida": False, "auditoria": False, "expresion": "1", "resultado": "entero", "depende_de": ["sanic_plazo"]}
DECIMAL = {"nombre": "sanic_monto", "tipo": "decimal", "displayname": por_idioma("Monto"), "descripcion": "x", "requerida": False, "protegida": False,
           "auditoria": False, "precision": 2, "minimo": 0, "maximo": 10}
MOTIVOS_MINIMOS = [{"statecode": 0, "valor": 1, "origen": "defecto", "etiqueta": por_idioma("Abierta"), "descripcion": ""}]


class LoQueNoSePuedeCompararTodavia(unittest.TestCase):
    """Queda sin muestra real la columna `formula` (la plataforma no deja crearla por código: la hace una persona, ver `tabla.py`). La herramienta
    no inventa su forma: la nombra como «sin comparar» y el resultado no es OK. Decimal y motivos de estado ya tienen muestra real (2026-10-09)
    y se comparan: ver `ContraLaMuestraRealDeDecimalYMotivos`."""

    def test_una_tabla_con_los_tipos_viejos_no_tiene_nada_sin_comparar(self):
        self.assertEqual([], mt.no_comparables(ENSAYO))
        self.assertEqual([], mt.no_comparables(dict(ENSAYO, motivos_estado=None)))

    def test_solo_la_formula_se_nombra(self):
        self.assertEqual(("formula",), mt.TIPOS_SIN_MUESTRA)
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"] += [DECIMAL, FORMULA]
        datos["motivos_estado"] = MOTIVOS_MINIMOS
        sin_comparar = mt.no_comparables(datos)
        self.assertEqual(1, len(sin_comparar), sin_comparar)
        self.assertIn("sanic_antiguedad", sin_comparar[0])
        self.assertIn("sin muestra real", sin_comparar[0])
        for texto in sin_comparar:
            self.assertNotIn("decimal", texto)
            self.assertNotIn("sanic_monto", texto)
            self.assertNotIn("motivos_estado", texto)

    def test_evaluar_solo_da_ok_si_no_hay_diferencias_ni_nada_sin_comparar(self):
        raiz = "sanic_carp_tbl_zzensayo"
        self.assertEqual(("OK", []), mt.evaluar(XML_ENSAYO, {raiz}, ENSAYO, IDIOMAS))
        estado, textos = mt.evaluar(XML_ENSAYO, set(), ENSAYO, IDIOMAS)
        self.assertEqual("DIFIERE", estado)
        self.assertTrue(any("RootComponent" in t for t in textos), textos)
        estado, textos = mt.evaluar(XML_ENSAYO, {raiz}, cambiar(ENSAYO, ["descripcion"], "Otra."), IDIOMAS)
        self.assertEqual("DIFIERE", estado)

    def test_una_formula_en_el_xml_da_sin_comparar_y_una_diferencia_real_manda_sobre_ella(self):
        raiz = "sanic_carp_tbl_zzensayo"
        con_formula = copy.deepcopy(ENSAYO)
        con_formula["columnas"].append(FORMULA)
        atributo = ('<attribute PhysicalName="sanic_antiguedad"><Type>int</Type><Name>sanic_antiguedad</Name><LogicalName>sanic_antiguedad</LogicalName>'
                    '<IsCustomField>1</IsCustomField></attribute>')
        xml = XML_ENSAYO.replace("<attributes>", "<attributes>" + atributo, 1)
        self.assertNotEqual(xml, XML_ENSAYO)
        estado, textos = mt.evaluar(xml, {raiz}, con_formula, IDIOMAS)
        self.assertEqual(("SIN COMPARAR", 1), (estado, len(textos)))
        estado, _ = mt.evaluar(xml, {raiz}, cambiar(con_formula, ["descripcion"], "Otra."), IDIOMAS)
        self.assertEqual("DIFIERE", estado)

    def test_sin_comparar_cuenta_como_no_coincidencia_para_el_codigo_de_salida(self):
        self.assertEqual(0, mt.no_coinciden(["OK", "OK"]))
        self.assertEqual(1, mt.no_coinciden(["OK", "SIN COMPARAR"]))
        self.assertEqual(2, mt.no_coinciden(["DIFIERE", "SIN COMPARAR", "OK"]))

    def test_una_columna_formula_presente_en_el_xml_no_se_compara_contra_una_forma_inventada(self):
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"].append(FORMULA)
        atributo = ('<attribute PhysicalName="sanic_antiguedad"><Type>decimal</Type><Name>sanic_antiguedad</Name><LogicalName>sanic_antiguedad</LogicalName>'
                    '<IsCustomField>1</IsCustomField></attribute>')
        con_la_columna = XML_ENSAYO.replace("<attributes>", "<attributes>" + atributo, 1)
        self.assertNotEqual(con_la_columna, XML_ENSAYO)
        self.assertEqual([], mt.comparar_con_xml(con_la_columna, datos, IDIOMAS))  # sin KeyError, sin diferencias inventadas
        estado, textos = mt.evaluar(con_la_columna, {"sanic_carp_tbl_zzensayo"}, datos, IDIOMAS)
        self.assertEqual("SIN COMPARAR", estado)
        self.assertIn("sanic_antiguedad", textos[0])

    def test_comparar_con_xml_no_falla_ni_da_por_buenas_las_columnas_sin_comparar(self):
        datos = copy.deepcopy(ENSAYO)
        datos["columnas"].append(FORMULA)
        difs = mt.comparar_con_xml(XML_ENSAYO, datos, IDIOMAS)
        # la columna no está en el XML de la muestra: se informa que falta, y NO se compara su tipo contra una forma inventada
        self.assertEqual(["falta la columna sanic_antiguedad"], difs)


def columna(datos, nombre):
    return next(n for n, c in enumerate(datos["columnas"]) if c["nombre"] == nombre)


def motivo_de(datos, valor):
    return next(n for n, m in enumerate(datos["motivos_estado"]) if m["valor"] == valor)


class ContraLaMuestraRealDeDecimalYMotivos(unittest.TestCase):
    """La exportación REAL (ExportSolution, 2026-10-09) de `sanic_carp_tbl_zzensayo`: tres decimales (precisión 2 con 0..999999999.99, y precisión 4
    con -1000..1000 auditada) y cinco motivos de estado (1 y 2 renombrados; 159460001..3 agregados). En el XML un decimal es `<Type>decimal</Type>`
    con `<MinValue>`, `<MaxValue>` y `<Accuracy>` (la precisión); los motivos son los `<status value state>` del atributo `statuscode`."""

    def diferencias(self, datos, xml=XML_DECIMAL_MOTIVOS):
        return mt.comparar_con_xml(xml, datos, IDIOMAS)

    def test_el_playbook_tal_cual_coincide_con_la_exportacion_real(self):
        self.assertEqual([], self.diferencias(DECIMAL_MOTIVOS))

    def test_evaluar_da_ok_porque_ya_no_queda_nada_sin_comparar(self):
        self.assertEqual([], mt.no_comparables(DECIMAL_MOTIVOS))
        self.assertEqual(("OK", []), mt.evaluar(XML_DECIMAL_MOTIVOS, {RAIZ_DECIMAL_MOTIVOS}, DECIMAL_MOTIVOS, IDIOMAS))

    def test_la_muestra_cubre_lo_que_dice_cubrir(self):
        self.assertEqual(["decimal"] * 3, [c["tipo"] for c in DECIMAL_MOTIVOS["columnas"]])
        self.assertEqual([1, 159460001, 2, 159460002, 159460003], [m["valor"] for m in DECIMAL_MOTIVOS["motivos_estado"]])
        # el monto máximo llega como Decimal, no como float: 999999999.99 exacto
        self.assertIsInstance(DECIMAL_MOTIVOS["columnas"][0]["maximo"], decimal.Decimal)

    def test_cada_diferencia_del_decimal_se_detecta_con_su_detalle(self):
        saldo, auditado = columna(DECIMAL_MOTIVOS, "sanic_saldoinicial"), columna(DECIMAL_MOTIVOS, "sanic_zzauditado")
        casos = [
            ("minimo distinto", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "minimo"], 1),
             "sanic_saldoinicial.minimo: xml='0' playbook='1'"),
            ("maximo distinto", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "maximo"], decimal.Decimal("999999999.98")),
             "sanic_saldoinicial.maximo: xml='999999999.99' playbook='999999999.98'"),
            ("maximo distinto en el limite del float", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "maximo"], decimal.Decimal("999999999.990000001")),
             "sanic_saldoinicial.maximo: xml='999999999.99' playbook='999999999.990000001'"),
            ("minimo negativo distinto", cambiar(DECIMAL_MOTIVOS, ["columnas", auditado, "minimo"], -999),
             "sanic_zzauditado.minimo: xml='-1000' playbook='-999'"),
            ("precision distinta", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "precision"], 3),
             "sanic_saldoinicial.precision: xml='2' playbook='3'"),
            ("precision distinta (la de 4)", cambiar(DECIMAL_MOTIVOS, ["columnas", auditado, "precision"], 2),
             "sanic_zzauditado.precision: xml='4' playbook='2'"),
            ("auditoria distinta (el playbook la apaga)", cambiar(DECIMAL_MOTIVOS, ["columnas", auditado, "auditoria"], False),
             "sanic_zzauditado.auditoria: xml='1' playbook='0'"),
            ("auditoria distinta (el playbook la prende)", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "auditoria"], True),
             "sanic_saldoinicial.auditoria: xml='0' playbook='1'"),
            ("requerida distinta", cambiar(DECIMAL_MOTIVOS, ["columnas", auditado, "requerida"], True),
             "sanic_zzauditado.requerida: xml='none' playbook='required'"),
            ("protegida distinta", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "protegida"], True),
             "sanic_saldoinicial.protegida: xml='0' playbook='1'"),
            ("etiqueta distinta en 3082", cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "displayname"], {"1033": "Saldo inicial", "3082": "Otro"}),
             "sanic_saldoinicial.displayname[3082]: xml='Saldo inicial' playbook='Otro'"),
        ]
        for nombre, datos, esperado in casos:
            with self.subTest(nombre):
                self.assertEqual([esperado], self.diferencias(datos))
        # la descripcion de una columna se exige igual en CADA idioma: dos diferencias, una por idioma
        datos = cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "descripcion"], "Otra.")
        real = "Saldo inicial del crédito al momento del arreglo, en la moneda del arreglo."
        self.assertEqual([f"sanic_saldoinicial.descripcion[1033]: xml={real!r} playbook='Otra.'",
                          f"sanic_saldoinicial.descripcion[3082]: xml={real!r} playbook='Otra.'"], self.diferencias(datos))

    def test_cero_y_cero_punto_cero_no_son_una_diferencia(self):
        saldo = columna(DECIMAL_MOTIVOS, "sanic_saldoinicial")
        for minimo in (0, 0.0, decimal.Decimal("0"), decimal.Decimal("0.0"), decimal.Decimal("0.00")):
            with self.subTest(minimo=repr(minimo)):
                self.assertEqual([], self.diferencias(cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "minimo"], minimo)))
        # el máximo escrito como float, como entero con ceros de más o con la misma cantidad con otros ceros, también coincide
        for maximo in (999999999.99, decimal.Decimal("999999999.99"), decimal.Decimal("999999999.9900")):
            with self.subTest(maximo=repr(maximo)):
                self.assertEqual([], self.diferencias(cambiar(DECIMAL_MOTIVOS, ["columnas", saldo, "maximo"], maximo)))
        for lim in (1000, 1000.0, decimal.Decimal("1000.0")):
            with self.subTest(limite=repr(lim)):
                auditado = columna(DECIMAL_MOTIVOS, "sanic_zzauditado")
                self.assertEqual([], self.diferencias(cambiar(DECIMAL_MOTIVOS, ["columnas", auditado, "maximo"], lim)))

    def test_un_valor_del_xml_que_no_es_un_numero_es_una_diferencia_y_no_una_excepcion(self):
        roto = XML_DECIMAL_MOTIVOS.replace("<MinValue>0</MinValue>", "<MinValue>cero</MinValue>", 1)
        self.assertNotEqual(roto, XML_DECIMAL_MOTIVOS)
        difs = self.diferencias(DECIMAL_MOTIVOS, roto)
        self.assertEqual(1, len(difs), difs)
        self.assertIn(".minimo: xml='cero' playbook='0'", difs[0])

    def test_cada_diferencia_de_los_motivos_se_detecta_con_su_detalle(self):
        vigente = motivo_de(DECIMAL_MOTIVOS, 159460001)
        sin_el_3 = copy.deepcopy(DECIMAL_MOTIVOS)
        sin_el_3["motivos_estado"].pop(motivo_de(DECIMAL_MOTIVOS, 159460003))
        sin_borrador = copy.deepcopy(DECIMAL_MOTIVOS)
        sin_borrador["motivos_estado"].pop(motivo_de(DECIMAL_MOTIVOS, 1))
        con_9 = copy.deepcopy(DECIMAL_MOTIVOS)
        con_9["motivos_estado"].append({"statecode": 0, "valor": 159460009, "origen": "agregado", "etiqueta": por_idioma("Nuevo"), "descripcion": ""})
        casos = [
            ("etiqueta distinta en 3082", cambiar(DECIMAL_MOTIVOS, ["motivos_estado", vigente, "etiqueta"], {"1033": "Vigente", "3082": "En vigor"}),
             ["motivos_estado[159460001].etiqueta[3082]: xml='Vigente' playbook='En vigor'"]),
            ("etiqueta distinta en 1033", cambiar(DECIMAL_MOTIVOS, ["motivos_estado", vigente, "etiqueta"], {"1033": "Valid", "3082": "Vigente"}),
             ["motivos_estado[159460001].etiqueta[1033]: xml='Vigente' playbook='Valid'"]),
            ("statecode distinto", cambiar(DECIMAL_MOTIVOS, ["motivos_estado", vigente, "statecode"], 1),
             ["motivos_estado[159460001].statecode: xml='0' playbook='1'"]),
            ("statecode distinto en un motivo por defecto", cambiar(DECIMAL_MOTIVOS, ["motivos_estado", 0, "statecode"], 1),
             ["motivos_estado[1].statecode: xml='0' playbook='1'"]),
            ("motivo declarado que no esta en el xml", con_9,
             ["motivos_estado[159460009]: el playbook lo declara y el statuscode del XML no lo tiene (falta)"]),
            ("motivo del xml que el playbook no declara", sin_el_3,
             ["motivos_estado[159460003]: el statuscode del XML lo tiene y el playbook no lo declara (sobra)"]),
            ("motivo por defecto del xml que el playbook no declara", sin_borrador,
             ["motivos_estado[1]: el statuscode del XML lo tiene y el playbook no lo declara (sobra)"]),
        ]
        for nombre, datos, esperado in casos:
            with self.subTest(nombre):
                self.assertEqual(esperado, self.diferencias(datos))

    def test_una_etiqueta_en_un_idioma_que_el_playbook_no_declara_es_una_diferencia(self):
        xml = XML_DECIMAL_MOTIVOS.replace('<label description="Vigente" languagecode="3082" />',
                                          '<label description="Vigente" languagecode="3082" /><label description="Vigente" languagecode="1046" />', 1)
        self.assertNotEqual(xml, XML_DECIMAL_MOTIVOS)
        difs = self.diferencias(DECIMAL_MOTIVOS, xml)
        self.assertEqual(1, len(difs), difs)
        self.assertIn("motivos_estado[159460001].etiqueta", difs[0])
        self.assertIn("1046", difs[0])
        self.assertIn("no declara", difs[0])

    def test_una_etiqueta_que_falta_en_el_xml_es_una_diferencia(self):
        xml = XML_DECIMAL_MOTIVOS.replace('<label description="Anulado" languagecode="3082" />', "", 1)
        self.assertNotEqual(xml, XML_DECIMAL_MOTIVOS)
        self.assertEqual(["motivos_estado[159460002].etiqueta[3082]: xml=None playbook='Anulado'"], self.diferencias(DECIMAL_MOTIVOS, xml))

    def test_sin_motivos_en_el_playbook_no_se_compara_el_statuscode(self):
        # `motivos_estado: null`: la tabla trae los motivos por defecto de la plataforma, y el XML los trae renombrados o no: no hay nada que comparar
        self.assertEqual([], self.diferencias(dict(DECIMAL_MOTIVOS, motivos_estado=None)))
        sin_clave = {k: v for k, v in DECIMAL_MOTIVOS.items() if k != "motivos_estado"}
        self.assertEqual([], self.diferencias(sin_clave))
        # y las tablas viejas, que nunca declararon motivos, siguen comparando limpio
        self.assertEqual([], mt.comparar_con_xml(XML_ENSAYO, ENSAYO, IDIOMAS))

    def test_el_xml_sin_el_atributo_statuscode_con_motivos_declarados_es_una_diferencia(self):
        ini = XML_DECIMAL_MOTIVOS.index('<attribute PhysicalName="statuscode">')
        fin = XML_DECIMAL_MOTIVOS.index("</attribute>", ini) + len("</attribute>")
        xml = XML_DECIMAL_MOTIVOS[:ini] + XML_DECIMAL_MOTIVOS[fin:]
        difs = self.diferencias(DECIMAL_MOTIVOS, xml)
        self.assertEqual(["motivos_estado: la tabla exportada no tiene el atributo statuscode"], difs)

    def test_la_descripcion_de_un_motivo_no_se_compara_porque_el_xml_no_la_trae(self):
        vigente = motivo_de(DECIMAL_MOTIVOS, 159460001)
        datos = cambiar(DECIMAL_MOTIVOS, ["motivos_estado", vigente, "descripcion"], "Cualquier texto que el XML no puede contradecir.")
        self.assertEqual([], self.diferencias(datos))

    def test_la_muestra_real_no_pisa_a_la_vieja_y_se_distingue_de_ella(self):
        # dos archivos distintos para la misma tabla: el viejo (forma derivada, sin decimales ni motivos) y el real nuevo
        self.assertEqual([], mt.comparar_con_xml(XML_ENSAYO, ENSAYO, IDIOMAS))
        self.assertNotEqual(XML_ENSAYO, XML_DECIMAL_MOTIVOS)
        # el playbook nuevo contra el XML viejo: le faltan las columnas decimales
        difs = mt.comparar_con_xml(XML_ENSAYO, DECIMAL_MOTIVOS, IDIOMAS)
        self.assertTrue(any("falta la columna sanic_saldoinicial" in d for d in difs), difs)


if __name__ == "__main__":
    unittest.main()
