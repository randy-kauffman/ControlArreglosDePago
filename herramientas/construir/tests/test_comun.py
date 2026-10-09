"""Pruebas de `_comun.py`: parseo de los dos bloques ```json``` del playbook
(Identidad y Qué se crea) y sus validaciones de forma. Ninguna toca el
entorno ni la red."""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
_CONSTRUIR = os.path.dirname(_AQUI)
if _CONSTRUIR not in sys.path:
    sys.path.insert(0, _CONSTRUIR)
if os.path.dirname(_CONSTRUIR) not in sys.path:
    sys.path.insert(0, os.path.dirname(_CONSTRUIR))

import _comun as c  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA, COMPONENTE_VALIDO, identidad, componente, playbook_md, label, por_idioma,
    armar_cliente_precondiciones_ok, respuesta_idioma_ok, respuesta_idiomas_provisionados_ok,
    respuesta_solucion_ok, es_ruta_solutions,
)


class DividirSecciones(unittest.TestCase):
    def test_separa_secciones_por_encabezado_de_nivel_2(self):
        texto = playbook_md()
        secciones = c.dividir_secciones(texto)
        self.assertIn("1", secciones)
        self.assertIn("2", secciones)
        self.assertIn("Identidad", secciones["1"]["titulo"])
        self.assertIn("Qué se crea", secciones["2"]["titulo"])

    def test_no_encuentra_seccion_que_no_existe(self):
        secciones = c.dividir_secciones(playbook_md(con_seccion_1=False))
        self.assertNotIn("1", secciones)


class ObtenerBloqueJson(unittest.TestCase):
    def test_seccion_ausente_es_error(self):
        secciones = c.dividir_secciones(playbook_md(con_seccion_2=False))
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_bloque_json(secciones, "2", "Qué se crea")

    def test_sin_bloques_json_es_error(self):
        texto = playbook_md() + "\n## 4. Vacía\n\nsin bloque json acá\n"
        secciones = c.dividir_secciones(texto)
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_bloque_json(secciones, "4", "Vacía")

    def test_dos_bloques_json_en_la_misma_seccion_es_error(self):
        texto = playbook_md(bloques_extra_seccion_2=1)
        secciones = c.dividir_secciones(texto)
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_bloque_json(secciones, "2", "Qué se crea")

    def test_json_invalido_es_error(self):
        texto = playbook_md().replace('"tipo": "choice-global"', '"tipo" "choice-global"')  # sin ':' -> JSON roto
        secciones = c.dividir_secciones(texto)
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_bloque_json(secciones, "2", "Qué se crea")

    def test_bloque_que_no_es_objeto_es_error(self):
        texto = playbook_md()
        # reemplaza el bloque de la sección 2 por una lista, no un objeto
        secciones = c.dividir_secciones(texto)
        secciones["2"]["cuerpo"] = "```json\n[1, 2, 3]\n```\n"
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_bloque_json(secciones, "2", "Qué se crea")


class NumerosDecimalesExactos(unittest.TestCase):
    """Un monto como 999999999.99 no pasa por `float`: se lee como `Decimal` y se compara exacto (skill 0.2.0, columna decimal)."""

    def test_los_decimales_del_bloque_se_leen_como_decimal_exacto_y_los_enteros_como_entero(self):
        import decimal
        texto = playbook_md().replace('"valor": 159460001,', '"valor": 159460001, "extra": 999999999.99, "otro": 0.1, "entero": 7,')
        datos = c.obtener_bloque_json(c.dividir_secciones(texto), "2", "Qué se crea")
        op = datos["opciones"][0]
        self.assertEqual((decimal.Decimal("999999999.99"), decimal.Decimal("0.1")), (op["extra"], op["otro"]))
        self.assertIsInstance(op["extra"], decimal.Decimal)
        self.assertIsInstance(op["entero"], int)
        self.assertNotEqual(0.1 + 0.2, float(decimal.Decimal("0.1") + decimal.Decimal("0.2")))  # el motivo de no usar float


