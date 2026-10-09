"""`tabla.py` y los motivos de estado (`motivos_estado`, skill power-platform-especificar 0.2.0, `tabla.md` «Motivos de estado»).

Dominio: los motivos de Conversación (3) y de Arreglo (5), diccionario de datos 3.2. `statecode` tiene dos estados fijos (0 Activo,
1 Inactivo) que no se personalizan; `statuscode` parte de los motivos 1 (estado 0) y 2 (estado 1), que se RENOMBRAN, y se agregan los
demás con el prefijo de opciones del publisher.

Sin ensayar contra la plataforma (0 ciclos): `UpdateOptionValue` sobre `statuscode`, `InsertStatusValue` con `Value` explícito y la
forma de la lectura de las opciones del status están pendientes de verificar. Los dobles de estas pruebas REPITEN la suposición del
código sobre esa forma (patrones de pruebas, 2.2): se validan contra una respuesta real en el primer ciclo. Ninguna prueba toca la red."""
import copy
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import tabla as tb  # noqa: E402
import test_tabla as tt  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import label, por_idioma  # noqa: E402
from test_tabla import BORRAR, col, con_cambio, cuerpo_state, cuerpo_status  # noqa: E402

TABLA = "sanic_carp_tbl_zzarreglo"


def motivo(statecode, valor, origen, nombre, descripcion=""):
    return {"statecode": statecode, "valor": valor, "origen": origen, "etiqueta": por_idioma(nombre), "descripcion": descripcion}


# Motivos de Arreglo (diccionario 3.2): dos por defecto renombrados y tres agregados.
MOTIVOS_ARREGLO = [
    motivo(0, 1, "defecto", "Borrador"),
    motivo(0, 159460001, "agregado", "Vigente", "Acuerdo formalizado."),
    motivo(1, 2, "defecto", "Terminado"),
    motivo(1, 159460002, "agregado", "Anulado", "Se anuló un arreglo vigente."),
    motivo(1, 159460003, "agregado", "Descartado"),
]
ARREGLO = {
    "tipo": "tabla",
    "nombre": TABLA,
    "displayname": por_idioma("Arreglo"),
    "displayname_plural": por_idioma("Arreglos"),
    "descripcion": "Arreglo de pago de un préstamo o de una tarjeta.",
    "propiedad": "usuario",
    "notas": False,
    "actividades": False,
    "auditoria": True,
    "motivos_estado": MOTIVOS_ARREGLO,
    "primaria": {"nombre": "sanic_nombre", "displayname": por_idioma("Codigo interno"), "descripcion": "Código interno correlativo.",
                 "largo": 100, "requerida": False, "autonumerico": "ARR-{SEQNUM:6}"},
    "columnas": [col("sanic_plazo", "entero", minimo=1, maximo=60, requerida=True)],
}
MOTIVOS_CONVERSACION = [motivo(0, 1, "defecto", "Abierta"), motivo(1, 2, "defecto", "Finalizada"), motivo(1, 159460001, "agregado", "Unida")]
SIN_MOTIVOS = frozenset(tb.CARACTERISTICAS) - {"motivos_estado"}
Base = tt.Base


