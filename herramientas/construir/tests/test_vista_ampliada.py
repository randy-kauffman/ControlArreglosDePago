"""Pruebas de la ampliación de `vista.py` (brechas G-V1 y G-V2 de `diseno/07`): vista de búsqueda rápida,
`link-type` `any` / `not any`, filtros anidados y los operadores de fecha relativa.

Las formas XML salen de lo probado en Dev (spikes `s62_q2b_quickfind.py` y `s52_q5_vista.py`, `spikes/plataforma/RESULTADO.md`):
el filtro de búsqueda rápida es `<filter type="or" isquickfindfields="1">` con `like` `{0}`, y el filtro relacionado es un
`<link-entity ... link-type="any">` hijo de `<entity>`, sin alias, con su propio `<filter>`.
"""
import copy
import os
import sys
import unittest
from xml.etree import ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import test_vista as tv  # noqa: E402
import vista as vi  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402

TABLA = tv.TABLA
PRIMARIA = tv.PRIMARIA

BUSQUEDA = {
    "tipo": "vista", "nombre": "Busqueda rapida de Arreglo", "tabla": "sanic_carp_tbl_arreglo",
    "clase": "BusquedaRapida", "descripcion": "Busqueda por numero de producto.", "pordefecto": True,
    "columnas": [{"nombre": "sanic_numeroproducto", "ancho": 150}, {"nombre": "sanic_nombre", "ancho": 110}],
    "orden": [],
    "filtro": {"tipo": "or", "buscar": True, "condiciones": [
        {"columna": "sanic_numeroproducto", "operador": "like", "valores": ["{0}"]},
        {"columna": "sanic_nombre", "operador": "like", "valores": ["{0}"]}]},
}

RIESGO = {
    "tipo": "vista", "nombre": "Arreglos en riesgo", "tabla": "sanic_carp_tbl_arreglo", "clase": "Publica",
    "descripcion": "Con alguna cuota vencida sin pago.", "pordefecto": False,
    "columnas": [{"nombre": "sanic_nombre", "ancho": 110}, {"nombre": "sanic_fechainicio", "ancho": 120}],
    "orden": [{"columna": "sanic_fechainicio", "sentido": "asc"}],
    "filtro": {"tipo": "and", "condiciones": [
        {"columna": "statecode", "operador": "eq", "valores": [0]}],
        "enlaces": [{
            "tabla": "sanic_carp_tbl_cuota", "tipo": "any", "de": "sanic_arregloid", "a": "sanic_carp_tbl_arregloid",
            "condiciones": [{"columna": "sanic_fechapago", "operador": "null"}],
            "filtros": [{"tipo": "or", "condiciones": [
                {"columna": "sanic_fechavencimiento", "operador": "olderthan-x-days", "valores": [1]},
                {"columna": "sanic_fechavencimiento", "operador": "yesterday"}]}],
        }]},
}


def con(base, **cambios):
    d = copy.deepcopy(base)
    d.update(cambios)
    return d


class BusquedaRapidaXml(unittest.TestCase):
    def test_el_filtro_de_busqueda_lleva_isquickfindfields_y_los_like_con_llaves(self):
        fetch = vi.fetchxml(BUSQUEDA, "sanic_carp_tbl_arregloid")
        self.assertIn('<filter type="or" isquickfindfields="1">', fetch)
        self.assertEqual(2, fetch.count('operator="like" value="{0}" />'))

    def test_un_filtro_normal_no_lleva_isquickfindfields(self):
        self.assertNotIn("isquickfindfields", vi.fetchxml(tv.COMPONENTE, PRIMARIA))


