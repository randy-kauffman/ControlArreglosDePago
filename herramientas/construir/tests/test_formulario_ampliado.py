"""Pruebas de la ampliación de `formulario.py` (brechas G-F1 a G-F4 de `diseno/07`): solo lectura por campo, control Timeline,
bibliotecas JS y manejadores de eventos, y varias pestañas.

Formas del XML (leídas de formularios reales de Dev el 2026-10-20, sin datos):
- Timeline: `<control id="notescontrol" classid="{06375649-C143-495E-A496-C962E5B4488E}" />` en una celda `showlabel="false"` con
  `rowspan` (Main `Information`/`Contact` de `contact`). No lleva parámetros.
- Biblioteca: `<formLibraries><Library name="..." libraryUniqueId="{GUID}" /></formLibraries>`.
- Evento: `<events><event name="onload" application="false" active="false"><Handlers><Handler functionName=".." libraryName=".."
  handlerUniqueId="{GUID}" enabled="true" parameters="" passExecutionContext="true" /></Handlers></event></events>`; el de cambio de
  columna agrega `attribute="col"`.
"""
import copy
import os
import sys
import unittest
from xml.etree import ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import formulario as fo  # noqa: E402
import test_formulario as tf  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import por_idioma  # noqa: E402

LIB = "sanic_carp_wr_js_comandos"
SG = {"tabla": tf.TABLA, "lookup": "sanic_unidaaid", "vista": "Correos sin respuesta"}

MULTI = {
    "tipo": "formulario", "nombre": "FRM - CARP - Conversacion - Principal", "tabla": tf.TABLA,
    "descripcion": "Formulario con dos pestanas, timeline y eventos.",
    "solo_lectura": False,
    "encabezado": ["sanic_resultado", {"nombre": "sanic_cantidadrespuestas", "solo_lectura": True}],
    "pestanas": [
        {"titulo": por_idioma("General"), "secciones": [
            {"titulo": por_idioma("Conversacion"), "campos": [
                {"nombre": "sanic_asunto", "solo_lectura": True}, "sanic_clienteid",
                {"nombre": "sanic_fechaprimercorreo", "solo_lectura": False}]},
            {"titulo": por_idioma("Correos"), "timeline": {"alto": 10}}]},
        {"titulo": por_idioma("Arreglos"), "secciones": [
            {"titulo": por_idioma("Arreglos de la conversacion"), "subgrilla": SG}]},
    ],
    "bibliotecas": [LIB],
    "eventos": [
        {"evento": "onload", "biblioteca": LIB, "funcion": "SanicCarp.Conversacion.alCargar", "pasar_contexto": True},
        {"evento": "onsave", "biblioteca": LIB, "funcion": "SanicCarp.Conversacion.alGuardar", "pasar_contexto": True},
        {"evento": "onchange", "columna": "sanic_clienteid", "biblioteca": LIB,
         "funcion": "SanicCarp.Conversacion.alCambiarCliente"},
    ],
}


def con(base, **cambios):
    d = copy.deepcopy(base)
    d.update(cambios)
    return d


def xml_de(datos):
    controles = {c: fo.CONTROL_POR_TIPO[t] for c, t in tf.TIPOS.items()}
    subgrillas = {i: {"vista_id": "{" + tf.VISTA_ID.upper() + "}", "relacion": tf.RELACION}
                  for i, s in enumerate(fo.secciones_de(datos)) if "subgrilla" in s}
    return fo.formxml(datos, tf.ETIQUETAS, controles, subgrillas, tf.IDIOMAS)


def fila_form(datos, **cambios):
    fila = {"formid": tf.FORM_ID, "name": datos["nombre"], "description": datos["descripcion"],
            "formxml": xml_de(datos), "type": 2, "formactivationstate": 1, "ismanaged": False}
    fila.update(cambios)
    return fila


def controles_de(xml):
    return {c.get("id"): c for c in ET.fromstring(xml).iter("control")}


