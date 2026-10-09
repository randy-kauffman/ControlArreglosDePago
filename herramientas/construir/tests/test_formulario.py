"""Pruebas de `herramientas/construir/formulario.py`. Nunca tocan la red.

Lo que más importa acá: que los GUID del XML sean DETERMINISTAS (si no, cada
corrida genera un XML distinto y la herramienta nunca converge) y que el
`classid` de cada control salga del TIPO REAL de la columna. La plataforma no
normaliza el classid: guarda el que se le manda, y uno equivocado se ve roto
recién en la app.
"""
import copy
import json
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import formulario as fo  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import (IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents, label,  # noqa: E402
                      por_idioma)

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="formulario", inventario="12.3")
IDIOMAS = IDENT["idiomas"]

TABLA = "sanic_carp_tbl_conversacion"
RELACION = "sanic_carp_conversacion_conversacion"
METADATA_ID = "dddddddd-dddd-dddd-dddd-dddddddddddd"
FORM_ID = "ffffffff-ffff-ffff-ffff-ffffffffffff"
VISTA_ID = "11112222-3333-4444-5555-666677778888"

# Columnas de `sanic_carp_tbl_conversacion` (diccionario de datos, sección 4.2) y su tipo.
TIPOS = {
    "sanic_resultado": "PicklistType", "sanic_clienteid": "LookupType", "sanic_cantidadrespuestas": "IntegerType",
    "sanic_motivosinrespuesta": "PicklistType", "sanic_asunto": "StringType", "sanic_detallesinrespuesta": "MemoType",
    "sanic_fechaprimercorreo": "DateTimeType", "sanic_correonuevoenfinalizada": "BooleanType",
}
# DisplayName de cada columna en el entorno, un texto por idioma ({columna: {"1033": ..., "3082": ...}}).
ETIQUETAS = {c: por_idioma(t) for c, t in {
    "sanic_resultado": "Resultado", "sanic_clienteid": "Cliente", "sanic_cantidadrespuestas": "Cantidad de respuestas",
    "sanic_motivosinrespuesta": "Motivo de sin respuesta", "sanic_asunto": "Asunto",
    "sanic_detallesinrespuesta": "Detalle de sin respuesta", "sanic_fechaprimercorreo": "Fecha del primer correo",
    "sanic_correonuevoenfinalizada": "Correo nuevo en finalizada",
}.items()}