class LinkTypeYFiltrosAnidados(unittest.TestCase):
    def test_un_enlace_any_va_DENTRO_de_un_filter_como_en_learn(self):
        # Learn, "Filter rows using FetchXML": "use a link-entity element within the filter element". El spike s52 lo
        # probó como hijo directo de <entity> y funcionó, pero lo documentado es dentro del filtro (M6 de la revision).
        raiz = ET.fromstring(vi.fetchxml(RIESGO, "sanic_carp_tbl_arregloid"))
        entidad = raiz.find("entity")
        self.assertEqual([], entidad.findall("link-entity"))
        padre = entidad.find("filter")
        self.assertEqual("and", padre.get("type"))
        self.assertEqual(1, len(padre.findall("link-entity")))
        self.assertEqual("statecode", padre.find("condition").get("attribute"))
        self.assertEqual("any", padre.find("link-entity").get("link-type"))

    def test_un_enlace_any_solo_con_su_filtro_raiz_vacio_igual_lleva_el_filter(self):
        d = copy.deepcopy(RIESGO)
        d["filtro"]["condiciones"] = []
        entidad = ET.fromstring(vi.fetchxml(d, "sanic_carp_tbl_arregloid")).find("entity")
        self.assertEqual(1, len(entidad.find("filter").findall("link-entity")))

    def test_un_enlace_inner_sigue_siendo_hijo_de_entity(self):
        entidad = ET.fromstring(vi.fetchxml(tv.COMPONENTE, PRIMARIA)).find("entity")
        self.assertEqual(1, len(entidad.findall("link-entity")))

    def test_un_enlace_any_sale_sin_alias_y_sin_atributos(self):
        fetch = vi.fetchxml(RIESGO, "sanic_carp_tbl_arregloid")
        self.assertIn('<link-entity name="sanic_carp_tbl_cuota" from="sanic_arregloid" '
                      'to="sanic_carp_tbl_arregloid" link-type="any">', fetch)
        enlace = fetch[fetch.index("<link-entity"):]
        self.assertNotIn("alias=", enlace)
        self.assertNotIn("<attribute", enlace)

    def test_el_filtro_or_anidado_queda_adentro_del_filtro_and_del_enlace(self):
        fetch = vi.fetchxml(RIESGO, "sanic_carp_tbl_arregloid")
        enlace = fetch[fetch.index("<link-entity"):]
        self.assertLess(enlace.index('<filter type="and">'), enlace.index('<filter type="or">'))
        self.assertLess(enlace.index('<filter type="or">'), enlace.index("</filter>"))
        self.assertIn('<condition attribute="sanic_fechavencimiento" operator="olderthan-x-days" value="1" />', enlace)
        self.assertIn('<condition attribute="sanic_fechavencimiento" operator="yesterday" />', enlace)
        self.assertIn('<condition attribute="sanic_fechapago" operator="null" />', enlace)

    def test_not_any_se_escribe_con_el_espacio(self):
        d = copy.deepcopy(RIESGO)
        d["filtro"]["enlaces"][0]["tipo"] = "not any"
        self.assertIn('link-type="not any"', vi.fetchxml(d, "sanic_carp_tbl_arregloid"))

    def test_sin_tipo_el_enlace_sigue_siendo_inner(self):
        self.assertIn('link-type="inner"', vi.fetchxml(tv.COMPONENTE, PRIMARIA))

    def test_un_filtro_anidado_en_la_raiz_sale_dentro_del_filtro_raiz(self):
        d = tv.comp(filtro={"tipo": "and", "condiciones": [{"columna": "sanic_asunto", "operador": "not-null"}],
                            "filtros": [{"tipo": "or", "condiciones": [
                                {"columna": "sanic_asunto", "operador": "like", "valores": ["a%"]},
                                {"columna": "sanic_asunto", "operador": "like", "valores": ["b%"]}]}]})
        fetch = vi.fetchxml(d, PRIMARIA)
        abre_and, abre_or = fetch.index('<filter type="and">'), fetch.index('<filter type="or">')
        self.assertLess(abre_and, abre_or)
        self.assertLess(abre_or, fetch.index("</filter>"))
        self.assertLess(fetch.index("</filter>"), fetch.rindex("</filter>"))
        self.assertEqual(2, fetch.count("</filter>"))

    def test_un_filtro_solo_con_anidados_tambien_se_emite(self):
        d = tv.comp(filtro={"filtros": [{"tipo": "or", "condiciones": [
            {"columna": "sanic_asunto", "operador": "not-null"}]}]})
        self.assertIn('<filter type="or">', vi.fetchxml(d, PRIMARIA))