class ValidacionDeLosMotivos(Base):
    def caso(self, motivos, esperado, nombre=None):
        datos = dict(copy.deepcopy(ARREGLO), motivos_estado=motivos)
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, datos)
        self.assertEqual(estado, "error", detalle)
        self.assertIn(esperado, detalle, nombre)
        self.assertNotIn("fallo inesperado", detalle)
        self.assertFalse(centinela.llamada)

    def test_la_clave_es_nula_o_una_lista_y_cualquier_otra_cosa_es_error(self):
        for malo in ("defecto", {}, 5, True):
            with self.subTest(malo=malo):
                self.caso(malo, "motivos_estado")
        self.caso([], "motivos_estado")

    def test_cada_motivo_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        def con(i, clave, valor):
            return con_cambio(MOTIVOS_ARREGLO, [i, clave], valor)

        casos = [
            ("un motivo que no es un objeto", con_cambio(MOTIVOS_ARREGLO, [1], "Vigente"), "motivos_estado[1]"),
            ("falta statecode", con_cambio(MOTIVOS_ARREGLO, [1, "statecode"], BORRAR), "statecode"),
            ("falta valor", con_cambio(MOTIVOS_ARREGLO, [1, "valor"], BORRAR), "valor"),
            ("falta origen", con_cambio(MOTIVOS_ARREGLO, [1, "origen"], BORRAR), "origen"),
            ("falta etiqueta", con_cambio(MOTIVOS_ARREGLO, [1, "etiqueta"], BORRAR), "etiqueta"),
            ("falta descripcion", con_cambio(MOTIVOS_ARREGLO, [1, "descripcion"], BORRAR), "descripcion"),
            ("clave de mas", con_cambio(MOTIVOS_ARREGLO, [1, "orden"], 3), "orden"),
            ("statecode 2: los estados no se crean", con(1, "statecode", 2), "statecode"),
            ("statecode texto", con(1, "statecode", "0"), "statecode"),
            ("statecode booleano", con(1, "statecode", False), "statecode"),
            ("origen desconocido", con(1, "origen", "nuevo"), "origen"),
            ("valor texto", con(1, "valor", "159460001"), "valor"),
            ("valor booleano", con(1, "valor", True), "valor"),
            ("descripcion no texto", con(1, "descripcion", None), "descripcion"),
            ("por defecto con el valor del otro estado", con(0, "valor", 2), "el motivo por defecto del statecode 0 tiene el valor 1"),
            ("por defecto con un valor propio", con(0, "valor", 159460007), "el motivo por defecto del statecode 0 tiene el valor 1"),
            ("por defecto del estado 1 con el valor 1", con(2, "valor", 1), "el motivo por defecto del statecode 1 tiene el valor 2"),
            ("agregado con el correlativo suelto", con(1, "valor", 3), "159460001"),
            ("agregado con el prefijo de opciones de otro publisher", con(1, "valor", 100000001), "15946"),
            ("agregado con correlativo cero", con(1, "valor", 159460000), "correlativo"),
            ("agregado con valor por defecto (1)", con(1, "valor", 1), "valor"),
            ("etiqueta en cadena unica", con(1, "etiqueta", "Vigente"), "etiqueta"),
            ("etiqueta sin el idioma 3082", con(1, "etiqueta", {"1033": "Vigente"}), "3082"),
            ("etiqueta con tilde", con(1, "etiqueta", por_idioma("Vigéntes")), "BP-PP-197"),
            ("etiqueta repetida en el mismo idioma", con(3, "etiqueta", por_idioma("Vigente")), "repetid"),
            ("valor repetido", con(4, "valor", 159460002), "repetid"),
        ]
        for nombre, motivos, esperado in casos:
            with self.subTest(nombre):
                self.caso(motivos, esperado, nombre)

    def test_faltan_los_motivos_por_defecto_o_sobran(self):
        sin_defecto_2 = [m for m in MOTIVOS_ARREGLO if m["valor"] != 2]
        self.caso(sin_defecto_2, "por defecto")
        self.caso([m for m in MOTIVOS_ARREGLO if m["valor"] != 1], "por defecto")
        self.caso(MOTIVOS_ARREGLO + [motivo(0, 1, "defecto", "Otro")], "repetid")

    def test_una_etiqueta_repetida_se_detecta_por_idioma_no_entre_idiomas(self):
        # «Vigente» en 1033 y «Active» en 3082 no choca con «Active» en 1033 y «Vigente» en 3082 de otro motivo?  Sí choca cada una en su idioma.
        a = dict(MOTIVOS_ARREGLO[1], etiqueta={"1033": "Valid", "3082": "Vigente"})
        b = dict(MOTIVOS_ARREGLO[3], etiqueta={"1033": "Cancelled", "3082": "Vigente"})
        self.caso(con_cambio(con_cambio(MOTIVOS_ARREGLO, [1], a), [3], b), "repetid")
        # el mismo texto en idiomas DISTINTOS de motivos distintos no es una repetición
        c = dict(MOTIVOS_ARREGLO[1], etiqueta={"1033": "Valid", "3082": "Vigente"})
        d = dict(MOTIVOS_ARREGLO[3], etiqueta={"1033": "Vigente", "3082": "Anulado"})
        datos = dict(copy.deepcopy(ARREGLO), motivos_estado=con_cambio(con_cambio(MOTIVOS_ARREGLO, [1], c), [3], d))
        cliente = tt.armar(ClienteSimulado(), datos)
        estado, _, detalle = self.con_cliente(cliente, datos, verificadas=SIN_MOTIVOS, permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_los_playbooks_de_la_version_0_1_0_no_traen_la_clave_y_se_leen_como_null(self):
        sin_clave = {k: v for k, v in tt.COMPLETO.items() if k != "motivos_estado"}
        self.assertNotIn("motivos_estado", sin_clave)
        cliente = tt.armar(ClienteSimulado(), sin_clave)
        estado, _, detalle = self.con_cliente(cliente, sin_clave)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertNotIn("motivos_estado", tb.caracteristicas_usadas(sin_clave))

    def test_un_valor_agregado_con_otro_prefijo_de_opciones_se_valida_contra_la_identidad_no_contra_una_constante(self):
        ident = dict(tt.IDENT, prefijo_opciones=10000)
        malo = dict(copy.deepcopy(ARREGLO), motivos_estado=MOTIVOS_ARREGLO)  # 159460001 con prefijo 10000: no coincide
        import tempfile
        from fixtures import playbook_md
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "p.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(ident, malo))
            estado, _, detalle = tb.construir(ruta, False, FabricaCentinela())
        self.assertEqual(estado, "error", detalle)
        self.assertIn("10000", detalle)


class MotivosSinEnsayar(Base):
    def test_sin_el_flag_es_bloqueado_sin_tocar_la_red_y_dice_que_esta_pendiente(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, ARREGLO, verificadas=SIN_MOTIVOS)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("motivos_estado", detalle)
        self.assertIn("--permitir-no-verificadas", detalle)
        self.assertIn("UpdateOptionValue", detalle)
        self.assertIn("InsertStatusValue", detalle)
        self.assertFalse(centinela.llamada)

    def test_motivos_null_no_usa_la_caracteristica(self):
        self.assertIn("motivos_estado", tb.CARACTERISTICAS)
        self.assertNotIn("motivos_estado", tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertIn("motivos_estado", tb.caracteristicas_usadas(ARREGLO))
        self.assertNotIn("motivos_estado", tb.caracteristicas_usadas(dict(ARREGLO, motivos_estado=None)))
        cliente = tt.armar(ClienteSimulado(), dict(ARREGLO, motivos_estado=None))
        estado, _, detalle = self.con_cliente(cliente, dict(ARREGLO, motivos_estado=None), verificadas=tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_ensayo_solo_vale_sobre_una_tabla_descartable(self):
        real = dict(copy.deepcopy(ARREGLO), nombre="sanic_carp_tbl_arreglo")
        estado, _, detalle = self.correr(FabricaCentinela(), real, verificadas=SIN_MOTIVOS, permitir=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("_tbl_zz", detalle)


class CreacionDeLosMotivos(Base):
    def kw(self, **extra):
        return dict(verificadas=SIN_MOTIVOS, permitir=True, **extra)

    def escrituras(self, cliente):
        return [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]

    def test_renombra_los_por_defecto_agrega_el_resto_en_el_orden_del_playbook_y_publica_al_final(self):
        cliente = tt.armar(ClienteSimulado(), ARREGLO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
        self.assertEqual(estado, "creado", detalle)
        orden = self.escrituras(cliente)
        self.assertEqual(orden, [("POST", "EntityDefinitions"),
                                 ("POST", "UpdateOptionValue"), ("POST", "InsertStatusValue"), ("POST", "UpdateOptionValue"),
                                 ("POST", "InsertStatusValue"), ("POST", "InsertStatusValue"), ("POST", "PublishXml")])

    def test_el_cuerpo_de_un_motivo_agregado_es_el_de_la_receta(self):
        cliente = tt.armar(ClienteSimulado(), ARREGLO, existe=False)
        self.con_cliente(cliente, ARREGLO, **self.kw())
        inserts = [l for l in cliente.llamadas if l["ruta"] == "InsertStatusValue"]
        self.assertEqual(3, len(inserts))
        cuerpo = inserts[0]["cuerpo"]
        self.assertEqual((TABLA, "statuscode", 0, 159460001), (cuerpo["EntityLogicalName"], cuerpo["AttributeLogicalName"], cuerpo["StateCode"], cuerpo["Value"]))
        self.assertEqual(tt.IDENT["solucion"], cuerpo["SolutionUniqueName"])
        self.assertEqual([(1033, "Vigente"), (3082, "Vigente")], [(l["LanguageCode"], l["Label"]) for l in cuerpo["Label"]["LocalizedLabels"]])
        self.assertEqual([1033, 3082], [l["LanguageCode"] for l in cuerpo["Description"]["LocalizedLabels"]])
        self.assertEqual("Acuerdo formalizado.", cuerpo["Description"]["LocalizedLabels"][0]["Label"])
        self.assertNotIn("Description", inserts[2]["cuerpo"])  # descripcion vacía: no se manda una etiqueta vacía
        self.assertEqual([1, 1], [i["cuerpo"]["StateCode"] for i in inserts[1:]])

    def test_el_cuerpo_de_un_renombre_es_el_de_la_receta(self):
        cliente = tt.armar(ClienteSimulado(), ARREGLO, existe=False)
        self.con_cliente(cliente, ARREGLO, **self.kw())
        renombres = [l for l in cliente.llamadas if l["ruta"] == "UpdateOptionValue"]
        self.assertEqual(2, len(renombres))
        cuerpo = renombres[0]["cuerpo"]
        self.assertEqual((TABLA, "statuscode", 1, True), (cuerpo["EntityLogicalName"], cuerpo["AttributeLogicalName"], cuerpo["Value"], cuerpo["MergeLabels"]))
        self.assertEqual([(1033, "Borrador"), (3082, "Borrador")], [(l["LanguageCode"], l["Label"]) for l in cuerpo["Label"]["LocalizedLabels"]])
        self.assertEqual(tt.IDENT["solucion"], cuerpo["SolutionUniqueName"])
        self.assertEqual(2, renombres[1]["cuerpo"]["Value"])

    def test_si_la_tabla_ya_coincide_no_escribe_nada(self):
        cliente = tt.armar(ClienteSimulado(), ARREGLO)
        estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_tabla_sin_motivos_no_manda_ninguna_llamada_de_estado(self):
        datos = dict(ARREGLO, motivos_estado=None)
        cliente = tt.armar(ClienteSimulado(), datos, existe=False)
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual([], [l for l in cliente.llamadas if l["ruta"] in ("InsertStatusValue", "UpdateOptionValue")])

    def test_si_la_plataforma_asigna_otro_valor_no_se_acepta_y_es_bloqueado_porque_esta_pendiente_de_verificar(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "InsertStatusValue", lambda ruta, cuerpo, sol: (200, {"NewOptionValue": cuerpo["Value"] + 7}, {}))
        tt.armar(cliente, ARREGLO, existe=False)
        estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("159460001", detalle)
        self.assertIn("159460008", detalle)
        self.assertIn("pendiente de verificar", detalle)
        self.assertEqual(1, len([l for l in cliente.llamadas if l["ruta"] == "InsertStatusValue"]))  # se detiene en el primero

    def test_la_respuesta_de_un_alta_con_forma_inesperada_es_error(self):
        for malo in ({"NewOptionValue": "159460001"}, {"NewOptionValue": None}, {}, {"NewOptionValue": True}):
            with self.subTest(malo=malo):
                cliente = ClienteSimulado()
                cliente.responder("POST", "InsertStatusValue", (200, malo, {}))
                tt.armar(cliente, ARREGLO, existe=False)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn("NewOptionValue", detalle)

    def test_si_la_plataforma_rechaza_el_renombre_del_motivo_por_defecto_es_bloqueado_y_explica_el_paso_manual(self):
        """Pendiente de la receta: que `UpdateOptionValue` acepte `statuscode`. Si no lo acepta, renombrar los dos motivos por
        defecto es un paso manual (como la fórmula). La herramienta no supone: lo dice y se detiene."""
        for estado_http in (400, 404, 405):
            with self.subTest(estado_http):
                cliente = ClienteSimulado()
                cliente.responder("POST", "UpdateOptionValue", (estado_http, {"error": "no se admite statuscode"}, {}))
                tt.armar(cliente, ARREGLO, existe=False)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertIn(f"HTTP {estado_http}", detalle)
                self.assertIn("UpdateOptionValue", detalle)
                self.assertIn("statuscode", detalle)
                self.assertIn("pendiente de verificar", detalle)
                self.assertIn("paso manual", detalle)
                self.assertIn("Borrador", detalle)

    def test_un_error_de_servidor_al_escribir_un_motivo_es_error_no_bloqueado(self):
        for ruta in ("UpdateOptionValue", "InsertStatusValue"):
            with self.subTest(ruta):
                cliente = ClienteSimulado()
                cliente.responder("POST", ruta, (500, {"error": "boom"}, {}))
                tt.armar(cliente, ARREGLO, existe=False)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertIn("incompleta", detalle)

    def test_el_orden_de_alta_respeta_el_del_playbook_aunque_los_por_defecto_vengan_al_final(self):
        reordenados = [MOTIVOS_ARREGLO[1], MOTIVOS_ARREGLO[3], MOTIVOS_ARREGLO[4], MOTIVOS_ARREGLO[0], MOTIVOS_ARREGLO[2]]
        datos = dict(copy.deepcopy(ARREGLO), motivos_estado=reordenados)
        cliente = tt.armar(ClienteSimulado(), datos, existe=False)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "creado", detalle)
        valores = [l["cuerpo"]["Value"] for l in cliente.llamadas if l["ruta"] in ("InsertStatusValue", "UpdateOptionValue")]
        self.assertEqual([m["valor"] for m in reordenados], valores)


class VerificacionDeLosMotivos(Base):
    def kw(self, **extra):
        return dict(verificadas=SIN_MOTIVOS, permitir=True, **extra)

    def difiere(self, esperado, datos=ARREGLO, **armado):
        cliente = tt.armar(ClienteSimulado(), datos, **armado)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn(esperado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_lee_statuscode_y_statecode_con_las_opciones_expandidas(self):
        cliente = tt.armar(ClienteSimulado(), ARREGLO)
        self.con_cliente(cliente, ARREGLO, **self.kw())
        rutas = [l["ruta"] for l in cliente.llamadas]
        status = [r for r in rutas if "statuscode')/Microsoft.Dynamics.CRM.StatusAttributeMetadata" in r]
        estado = [r for r in rutas if "statecode')/Microsoft.Dynamics.CRM.StateAttributeMetadata" in r]
        self.assertEqual((1, 1), (len(status), len(estado)))
        self.assertIn("$expand=OptionSet", status[0])
        self.assertIn("$expand=OptionSet", estado[0])

    def test_cada_diferencia_de_un_motivo(self):
        base = tt.opciones_status(ARREGLO)
        casos = [
            ("statuscode: falta el motivo 159460003", [o for o in base if o["Value"] != 159460003]),
            ("statuscode: el motivo 159460009 no está en el playbook", base + [{"Value": 159460009, "State": 1, "Label": label("Extra")}]),
            ("statuscode.159460001.State", [dict(o, State=1) if o["Value"] == 159460001 else o for o in base]),
            ("statuscode.1.Label[3082]", [dict(o, Label=label({"1033": "Borrador", "3082": "Active"})) if o["Value"] == 1 else o for o in base]),
            ("statuscode.2.Label[1033]", [dict(o, Label=label({"1033": "Inactive", "3082": "Terminado"})) if o["Value"] == 2 else o for o in base]),
            ("statuscode.159460002.Label[3082]", [dict(o, Label=label("Anulado", idiomas=[1033])) if o["Value"] == 159460002 else o for o in base]),
            ("statuscode.159460002.Label tiene etiquetas en un idioma que el playbook no declara",
             [dict(o, Label=label("Anulado", otros_idiomas=[(1046, "Anulado")])) if o["Value"] == 159460002 else o for o in base]),
        ]
        for esperado, opciones in casos:
            with self.subTest(esperado):
                self.difiere(esperado, status=tt.cuerpo_status(ARREGLO, opciones))

    def test_los_estados_son_exactamente_el_0_y_el_1(self):
        self.difiere("statecode", state=cuerpo_state([{"Value": 0, "Label": label("Active")}]))
        self.difiere("statecode", state=cuerpo_state([{"Value": 0, "Label": label("Active")}, {"Value": 1, "Label": label("Inactive")},
                                                      {"Value": 2, "Label": label("Otro")}]))

    def test_sin_motivos_declarados_statuscode_tiene_exactamente_el_1_del_estado_0_y_el_2_del_estado_1(self):
        datos = dict(ARREGLO, motivos_estado=None)
        base = tt.opciones_status(datos)
        casos = [
            ("statuscode: falta el motivo 2", [o for o in base if o["Value"] != 2]),
            ("statuscode: el motivo 159460001 no está en el playbook", base + [{"Value": 159460001, "State": 0, "Label": label("Vigente")}]),
            ("statuscode.2.State", [dict(o, State=0) if o["Value"] == 2 else o for o in base]),
        ]
        for esperado, opciones in casos:
            with self.subTest(esperado):
                self.difiere(esperado, datos=datos, status=tt.cuerpo_status(datos, opciones))
        # y NO compara etiquetas: con null el playbook no toca los nombres por defecto
        renombrados = [dict(o, Label=label("Cualquier nombre")) for o in base]
        cliente = tt.armar(ClienteSimulado(), datos, status=tt.cuerpo_status(datos, renombrados))
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_forma_inesperada_es_error_y_nombra_el_campo(self):
        base = tt.opciones_status(ARREGLO)
        malos = [
            ("OptionSet", {"LogicalName": "statuscode", "OptionSet": None}),
            ("OptionSet.Options", {"LogicalName": "statuscode", "OptionSet": {"Options": "x"}}),
            ("OptionSet.Options[0]", {"LogicalName": "statuscode", "OptionSet": {"Options": ["x"] + base[1:]}}),
            ("OptionSet.Options[1].Value", {"LogicalName": "statuscode", "OptionSet": {"Options": con_cambio(base, [1, "Value"], "159460001")}}),
            ("OptionSet.Options[1].State", {"LogicalName": "statuscode", "OptionSet": {"Options": con_cambio(base, [1, "State"], None)}}),
            ("OptionSet.Options[1].State", {"LogicalName": "statuscode", "OptionSet": {"Options": con_cambio(base, [1, "State"], True)}}),
            ("Label", {"LogicalName": "statuscode", "OptionSet": {"Options": con_cambio(base, [1, "Label"], "Vigente")}}),
            ("OptionSet.Options", {"LogicalName": "statuscode", "OptionSet": {"Options": []}}),
        ]
        for campo, cuerpo in malos:
            with self.subTest(campo):
                cliente = tt.armar(ClienteSimulado(), ARREGLO, status=cuerpo)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(campo, detalle)
                self.assertIn("statuscode", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_de_statecode_es_error(self):
        for campo, cuerpo in [("OptionSet.Options[0].Value", {"OptionSet": {"Options": [{"Value": "0", "Label": label("Active")}]}}),
                              ("OptionSet", {"OptionSet": [] })]:
            with self.subTest(campo):
                cliente = tt.armar(ClienteSimulado(), ARREGLO, state=cuerpo)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(campo, detalle)
                self.assertIn("statecode", detalle)

    def test_http_inesperado_al_leer_el_estado_es_error_y_nombra_la_consulta(self):
        for fragmento, nombre in (("StatusAttributeMetadata", "statuscode"), ("StateAttributeMetadata", "statecode")):
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder("GET", lambda r, f=fragmento: f in r, (500, {"error": {"message": "boom"}}, {}))
                tt.armar(cliente, ARREGLO)
                estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertIn(nombre, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_corregir_nombres_no_toca_los_motivos(self):
        base = tt.opciones_status(ARREGLO)
        cliente = tt.armar(ClienteSimulado(), ARREGLO, status=tt.cuerpo_status(ARREGLO, [dict(o, Label=label("Viejo")) if o["Value"] == 1 else o for o in base]))
        estado, _, detalle = self.con_cliente(cliente, ARREGLO, **self.kw(corregir_nombres=True))
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