class ObtenerIdentidad(unittest.TestCase):
    def secciones_con(self, identidad_dict):
        return c.dividir_secciones(playbook_md(identidad_dict=identidad_dict))

    def test_identidad_valida_se_lee_completa(self):
        secciones = self.secciones_con(IDENTIDAD_VALIDA)
        resultado = c.obtener_identidad(secciones)
        self.assertEqual(resultado, IDENTIDAD_VALIDA)

    def test_falta_una_clave_es_error(self):
        rota = {k: v for k, v in IDENTIDAD_VALIDA.items() if k != "abrev"}
        secciones = self.secciones_con(rota)
        with self.assertRaises(c.ErrorPlaybook) as ctx:
            c.obtener_identidad(secciones)
        self.assertIn("abrev", str(ctx.exception))

    def test_clave_de_mas_es_error(self):
        con_extra = identidad(algo_inventado="x")
        secciones = self.secciones_con(con_extra)
        with self.assertRaises(c.ErrorPlaybook) as ctx:
            c.obtener_identidad(secciones)
        self.assertIn("algo_inventado", str(ctx.exception))

    def test_fase_no_entero_es_error(self):
        secciones = self.secciones_con(identidad(fase="1"))
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_identidad(secciones)

    def test_la_clave_lcid_del_formato_viejo_ya_no_existe(self):
        # La skill 0.2.0 reemplazó `lcid` (un solo idioma) por `idiomas` (lista): un playbook viejo es un error, no se lee en silencio.
        vieja = {k: v for k, v in IDENTIDAD_VALIDA.items() if k != "idiomas"}
        vieja["lcid"] = 1033
        with self.assertRaises(c.ErrorPlaybook) as ctx:
            c.obtener_identidad(self.secciones_con(vieja))
        self.assertIn("idiomas", str(ctx.exception))
        self.assertIn("lcid", str(ctx.exception))

    def test_idiomas_mal_formados_son_error_una_fila_por_caso(self):
        casos = [
            ("un entero suelto", 1033),
            ("un texto", "1033"),
            ("lista vacia", []),
            ("elemento texto", [1033, "3082"]),
            ("elemento booleano", [1033, True]),
            ("elemento decimal", [1033, 3082.0]),
            ("repetidos", [1033, 1033]),
            ("sin el 1033", [3082]),
            ("lcid no positivo", [1033, 0]),
        ]
        for nombre, valor in casos:
            with self.subTest(nombre):
                with self.assertRaises(c.ErrorPlaybook) as ctx:
                    c.obtener_identidad(self.secciones_con(identidad(idiomas=valor)))
                self.assertIn("idiomas", str(ctx.exception))

    def test_idiomas_con_un_tercer_idioma_se_aceptan(self):
        self.assertEqual([1033, 3082, 1046], c.obtener_identidad(self.secciones_con(identidad(idiomas=[1033, 3082, 1046])))["idiomas"])

    def test_prefijo_vacio_es_error(self):
        secciones = self.secciones_con(identidad(prefijo=""))
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_identidad(secciones)

    def test_publisher_no_texto_es_error(self):
        secciones = self.secciones_con(identidad(publisher=123))
        with self.assertRaises(c.ErrorPlaybook):
            c.obtener_identidad(secciones)


class ObtenerComponente(unittest.TestCase):
    def test_tipo_correcto_se_lee(self):
        secciones = c.dividir_secciones(playbook_md())
        datos = c.obtener_componente(secciones, "choice-global")
        self.assertEqual(datos, COMPONENTE_VALIDO)

    def test_tipo_distinto_es_error_sin_leer_nada_mas(self):
        secciones = c.dividir_secciones(playbook_md(componente_dict=componente(tipo="tabla-y-columnas")))
        with self.assertRaises(c.ErrorPlaybook) as ctx:
            c.obtener_componente(secciones, "choice-global")
        self.assertIn("tabla-y-columnas", str(ctx.exception))


