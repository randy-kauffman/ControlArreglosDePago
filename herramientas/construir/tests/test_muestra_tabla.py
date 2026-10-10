"""Comparación de una tabla contra el XML de la solución exportada. Usa las muestras de `tests/muestras/tabla/`: formas de exportación derivadas
de las del proyecto anterior (ver su LEEME.txt), con una etiqueta por cada idioma 1033 y 3082; se reemplazan por la exportación real de este
proyecto en el primer ciclo. Más dos muestras REALES (exportaciones del 2026-10-09) de la tabla de ensayo: con columnas decimal y motivos de estado
(`sanic_carp_tbl_zzensayo_decimal_motivos.*`) y con una columna de fórmula (`sanic_carp_tbl_zzensayo_formula.*`; ver sus entradas en el LEEME.txt)."""
import copy
import decimal
import io
import os
import sys
import tempfile
import unittest
import zipfile

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


def texto_de(nombre):
    with open(os.path.join(MUESTRAS, nombre), encoding="utf-8-sig") as f:
        return f.read()


# La exportación REAL (ExportSolution, 2026-10-09, Dev) de la misma tabla con una columna de fórmula creada a mano en el editor de Power Apps: el XML de la
# tabla, el `solution.xml`, el yaml `Formulas/<tabla>-FormulaDefinitions.yaml` y el playbook de ensayo que la describe. La columna se renombró en la copia
# (`sanic_antiguedadq` -> `sanic_antiguedad`) para coincidir con el playbook; ver el LEEME.txt.
FORMULA_MD = datos_de("sanic_carp_tbl_zzensayo_formula.md")
XML_FORMULA = xml_de("sanic_carp_tbl_zzensayo_formula.solucion.xml")
SOLUTION_FORMULA = texto_de("sanic_carp_tbl_zzensayo_formula.solution.xml")
YAML_FORMULA = texto_de("sanic_carp_tbl_zzensayo_formula.formulas.yaml")
FORMULAS = {"Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml": YAML_FORMULA}


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


def evaluar(xml, en_solucion, datos, formulas=None):
    return mt.evaluar(xml, en_solucion, datos, IDIOMAS, formulas)


class Veredicto(unittest.TestCase):
    """`evaluar` da `OK` solo sin diferencias; ya no queda ningún tipo «sin comparar» (la fórmula tuvo su exportación real el 2026-10-09)."""

    def test_evaluar_solo_da_ok_si_no_hay_diferencias(self):
        raiz = "sanic_carp_tbl_zzensayo"
        self.assertEqual(("OK", []), evaluar(XML_ENSAYO, {raiz}, ENSAYO))
        estado, textos = evaluar(XML_ENSAYO, set(), ENSAYO)
        self.assertEqual("DIFIERE", estado)
        self.assertTrue(any("RootComponent" in t for t in textos), textos)
        estado, textos = evaluar(XML_ENSAYO, {raiz}, cambiar(ENSAYO, ["descripcion"], "Otra."))
        self.assertEqual("DIFIERE", estado)

    def test_ya_no_existe_el_mecanismo_de_lo_que_no_se_puede_comparar(self):
        self.assertFalse(hasattr(mt, "TIPOS_SIN_MUESTRA"))
        self.assertFalse(hasattr(mt, "no_comparables"))

    def test_lo_que_no_es_ok_cuenta_como_no_coincidencia_para_el_codigo_de_salida(self):
        self.assertEqual(0, mt.no_coinciden(["OK", "OK"]))
        self.assertEqual(1, mt.no_coinciden(["OK", "DIFIERE"]))
        self.assertEqual(2, mt.no_coinciden(["DIFIERE", "DIFIERE", "OK"]))


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

    def test_evaluar_da_ok_con_la_muestra_real(self):
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


RAIZ_FORMULA = "sanic_carp_tbl_zzensayo"
RUTA_YAML = "Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml"
EXPRESION_REAL = "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 60"
DESCRIPCION_REAL = "Horas desde que el último correo del cliente espera respuesta, calculada al leer."


def en_atributo(xml, nombre, viejo, nuevo):
    """Cambia `viejo` por `nuevo` SOLO dentro del `<attribute>` de la columna `nombre` (otras columnas repiten las mismas etiquetas)."""
    ini = xml.index(f'<attribute PhysicalName="{nombre}">')
    fin = xml.index("</attribute>", ini)
    trozo = xml[ini:fin]
    assert viejo in trozo, f"{viejo!r} no está en el atributo {nombre}"
    return xml[:ini] + trozo.replace(viejo, nuevo, 1) + xml[fin:]