COMPONENTE = {
    "tipo": "formulario",
    "nombre": "FRM - CARP - Conversacion - Principal",
    "tabla": TABLA,
    "descripcion": "Formulario principal de Conversacion. Todo de solo lectura: el estado se cambia con los botones.",
    "pestana": por_idioma("General"),
    "solo_lectura": True,
    "encabezado": ["sanic_resultado", "sanic_clienteid", "sanic_cantidadrespuestas"],
    "secciones": [
        {"titulo": por_idioma("Correo"), "campos": ["sanic_asunto", "sanic_fechaprimercorreo"]},
        {"titulo": por_idioma("Finalizacion"), "campos": ["sanic_detallesinrespuesta"]},
        {"titulo": por_idioma("Conversaciones unidas"), "subgrilla": {"tabla": TABLA, "lookup": "sanic_unidaaid",
                                                          "vista": "Correos sin respuesta"}},
    ],
}


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None, identidad=None):
    componente = COMPONENTE if componente is None else componente
    identidad = IDENT if identidad is None else identidad
    return (
        "# Playbook: formulario · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(identidad, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def xml_de(datos=None, idiomas=None, etiquetas=None):
    datos = COMPONENTE if datos is None else datos
    idiomas = IDIOMAS if idiomas is None else idiomas
    etiquetas = ETIQUETAS if etiquetas is None else etiquetas
    controles = {c: fo.CONTROL_POR_TIPO[TIPOS[c]] for c in TIPOS}
    subgrillas = {i: {"vista_id": "{" + VISTA_ID.upper() + "}", "relacion": RELACION}
                  for i, s in enumerate(datos["secciones"]) if "subgrilla" in s}
    return fo.formxml(datos, etiquetas, controles, subgrillas, idiomas)


def fila_form(datos=None, **cambios):
    datos = COMPONENTE if datos is None else datos
    fila = {"formid": FORM_ID, "name": datos["nombre"], "description": datos["descripcion"],
            "formxml": xml_de(datos), "type": 2, "formactivationstate": 1, "ismanaged": False}
    fila.update(cambios)
    return fila


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None, identidad=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente, identidad))
        return self.ruta

    def armar(self, cliente, form=None, en_solucion=True, comportamiento=0, tipos=None, identidad=None, etiquetas=None):
        armar_cliente_precondiciones_ok(cliente, IDENT if identidad is None else identidad)
        self.form = form
        tipos = TIPOS if tipos is None else tipos
        etiquetas = ETIQUETAS if etiquetas is None else etiquetas

        def definiciones(ruta, cuerpo, solucion):
            if "/Attributes" in ruta:
                return (200, {"value": [{"LogicalName": c, "AttributeTypeName": {"Value": t},
                                         "DisplayName": label(etiquetas.get(c, por_idioma(c)), [int(k) for k in etiquetas.get(c, por_idioma(c))])}
                                        for c, t in tipos.items()]}, {})
            if "OneToManyRelationships" in ruta:
                return (200, {"value": [{"SchemaName": RELACION,
                                         "ReferencingEntity": TABLA,
                                         "ReferencingAttribute": "sanic_unidaaid"}]}, {})
            return (200, {"MetadataId": METADATA_ID}, {})

        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions("), definiciones)
        cliente.responder("GET", lambda r: r.startswith("savedqueries?"),
                          (200, {"value": [{"savedqueryid": VISTA_ID}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("systemforms?"),
                          lambda r, c, s: (200, {"value": [] if self.form is None else [self.form]}, {}))
        fila_tabla = [{"componenttype": 1, "rootcomponentbehavior": comportamiento, "objectid": METADATA_ID}]
        cliente.responder("GET", es_ruta_solutioncomponents,
                          (200, {"value": fila_tabla if en_solucion else []}, {}))
        return cliente


# ---------------------------------------------------------------------------
# El XML generado
# ---------------------------------------------------------------------------
class XmlGenerado(unittest.TestCase):
    def test_los_guid_son_deterministas(self):
        # Si fueran aleatorios, cada corrida daria un XML distinto y la
        # herramienta diria "difiere" para siempre.
        self.assertEqual(xml_de(), xml_de())
        self.assertEqual(fo.guid("a", "b"), fo.guid("a", "b"))
        self.assertNotEqual(fo.guid("a", "b"), fo.guid("a", "c"))

    def test_cada_control_usa_el_classid_de_su_tipo_real(self):
        x = xml_de()
        self.assertIn(f'datafieldname="sanic_resultado" ', x)
        self.assertIn(fo.CONTROL_POR_TIPO["PicklistType"], x)
        self.assertIn(fo.CONTROL_POR_TIPO["MemoType"], x)      # sanic_detallesinrespuesta
        self.assertIn(fo.CONTROL_POR_TIPO["IntegerType"], x)   # sanic_cantidadrespuestas
        self.assertIn(fo.CONTROL_POR_TIPO["LookupType"], x)    # sanic_clienteid

    def test_un_formulario_de_solo_lectura_deshabilita_todos_sus_controles(self):
        self.assertNotIn('disabled="false"', xml_de())
        self.assertIn('disabled="true"', xml_de())

    def test_un_formulario_editable_no_deshabilita_nada(self):
        x = xml_de(comp(solo_lectura=False))
        self.assertNotIn('disabled="true"', x)

    def test_el_encabezado_siempre_tiene_tres_huecos(self):
        x = xml_de(comp(encabezado=["sanic_resultado"]))
        header = x[x.index("<header"):]
        self.assertEqual(3, header.count("<cell "))
        self.assertIn('datafieldname="sanic_resultado"', header)

    def test_la_subgrilla_lleva_su_vista_y_su_relacion(self):
        x = xml_de()
        self.assertIn('indicationOfSubgrid="true"', x)
        self.assertIn(fo.CONTROL_SUBGRILLA, x)
        self.assertIn("<TargetEntityType>sanic_carp_tbl_conversacion</TargetEntityType>", x)
        self.assertIn("<RelationshipName>sanic_carp_conversacion_conversacion</RelationshipName>", x)
        self.assertIn("{" + VISTA_ID.upper() + "}", x)

    def test_las_etiquetas_salen_de_la_metadata_no_del_playbook(self):
        # El playbook no declara etiquetas: si alguien renombra la columna, la
        # etiqueta del formulario lo sigue.
        self.assertIn('description="Cantidad de respuestas"', xml_de())


# ---------------------------------------------------------------------------
# Validación offline
# ---------------------------------------------------------------------------
class WebResourceEnElFormulario(Base):
    """Un web resource en una sección: es como se muestra el correo que se le
    envió al cliente, renderizado tal cual (12.9)."""

    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = fo.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def seccion(self, **cambios):
        s = {"titulo": por_idioma("Comunicacion enviada"),
             "webresource": {"nombre": "sanic_carp_wr_html_comunicacion", "alto": 12}}
        s.update(cambios)
        return s

    def test_el_control_lleva_el_classid_de_web_resource(self):
        x = fo._celda_webresource({"nombre": "F"}, "sec0.wr", "WebResource_0", por_idioma("Comunicacion"),
                                  "sanic_carp_wr_html_comunicacion", 12, IDIOMAS)
        self.assertIn(fo.CONTROL_WEBRESOURCE, x)
        self.assertIn("<Url>sanic_carp_wr_html_comunicacion</Url>", x)

    def test_pasa_los_parametros_del_registro(self):
        # Sin PassParameters la página no recibe el `id` y no sabe qué mostrar.
        x = fo._celda_webresource({"nombre": "F"}, "sec0.wr", "WebResource_0", por_idioma("C"),
                                  "sanic_carp_wr_html_comunicacion", 12, IDIOMAS)
        self.assertIn("<PassParameters>true</PassParameters>", x)

    def test_el_alto_va_en_el_rowspan(self):
        x = fo._celda_webresource({"nombre": "F"}, "sec0.wr", "WebResource_0", por_idioma("C"),
                                  "sanic_carp_wr_html_comunicacion", 12, IDIOMAS)
        self.assertIn('rowspan="12"', x)

    def test_una_seccion_no_puede_llevar_campos_y_webresource(self):
        mala = self.seccion(campos=["sanic_asunto"])
        self.rechaza(comp(secciones=[mala]), "exactamente uno")

    def test_un_alto_fuera_de_rango_se_rechaza(self):
        self.rechaza(comp(secciones=[self.seccion(
            webresource={"nombre": "sanic_carp_wr_html_comunicacion", "alto": 40})]),
            "entre 1 y 20")

    def test_un_webresource_sin_alto_se_rechaza(self):
        self.rechaza(comp(secciones=[self.seccion(
            webresource={"nombre": "sanic_carp_wr_html_comunicacion"})]), "faltan ['alto']")


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = fo.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_una_clave_de_mas_o_de_menos_se_rechaza(self):
        self.rechaza({k: v for k, v in COMPONENTE.items() if k != "secciones"}, "faltan ['secciones']")
        self.rechaza(comp(sobrante=1), "sobran ['sobrante']")

    def test_los_titulos_son_un_texto_por_idioma_de_la_identidad(self):
        # Pestana y titulo de cada clase de seccion: objeto {"1033": ..., "3082": ...}, ni de mas ni de menos, sin tildes.
        casos = [
            ("cadena unica (formato viejo)", "General", "tiene que ser un objeto con un texto por cada idioma"),
            ("sin el 3082", {"1033": "General"}, "faltan ['3082']"),
            ("con un idioma de mas", {"1033": "General", "3082": "General", "1034": "General"}, "sobran ['1034']"),
            ("con tilde", por_idioma("Gestión"), "fuera de ASCII"),
            ("texto vacio", {"1033": "General", "3082": " "}, "texto no vacío"),
        ]
        sg = {"tabla": TABLA, "lookup": "sanic_unidaaid", "vista": "Correos sin respuesta"}
        wr = {"nombre": "sanic_carp_wr_html_comunicacion", "alto": 4}
        for donde in ("pestana", "campos", "subgrilla", "webresource"):
            for nombre, valor, fragmento in casos:
                with self.subTest(donde=donde, caso=nombre):
                    if donde == "pestana":
                        componente = comp(pestana=valor)
                    else:
                        cuerpo = {"campos": ["sanic_asunto"]} if donde == "campos" else {donde: sg if donde == "subgrilla" else wr}
                        componente = comp(secciones=[dict(cuerpo, titulo=valor)])
                    self.rechaza(componente, fragmento)

    def test_los_titulos_se_validan_contra_los_idiomas_de_la_identidad_y_no_contra_unos_fijos(self):
        # Con tres idiomas en la Identidad, dos textos ya no alcanzan.
        centinela = FabricaCentinela()
        identidad = dict(IDENT, idiomas=[1033, 3082, 1034])
        estado, _, detalle = fo.construir(self.escribir(COMPONENTE, identidad), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn("faltan ['1034']", detalle)
        self.assertFalse(centinela.llamada)

    def test_una_seccion_lleva_campos_o_subgrilla_nunca_las_dos(self):
        self.rechaza(comp(secciones=[{"titulo": por_idioma("X"), "campos": ["a"], "subgrilla": {}}]), "exactamente uno")
        self.rechaza(comp(secciones=[{"titulo": por_idioma("X")}]), "exactamente uno")

    def test_una_columna_repetida_en_el_formulario_se_rechaza(self):
        malo = comp(secciones=[{"titulo": por_idioma("A"), "campos": ["sanic_asunto"]},
                               {"titulo": por_idioma("B"), "campos": ["sanic_asunto"]}])
        self.rechaza(malo, "ya está en el formulario")

    def test_una_columna_del_encabezado_no_se_repite_en_una_seccion(self):
        malo = comp(secciones=[{"titulo": por_idioma("A"), "campos": ["sanic_resultado"]}])
        self.rechaza(malo, "ya está en el formulario")

    def test_mas_de_tres_columnas_en_el_encabezado_se_rechaza(self):
        self.rechaza(comp(encabezado=["a", "b", "c", "d"]), "solo entran 3")

    def test_dos_subgrillas_sobre_la_misma_relacion_se_rechazan(self):
        sg = {"tabla": "t", "lookup": "l", "vista": "v"}
        self.rechaza(comp(secciones=[{"titulo": por_idioma("A"), "subgrilla": dict(sg)},
                                     {"titulo": por_idioma("B"), "subgrilla": dict(sg)}]), "ya hay otra subgrilla")

    def test_los_titulos_van_sin_tildes(self):
        self.rechaza(comp(secciones=[{"titulo": por_idioma("Resolución"), "campos": ["sanic_asunto"]}]), "fuera de ASCII")


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
class ContraElEntorno(Base):
    def test_un_idioma_de_la_identidad_sin_provisionar_bloquea_sin_escribir(self):
        # `comprobar_solucion_e_idioma` exige cada idioma de `idiomas` provisionado: si falta el 3082 la creacion
        # responderia 204 y el componente quedaria sin etiqueta en ese idioma.
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        cliente._reglas.insert(0, ("GET", "RetrieveProvisionedLanguages",
                                   (200, {"RetrieveProvisionedLanguages": [1033]}, {})))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("[3082]", detalle)
        self.assertIn("no están provisionados", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_columna_que_la_tabla_no_tiene_queda_bloqueada(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        malo = comp(secciones=[{"titulo": por_idioma("X"), "campos": ["sanic_inventada"]}])
        estado, _, detalle = fo.construir(self.escribir(malo), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("sanic_inventada", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_tipo_de_columna_sin_control_conocido_queda_bloqueado_y_no_inventa(self):
        # La plataforma guarda el classid tal cual: adivinar se ve roto en la app.
        tipos = dict(TIPOS, sanic_resultado="MultiSelectPicklist")
        cliente = self.armar(ClienteSimulado(), form=fila_form(), tipos=tipos)
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("MultiSelectPicklist", detalle)
        self.assertIn("No se inventa un classid", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_vista_de_subgrilla_que_no_existe_queda_bloqueada(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("savedqueries?"), (200, {"value": []}, {})))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("hay que crearla primero", detalle)

    def test_si_ya_esta_bien_no_se_escribe_nada(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_se_ACTUALIZA_el_principal_que_ya_existe_en_vez_de_crear_otro(self):
        # Dos Main compitiendo es el mismo problema que dos vistas por defecto.
        viejo = fila_form(name="Information", formxml="<form/>")
        cliente = self.armar(ClienteSimulado(), form=viejo)

        def corregir(ruta, cuerpo, solucion):
            self.form = fila_form()
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("systemforms("), corregir)
        cliente.responder("POST", "PublishXml", (204, {}, {}))

        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("actualizado", estado, detalle)
        self.assertNotIn("systemforms", [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"])
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual(f"systemforms({FORM_ID})", patch["ruta"])
        self.assertEqual(COMPONENTE["nombre"], patch["cuerpo"]["name"])
        self.assertIn("formxml", patch["cuerpo"])

    def test_despues_de_corregir_se_publica(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml="<form/>"))

        def corregir(ruta, cuerpo, solucion):
            self.form = fila_form()
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("systemforms("), corregir)
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        fo.construir(self.escribir(), False, lambda: cliente)
        publicacion = next(l for l in cliente.llamadas if l["ruta"] == "PublishXml")
        self.assertIn(f"<entity>{TABLA}</entity>", publicacion["cuerpo"]["ParameterXml"])

    def test_un_formulario_inactivo_se_reactiva(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(formactivationstate=0))

        def corregir(ruta, cuerpo, solucion):
            self.form = fila_form()
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("systemforms("), corregir)
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("actualizado", estado, detalle)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual(1, patch["cuerpo"]["formactivationstate"])

    def test_dos_formularios_principales_es_forma_inesperada(self):
        cliente = ClienteSimulado()
        self.armar(cliente, form=fila_form())
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("systemforms?"),
                                   (200, {"value": [fila_form(), fila_form()]}, {})))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("el playbook deja de mandar", detalle)

    def test_un_formulario_managed_no_se_toca(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(ismanaged=True, name="otro"))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("managed", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_la_tabla_no_esta_en_la_solucion_el_formulario_no_viajaria(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(), en_solucion=False)
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no viajaría", detalle)

    def test_solo_verificar_nunca_escribe(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml="<form/>"))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("formxml", detalle)
        self.assertFalse(cliente.hubo_escritura())


def _sin_idioma(xml, lcid):
    """El formxml sin las etiquetas de un idioma (como lo dejaria un formulario armado antes de la migracion)."""
    import re
    return re.sub(rf'<label description="[^"]*" languagecode="{lcid}" />', "", xml)


class EtiquetasPorIdioma(Base):
    """Toda etiqueta EMBEBIDA en el formxml va una vez por cada idioma de la Identidad: ahi la plataforma no cae al
    idioma base. La verificacion compara cada idioma, en los dos sentidos."""

    def contar(self, xml, lcid):
        return xml.count(f'languagecode="{lcid}"')

    def test_cada_etiqueta_del_xml_sale_en_todos_los_idiomas_de_la_identidad(self):
        x = xml_de()
        self.assertGreater(self.contar(x, 1033), 0)
        self.assertEqual(self.contar(x, 1033), self.contar(x, 3082))
        self.assertEqual(self.contar(x, 1033) + self.contar(x, 3082), x.count("languagecode="))

    def test_la_celda_de_relleno_del_encabezado_tambien_lleva_una_etiqueta_vacia_por_idioma(self):
        x = xml_de(comp(encabezado=["sanic_resultado"]))
        self.assertEqual(2, x.count('<label description="" languagecode="1033" />'))
        self.assertEqual(2, x.count('<label description="" languagecode="3082" />'))

    def test_el_orden_de_las_etiquetas_es_el_de_los_idiomas_de_la_identidad(self):
        x = xml_de(idiomas=[3082, 1033])
        self.assertIn('<labels><label description="General" languagecode="3082" />'
                      '<label description="General" languagecode="1033" /></labels>', x)

    def test_la_etiqueta_de_un_campo_sale_del_displayname_de_cada_idioma(self):
        etiquetas = dict(ETIQUETAS, sanic_asunto={"1033": "Subject", "3082": "Asunto"})
        x = xml_de(etiquetas=etiquetas)
        self.assertIn('<label description="Subject" languagecode="1033" /><label description="Asunto" languagecode="3082" />', x)

    def test_con_tres_idiomas_en_la_identidad_el_formulario_se_crea_con_tres_etiquetas_por_texto(self):
        idiomas = [1033, 3082, 1034]
        identidad = dict(IDENT, idiomas=idiomas)
        datos = copy.deepcopy(COMPONENTE)
        datos["pestana"] = por_idioma("General", idiomas)
        for sec in datos["secciones"]:
            sec["titulo"] = por_idioma(sec["titulo"]["1033"], idiomas)
        etiquetas = {c: por_idioma(t["1033"], idiomas) for c, t in ETIQUETAS.items()}
        cliente = self.armar(ClienteSimulado(), identidad=identidad, etiquetas=etiquetas)

        def crear(ruta, cuerpo, solucion):
            self.form = fila_form(datos, formxml=cuerpo["formxml"])
            return (204, {}, {})

        cliente.responder("POST", "systemforms", crear)
        estado, _, detalle = fo.construir(self.escribir(datos, identidad), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        enviado = next(l["cuerpo"]["formxml"] for l in cliente.llamadas if l["metodo"] == "POST")
        self.assertEqual(xml_de(datos, idiomas, etiquetas), enviado)
        self.assertEqual(self.contar(enviado, 1033), self.contar(enviado, 1034))

    def test_una_columna_sin_etiqueta_en_un_idioma_de_la_identidad_queda_bloqueada_sin_escribir(self):
        # La consulta respondio bien y falta un dato de NEGOCIO (la tabla no tiene la etiqueta): no es una forma
        # inesperada (`error`) ni un formulario que difiere, es una precondicion sin cumplir -> `bloqueado`.
        casos = [("sin la etiqueta", {"1033": "Asunto"}), ("con la etiqueta vacia", {"1033": "Asunto", "3082": ""})]
        for nombre, textos in casos:
            with self.subTest(caso=nombre):
                etiquetas = dict(ETIQUETAS, sanic_asunto=textos)
                cliente = self.armar(ClienteSimulado(), form=fila_form(), etiquetas=etiquetas)
                estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
                self.assertEqual("bloqueado", estado, detalle)
                self.assertIn("sanic_asunto", detalle)
                self.assertIn("3082", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_una_columna_no_declarada_sin_etiqueta_no_estorba(self):
        etiquetas = dict(ETIQUETAS, sanic_unidaaid={"1033": "Unida a"})
        tipos = dict(TIPOS, sanic_unidaaid="LookupType")
        cliente = self.armar(ClienteSimulado(), form=fila_form(), etiquetas=etiquetas, tipos=tipos)
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_la_forma_del_displayname_se_valida(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        cliente._reglas.insert(0, ("GET", lambda r: "/Attributes" in r, (200, {"value": [
            {"LogicalName": "sanic_asunto", "AttributeTypeName": {"Value": "StringType"},
             "DisplayName": {"LocalizedLabels": [{"LanguageCode": 1033, "Label": 7}]}}]}, {})))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("LocalizedLabels[0].Label", detalle)

    def test_un_formulario_con_una_sola_etiqueta_por_texto_difiere_y_dice_que_idioma_falta(self):
        viejo = fila_form(formxml=_sin_idioma(xml_de(), 3082))
        cliente = self.armar(ClienteSimulado(), form=viejo)
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("falta la etiqueta 3082", detalle)
        self.assertIn("la pestaña", detalle)
        self.assertIn("el control 'sanic_asunto'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_etiqueta_de_3082_distinta_en_el_entorno_difiere(self):
        distinto = xml_de().replace('<label description="General" languagecode="3082" />',
                                    '<label description="Otro" languagecode="3082" />')
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml=distinto))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("la etiqueta 3082 es 'Otro' y el playbook dice 'General'", detalle)

    def test_una_etiqueta_en_un_idioma_que_la_identidad_no_declara_difiere(self):
        # El otro sentido de la comparacion: lo que SOBRA tambien es una diferencia.
        de_mas = xml_de().replace('<label description="General" languagecode="3082" />',
                                  '<label description="General" languagecode="3082" />'
                                  '<label description="General" languagecode="1034" />')
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml=de_mas))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("trae una etiqueta en el idioma 1034, que la Identidad no declara", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_formxml_sin_la_estructura_se_denuncia_una_vez_y_no_etiqueta_por_etiqueta(self):
        # Si faltan las celdas enteras, el problema es la estructura (lo dice la comparacion del XML entero); listar
        # "falta la etiqueta" de cada una seria ruido.
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml="<form/>"))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("el formxml no coincide", detalle)
        self.assertNotIn("falta la etiqueta", detalle)

    def test_un_formxml_que_no_es_xml_difiere_sin_romper(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml="<form><tabs>"))
        estado, _, detalle = fo.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no es XML válido", detalle)

    def test_ya_existia_solo_si_coinciden_las_etiquetas_de_todos_los_idiomas(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form())
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_reescribe_el_xml_con_las_etiquetas_de_todos_los_idiomas(self):
        cliente = self.armar(ClienteSimulado(), form=fila_form(formxml=_sin_idioma(xml_de(), 3082)))

        def corregir(ruta, cuerpo, solucion):
            self.form = fila_form(formxml=cuerpo["formxml"])
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("systemforms("), corregir)
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        estado, _, detalle = fo.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual(xml_de(), patch["cuerpo"]["formxml"])


class Uso(Base):
    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original, capturado = sys.argv, []
        real = fo.salida
        try:
            sys.argv = ["formulario.py", self.escribir(), "--forzar"]
            fo.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            fo.main()
        finally:
            sys.argv, fo.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("--forzar", capturado[0][2])


if __name__ == "__main__":
    unittest.main()