class Salida(unittest.TestCase):
    def test_creado_y_ya_existia_devuelven_codigo_0(self):
        self.assertEqual(c.salida("creado", "x", "d"), 0)
        self.assertEqual(c.salida("ya_existia", "x", "d"), 0)

    def test_los_demas_estados_devuelven_codigo_distinto_de_0(self):
        for estado in ("difiere", "bloqueado", "error"):
            self.assertNotEqual(c.salida(estado, "x", "d"), 0)

    def test_imprime_una_linea_json_con_las_tres_claves(self):
        import io
        import contextlib
        import json as _json

        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            c.salida("creado", "sanic_carp_ch_moneda", "detalle")
        linea = buf.getvalue().strip().splitlines()[-1]
        obj = _json.loads(linea)
        self.assertEqual(obj, {"estado": "creado", "componente": "sanic_carp_ch_moneda", "detalle": "detalle"})


class EstadoDeEscritura(unittest.TestCase):
    """Aprobado el 2026-10-01: 'creado' solo si se dio de alta algo; 'actualizado' si solo se corrigió lo que ya existía."""

    def test_alta_es_creado_aunque_ademas_se_haya_corregido_algo(self):
        self.assertEqual("creado", c.estado_de_escritura(True, True))

    def test_solo_correccion_es_actualizado(self):
        self.assertEqual("actualizado", c.estado_de_escritura(False, True))

    def test_sin_escritura_es_ya_existia(self):
        self.assertEqual("ya_existia", c.estado_de_escritura(False, False))

    def test_actualizado_sale_con_codigo_cero_como_los_otros_estados_de_exito(self):
        import io, contextlib
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, c.salida("actualizado", "x", "y"))
            self.assertEqual(1, c.salida("difiere", "x", "y"))


class ExigirForma(unittest.TestCase):
    """`exigir_forma` es el único punto donde se valida el tipo de un valor
    leído de una respuesta del entorno."""

    def ok(self, valor, tipo, **kw):
        return c.exigir_forma(valor, tipo, "GET consulta", "campo", **kw)

    def test_devuelve_el_valor_cuando_cumple(self):
        self.assertEqual(self.ok(1033, int), 1033)
        self.assertEqual(self.ok("x", str), "x")
        self.assertIs(self.ok(False, bool), False)
        self.assertEqual(self.ok({"a": 1}, dict), {"a": 1})
        self.assertEqual(self.ok([1, 2], [int]), [1, 2])
        self.assertEqual(self.ok([], [dict]), [])
        self.assertIsNone(self.ok(None, dict, permite_nulo=True))

    def test_rechaza_el_tipo_equivocado(self):
        for valor, tipo in [("1033", int), (None, int), (1.0, int), (5, str), (None, str), ("false", bool),
                            (0, bool), ([], dict), ({}, [int]), ("abc", [str]), (None, [int])]:
            with self.subTest(valor=valor, tipo=tipo):
                with self.assertRaises(c.ErrorEntorno):
                    self.ok(valor, tipo)

    def test_numero_acepta_entero_y_decimal_pero_no_booleano_ni_texto(self):
        self.assertEqual(self.ok(100, "numero"), 100)
        self.assertEqual(self.ok(999999999.99, "numero"), 999999999.99)
        for malo in (True, False, "1.5", None, [1], {"a": 1}):
            with self.subTest(malo=malo):
                with self.assertRaises(c.ErrorEntorno) as ctx:
                    self.ok(malo, "numero")
                self.assertIn("un número", str(ctx.exception))

    def test_un_booleano_no_es_un_entero(self):
        with self.assertRaises(c.ErrorEntorno):
            self.ok(True, int)
        with self.assertRaises(c.ErrorEntorno):
            self.ok([1033, True], [int])

    def test_lista_valida_cada_elemento_y_dice_cual_fallo(self):
        with self.assertRaises(c.ErrorEntorno) as ctx:
            self.ok([1033, "3082"], [int])
        self.assertIn("'campo[1]'", str(ctx.exception))

    def test_no_vacio_aplica_a_texto_y_a_lista(self):
        for valor, tipo in [("", str), ([], [int])]:
            with self.subTest(valor=valor):
                with self.assertRaises(c.ErrorEntorno):
                    self.ok(valor, tipo, no_vacio=True)

    def test_el_mensaje_es_uniforme_y_acota_lo_que_vuelca(self):
        with self.assertRaises(c.ErrorEntorno) as ctx:
            self.ok("x" * 5000, int)
        msg = str(ctx.exception)
        self.assertIn("GET consulta", msg)
        self.assertIn("forma inesperada", msg)
        self.assertIn("'campo'", msg)
        self.assertIn("un entero", msg)
        self.assertLess(len(msg), 600)