class OperadoresDeFecha(unittest.TestCase):
    def test_yesterday_y_today_no_llevan_valor(self):
        for op in ("yesterday", "today"):
            d = tv.comp(filtro={"condiciones": [{"columna": "modifiedon", "operador": op}]})
            vi.validar_playbook(d)
            self.assertIn(f'<condition attribute="modifiedon" operator="{op}" />', vi.fetchxml(d, PRIMARIA))

    def test_olderthan_x_days_y_on_or_before_llevan_un_valor(self):
        for op, valor in (("olderthan-x-days", 30), ("on-or-before", "2026-10-20")):
            d = tv.comp(filtro={"condiciones": [{"columna": "modifiedon", "operador": op, "valores": [valor]}]})
            vi.validar_playbook(d)
            self.assertIn(f'operator="{op}" value="{valor}" />', vi.fetchxml(d, PRIMARIA))


class ValidacionAmpliada(tv.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = vi.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_tabla_de_rechazos(self):
        casos = [
            ("busqueda sin buscar", con(BUSQUEDA, filtro={k: v for k, v in BUSQUEDA["filtro"].items() if k != "buscar"}),
             "tiene que declarar"),
            ("buscar en una vista publica", tv.comp(filtro={"tipo": "or", "buscar": True, "condiciones": [
                {"columna": "sanic_asunto", "operador": "like", "valores": ["{0}"]}]}), "solo vale en una vista de clase"),
            ("buscar con tipo and", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], tipo="and")), "tipo 'or'"),
            ("buscar con operador eq", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], condiciones=[
                {"columna": "sanic_nombre", "operador": "eq", "valores": ["{0}"]}])), "'like'"),
            ("buscar con un valor distinto de {0}", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], condiciones=[
                {"columna": "sanic_nombre", "operador": "like", "valores": ["%x%"]}])), "'{0}'"),
            ("buscar sin condiciones", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], condiciones=[])), "al menos una"),
            ("buscar con enlaces", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], enlaces=[
                {"tabla": "t", "de": "a", "a": "b", "alias": "x"}])), "ni enlaces ni filtros anidados"),
            ("buscar con filtros anidados", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], filtros=[
                {"condiciones": [{"columna": "x", "operador": "null"}]}])), "ni enlaces ni filtros anidados"),
            ("buscar que no es booleano", con(BUSQUEDA, filtro=dict(BUSQUEDA["filtro"], buscar="si")), "buscar"),
            ("tipo de enlace inexistente", self._enlace(RIESGO, tipo="outer"), "no admitido"),
            ("any con alias", self._enlace(RIESGO, alias="c"), "no lleva alias"),
            ("inner sin alias", self._enlace(RIESGO, tipo="inner"), "faltan ['alias']"),
            ("columna sacada de un enlace any (no tiene alias)", con(RIESGO, columnas=RIESGO["columnas"] + [
                {"nombre": "sanic_monto", "enlace": "c"}]), "que el filtro no declara"),
            ("any con enlaces adentro", self._enlace(RIESGO, enlaces=[
                {"tabla": "t", "de": "a", "a": "b", "alias": "z"}]), "no admite enlaces"),
            ("tipo de enlace que no es texto", self._enlace(RIESGO, tipo=1), "tipo"),
            ("filtro anidado con tipo raro", tv.comp(filtro={"filtros": [{"tipo": "xor", "condiciones": []}]}),
             "'and' u 'or'"),
            ("filtro anidado con clave de mas", tv.comp(filtro={"filtros": [{"sobra": 1}]}), "sobra"),
            ("filtro anidado que no es objeto", tv.comp(filtro={"filtros": ["x"]}), "filtros[0]"),
            ("filtros que no es lista", tv.comp(filtro={"filtros": {}}), "filtros"),
            ("filtros anidados 4 niveles", tv.comp(filtro={"filtros": [self._anidar(4)]}), "anidan más de 3"),
            ("olderthan con texto", tv.comp(filtro={"condiciones": [
                {"columna": "modifiedon", "operador": "olderthan-x-days", "valores": ["30"]}]}), "entero"),
            ("olderthan con booleano", tv.comp(filtro={"condiciones": [
                {"columna": "modifiedon", "operador": "olderthan-x-days", "valores": [True]}]}), "entero"),
            ("olderthan negativo", tv.comp(filtro={"condiciones": [
                {"columna": "modifiedon", "operador": "olderthan-x-days", "valores": [-1]}]}), "entero"),
            ("yesterday con valor", tv.comp(filtro={"condiciones": [
                {"columna": "modifiedon", "operador": "yesterday", "valores": [1]}]}), "no lleva valores"),
            ("on-or-before sin valor", tv.comp(filtro={"condiciones": [
                {"columna": "modifiedon", "operador": "on-or-before"}]}), "exactamente un valor"),
        ]
        for nombre, componente, fragmento in casos:
            with self.subTest(nombre):
                self.rechaza(componente, fragmento)

    @staticmethod
    def _enlace(base, **cambios):
        d = copy.deepcopy(base)
        d["filtro"]["enlaces"][0].update(cambios)
        return d

    @staticmethod
    def _anidar(n):
        g = {"tipo": "and", "condiciones": [{"columna": "x", "operador": "null"}]}
        return g if n == 0 else dict(g, filtros=[ValidacionAmpliada._anidar(n - 1)])

    def test_el_playbook_de_riesgo_y_el_de_busqueda_validan(self):
        vi.validar_playbook(RIESGO)
        vi.validar_playbook(BUSQUEDA)