class SoloLecturaPorCampo(unittest.TestCase):
    def test_cada_campo_lleva_su_propio_disabled(self):
        c = controles_de(xml_de(MULTI))
        self.assertEqual("true", c["sanic_asunto"].get("disabled"))
        self.assertEqual("false", c["sanic_clienteid"].get("disabled"))
        self.assertEqual("false", c["sanic_fechaprimercorreo"].get("disabled"))

    def test_el_encabezado_tambien_acepta_el_objeto(self):
        c = controles_de(xml_de(MULTI))
        self.assertEqual("false", c["sanic_resultado"].get("disabled"))
        self.assertEqual("true", c["sanic_cantidadrespuestas"].get("disabled"))

    def test_el_valor_del_campo_pisa_al_del_formulario_en_los_dos_sentidos(self):
        a_lectura = con(MULTI, solo_lectura=True)
        c = controles_de(xml_de(a_lectura))
        self.assertEqual("true", c["sanic_clienteid"].get("disabled"))      # sin objeto: el del formulario
        self.assertEqual("false", c["sanic_fechaprimercorreo"].get("disabled"))  # objeto explicito: gana
        self.assertEqual("false", controles_de(xml_de(MULTI))["sanic_fechaprimercorreo"].get("disabled"))


class VariasPestanas(unittest.TestCase):
    def test_hay_una_pestana_por_entrada_con_ids_distintos(self):
        tabs = ET.fromstring(xml_de(MULTI)).findall("./tabs/tab")
        self.assertEqual(2, len(tabs))
        self.assertEqual(2, len({t.get("id") for t in tabs}))
        self.assertEqual(["General", "Arreglos"], [t.find("labels/label").get("description") for t in tabs])

    def test_los_ids_de_secciones_y_subgrillas_no_se_repiten_entre_pestanas(self):
        raiz = ET.fromstring(xml_de(MULTI))
        ids = [s.get("id") for s in raiz.iter("section")]
        self.assertEqual(3, len(ids))
        self.assertEqual(3, len(set(ids)))
        self.assertIn("Subgrid_2", controles_de(xml_de(MULTI)))   # indice global de seccion

    def test_la_pestana_unica_vieja_da_el_mismo_xml_que_pestanas_con_una_sola(self):
        viejo = tf.COMPONENTE
        nuevo = {k: v for k, v in viejo.items() if k not in ("pestana", "secciones")}
        nuevo["pestanas"] = [{"titulo": viejo["pestana"], "secciones": viejo["secciones"]}]
        self.assertEqual(tf.xml_de(viejo), xml_de(nuevo))

    def test_sin_bibliotecas_ni_eventos_el_xml_no_trae_ninguno(self):
        x = tf.xml_de()
        self.assertNotIn("formLibraries", x)
        self.assertNotIn("<events", x)


class Timeline(unittest.TestCase):
    def test_la_celda_lleva_el_control_notescontrol_y_el_alto(self):
        raiz = ET.fromstring(xml_de(MULTI))
        control = next(c for c in raiz.iter("control") if c.get("id") == "notescontrol")
        self.assertEqual(fo.CONTROL_TIMELINE, control.get("classid"))
        self.assertEqual("{06375649-C143-495E-A496-C962E5B4488E}", fo.CONTROL_TIMELINE)
        self.assertEqual(0, len(list(control)))   # sin parametros, como el de contact
        celda = next(c for c in raiz.iter("cell") if control in list(c))
        self.assertEqual("false", celda.get("showlabel"))
        self.assertEqual("10", celda.get("rowspan"))