class TextosPorIdioma(unittest.TestCase):
    """Todo nombre visible se escribe una vez por CADA LCID de `idiomas` (skill 0.2.0, regla transversal)."""

    IDIOMAS = [1033, 3082]

    def validar(self, valor, **kw):
        return c.validar_textos_por_idioma(valor, self.IDIOMAS, "'displayname'", **kw)

    def test_acepta_un_texto_por_idioma(self):
        self.assertEqual({"1033": "Cuota", "3082": "Cuota"}, self.validar({"1033": "Cuota", "3082": "Cuota"}))

    def test_rechaza_lo_que_no_es_un_objeto_con_exactamente_los_idiomas_y_textos_no_vacios(self):
        casos = [
            ("cadena unica (formato viejo)", "Cuota", "objeto"),
            ("lista", ["Cuota"], "objeto"),
            ("falta un idioma", {"1033": "Cuota"}, "3082"),
            ("sobra un idioma", {"1033": "Cuota", "3082": "Cuota", "1046": "Parcela"}, "1046"),
            ("clave numerica en vez de texto", {1033: "Cuota", 3082: "Cuota"}, "1033"),
            ("texto vacio", {"1033": "Cuota", "3082": "  "}, "3082"),
            ("valor no texto", {"1033": "Cuota", "3082": 5}, "3082"),
            ("nulo", {"1033": None, "3082": "Cuota"}, "1033"),
        ]
        for nombre, valor, parte in casos:
            with self.subTest(nombre):
                with self.assertRaises(c.ErrorPlaybook) as ctx:
                    self.validar(valor)
                self.assertIn("'displayname'", str(ctx.exception))
                self.assertIn(parte, str(ctx.exception))

    def test_las_tildes_se_rechazan_en_cada_idioma_y_el_mensaje_dice_cual(self):
        with self.assertRaises(c.ErrorPlaybook) as ctx:
            self.validar({"1033": "Cuota", "3082": "Conversación"})
        self.assertIn("3082", str(ctx.exception))
        self.assertIn("BP-PP-197", str(ctx.exception))

    def test_sin_tildes_false_no_las_revisa(self):
        self.assertEqual("Conversación", self.validar({"1033": "Conversación", "3082": "Conversación"}, sin_tildes=False)["1033"])


class EtiquetasWebApi(unittest.TestCase):
    def test_una_etiqueta_localizada_por_idioma_en_el_orden_de_idiomas(self):
        et = c.etiqueta_web_api({"3082": "Cuota", "1033": "Installment"}, [1033, 3082])
        self.assertEqual("Microsoft.Dynamics.CRM.Label", et["@odata.type"])
        self.assertEqual([(1033, "Installment"), (3082, "Cuota")], [(l["LanguageCode"], l["Label"]) for l in et["LocalizedLabels"]])
        self.assertTrue(all(l["@odata.type"] == "Microsoft.Dynamics.CRM.LocalizedLabel" for l in et["LocalizedLabels"]))

    def test_la_descripcion_es_el_mismo_texto_en_cada_idioma(self):
        et = c.etiqueta_uniforme("Día en que vence la cuota.", [1033, 3082])
        self.assertEqual([(1033, "Día en que vence la cuota."), (3082, "Día en que vence la cuota.")],
                         [(l["LanguageCode"], l["Label"]) for l in et["LocalizedLabels"]])


