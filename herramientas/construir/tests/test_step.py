"""Pruebas de `herramientas/construir/step.py`. Nunca tocan la red.

El foco está en las IMÁGENES y en las combinaciones que Learn prohíbe: un step
mal registrado no falla al registrarse, falla en producción la primera vez que
alguien guarda un registro. Todo eso tiene que quedar frenado sin red.
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

import step as st  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, SOLUTION_ID, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="steps", inventario="8.2")

# Dominio de este proyecto (convenciones, seccion 2: `STEP - CARP - tabla - mensaje - etapa`): un step sobre el
# correo entrante, sin imagen, y uno sobre la conversacion, con filtro de columnas y pre-imagen.
TIPO_CORREO = "Sanic.Carp.Plugins.Steps.VincularCorreoStep"
TIPO_CONVERSACION = "Sanic.Carp.Plugins.Steps.TransicionConversacionStep"
CORREO = "email"
CONVERSACION = "sanic_carp_tbl_conversacion"

STEP_CORREO = {
    "nombre": "STEP - CARP - email - Create - PreOperation",
    "descripcion": "Vincula el correo entrante con su conversacion antes de guardarlo.",
    "plugintype": TIPO_CORREO,
    "mensaje": "Create",
    "tabla": CORREO,
    "etapa": "PreOperation",
    "modo": "Sincrono",
    "orden": 0,
    "filtro": [],
    "preimagen": None,
}

STEP_CONVERSACION = {
    "nombre": "STEP - CARP - conversacion - Update - PreOperation",
    "descripcion": "Autoriza Finalizar y Reabrir una conversacion y escribe quien actuo.",
    "plugintype": TIPO_CONVERSACION,
    "mensaje": "Update",
    "tabla": CONVERSACION,
    "etapa": "PreOperation",
    "modo": "Sincrono",
    "orden": 1,
    "filtro": ["sanic_resultado", "sanic_motivosinrespuesta"],
    "preimagen": {"alias": "PreImagen", "columnas": ["sanic_resultado", "sanic_motivosinrespuesta", "sanic_finalizadoporid"]},
}

COMPONENTE = {"tipo": "steps", "paquete": "sanic_carp_pkg_plugins", "steps": [STEP_CORREO, STEP_CONVERSACION]}

IDS = {TIPO_CORREO: "t-correo", TIPO_CONVERSACION: "t-conversacion"}
# Columnas de la pre-imagen tal como las guarda la herramienta: ordenadas y separadas por coma.
COLUMNAS_IMAGEN = "sanic_finalizadoporid,sanic_motivosinrespuesta,sanic_resultado"


def comp(steps=None, **cambios):
    d = copy.deepcopy(COMPONENTE)
    if steps is not None:
        d["steps"] = steps
    d.update(cambios)
    return d


def un_step(base=STEP_CORREO, **cambios):
    s = copy.deepcopy(base)
    s.update(cambios)
    return s


def playbook(componente=None):
    componente = COMPONENTE if componente is None else componente
    return (
        "# Playbook: steps · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(IDENT, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def step_id_de(s):
    return "step-" + s["plugintype"].rsplit(".", 1)[-1]


def fila_step(s, **cambios):
    fila = {
        "sdkmessageprocessingstepid": step_id_de(s),
        "name": s["nombre"],
        "description": s["descripcion"],
        "stage": st.ETAPAS[s["etapa"]],
        "mode": st.MODOS[s["modo"]],
        "rank": s["orden"],
        "filteringattributes": st.texto_de_filtro(s["filtro"]),
        "statecode": 0,
        "supporteddeployment": 0,
    }
    fila.update(cambios)
    return fila


def fila_imagen(s, **cambios):
    imagen = {
        "sdkmessageprocessingstepimageid": "img-" + step_id_de(s),
        "imagetype": st.IMAGEN_PRE,
        "entityalias": s["preimagen"]["alias"],
        "attributes": ",".join(sorted(s["preimagen"]["columnas"])),
        "messagepropertyname": "Target",
        "name": s["preimagen"]["alias"],
    }
    imagen.update(cambios)
    return imagen


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente))
        return self.ruta

    def armar(self, cliente, steps_en_entorno=None, imagenes=None, en_solucion=True, tipos_faltantes=()):
        """`steps_en_entorno` e `imagenes` son dicts por step_id. Se responde
        siempre lo mismo salvo que la prueba los mute entre llamadas."""
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.steps_en_entorno = {} if steps_en_entorno is None else steps_en_entorno
        self.imagenes = {} if imagenes is None else imagenes

        def responder_tipos(ruta, cuerpo, solucion):
            for typename, tid in IDS.items():
                if f"'{typename}'" in ruta:
                    if typename in tipos_faltantes:
                        return (200, {"value": []}, {})
                    return (200, {"value": [{"plugintypeid": tid, "typename": typename}]}, {})
            return (200, {"value": []}, {})

        cliente.responder("GET", lambda r: r.startswith("plugintypes?"), responder_tipos)
        def responder_mensajes(ruta, cuerpo, solucion):
            nombre = ruta.split("name eq '")[1].rstrip("'")
            return (200, {"value": [{"sdkmessageid": "m-" + nombre.lower(), "name": nombre}]}, {})

        def responder_filtros(ruta, cuerpo, solucion):
            tabla = ruta.split("primaryobjecttypecode eq '")[1].rstrip("'")
            return (200, {"value": [{"sdkmessagefilterid": f"f-{tabla}-{m.lower()}", "primaryobjecttypecode": tabla,
                                     "sdkmessageid": {"name": m}} for m in ("Create", "Update")]}, {})

        cliente.responder("GET", lambda r: r.startswith("sdkmessages?"), responder_mensajes)
        cliente.responder("GET", lambda r: r.startswith("sdkmessagefilters?"), responder_filtros)

        def responder_steps(ruta, cuerpo, solucion):
            for typename, tid in IDS.items():
                if f"eq {tid} " in ruta:
                    fila = self.steps_en_entorno.get(typename)
                    return (200, {"value": [] if fila is None else [fila]}, {})
            return (200, {"value": []}, {})

        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingsteps?"), responder_steps)

        def responder_imagenes(ruta, cuerpo, solucion):
            for sid, filas in self.imagenes.items():
                if f"eq {sid}" in ruta:
                    return (200, {"value": filas}, {})
            return (200, {"value": []}, {})

        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingstepimages?"), responder_imagenes)
        cliente.responder("GET", es_ruta_solutioncomponents,
                          (200, {"value": [{"componenttype": 92, "objectid": "x"}] if en_solucion else []}, {}))
        return cliente


# ---------------------------------------------------------------------------
# Validación offline
# ---------------------------------------------------------------------------
class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = st.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        # Un dato mal formado del PLAYBOOK no es "una respuesta del entorno" ni un fallo imprevisto.
        self.assertNotIn("fallo inesperado", detalle)
        self.assertNotIn("devolvió 200", detalle)
        self.assertFalse(centinela.llamada, "no tenía que intentar construir el cliente de Dataverse")

    def test_una_clave_de_mas_o_de_menos_se_rechaza(self):
        self.rechaza(comp(steps=[{k: v for k, v in STEP_CORREO.items() if k != "etapa"}]), "faltan ['etapa']")
        self.rechaza(comp(steps=[un_step(sobrante=1)]), "sobran ['sobrante']")

    def test_una_etapa_o_un_modo_que_no_existen_se_rechazan(self):
        self.rechaza(comp(steps=[un_step(etapa="MainOperation")]), "no es válida")
        self.rechaza(comp(steps=[un_step(modo="Diferido")]), "no es válido")

    def test_un_step_asincrono_fuera_de_postoperation_se_rechaza(self):
        # Learn: async solo se admite en PostOperation.
        self.rechaza(comp(steps=[un_step(modo="Asincrono", etapa="PreOperation")]), "solo se puede registrar en PostOperation")
        self.rechaza(comp(steps=[un_step(modo="Asincrono", etapa="PreValidation")]), "solo se puede registrar en PostOperation")

    def test_un_step_asincrono_en_postoperation_se_acepta(self):
        bueno = comp(steps=[un_step(modo="Asincrono", etapa="PostOperation")])
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = st.construir(self.escribir(bueno), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)  # no existe todavía, pero pasó la validación

    def test_una_preimagen_sobre_create_se_rechaza(self):
        # No hay "antes" de un Create.
        malo = un_step(base=STEP_CONVERSACION, mensaje="Create", filtro=[])
        self.rechaza(comp(steps=[malo]), "no puede tener pre-imagen")

    def test_una_preimagen_sobre_un_mensaje_que_no_la_admite_se_rechaza(self):
        malo = un_step(base=STEP_CONVERSACION, mensaje="Associate", filtro=[])
        self.rechaza(comp(steps=[malo]), "no admite imágenes")

    def test_una_preimagen_sin_columnas_se_rechaza(self):
        # Vacío significa TODAS las columnas: mala práctica de rendimiento.
        malo = un_step(base=STEP_CONVERSACION, preimagen={"alias": "PreImagen", "columnas": []})
        self.rechaza(comp(steps=[malo]), "columnas")

    def test_una_preimagen_con_columnas_repetidas_se_rechaza(self):
        malo = un_step(base=STEP_CONVERSACION, preimagen={"alias": "PreImagen", "columnas": ["sanic_resultado", "sanic_resultado"]})
        self.rechaza(comp(steps=[malo]), "columnas repetidas")

    def test_un_filtro_sobre_create_se_rechaza(self):
        self.rechaza(comp(steps=[un_step(mensaje="Create", filtro=["sanic_resultado"])]), "solo tiene sentido en")

    def test_el_filtro_no_puede_llevar_la_clave_primaria(self):
        # Viene siempre en el Target: deja el filtro sin efecto.
        malo = un_step(base=STEP_CONVERSACION, filtro=["sanic_resultado", CONVERSACION + "id"])
        self.rechaza(comp(steps=[malo]), f"no puede incluir la clave primaria '{CONVERSACION}id'")
        # En una tabla estandar la clave primaria tambien es <tabla>id.
        self.rechaza(comp(steps=[un_step(mensaje="Update", filtro=["emailid"])]), "no puede incluir la clave primaria 'emailid'")

    def test_la_clave_primaria_de_otra_tabla_no_es_la_de_este_step(self):
        otro = un_step(base=STEP_CONVERSACION, filtro=["sanic_resultado", "emailid"])
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = st.construir(self.escribir(comp(steps=[otro])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)  # no existe todavia, pero pasó la validación

    def test_dos_steps_del_mismo_plugin_sobre_el_mismo_mensaje_y_tabla_se_rechazan(self):
        self.rechaza(comp(steps=[STEP_CORREO, un_step(nombre="Otro")]), "ya hay otro step del mismo plugin")

    def test_dos_steps_con_el_mismo_nombre_se_rechazan(self):
        otro = un_step(base=STEP_CONVERSACION, nombre=STEP_CORREO["nombre"])
        self.rechaza(comp(steps=[STEP_CORREO, otro]), "el nombre está repetido")

    def test_los_nombres_visibles_van_sin_tildes(self):
        self.rechaza(comp(steps=[un_step(nombre="STEP - CARP - email - Create - Recepción")]), "fuera de ASCII")

    def test_las_tablas_de_valores_son_las_de_learn(self):
        self.assertEqual(st.ETAPAS, {"PreValidation": 10, "PreOperation": 20, "PostOperation": 40})
        self.assertEqual(st.MODOS, {"Sincrono": 0, "Asincrono": 1})
        self.assertEqual((st.DESPLIEGUE_SOLO_SERVIDOR, st.INVOCACION_PADRE, st.IMAGEN_PRE, st.IMAGEN_POST), (0, 0, 0, 1))
        self.assertEqual(st.PROPIEDAD_DE_IMAGEN, {"Assign": "Target", "Create": "Target", "Delete": "Target", "DeliverIncoming": "EmailId",
                                                  "DeliverPromote": "EmailId", "Route": "Target", "SetState": "EntityMoniker", "Update": "Target"})
        self.assertEqual(st.MENSAJES_CON_FILTRO, {"Update"})

    def test_la_propiedad_de_la_pre_imagen_depende_del_mensaje(self):
        for mensaje, propiedad in (("Update", "Target"), ("SetState", "EntityMoniker"), ("Assign", "Target"), ("Delete", "Target"), ("Route", "Target"),
                                   ("DeliverIncoming", "EmailId"), ("DeliverPromote", "EmailId")):
            with self.subTest(mensaje):
                s = un_step(base=STEP_CONVERSACION, mensaje=mensaje, filtro=[])
                self.assertEqual(st._cuerpo_imagen(s, "step-x")["messagepropertyname"], propiedad)

    def test_el_nombre_y_el_alias_respetan_el_maximo(self):
        justo = "STEP - CARP - " + "x" * (256 - len("STEP - CARP - "))
        self.assertEqual(len(justo), 256)
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = st.construir(self.escribir(comp(steps=[un_step(nombre=justo)])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)  # pasó la validación; no existe todavía
        self.rechaza(comp(steps=[un_step(nombre=justo + "x")]), "nombre tiene más de 256 caracteres")
        alias_justo = dict(STEP_CONVERSACION["preimagen"], alias="a" * 256)
        estado, _, detalle = st.construir(self.escribir(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen=alias_justo)])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen=dict(alias_justo, alias="a" * 257))]), "el alias de la pre-imagen tiene más de 256")

    def test_un_texto_vacio_o_un_tipo_equivocado_se_rechaza_en_cada_campo(self):
        for clave in ("nombre", "descripcion", "plugintype", "mensaje", "tabla", "etapa", "modo"):
            with self.subTest(clave):
                self.rechaza(comp(steps=[un_step(**{clave: ""})]), clave)
        for valor in ("0", True, None, 1.5):
            with self.subTest(f"orden {valor!r}"):
                self.rechaza(comp(steps=[un_step(orden=valor)]), "orden")
        self.rechaza(comp(steps=[un_step(filtro="sanic_resultado")]), "filtro")
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen={"alias": "", "columnas": ["a"]})]), "preimagen.alias")
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen={"alias": "P", "columnas": "a"})]), "preimagen.columnas")
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen={"alias": "P"})]), "faltan ['columnas']")
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, preimagen="PreImagen")]), "preimagen")
        self.rechaza(comp(steps=["no es un objeto"]), "steps[0]")

    def test_el_bloque_exige_paquete_y_al_menos_un_step(self):
        self.rechaza(comp(paquete=""), "paquete")
        self.rechaza(comp(steps=[]), "steps")
        with self.assertRaises(st.ErrorPlaybook) as e:
            st.validar_playbook(comp(tipo="rol"))
        self.assertIn("se esperaba 'steps'", str(e.exception))

    def test_el_tipo_del_plugin_tiene_que_ser_un_nombre_dotnet_completo(self):
        self.rechaza(comp(steps=[un_step(plugintype="VincularCorreoStep")]), "no parece un nombre de tipo .NET completo")

    def test_el_filtro_no_admite_columnas_repetidas(self):
        self.rechaza(comp(steps=[un_step(base=STEP_CONVERSACION, filtro=["sanic_resultado", "sanic_resultado"])]), "el filtro de atributos tiene columnas repetidas")

    def test_el_mismo_plugin_y_mensaje_sobre_tablas_distintas_no_es_un_duplicado(self):
        otra_tabla = un_step(base=STEP_CONVERSACION, nombre="STEP - CARP - arreglo - Update - PreOperation", tabla="sanic_carp_tbl_arreglo", preimagen=None, filtro=[])
        st.validar_playbook(comp(steps=[STEP_CONVERSACION, otra_tabla]))  # no lanza: la clave natural incluye la tabla

    def test_el_filtro_se_ordena_para_que_el_orden_no_produzca_falsas_diferencias(self):
        self.assertEqual("a,b,c", st.texto_de_filtro(["c", "a", "b"]))
        self.assertIsNone(st.texto_de_filtro([]))


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
class ContraElEntorno(Base):
    def test_sin_el_paquete_registrado_queda_bloqueado_y_no_escribe(self):
        cliente = self.armar(ClienteSimulado(), tipos_faltantes=(TIPO_CORREO,))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("paquete_plugins.py", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_idioma_del_playbook_que_el_entorno_no_provisiono_bloquea_sin_escribir(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": [1033]}, {}))
        self.armar(cliente)
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("3082", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_componente_informado_es_el_paquete_del_playbook(self):
        cliente = self.armar(ClienteSimulado(), tipos_faltantes=(TIPO_CORREO,))
        _, componente, _ = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual(COMPONENTE["paquete"], componente)

    def test_una_tabla_que_no_admite_el_mensaje_queda_bloqueada(self):
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        cliente.responder("GET", lambda r: r.startswith("plugintypes?"),
                          (200, {"value": [{"plugintypeid": "t-correo", "typename": TIPO_CORREO}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("sdkmessages?"),
                          (200, {"value": [{"sdkmessageid": "m-create", "name": "Create"}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("sdkmessagefilters?"), (200, {"value": []}, {}))
        estado, _, detalle = st.construir(self.escribir(comp(steps=[STEP_CORREO])), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("no admite el mensaje 'Create'", detalle)
        self.assertIn(f"la tabla '{CORREO}'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_alta_manda_el_eventhandler_por_su_propiedad_polimorfica(self):
        # `eventhandler` NO acepta un @odata.bind genérico: hay que usar la
        # propiedad de navegación del destino.
        cliente = self.armar(ClienteSimulado())
        creados = {}

        def crear(ruta, cuerpo, solucion):
            creados["cuerpo"] = cuerpo
            s = STEP_CORREO if cuerpo["plugintypeid@odata.bind"].endswith("(t-correo)") else STEP_CONVERSACION
            self.steps_en_entorno[s["plugintype"]] = fila_step(s)
            if s["preimagen"]:
                self.imagenes[step_id_de(s)] = []
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingsteps", crear)

        def crear_imagen(ruta, cuerpo, solucion):
            self.imagenes[step_id_de(STEP_CONVERSACION)] = [fila_imagen(STEP_CONVERSACION)]
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingstepimages", crear_imagen)

        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("creado", estado, detalle)
        cuerpo = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "sdkmessageprocessingsteps")
        self.assertIn("eventhandler_plugintype@odata.bind", cuerpo)
        self.assertNotIn("eventhandler@odata.bind", cuerpo)
        self.assertEqual(cuerpo["plugintypeid@odata.bind"], cuerpo["eventhandler_plugintype@odata.bind"])
        self.assertEqual(20, cuerpo["stage"])   # PreOperation
        self.assertEqual(0, cuerpo["mode"])     # Sincrono
        self.assertEqual(0, cuerpo["supporteddeployment"])
        self.assertEqual(IDENT["solucion"], next(l["solucion"] for l in cliente.llamadas if l["metodo"] == "POST"))

    def test_el_cuerpo_completo_del_alta_de_cada_step(self):
        cliente = self.armar(ClienteSimulado())
        cuerpos = []

        def crear(ruta, cuerpo, solucion):
            cuerpos.append(cuerpo)
            s = STEP_CORREO if cuerpo["plugintypeid@odata.bind"].endswith("(t-correo)") else STEP_CONVERSACION
            self.steps_en_entorno[s["plugintype"]] = fila_step(s)
            self.imagenes[step_id_de(s)] = [fila_imagen(s)] if s["preimagen"] else []
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingsteps", crear)
        cliente.responder("POST", "sdkmessageprocessingstepimages", (204, {}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        self.assertIn("2 registrados, 0 actualizados y 0 que ya estaban, de 2", detalle)
        self.assertEqual(cuerpos[0], {
            "name": STEP_CORREO["nombre"], "description": STEP_CORREO["descripcion"], "stage": 20, "mode": 0, "rank": 0,
            "supporteddeployment": 0, "invocationsource": 0, "filteringattributes": None,
            "sdkmessageid@odata.bind": "/sdkmessages(m-create)", "sdkmessagefilterid@odata.bind": "/sdkmessagefilters(f-email-create)",
            "plugintypeid@odata.bind": "/plugintypes(t-correo)", "eventhandler_plugintype@odata.bind": "/plugintypes(t-correo)"})
        self.assertEqual(cuerpos[1]["filteringattributes"], "sanic_motivosinrespuesta,sanic_resultado")
        self.assertEqual(cuerpos[1]["sdkmessageid@odata.bind"], "/sdkmessages(m-update)")
        self.assertEqual(cuerpos[1]["sdkmessagefilterid@odata.bind"], f"/sdkmessagefilters(f-{CONVERSACION}-update)")
        self.assertEqual(cuerpos[1]["rank"], 1)

    def test_la_preimagen_se_crea_con_el_cuerpo_exacto(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )
        cliente.responder("POST", "sdkmessageprocessingstepimages", (204, {}, {}))
        st.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        cuerpo = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "sdkmessageprocessingstepimages")
        self.assertEqual(cuerpo, {"name": "PreImagen", "entityalias": "PreImagen", "imagetype": 0, "messagepropertyname": "Target",
                                  "attributes": COLUMNAS_IMAGEN, "sdkmessageprocessingstepid@odata.bind": f"/sdkmessageprocessingsteps({step_id_de(STEP_CONVERSACION)})"})

    def test_la_preimagen_se_crea_con_target_y_las_columnas_declaradas(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )

        def crear_imagen(ruta, cuerpo, solucion):
            self.imagenes[step_id_de(STEP_CONVERSACION)] = [fila_imagen(STEP_CONVERSACION)]
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingstepimages", crear_imagen)

        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)

        self.assertEqual("actualizado", estado, detalle)
        cuerpo = next(l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "sdkmessageprocessingstepimages")
        self.assertEqual(0, cuerpo["imagetype"])  # PreImage
        self.assertEqual("Target", cuerpo["messagepropertyname"])
        self.assertEqual("PreImagen", cuerpo["entityalias"])
        self.assertEqual(COLUMNAS_IMAGEN, cuerpo["attributes"])
        self.assertEqual(f"/sdkmessageprocessingsteps({step_id_de(STEP_CONVERSACION)})",
                         cuerpo["sdkmessageprocessingstepid@odata.bind"])

    def test_una_preimagen_que_falta_se_agrega_aunque_no_pidan_completar(self):
        # No es un ajuste de gusto: sin ella el plugin revienta al primer guardado.
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )

        def crear_imagen(ruta, cuerpo, solucion):
            self.imagenes[step_id_de(STEP_CONVERSACION)] = [fila_imagen(STEP_CONVERSACION)]
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingstepimages", crear_imagen)
        estado, _, _ = st.construir(self.escribir(), False, lambda: cliente, completar=False)
        self.assertEqual("actualizado", estado)

    def test_solo_verificar_denuncia_la_preimagen_que_falta_y_no_escribe(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("le falta la pre-imagen", detalle)
        self.assertIn("revienta al primer guardado", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_ya_esta_todo_bien_no_se_escribe_nada(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertIn("2 que ya estaban", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_orden_distinto_se_denuncia_nombrando_el_step(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(STEP_CORREO["nombre"], detalle)
        self.assertIn("rank es 5", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_corrige_el_orden_con_un_patch(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )

        def corregir(ruta, cuerpo, solucion):
            self.steps_en_entorno[TIPO_CORREO] = fila_step(STEP_CORREO)
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), corregir)

        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True)

        self.assertEqual("actualizado", estado, detalle)
        patches = [l for l in cliente.llamadas if l["metodo"] == "PATCH"]
        self.assertEqual(1, len(patches))
        self.assertEqual(0, patches[0]["cuerpo"]["rank"])
        # La clave natural nunca se toca: eso sería otro step, no una corrección.
        for prohibido in ("plugintypeid@odata.bind", "sdkmessageid@odata.bind", "sdkmessagefilterid@odata.bind"):
            self.assertNotIn(prohibido, patches[0]["cuerpo"])

    def test_completar_puede_borrar_un_filtro_que_sobra(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={
                TIPO_CORREO: fila_step(STEP_CORREO, filteringattributes="sanic_resultado"),
                TIPO_CONVERSACION: fila_step(STEP_CONVERSACION),
            },
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )

        def corregir(ruta, cuerpo, solucion):
            self.steps_en_entorno[TIPO_CORREO] = fila_step(STEP_CORREO)
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), corregir)
        st.construir(self.escribir(), False, lambda: cliente, completar=True)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertIn("filteringattributes", patch["cuerpo"])
        self.assertIsNone(patch["cuerpo"]["filteringattributes"])

    def test_cada_diferencia_del_step_se_nombra(self):
        casos = [("name", "otro"), ("description", "otra"), ("stage", 40), ("mode", 1), ("rank", 7), ("filteringattributes", "sanic_otra")]
        for campo, valor in casos:
            with self.subTest(campo):
                cliente = self.armar(
                    ClienteSimulado(),
                    steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, **{campo: valor}), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
                    imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
                )
                estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
                self.assertEqual("difiere", estado, detalle)
                self.assertIn(f"{STEP_CORREO['nombre']}: {campo} es {valor!r}", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_cada_diferencia_de_la_preimagen_se_nombra(self):
        otra_propiedad = fila_imagen(STEP_CONVERSACION, messagepropertyname="Id")
        casos = [("el alias de la pre-imagen es 'Otra'", [fila_imagen(STEP_CONVERSACION, entityalias="Otra")]),
                 ("messagepropertyname de la pre-imagen es 'Id' y para 'Update' tiene que ser 'Target'", [otra_propiedad]),
                 ("tiene 2 pre-imágenes; el playbook declara una", [fila_imagen(STEP_CONVERSACION), fila_imagen(STEP_CONVERSACION, sdkmessageprocessingstepimageid="otra")]),
                 ("las columnas de la pre-imagen son None", [fila_imagen(STEP_CONVERSACION, attributes=None)])]
        for esperado, imagenes in casos:
            with self.subTest(esperado):
                cliente = self.armar(
                    ClienteSimulado(),
                    steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
                    imagenes={step_id_de(STEP_CONVERSACION): imagenes},
                )
                estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
                self.assertEqual("difiere", estado, detalle)
                self.assertIn(esperado, detalle)

    def test_una_post_imagen_no_cuenta_como_la_pre_imagen_ni_como_sobrante(self):
        post = fila_imagen(STEP_CONVERSACION, imagetype=1, entityalias="Post")
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION), post], step_id_de(STEP_CORREO): [post]},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_una_pre_imagen_que_el_playbook_no_declara_se_denuncia_y_nunca_se_borra(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)], step_id_de(STEP_CORREO): [fila_imagen(STEP_CONVERSACION, entityalias="Colada")]},
        )
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("tiene 1 pre-imagen(es) que el playbook no declara (esta herramienta nunca borra)", detalle)
        self.assertNotIn("DELETE", [l["metodo"] for l in cliente.llamadas])

    def test_un_step_de_mas_o_dos_steps_para_la_misma_clave_natural_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingsteps?"), (200, {"value": [fila_step(STEP_CORREO), fila_step(STEP_CORREO)]}, {}))
        self.armar(cliente)
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("devolvió 2 steps para la misma clave natural", detalle)

    def test_el_mismo_tipo_de_plugin_en_dos_steps_se_consulta_una_sola_vez(self):
        otro = un_step(base=STEP_CONVERSACION, plugintype=TIPO_CORREO, preimagen=None, nombre="STEP - CARP - conversacion - Update - PostOperation", etapa="PostOperation")
        cliente = self.armar(ClienteSimulado())
        st.construir(self.escribir(comp(steps=[STEP_CORREO, otro])), True, lambda: cliente)
        self.assertEqual(len([l for l in cliente.llamadas if l["ruta"].startswith("plugintypes?")]), 1)

    def test_las_columnas_de_la_pre_imagen_se_comparan_sin_importar_el_orden(self):
        desordenadas = fila_imagen(STEP_CONVERSACION, attributes="sanic_resultado,sanic_motivosinrespuesta,sanic_finalizadoporid")
        self.assertNotEqual(desordenadas["attributes"], COLUMNAS_IMAGEN)
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [desordenadas]},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_el_patch_de_correccion_lleva_exactamente_los_campos_modificables(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION, rank=9)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )

        def corregir(ruta, cuerpo, solucion):
            self.steps_en_entorno[TIPO_CONVERSACION] = fila_step(STEP_CONVERSACION)
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), corregir)
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        patch = next(l for l in cliente.llamadas if l["metodo"] == "PATCH")
        self.assertEqual(patch["ruta"], f"sdkmessageprocessingsteps({step_id_de(STEP_CONVERSACION)})")
        self.assertEqual(patch["solucion"], IDENT["solucion"])
        self.assertEqual(patch["cuerpo"], {"name": STEP_CONVERSACION["nombre"], "description": STEP_CONVERSACION["descripcion"], "stage": 20, "mode": 0,
                                           "rank": 1, "filteringattributes": "sanic_motivosinrespuesta,sanic_resultado"})

    def test_el_catalogo_consulta_una_sola_vez_cada_mensaje_tabla_y_tipo(self):
        otro = un_step(nombre="STEP - CARP - email - Create - PostOperation", plugintype=TIPO_CONVERSACION, etapa="PostOperation", orden=5)
        cliente = self.armar(ClienteSimulado())
        st.construir(self.escribir(comp(steps=[STEP_CORREO, otro])), True, lambda: cliente)
        rutas = [l["ruta"] for l in cliente.llamadas]
        self.assertEqual(len([r for r in rutas if r.startswith("sdkmessages?")]), 1)
        self.assertEqual(len([r for r in rutas if r.startswith("sdkmessagefilters?")]), 1)
        self.assertEqual(len([r for r in rutas if r.startswith("plugintypes?")]), 2)

    def test_las_consultas_filtran_por_lo_que_el_playbook_declara(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        st.construir(self.escribir(), False, lambda: cliente)
        rutas = [l["ruta"] for l in cliente.llamadas]
        self.assertTrue([r for r in rutas if r.startswith("plugintypes?") and f"typename eq '{TIPO_CORREO}'" in r])
        self.assertTrue([r for r in rutas if r.startswith("sdkmessages?") and "name eq 'Update'" in r])
        self.assertTrue([r for r in rutas if r.startswith("sdkmessagefilters?") and f"primaryobjecttypecode eq '{CONVERSACION}'" in r])
        steps = [r for r in rutas if r.startswith("sdkmessageprocessingsteps?") and "t-conversacion" in r]
        self.assertTrue(steps and "_sdkmessageid_value eq m-update" in steps[0] and f"_sdkmessagefilterid_value eq f-{CONVERSACION}-update" in steps[0], steps)
        self.assertTrue([r for r in rutas if r.startswith("sdkmessageprocessingstepimages?") and f"_sdkmessageprocessingstepid_value eq {step_id_de(STEP_CONVERSACION)}" in r])
        pertenencia = [r for r in rutas if r.startswith("solutioncomponents?")]
        self.assertEqual(len(pertenencia), 2)
        self.assertIn(f"objectid eq {step_id_de(STEP_CORREO)}", pertenencia[0])
        self.assertIn(f"_solutionid_value eq {SOLUTION_ID}", pertenencia[0])

    def test_un_mensaje_que_no_existe_o_repetido_queda_bloqueado(self):
        for filas in ([], [{"sdkmessageid": "a", "name": "Create"}, {"sdkmessageid": "b", "name": "Create"}]):
            with self.subTest(len(filas)):
                cliente = ClienteSimulado()
                armar_cliente_precondiciones_ok(cliente, IDENT)
                cliente.responder("GET", lambda r: r.startswith("plugintypes?"), (200, {"value": [{"plugintypeid": "t-correo", "typename": TIPO_CORREO}]}, {}))
                cliente.responder("GET", lambda r: r.startswith("sdkmessages?"), (200, {"value": filas}, {}))
                estado, _, detalle = st.construir(self.escribir(comp(steps=[STEP_CORREO])), False, lambda: cliente)
                self.assertEqual("bloqueado", estado, detalle)
                self.assertIn("el mensaje 'Create' no existe en el entorno, o hay más de uno", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_dos_tipos_de_plugin_con_el_mismo_nombre_es_error(self):
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        cliente.responder("GET", lambda r: r.startswith("plugintypes?"),
                          (200, {"value": [{"plugintypeid": "a", "typename": TIPO_CORREO}, {"plugintypeid": "b", "typename": TIPO_CORREO}]}, {}))
        estado, _, detalle = st.construir(self.escribir(comp(steps=[STEP_CORREO])), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("devolvió 2 filas", detalle)

    def test_sin_el_step_y_con_solo_verificar_se_denuncia_que_no_existe(self):
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(f"{STEP_CORREO['nombre']}: no existe en el entorno", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_tras_el_alta_el_step_no_aparece_al_releer_es_error(self):
        cliente = self.armar(ClienteSimulado())
        cliente.responder("POST", "sdkmessageprocessingsteps", (204, {}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("el step no aparece al releer", detalle)

    def test_si_el_alta_de_la_pre_imagen_falla_es_error(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )
        cliente.responder("POST", "sdkmessageprocessingstepimages", (400, {"error": "boom"}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("el alta de la pre-imagen devolvió HTTP 400", detalle)

    def test_si_la_correccion_falla_o_el_step_desaparece_es_error(self):
        entorno = {TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)}
        imagenes = {step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]}
        cliente = self.armar(ClienteSimulado(), steps_en_entorno=dict(entorno), imagenes=imagenes)
        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), (400, {"error": "boom"}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("la corrección devolvió HTTP 400", detalle)

        cliente = self.armar(ClienteSimulado(), steps_en_entorno=dict(entorno), imagenes=imagenes)

        def desaparece(ruta, cuerpo, solucion):
            self.steps_en_entorno.pop(TIPO_CORREO)
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), desaparece)
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("error", estado, detalle)
        self.assertIn("el step desapareció al releer después de corregirlo", detalle)

    def test_si_tras_escribir_el_entorno_no_coincide_es_error_y_no_difiere(self):
        # El PATCH responde 204 pero la relectura sigue mostrando el rank viejo.
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), (204, {}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("error", estado, detalle)
        self.assertIn("rank es 5", detalle)

    def test_si_se_escribio_un_step_y_otro_difiere_es_error_y_lo_dice(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CONVERSACION: fila_step(STEP_CONVERSACION, rank=9)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )

        def crear(ruta, cuerpo, solucion):
            self.steps_en_entorno[TIPO_CORREO] = fila_step(STEP_CORREO)
            self.imagenes[step_id_de(STEP_CORREO)] = []
            return (204, {}, {})

        cliente.responder("POST", "sdkmessageprocessingsteps", crear)
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, completar=False)
        self.assertEqual("error", estado, detalle)
        self.assertTrue(detalle.startswith("se escribió pero no quedó bien: "), detalle)
        self.assertIn("rank es 9", detalle)

    def test_sin_completar_ni_siquiera_con_una_diferencia_se_hace_patch(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        estado, _, _ = st.construir(self.escribir(), False, lambda: cliente, completar=False)
        self.assertEqual("difiere", estado)
        self.assertFalse(cliente.hubo_escritura())

    def test_con_solo_verificar_y_completar_no_se_escribe_nada(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, rank=5), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): []},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_step_deshabilitado_se_denuncia(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO, statecode=1), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("deshabilitado", detalle)

    def test_columnas_de_preimagen_distintas_se_denuncian(self):
        pobre = fila_imagen(STEP_CONVERSACION, attributes="sanic_resultado")
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [pobre]},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("columnas de la pre-imagen", detalle)

    def test_el_orden_de_las_columnas_del_filtro_no_produce_una_falsa_diferencia(self):
        # Las dos columnas del filtro, en el orden contrario al que la herramienta escribe.
        al_reves = fila_step(STEP_CONVERSACION, filteringattributes="sanic_resultado,sanic_motivosinrespuesta")
        self.assertNotEqual(al_reves["filteringattributes"], st.texto_de_filtro(STEP_CONVERSACION["filtro"]))
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: al_reves},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_un_step_fuera_de_la_solucion_se_denuncia(self):
        cliente = self.armar(
            ClienteSimulado(),
            steps_en_entorno={TIPO_CORREO: fila_step(STEP_CORREO), TIPO_CONVERSACION: fila_step(STEP_CONVERSACION)},
            imagenes={step_id_de(STEP_CONVERSACION): [fila_imagen(STEP_CONVERSACION)]},
            en_solucion=False,
        )
        estado, _, detalle = st.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no figura como componente de la solución", detalle)

    def test_un_http_inesperado_en_el_alta_no_se_confunde_con_exito(self):
        cliente = self.armar(ClienteSimulado())
        cliente.responder("POST", "sdkmessageprocessingsteps", (400, {"error": "rank invalido"}, {}))
        estado, _, detalle = st.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HTTP 400", detalle)


class Uso(Base):
    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original, capturado = sys.argv, []
        real = st.salida
        try:
            sys.argv = ["step.py", self.escribir(), "--forzar"]
            st.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            st.main()
        finally:
            sys.argv, st.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("--forzar", capturado[0][2])


if __name__ == "__main__":
    unittest.main()