class BibliotecasYEventos(unittest.TestCase):
    def setUp(self):
        self.raiz = ET.fromstring(xml_de(MULTI))

    def test_la_biblioteca_sale_en_formlibraries_con_guid_determinista(self):
        libs = self.raiz.findall("./formLibraries/Library")
        self.assertEqual([LIB], [l.get("name") for l in libs])
        self.assertRegex(libs[0].get("libraryUniqueId"), r"^\{[0-9A-F-]{36}\}$")
        self.assertEqual(xml_de(MULTI), xml_de(MULTI))

    def test_un_evento_por_nombre_con_sus_manejadores(self):
        eventos = {e.get("name"): e for e in self.raiz.findall("./events/event")}
        self.assertEqual({"onload", "onsave", "onchange"}, set(eventos))
        h = eventos["onload"].find("Handlers/Handler")
        self.assertEqual("SanicCarp.Conversacion.alCargar", h.get("functionName"))
        self.assertEqual(LIB, h.get("libraryName"))
        self.assertEqual("true", h.get("enabled"))
        self.assertEqual("", h.get("parameters"))
        self.assertEqual("true", h.get("passExecutionContext"))
        self.assertRegex(h.get("handlerUniqueId"), r"^\{[0-9A-F-]{36}\}$")
        self.assertEqual("false", eventos["onload"].get("application"))

    def test_pasar_contexto_por_omision_es_false(self):
        h = self.raiz.find("./events/event[@name='onchange']/Handlers/Handler")
        self.assertEqual("false", h.get("passExecutionContext"))

    def test_el_evento_de_cambio_nombra_la_columna(self):
        e = self.raiz.find("./events/event[@name='onchange']")
        self.assertEqual("sanic_clienteid", e.get("attribute"))
        self.assertIsNone(self.raiz.find("./events/event[@name='onload']").get("attribute"))

    def test_dos_manejadores_del_mismo_evento_van_en_el_mismo_event_y_en_orden(self):
        d = con(MULTI, eventos=MULTI["eventos"] + [
            {"evento": "onload", "biblioteca": LIB, "funcion": "SanicCarp.Conversacion.otra"}])
        hs = ET.fromstring(xml_de(d)).findall("./events/event[@name='onload']/Handlers/Handler")
        self.assertEqual(["SanicCarp.Conversacion.alCargar", "SanicCarp.Conversacion.otra"],
                         [h.get("functionName") for h in hs])
        self.assertEqual(1, len(ET.fromstring(xml_de(d)).findall("./events/event[@name='onload']")))

    def test_dos_onchange_de_columnas_distintas_son_dos_event(self):
        d = con(MULTI, eventos=MULTI["eventos"] + [
            {"evento": "onchange", "columna": "sanic_asunto", "biblioteca": LIB, "funcion": "SanicCarp.X.y"}])
        self.assertEqual(2, len(ET.fromstring(xml_de(d)).findall("./events/event[@name='onchange']")))

    def test_bibliotecas_y_eventos_van_despues_del_encabezado(self):
        hijos = [h.tag for h in self.raiz]
        self.assertLess(hijos.index("header"), hijos.index("formLibraries"))
        self.assertLess(hijos.index("formLibraries"), hijos.index("events"))