class BusquedaRapidaContraElEntorno(tv.Base):
    """La quick find de fábrica ya existe en toda tabla (spike 2b): se REPARA con `--completar`, no se crea otra."""

    def armar_busqueda(self, vista, otras=()):
        cliente = ClienteSimulado()
        self.armar(cliente, vista=vista)
        self.otras = list(otras)

        def responder(ruta, cuerpo, solucion):
            if "querytype eq 4" in ruta:
                return (200, {"value": ([self.vista] if self.vista else []) + self.otras}, {})
            return (200, {"value": [self.vista] if self.vista else []}, {})

        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("savedqueries?"), responder))
        return cliente

    def fila(self, **cambios):
        return tv.fila_vista(BUSQUEDA, **dict({"isquickfindquery": True}, **cambios))

    def correr(self, cliente, solo_verificar=False, completar=False):
        return vi.construir(self.escribir(BUSQUEDA), solo_verificar, lambda: cliente, completar=completar,
                            dormir=lambda s: None)

    def test_la_busqueda_rapida_ya_correcta_da_ya_existia_y_no_escribe(self):
        cliente = self.armar_busqueda(self.fila())
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_busqueda_rapida_por_defecto_no_desmarca_las_vistas_publicas(self):
        # `isdefault` de la quick find de fábrica es otra cosa que la vista pública por defecto de la tabla: tocar la
        # pública dejaría a la tabla sin su vista inicial.
        cliente = self.armar_busqueda(self.fila())
        publica = {"savedqueryid": "publica-id", "name": "Arreglos vigentes"}
        previo = cliente._reglas[0]

        def responder(ruta, cuerpo, solucion):
            if "isdefault eq true" in ruta:
                return (200, {"value": [publica]}, {})
            return previo[2](ruta, cuerpo, solucion)

        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("savedqueries?"), responder))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_mismo_resultado_en_los_tres_caminos_para_un_filtro_distinto(self):
        mala = self.fila(fetchxml="<fetch><entity name='x'/></fetch>")
        for solo_verificar in (False, True):
            cliente = self.armar_busqueda(mala)
            estado, _, detalle = self.correr(cliente, solo_verificar=solo_verificar)
            self.assertEqual("difiere", estado, detalle)
            self.assertIn("fetchxml", detalle)

    def test_completar_repara_el_filtro_de_la_busqueda_rapida_y_publica(self):
        cliente = self.armar_busqueda(self.fila(fetchxml="<fetch/>"))

        def corregir(ruta, cuerpo, solucion):
            self.vista = self.fila()
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("savedqueries("), corregir)
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        estado, _, detalle = self.correr(cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertIn('isquickfindfields="1"', patch["cuerpo"]["fetchxml"])

    def test_completar_manda_los_cuatro_campos_juntos_aunque_solo_difiera_el_fetchxml(self):
        # Defecto A: la plataforma da 400 si el PATCH de una quick find no lleva {fetchxml, layoutxml, name, returnedtypecode}.
        cliente = self.armar_busqueda(self.fila(fetchxml="<fetch/>"))
        cliente.responder("PATCH", lambda r: r.startswith("savedqueries("), (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        self.correr(cliente, completar=True)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual({"fetchxml", "layoutxml", "name", "returnedtypecode"}, set(patch["cuerpo"]))
        self.assertEqual(BUSQUEDA["nombre"], patch["cuerpo"]["name"])
        self.assertEqual(BUSQUEDA["tabla"], patch["cuerpo"]["returnedtypecode"])

    def test_completar_en_busqueda_rapida_con_solo_otra_descripcion_tambien_manda_los_cuatro(self):
        cliente = self.armar_busqueda(self.fila(description="otra"))
        cliente.responder("PATCH", lambda r: r.startswith("savedqueries("), (204, {}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        self.correr(cliente, completar=True)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual({"description", "fetchxml", "layoutxml", "name", "returnedtypecode"}, set(patch["cuerpo"]))

    def test_una_busqueda_rapida_inexistente_no_se_crea_y_dice_cuales_hay(self):
        # Una segunda quick find no se usa (solo cuenta la de la tabla): el nombre se toma de Dev.
        cliente = self.armar_busqueda(None, otras=[tv.fila_vista(BUSQUEDA, name="Quick Find Active Arreglos")])
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("Quick Find Active Arreglos", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_busqueda_rapida_sin_ninguna_en_la_tabla_tampoco_se_crea(self):
        cliente = self.armar_busqueda(None)
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("ninguna", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_otra_busqueda_rapida_sobrante_se_denuncia_y_no_se_borra(self):
        cliente = self.armar_busqueda(self.fila(), otras=[
            {"savedqueryid": "otra-id", "name": "Quick Find Active Arreglos"}])
        for solo_verificar in (False, True):
            estado, _, detalle = self.correr(cliente, solo_verificar=solo_verificar)
            self.assertEqual("difiere", estado, detalle)
            self.assertIn("Quick Find Active Arreglos", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_la_plataforma_no_la_marca_como_busqueda_rapida_difiere(self):
        cliente = self.armar_busqueda(self.fila(isquickfindquery=False))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("isquickfindquery", detalle)

    def test_isquickfindquery_ausente_es_forma_inesperada(self):
        fila = self.fila()
        del fila["isquickfindquery"]
        cliente = self.armar_busqueda(fila)
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("isquickfindquery", detalle)

    def test_la_consulta_de_otras_busquedas_con_http_inesperado_es_error(self):
        cliente = self.armar_busqueda(self.fila())
        cliente._reglas.insert(0, ("GET", lambda r: "querytype eq 4" in r, (500, {}, {})))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("500", detalle)


if __name__ == "__main__":
    unittest.main()