def zip_de_la_muestra(con_bom=True):
    """Un zip con la forma de un ExportSolution, armado con las partes reales guardadas (la fórmula con BOM, como pide leerla `utf-8-sig`)."""
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as z:
        z.writestr("customizations.xml", XML_FORMULA)
        z.writestr("solution.xml", SOLUTION_FORMULA)
        z.writestr(RUTA_YAML, ("﻿" if con_bom else "") + YAML_FORMULA)
        z.writestr("[Content_Types].xml", "<Types />")
    return buffer.getvalue()


class ContraLaMuestraRealDeLaFormula(unittest.TestCase):
    """La exportación REAL (ExportSolution unmanaged, 2026-10-09, Dev) de `sanic_carp_tbl_zzensayo` con la columna de fórmula que creó una persona en
    el editor de Power Apps. En `customizations.xml` el atributo es `<Type>decimal</Type>` (el tipo de su resultado) con `<SourceType>3</SourceType>`,
    `<Accuracy>` (la precisión), `<MinValue>`/`<MaxValue>` que fija la plataforma (NO se comparan) y `<FormulaDefinitionFileName>`; la expresión no está
    ahí sino en `Formulas/<tabla>-FormulaDefinitions.yaml` del zip, una línea por columna: `<columna>: =<expresión>`."""

    def diferencias(self, datos=FORMULA_MD, xml=XML_FORMULA, formulas=FORMULAS):
        return mt.comparar_con_xml(xml, datos, IDIOMAS, formulas)

    def test_el_playbook_de_ensayo_tal_cual_coincide_con_la_exportacion_real(self):
        self.assertEqual([], self.diferencias())
        self.assertEqual(("OK", []), evaluar(XML_FORMULA, {RAIZ_FORMULA}, FORMULA_MD, FORMULAS))

    def test_la_muestra_cubre_lo_que_dice_cubrir(self):
        f = FORMULA_MD["columnas"][columna(FORMULA_MD, "sanic_antiguedad")]
        self.assertEqual(("formula", "decimal", 2, EXPRESION_REAL), (f["tipo"], f["resultado"], f["precision"], f["expresion"]))
        self.assertEqual(f"sanic_antiguedad: ={EXPRESION_REAL}", YAML_FORMULA.strip())
        # el atributo se renombró en la copia (la persona lo tipeó `sanic_antiguedadq`) para coincidir con el playbook de ensayo
        self.assertNotIn("sanic_antiguedadq", XML_FORMULA + YAML_FORMULA)
        self.assertIn("<SourceType>3</SourceType>", XML_FORMULA)

    def test_cada_diferencia_de_la_formula_se_detecta_con_su_detalle(self):
        i = columna(FORMULA_MD, "sanic_antiguedad")
        sin_precision = cambiar(cambiar(FORMULA_MD, ["columnas", i, "resultado"], "entero"), ["columnas", i, "precision"], None)
        casos = [
            ("expresion distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "expresion"], "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Hours)")),
             [f"sanic_antiguedad.expresion: yaml={EXPRESION_REAL!r} playbook='DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Hours)'"]),
            ("resultado entero contra un decimal", dict(datos=sin_precision), ["sanic_antiguedad.tipo: xml='decimal' playbook='int'"]),
            ("precision distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "precision"], 3)), ["sanic_antiguedad.precision: xml='2' playbook='3'"]),
            ("SourceType distinto", dict(xml=en_atributo(XML_FORMULA, "sanic_antiguedad", "<SourceType>3</SourceType>", "<SourceType>0</SourceType>")),
             ["sanic_antiguedad.SourceType: xml='0' playbook='3'"]),
            ("etiqueta 3082 distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "displayname"], {"1033": "Antiguedad (horas)", "3082": "Otra"})),
             ["sanic_antiguedad.displayname[3082]: xml='Antiguedad (horas)' playbook='Otra'"]),
            ("descripcion 3082 distinta", dict(xml=en_atributo(XML_FORMULA, "sanic_antiguedad", f'<Description description="{DESCRIPCION_REAL}" languagecode="3082" />',
                                                                '<Description description="Otra." languagecode="3082" />')),
             [f"sanic_antiguedad.descripcion[3082]: xml='Otra.' playbook={DESCRIPCION_REAL!r}"]),
            ("el yaml no tiene la columna", dict(formulas={RUTA_YAML: "sanic_otra: =1\n"}),
             ["sanic_antiguedad.expresion: /Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml no tiene la columna sanic_antiguedad"]),
            ("el yaml no esta en el zip", dict(formulas={}),
             ["sanic_antiguedad.expresion: el atributo apunta a /Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml y la exportación no trae ese archivo"]),
            ("el yaml no se pasa", dict(formulas=None),
             ["sanic_antiguedad.expresion: el atributo apunta a /Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml y la exportación no trae ese archivo"]),
            ("el atributo no trae FormulaDefinitionFileName",
             dict(xml=en_atributo(XML_FORMULA, "sanic_antiguedad", "<FormulaDefinitionFileName>/Formulas/sanic_carp_tbl_zzensayo-FormulaDefinitions.yaml</FormulaDefinitionFileName>", "")),
             ["sanic_antiguedad.expresion: el atributo no trae FormulaDefinitionFileName (no es una columna de fórmula en el XML exportado)"]),
            ("requerida distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "requerida"], True)), ["sanic_antiguedad.requerida: xml='none' playbook='required'"]),
            ("protegida distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "protegida"], True)), ["sanic_antiguedad.protegida: xml='0' playbook='1'"]),
            ("auditoria distinta", dict(datos=cambiar(FORMULA_MD, ["columnas", i, "auditoria"], True)), ["sanic_antiguedad.auditoria: xml='0' playbook='1'"]),
        ]
        for nombre, kw, esperado in casos:
            with self.subTest(nombre):
                self.assertEqual(esperado, self.diferencias(**kw))

    def test_la_expresion_se_compara_normalizando_espacios_y_sin_el_igual_inicial(self):
        i = columna(FORMULA_MD, "sanic_antiguedad")
        con_espacios = "  DateDiff(sanic_sinrespuestadesde,\n   Now(),   TimeUnit.Minutes)\t/  60 "
        self.assertEqual([], self.diferencias(datos=cambiar(FORMULA_MD, ["columnas", i, "expresion"], con_espacios)))
        # el yaml con otros espacios, con el igual pegado o con CRLF
        for yaml in ("sanic_antiguedad:   =  DateDiff(sanic_sinrespuestadesde,   Now(),  TimeUnit.Minutes) /   60\r\n",
                     f"sanic_antiguedad: {EXPRESION_REAL}\n",   # sin el `=` inicial
                     f"otra: =1\r\nsanic_antiguedad: ={EXPRESION_REAL}\r\n"):
            with self.subTest(yaml=yaml):
                self.assertEqual([], self.diferencias(formulas={RUTA_YAML: yaml}))
        # y un espacio de MENOS donde el playbook tiene uno de más en un lugar significativo sigue siendo una diferencia (se normaliza el blanco, no se lo borra)
        pegado = cambiar(FORMULA_MD, ["columnas", i, "expresion"], "DateDiff(sanic_sinrespuestadesde,Now(),TimeUnit.Minutes)/60")
        self.assertEqual(1, len(self.diferencias(datos=pegado)))

    def test_el_yaml_con_bom_se_lee_igual(self):
        self.assertEqual([], self.diferencias(formulas={RUTA_YAML: "﻿" + YAML_FORMULA}))

    def test_no_se_comparan_el_minimo_ni_el_maximo_de_una_formula(self):
        # los fija la plataforma (±100.000.000.000): una fórmula del playbook no los declara y el XML puede traer cualquiera
        xml = en_atributo(en_atributo(XML_FORMULA, "sanic_antiguedad", "<MinValue>-100000000000</MinValue>", "<MinValue>5</MinValue>"),
                          "sanic_antiguedad", "<MaxValue>100000000000</MaxValue>", "<MaxValue>6</MaxValue>")
        self.assertEqual([], self.diferencias(xml=xml))

    def test_una_formula_de_resultado_entero_se_compara_como_int_sin_precision(self):
        i = columna(FORMULA_MD, "sanic_antiguedad")
        entero = {k: v for k, v in FORMULA_MD["columnas"][i].items() if k != "precision"}
        entero["resultado"] = "entero"
        datos = cambiar(FORMULA_MD, ["columnas", i], entero)
        xml = en_atributo(XML_FORMULA, "sanic_antiguedad", "<Type>decimal</Type>", "<Type>int</Type>")
        self.assertEqual([], self.diferencias(datos=datos, xml=xml))
        # y el mismo playbook contra el XML decimal de verdad difiere en el tipo
        self.assertEqual(["sanic_antiguedad.tipo: xml='decimal' playbook='int'"], self.diferencias(datos=datos))

    def test_la_formula_que_falta_o_sobra_en_el_xml_se_denuncia(self):
        sin_la_columna = cambiar(FORMULA_MD, ["columnas", columna(FORMULA_MD, "sanic_antiguedad")], dict(FORMULA_MD["columnas"][columna(FORMULA_MD, "sanic_antiguedad")], nombre="sanic_otra"))
        difs = self.diferencias(datos=sin_la_columna)
        self.assertIn("falta la columna sanic_otra", difs)
        self.assertTrue(any("sanic_antiguedad" in d and "no declara" in d for d in difs), difs)

    def test_la_muestra_no_pisa_a_las_anteriores(self):
        self.assertNotEqual(XML_FORMULA, XML_DECIMAL_MOTIVOS)
        # el playbook de la fórmula contra el XML sin fórmula: le falta la columna (y las demás, que sí están)
        difs = mt.comparar_con_xml(XML_DECIMAL_MOTIVOS, FORMULA_MD, IDIOMAS, FORMULAS)
        self.assertTrue(any("falta la columna sanic_antiguedad" in d for d in difs), difs)


class LecturaDelZipExportado(unittest.TestCase):
    """`leer_exportacion` saca del zip de ExportSolution lo que `main` compara: `customizations.xml`, los RootComponent de tipo tabla y los yaml de
    `Formulas/`. `guardar_muestra` deja junto al XML de la tabla el yaml de sus fórmulas (`--guardar`)."""

    def test_lee_el_xml_las_tablas_de_la_solucion_y_los_yaml_de_formulas(self):
        custom, en_solucion, formulas = mt.leer_exportacion(zip_de_la_muestra())
        self.assertEqual(XML_FORMULA, custom)
        self.assertIn(RAIZ_FORMULA, en_solucion)
        self.assertIn("sanic_carp_tbl_cliente", en_solucion)
        self.assertNotIn("sanic_carp_ch_moneda", en_solucion)  # un choice global es RootComponent type 9, no una tabla
        self.assertEqual({RUTA_YAML}, set(formulas))
        self.assertEqual(YAML_FORMULA, formulas[RUTA_YAML])  # sin el BOM
        self.assertEqual(("OK", []), evaluar(custom, en_solucion, FORMULA_MD, formulas))

    def test_el_yaml_sin_bom_tambien_y_un_zip_sin_formulas_da_un_diccionario_vacio(self):
        self.assertEqual({RUTA_YAML: YAML_FORMULA}, mt.leer_exportacion(zip_de_la_muestra(con_bom=False))[2])
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w") as z:
            z.writestr("customizations.xml", XML_ENSAYO)
            z.writestr("solution.xml", SOLUTION_FORMULA)
        self.assertEqual({}, mt.leer_exportacion(buffer.getvalue())[2])

    def test_un_customizations_xml_con_bom_se_lee_sin_el_bom(self):
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w") as z:
            z.writestr("customizations.xml", "\ufeff" + XML_ENSAYO)
            z.writestr("solution.xml", SOLUTION_FORMULA)
        custom, _, _ = mt.leer_exportacion(buffer.getvalue())
        self.assertEqual(XML_ENSAYO, custom)
        self.assertEqual([], mt.comparar_con_xml(custom, ENSAYO, IDIOMAS))

    def test_guardar_deja_el_xml_de_la_tabla_y_el_yaml_de_sus_formulas(self):
        with tempfile.TemporaryDirectory() as d:
            escritos = mt.guardar_muestra(d, RAIZ_FORMULA, XML_FORMULA, FORMULAS)
            self.assertEqual([os.path.join(d, f"{RAIZ_FORMULA}.solucion.xml"), os.path.join(d, f"{RAIZ_FORMULA}.formulas.yaml")], escritos)
            with open(escritos[0], encoding="utf-8") as f:
                guardado = f.read()
            self.assertIn("<attribute PhysicalName=\"sanic_antiguedad\">", guardado)
            self.assertIn("FormulaDefinitionFileName", guardado)
            with open(escritos[1], encoding="utf-8-sig") as f:
                self.assertEqual(YAML_FORMULA, f.read())

    def test_guardar_sin_formulas_no_escribe_yaml_y_una_tabla_ausente_no_escribe_nada(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual([os.path.join(d, f"{RAIZ_FORMULA}.solucion.xml")], mt.guardar_muestra(d, RAIZ_FORMULA, XML_ENSAYO, {}))
            self.assertEqual([], mt.guardar_muestra(d, "sanic_carp_tbl_noexiste", XML_ENSAYO, {}))
            self.assertEqual([f"{RAIZ_FORMULA}.solucion.xml"], os.listdir(d))

    def test_lo_guardado_vuelve_a_comparar_limpio(self):
        with tempfile.TemporaryDirectory() as d:
            xml_guardado, yaml_guardado = mt.guardar_muestra(d, RAIZ_FORMULA, XML_FORMULA, FORMULAS)
            with open(xml_guardado, encoding="utf-8") as f:
                custom = "<ImportExportXml><Entities>" + f.read() + "</Entities></ImportExportXml>"
            with open(yaml_guardado, encoding="utf-8-sig") as f:
                formulas = {RUTA_YAML: f.read()}
        self.assertEqual([], mt.comparar_con_xml(custom, FORMULA_MD, IDIOMAS, formulas))


if __name__ == "__main__":
    unittest.main()