class CompararEtiquetas(unittest.TestCase):
    IDIOMAS = [1033, 3082]
    ESPERADO = {"1033": "Installment", "3082": "Cuota"}

    def comparar(self, etiqueta, **kw):
        difs = []
        c.comparar_etiquetas(etiqueta, self.IDIOMAS, "DisplayName", self.ESPERADO, difs, "GET consulta", "DisplayName", **kw)
        return difs

    def test_coincide_en_todos_los_idiomas(self):
        self.assertEqual([], self.comparar(label(self.ESPERADO)))

    def test_difiere_en_un_idioma_y_dice_cual(self):
        difs = self.comparar(label({"1033": "Installment", "3082": "Parcela"}))
        self.assertEqual(1, len(difs))
        self.assertIn("3082", difs[0])
        self.assertIn("Parcela", difs[0])

    def test_falta_la_etiqueta_de_un_idioma(self):
        difs = self.comparar(label({"1033": "Installment"}))
        self.assertEqual(1, len(difs))
        self.assertIn("3082", difs[0])

    def test_etiqueta_en_un_idioma_que_el_playbook_no_declara_difiere(self):
        difs = self.comparar(label(self.ESPERADO, otros_idiomas=[(1046, "Parcela")]))
        self.assertEqual(1, len(difs))
        self.assertIn("1046", difs[0])

    def test_forma_inesperada_es_error_de_entorno_no_diferencia(self):
        for malo in ({"LocalizedLabels": [{"LanguageCode": "1033", "Label": "x"}]}, {"LocalizedLabels": "x"}, [], "texto"):
            with self.subTest(malo=malo):
                with self.assertRaises(c.ErrorEntorno):
                    self.comparar(malo)

    def test_nulo_solo_si_se_permite(self):
        with self.assertRaises(c.ErrorEntorno):
            self.comparar(None)
        self.assertEqual(2, len(self.comparar(None, permite_nulo=True)))  # falta la etiqueta de cada idioma

    def test_la_descripcion_se_compara_en_cada_idioma_con_el_mismo_texto(self):
        difs = []
        c.comparar_descripcion(label("Día de vencimiento."), self.IDIOMAS, "Description", "Día de vencimiento.", difs, "GET consulta", "Description")
        self.assertEqual([], difs)
        c.comparar_descripcion(label("Día de vencimiento.", idiomas=[1033]), self.IDIOMAS, "Description", "Día de vencimiento.", difs, "GET consulta", "Description")
        self.assertEqual(1, len(difs))
        self.assertIn("3082", difs[0])

    def test_descripcion_vacia_o_ausente_equivale_a_texto_vacio(self):
        difs = []
        c.comparar_descripcion(None, self.IDIOMAS, "Description", "", difs, "GET consulta", "Description", permite_nulo=True)
        c.comparar_descripcion({"LocalizedLabels": []}, self.IDIOMAS, "Description", "", difs, "GET consulta", "Description", permite_nulo=True)
        self.assertEqual([], difs)