class ValidacionAmpliada(tf.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = fo.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    @staticmethod
    def evento(**cambios):
        e = {"evento": "onload", "biblioteca": LIB, "funcion": "SanicCarp.A.b", "pasar_contexto": True}
        e.update(cambios)
        return e

    def test_tabla_de_rechazos(self):
        casos = [
            ("pestana y pestanas a la vez", con(MULTI, pestana=por_idioma("X")), "sobran ['pestana']"),
            ("pestanas con secciones a la raiz", con(MULTI, secciones=[]), "sobran ['secciones']"),
            ("ni pestana ni pestanas", {k: v for k, v in tf.COMPONENTE.items() if k not in ("pestana",)}, "faltan ['pestana']"),
            ("pestanas vacia", con(MULTI, pestanas=[]), "pestanas"),
            ("pestana sin titulo", con(MULTI, pestanas=[{"secciones": MULTI["pestanas"][0]["secciones"]}]), "faltan ['titulo']"),
            ("pestana sin secciones", con(MULTI, pestanas=[{"titulo": por_idioma("X")}]), "faltan ['secciones']"),
            ("pestana con clave de mas", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], x=1)]), "sobran ['x']"),
            ("titulo de pestana con un solo idioma", con(MULTI, pestanas=[
                {"titulo": {"1033": "X"}, "secciones": MULTI["pestanas"][0]["secciones"]}]), "faltan ['3082']"),
            ("columna repetida entre pestanas", con(MULTI, pestanas=MULTI["pestanas"] + [
                {"titulo": por_idioma("Z"), "secciones": [{"titulo": por_idioma("Z"), "campos": ["sanic_asunto"]}]}]),
             "ya está en el formulario"),
            ("campo objeto sin solo_lectura", con(MULTI, encabezado=[{"nombre": "sanic_resultado"}]), "faltan ['solo_lectura']"),
            ("campo objeto sin nombre", con(MULTI, encabezado=[{"solo_lectura": True}]), "faltan ['nombre']"),
            ("campo objeto con clave de mas", con(MULTI, encabezado=[
                {"nombre": "sanic_resultado", "solo_lectura": True, "x": 1}]), "sobran ['x']"),
            ("solo_lectura del campo no es booleano", con(MULTI, encabezado=[
                {"nombre": "sanic_resultado", "solo_lectura": "si"}]), "solo_lectura"),
            ("nombre del campo no es texto", con(MULTI, encabezado=[{"nombre": 3, "solo_lectura": True}]), "nombre"),
            ("campo que no es texto ni objeto", con(MULTI, encabezado=[3]), "encabezado"),
            ("columna repetida entre un texto y un objeto", con(MULTI, encabezado=[
                "sanic_asunto", {"nombre": "sanic_asunto", "solo_lectura": True}]), "ya está en el formulario"),
            ("mas de tres en el encabezado con objetos", con(MULTI, encabezado=[
                {"nombre": c, "solo_lectura": True} for c in "abcd"]), "solo entran 3"),
            ("dos timeline", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], secciones=MULTI["pestanas"][0]["secciones"] + [
                {"titulo": por_idioma("Otro"), "timeline": {"alto": 5}}])] + MULTI["pestanas"][1:]), "un solo timeline"),
            ("timeline sin alto", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], secciones=[
                {"titulo": por_idioma("T"), "timeline": {}}])]), "faltan ['alto']"),
            ("timeline con alto fuera de rango", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], secciones=[
                {"titulo": por_idioma("T"), "timeline": {"alto": 99}}])]), "entre 1 y 30"),
            ("timeline con alto de texto", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], secciones=[
                {"titulo": por_idioma("T"), "timeline": {"alto": "10"}}])]), "alto"),
            ("timeline y campos en la misma seccion", con(MULTI, pestanas=[dict(MULTI["pestanas"][0], secciones=[
                {"titulo": por_idioma("T"), "timeline": {"alto": 5}, "campos": ["sanic_asunto"]}])]), "exactamente uno"),
            ("timeline en creacion rapida", con(tf.COMPONENTE, clase="creacion_rapida", encabezado=[], secciones=[
                {"titulo": por_idioma("T"), "timeline": {"alto": 5}}]), "solo admite campos"),
            ("bibliotecas no es lista", con(MULTI, bibliotecas="x"), "bibliotecas"),
            ("biblioteca repetida", con(MULTI, bibliotecas=[LIB, LIB]), "repetida"),
            ("biblioteca vacia", con(MULTI, bibliotecas=[""]), "bibliotecas"),
            ("eventos no es lista", con(MULTI, eventos={}), "eventos"),
            ("evento que no es objeto", con(MULTI, eventos=["onload"]), "eventos[0]"),
            ("evento desconocido", con(MULTI, eventos=[self.evento(evento="onclick")]), "onload"),
            ("evento sin funcion", con(MULTI, eventos=[{"evento": "onload", "biblioteca": LIB}]), "faltan ['funcion']"),
            ("evento con clave de mas", con(MULTI, eventos=[self.evento(x=1)]), "sobran ['x']"),
            ("funcion con espacios", con(MULTI, eventos=[self.evento(funcion="hacer cosas")]), "funcion"),
            ("funcion que no es texto", con(MULTI, eventos=[self.evento(funcion=3)]), "funcion"),
            ("biblioteca no declarada", con(MULTI, eventos=[self.evento(biblioteca="otra")]), "no está en 'bibliotecas'"),
            ("biblioteca del evento no es texto", con(MULTI, eventos=[self.evento(biblioteca=3)]), "biblioteca"),
            ("pasar_contexto no es booleano", con(MULTI, eventos=[self.evento(pasar_contexto="si")]), "pasar_contexto"),
            ("onchange sin columna", con(MULTI, eventos=[self.evento(evento="onchange")]), "necesita 'columna'"),
            ("onload con columna", con(MULTI, eventos=[self.evento(columna="sanic_asunto")]), "solo lleva 'columna' en onchange"),
            ("onchange de una columna que no esta en el formulario", con(MULTI, eventos=[
                self.evento(evento="onchange", columna="sanic_inventada")]), "no está en el formulario"),
            ("manejador repetido", con(MULTI, eventos=[self.evento(), self.evento()]), "repetido"),
            ("eventos sin bibliotecas", con(MULTI, bibliotecas=[]), "no está en 'bibliotecas'"),
        ]
        for nombre, componente, fragmento in casos:
            with self.subTest(nombre):
                self.rechaza(componente, fragmento)

    def test_el_onchange_puede_nombrar_una_columna_del_encabezado(self):
        fo.validar_playbook(con(MULTI, eventos=[self.evento(evento="onchange", columna="sanic_resultado")]), tf.IDENT)

    def test_el_playbook_multi_valida(self):
        fo.validar_playbook(MULTI, tf.IDENT)