class ComprobarSolucionEIdioma(unittest.TestCase):
    """Precondición común: los idiomas del playbook están provisionados y el idioma base del entorno es uno de ellos."""

    def correr(self, identidad_dict=None, base=1033, provisionados="los_del_playbook"):
        identidad_dict = identidad_dict or IDENTIDAD_VALIDA
        cl = ClienteSimulado()
        cl.responder("GET", es_ruta_solutions, respuesta_solucion_ok(identidad_dict))
        cl.responder("GET", "organizations?$select=languagecode", respuesta_idioma_ok(identidad_dict, base=base))
        cl.responder("GET", "RetrieveProvisionedLanguages",
                     (200, {"RetrieveProvisionedLanguages": identidad_dict["idiomas"] if provisionados == "los_del_playbook" else provisionados}, {}))
        return c.comprobar_solucion_e_idioma(cl, identidad_dict)

    def test_camino_feliz_devuelve_el_id_de_la_solucion(self):
        self.assertEqual("22222222-2222-2222-2222-222222222222", self.correr())

    def test_un_idioma_del_playbook_sin_provisionar_es_bloqueado_y_dice_cual(self):
        with self.assertRaises(c.Bloqueado) as ctx:
            self.correr(provisionados=[1033])
        self.assertIn("3082", str(ctx.exception))
        self.assertIn("provisionad", str(ctx.exception))

    def test_el_idioma_base_del_entorno_tiene_que_estar_en_los_idiomas_del_playbook(self):
        # Un entorno con base 3082 y un playbook solo con 1033 dejaría los nombres sin etiqueta en el idioma que se ve.
        with self.assertRaises(c.Bloqueado) as ctx:
            self.correr(identidad_dict=identidad(idiomas=[1033]), base=3082, provisionados=[1033, 3082])
        self.assertIn("3082", str(ctx.exception))
        self.assertIn("base", str(ctx.exception))

    def test_base_3082_con_los_dos_idiomas_en_el_playbook_pasa(self):
        self.assertTrue(self.correr(base=3082))

    def test_forma_inesperada_de_los_idiomas_provisionados_es_error_de_entorno(self):
        for malo in ([1033, "3082"], "1033", [], None):
            with self.subTest(malo=malo):
                with self.assertRaises(c.ErrorEntorno):
                    self.correr(provisionados=malo)


class CarpetaDePlaybooks(unittest.TestCase):
    """La carpeta de los playbooks sale de `proyecto.json` (la única configuración versionada), no de una ruta escrita en cada herramienta."""

    def test_sale_de_la_configuracion_y_es_absoluta_dentro_de_la_raiz_del_proyecto(self):
        import credenciales
        ruta = c.carpeta_playbooks({"playbooks": "playbooks"})
        self.assertEqual(os.path.join(credenciales.RAIZ_PROYECTO, "playbooks"), ruta)
        self.assertEqual(os.path.join(credenciales.RAIZ_PROYECTO, "otra", "carpeta"), c.carpeta_playbooks({"playbooks": "otra/carpeta"}))

    def test_una_clave_playbooks_ausente_o_que_no_es_texto_es_un_error_que_la_nombra(self):
        for config in ({}, {"playbooks": None}, {"playbooks": 5}, {"playbooks": "  "}):
            with self.subTest(config=config):
                with self.assertRaises(ValueError) as ctx:
                    c.carpeta_playbooks(config)
                self.assertIn("playbooks", str(ctx.exception))

    def test_la_raiz_se_puede_inyectar_para_las_pruebas(self):
        self.assertEqual(os.path.join("/raiz", "playbooks"), c.carpeta_playbooks({"playbooks": "playbooks"}, raiz="/raiz"))

    def test_sin_argumento_lee_proyecto_json(self):
        import credenciales
        self.assertEqual(os.path.join(credenciales.RAIZ_PROYECTO, credenciales.cargar_configuracion()["playbooks"]), c.carpeta_playbooks())


class NombreDeArchivo(unittest.TestCase):
    """Un componente sin nombre lógico (rol, perfil) se guarda con su nombre visible hecho archivo."""

    def test_translitera_tildes_y_enie_en_vez_de_perderlas(self):
        import _comun
        casos = {"SR - CARP - Administrador técnico": "sr_carp_administrador_tecnico", "CSP - CARP - Datos sensibles": "csp_carp_datos_sensibles",
                 "SR - CARP - Señor Ñandú (época Ü)": "sr_carp_senor_nandu_epoca_u", "  --Raro--  ": "raro"}
        for nombre, esperado in casos.items():
            with self.subTest(nombre):
                self.assertEqual(_comun.nombre_de_archivo(nombre), esperado)


if __name__ == "__main__":
    unittest.main()