class ControlDecimal(tf.Base):
    """A1: una columna decimal sacaba `bloqueado` ("no se sabe qué control usa DecimalType"). El classid se leyó de un formulario
    real de Dev el 2026-10-20 (`feedback.normalizedrating`, DecimalType): la plataforma lo escribe con ese casing."""

    def test_el_classid_del_decimal_es_el_leido_de_dev(self):
        self.assertEqual("{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}", fo.CONTROL_POR_TIPO["DecimalType"])

    def test_un_formulario_con_una_columna_decimal_se_arma_con_ese_control(self):
        datos = con(MULTI, eventos=[], bibliotecas=[], pestanas=[
            {"titulo": por_idioma("General"), "secciones": [
                {"titulo": por_idioma("Montos"), "campos": ["sanic_saldo"]}]}])
        tipos = dict(tf.TIPOS, sanic_saldo="DecimalType")
        etiquetas = dict(tf.ETIQUETAS, sanic_saldo=por_idioma("Saldo"))
        cliente = self.armar(ClienteSimulado(), form=None, tipos=tipos, etiquetas=etiquetas)

        def crear(ruta, cuerpo, solucion):
            self.form = {"formid": tf.FORM_ID, "name": datos["nombre"], "description": datos["descripcion"],
                         "formxml": cuerpo["formxml"], "type": 2, "formactivationstate": 1, "ismanaged": False}
            return (204, {}, {})

        cliente.responder("POST", "systemforms", crear)
        cliente.responder("POST", "PublishXml", (204, {}, {}))   # el alta también publica
        estado, _, detalle = fo.construir(self.escribir(datos), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("creado", estado, detalle)
        self.assertEqual("{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}", controles_de(self.form["formxml"])["sanic_saldo"].get("classid"))

    def test_un_tipo_que_sigue_sin_control_sigue_bloqueado(self):
        datos = con(MULTI, eventos=[], bibliotecas=[], pestanas=[
            {"titulo": por_idioma("General"), "secciones": [{"titulo": por_idioma("X"), "campos": ["sanic_saldo"]}]}])
        cliente = self.armar(ClienteSimulado(), tipos=dict(tf.TIPOS, sanic_saldo="ImageType"),
                             etiquetas=dict(tf.ETIQUETAS, sanic_saldo=por_idioma("Saldo")))
        estado, _, detalle = fo.construir(self.escribir(datos), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("ImageType", detalle)


class RegistrosPorPagina(tf.Base):
    """M1: la subgrilla fijaba `RecordsPerPage` en 10. Learn ("Add and configure a subgrid component on a form"): el máximo de
    filas va de 2 a 250."""

    def sg(self, **extra):
        return con(MULTI, pestanas=[MULTI["pestanas"][1]], eventos=[], bibliotecas=[]) if not extra else con(
            MULTI, eventos=[], bibliotecas=[], pestanas=[{"titulo": por_idioma("A"), "secciones": [
                {"titulo": por_idioma("Cuotas"), "subgrilla": dict(SG, **extra)}]}])

    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = fo.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def paginas(self, datos):
        return [e.text for e in ET.fromstring(xml_de(datos)).iter("RecordsPerPage")]

    def test_por_omision_son_diez_y_el_xml_no_cambia(self):
        self.assertEqual(["10"], self.paginas(self.sg()))

    def test_el_valor_declarado_va_al_xml(self):
        self.assertEqual(["60"], self.paginas(self.sg(registros_por_pagina=60)))
        self.assertEqual(["28"], self.paginas(self.sg(registros_por_pagina=28)))

    def test_los_limites_de_learn_se_admiten(self):
        for n in (2, 250):
            fo.validar_playbook(self.sg(registros_por_pagina=n), tf.IDENT)

    def test_tabla_de_rechazos(self):
        for nombre, valor, fragmento in (("uno", 1, "entre 2 y 250"), ("251", 251, "entre 2 y 250"),
                                         ("cero", 0, "entre 2 y 250"), ("negativo", -5, "entre 2 y 250"),
                                         ("texto", "60", "registros_por_pagina"), ("booleano", True, "registros_por_pagina"),
                                         ("decimal", 10.5, "registros_por_pagina")):
            with self.subTest(nombre):
                self.rechaza(self.sg(registros_por_pagina=valor), fragmento)

    def test_una_clave_de_mas_en_la_subgrilla_sigue_rechazada(self):
        self.rechaza(self.sg(otra=1), "sobran ['otra']")


class ContraElEntorno(tf.Base):
    def armar_multi(self, form=None, **kw):
        cliente = self.armar(ClienteSimulado(), form=form)
        self.web = {LIB: 3}
        self.tiene_actividades = True

        def webresource(ruta, cuerpo, solucion):
            nombre = ruta.split("name eq '")[1].split("'")[0]
            return (200, {"value": [{"name": nombre, "webresourcetype": self.web[nombre]}] if nombre in self.web else []}, {})

        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("webresourceset?"), webresource))
        cliente._reglas.insert(0, ("GET", lambda r: "HasActivities" in r,
                                   lambda r, c, s: (200, {"HasActivities": self.tiene_actividades}, {})))
        return cliente

    def correr(self, cliente, solo_verificar=False, datos=MULTI):
        return fo.construir(self.escribir(datos), solo_verificar, lambda: cliente, dormir=lambda s: None)

    def test_ya_existia_y_solo_verificar_dicen_lo_mismo(self):
        for solo_verificar in (False, True):
            cliente = self.armar_multi(form=fila_form(MULTI))
            estado, _, detalle = self.correr(cliente, solo_verificar)
            self.assertEqual("ya_existia", estado, detalle)
            self.assertFalse(cliente.hubo_escritura())

    def test_una_biblioteca_que_no_existe_bloquea_sin_escribir(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        self.web = {}
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn(LIB, detalle)
        self.assertIn("`web_resource.py`", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_biblioteca_que_no_es_javascript_bloquea(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        self.web = {LIB: 1}
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("no es JavaScript", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_http_inesperado_al_buscar_la_biblioteca_es_error_y_no_bloqueo(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("webresourceset?"), (500, {}, {})))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("500", detalle)

    def test_un_timeline_sobre_una_tabla_sin_actividades_bloquea(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        self.tiene_actividades = False
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("HasActivities", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_hasactivities_con_otro_tipo_es_forma_inesperada(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        self.tiene_actividades = "si"
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HasActivities", detalle)

    def test_sin_timeline_no_se_pregunta_por_las_actividades(self):
        sin = con(MULTI, pestanas=[MULTI["pestanas"][1]])
        sin["eventos"] = [e for e in MULTI["eventos"] if e.get("columna") != "sanic_clienteid"]
        cliente = self.armar_multi(form=fila_form(sin))
        cliente._reglas.insert(0, ("GET", lambda r: "HasActivities" in r, AssertionError("no se debia preguntar")))
        estado, _, detalle = self.correr(cliente, datos=sin)
        self.assertEqual("ya_existia", estado, detalle)

    def test_sin_bibliotecas_no_se_consulta_ningun_web_resource(self):
        sin = {k: v for k, v in MULTI.items() if k not in ("bibliotecas", "eventos")}
        cliente = self.armar_multi(form=fila_form(sin))
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("webresourceset?"), AssertionError("no se debia consultar")))
        estado, _, detalle = self.correr(cliente, datos=sin)
        self.assertEqual("ya_existia", estado, detalle)

    def test_el_alta_manda_el_xml_con_bibliotecas_y_eventos(self):
        cliente = self.armar_multi(form=None)

        def crear(ruta, cuerpo, solucion):
            self.form = fila_form(MULTI)
            return (204, {}, {})

        cliente.responder("POST", "systemforms", crear)
        cliente.responder("POST", "PublishXml", (204, {}, {}))   # el alta también publica
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("creado", estado, detalle)
        post = next(l for l in cliente.llamadas if l["metodo"] == "POST")
        self.assertIn("<formLibraries>", post["cuerpo"]["formxml"])
        self.assertIn('functionName="SanicCarp.Conversacion.alGuardar"', post["cuerpo"]["formxml"])

    def test_falta_un_manejador_en_el_formulario_real_se_nombra(self):
        recortado = con(MULTI, eventos=MULTI["eventos"][:2])
        cliente = self.armar_multi(form=fila_form(MULTI, formxml=xml_de(recortado)))
        estado, _, detalle = self.correr(cliente, True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("falta el manejador onchange[sanic_clienteid] SanicCarp.Conversacion.alCambiarCliente", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_sobra_un_manejador_en_el_formulario_real_se_nombra(self):
        de_mas = con(MULTI, eventos=MULTI["eventos"] + [
            {"evento": "onload", "biblioteca": LIB, "funcion": "SanicCarp.Sobra.x"}])
        cliente = self.armar_multi(form=fila_form(MULTI, formxml=xml_de(de_mas)))
        estado, _, detalle = self.correr(cliente, True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sobra el manejador onload SanicCarp.Sobra.x", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_campo_con_otro_solo_lectura_se_detecta(self):
        otro = con(MULTI, encabezado=["sanic_resultado", {"nombre": "sanic_cantidadrespuestas", "solo_lectura": False}])
        cliente = self.armar_multi(form=fila_form(MULTI, formxml=xml_de(otro)))
        estado, _, detalle = self.correr(cliente, True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("formxml no coincide", detalle)

    def test_las_subgrillas_de_la_segunda_pestana_se_resuelven_contra_el_entorno(self):
        cliente = self.armar_multi(form=fila_form(MULTI))
        self.correr(cliente, True)
        self.assertTrue([l for l in cliente.llamadas if "OneToManyRelationships" in l["ruta"]])
        self.assertTrue([l for l in cliente.llamadas if l["ruta"].startswith("savedqueries?")])


if __name__ == "__main__":
    unittest.main()
