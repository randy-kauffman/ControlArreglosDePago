"""`tabla.py` y la columna `decimal` (skill power-platform-especificar 0.2.0, `tabla.md` «Columna `decimal`»).

Dominio: la tabla Cuota del diccionario (`diseno/02-diccionario-datos.md`, 4.4) con sus montos `decimal(2) [0..999999999.99]`.
Ninguna prueba toca la red. El tipo está SIN ensayar contra la plataforma (0 ciclos): sin `--permitir-no-verificadas`
la herramienta devuelve `bloqueado` y no llama al entorno."""
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
from fixtures import por_idioma  # noqa: E402
from test_tabla import BORRAR, col, con_cambio  # noqa: E402

TABLA = "sanic_carp_tbl_zzcuota"

# La tabla Cuota del diccionario, con nombre descartable para poder ensayar (`_tbl_zz`).
CUOTA = {
    "tipo": "tabla",
    "nombre": TABLA,
    "displayname": por_idioma("Cuota"),
    "displayname_plural": por_idioma("Cuotas"),
    "descripcion": "Cuota del calendario de un arreglo, con los datos de su pago.",
    "propiedad": "usuario",
    "notas": False,
    "actividades": False,
    "auditoria": True,
    "primaria": {"nombre": "sanic_nombre", "displayname": por_idioma("Codigo"), "descripcion": "Código de la cuota.",
                 "largo": 30, "requerida": True, "autonumerico": ""},
    "columnas": [
        col("sanic_numero", "entero", minimo=1, maximo=60, requerida=True),
        col("sanic_fechavencimiento", "fecha", requerida=True),
        col("sanic_monto", "decimal", precision=2, minimo=0, maximo=999999999.99, requerida=True),
        col("sanic_fechapago", "fecha", auditoria=True),
        col("sanic_montopagado", "decimal", precision=2, minimo=0, maximo=999999999.99, auditoria=True),
    ],
}
SIN_DECIMAL = frozenset(tb.CARACTERISTICAS) - {"tipo:decimal"}
POSICION_MONTO = 2  # índice de sanic_monto en CUOTA["columnas"]


Base = tt.Base


class ValidacionDelDecimal(Base):
    """Cada clave del tipo, presente y con el tipo equivocado, ausente y de más; ninguna llega a la red."""

    CASOS = [
        (["columnas", POSICION_MONTO, "precision"], BORRAR, "precision"),
        (["columnas", POSICION_MONTO, "minimo"], BORRAR, "minimo"),
        (["columnas", POSICION_MONTO, "maximo"], BORRAR, "maximo"),
        (["columnas", POSICION_MONTO, "formato"], "Text", "formato"),          # el decimal NO tiene formato (DecimalAttributeMetadata no lo trae)
        (["columnas", POSICION_MONTO, "precision"], "2", "precision"),
        (["columnas", POSICION_MONTO, "precision"], True, "precision"),
        (["columnas", POSICION_MONTO, "precision"], 2.5, "precision"),
        (["columnas", POSICION_MONTO, "precision"], 11, "precision"),
        (["columnas", POSICION_MONTO, "precision"], -1, "precision"),
        (["columnas", POSICION_MONTO, "minimo"], "0", "minimo"),
        (["columnas", POSICION_MONTO, "minimo"], True, "minimo"),
        (["columnas", POSICION_MONTO, "minimo"], None, "minimo"),
        (["columnas", POSICION_MONTO, "maximo"], "999999999.99", "maximo"),
        (["columnas", POSICION_MONTO, "maximo"], False, "maximo"),
        (["columnas", POSICION_MONTO, "minimo"], -100000000000.01, "minimo"),   # fuera de ±100.000.000.000
        (["columnas", POSICION_MONTO, "maximo"], 100000000000.01, "maximo"),
        (["columnas", POSICION_MONTO, "minimo"], 1000000000, "minimo"),          # minimo mayor que maximo (maximo 999999999.99)
        (["columnas", POSICION_MONTO, "maximo"], -1, "maximo"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta} <- {'BORRAR' if valor is BORRAR else repr(valor)}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(CUOTA, ruta, valor))
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertIn(f"columnas[{POSICION_MONTO}]", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)

    def test_mas_de_15_digitos_se_rechaza_porque_las_apps_manejan_solo_15_con_exactitud(self):
        # 11 dígitos enteros + 10 decimales = 21 > 15
        datos = con_cambio(con_cambio(CUOTA, ["columnas", POSICION_MONTO, "precision"], 10), ["columnas", POSICION_MONTO, "maximo"], 99999999999)
        estado, _, detalle = self.correr(FabricaCentinela(), datos)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("15", detalle)
        self.assertIn("columnas[2]", detalle)
        # 12 enteros + 2 decimales = 14: pasa la validación (el máximo documentado, 100.000.000.000, tiene 12 dígitos)
        limite = con_cambio(CUOTA, ["columnas", POSICION_MONTO, "maximo"], 100000000000)
        cliente = tt.armar(ClienteSimulado(), limite)
        estado, _, detalle = self.con_cliente(cliente, limite, verificadas=SIN_DECIMAL, permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_un_playbook_valido_con_enteros_y_con_decimales_en_el_rango_pasa(self):
        for minimo, maximo in ((0, 999999999.99), (0.01, 10), (-100000000000, 100000000000), (0, 0)):
            with self.subTest(minimo=minimo, maximo=maximo):
                datos = con_cambio(con_cambio(CUOTA, ["columnas", POSICION_MONTO, "minimo"], minimo), ["columnas", POSICION_MONTO, "maximo"], maximo)
                cliente = tt.armar(ClienteSimulado(), datos)
                estado, _, detalle = self.con_cliente(cliente, datos, verificadas=SIN_DECIMAL, permitir=True)
                self.assertEqual(estado, "ya_existia", detalle)


class DecimalSinEnsayar(Base):
    """«Ensayo antes de usar algo por primera vez»: el tipo no está verificado contra la plataforma."""

    def test_sin_el_flag_es_bloqueado_sin_tocar_la_red_y_dice_que_hay_que_ensayarlo(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, CUOTA, verificadas=SIN_DECIMAL)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("tipo:decimal", detalle)
        self.assertIn("--permitir-no-verificadas", detalle)
        self.assertFalse(centinela.llamada)

    def test_el_tipo_decimal_es_una_caracteristica_del_formato_y_todavia_no_esta_verificada(self):
        self.assertIn("tipo:decimal", tb.CARACTERISTICAS)
        self.assertNotIn("tipo:decimal", tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertIn("tipo:decimal", tb.caracteristicas_usadas(CUOTA))
        self.assertNotIn("tipo:decimal", tb.caracteristicas_usadas(tt.COMPLETO))

    def test_el_ensayo_solo_vale_sobre_una_tabla_descartable(self):
        real = dict(copy.deepcopy(CUOTA), nombre="sanic_carp_tbl_cuota")
        estado, _, detalle = self.correr(FabricaCentinela(), real, verificadas=SIN_DECIMAL, permitir=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("_tbl_zz", detalle)

    def test_la_herramienta_real_por_defecto_bloquea_el_decimal(self):
        estado, _, detalle = self.correr(FabricaCentinela(), CUOTA, verificadas=tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertEqual(estado, "bloqueado", detalle)

    def test_la_precision_cero_esta_pendiente_de_verificar_y_se_bloquea_aparte(self):
        # Learn documenta 1 a 10; el 0 para un decimal común no se verificó. Aunque el decimal estuviera verificado, el 0 no.
        cero = con_cambio(CUOTA, ["columnas", POSICION_MONTO, "precision"], 0)
        self.assertIn("decimal:precision0", tb.caracteristicas_usadas(cero))
        self.assertNotIn("decimal:precision0", tb.caracteristicas_usadas(CUOTA))
        solo_decimal = frozenset(tb.CARACTERISTICAS) - {"decimal:precision0"}
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, cero, verificadas=solo_decimal)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("decimal:precision0", detalle)
        self.assertIn("entero", detalle)  # la salida honesta: si no hay decimales, la columna es un entero
        self.assertFalse(centinela.llamada)


class CuerpoDelDecimal(unittest.TestCase):
    def test_el_cuerpo_es_el_documentado_por_learn(self):
        ident = tt.IDENT
        payload = tb.construir_payload(CUOTA, ident, {})
        monto = next(a for a in payload["Attributes"] if a["SchemaName"] == "sanic_monto")
        self.assertEqual("Microsoft.Dynamics.CRM.DecimalAttributeMetadata", monto["@odata.type"])
        self.assertEqual("Decimal", monto["AttributeType"])
        self.assertEqual({"Value": "DecimalType"}, monto["AttributeTypeName"])
        self.assertEqual((2, 0, 999999999.99), (monto["Precision"], monto["MinValue"], monto["MaxValue"]))
        self.assertEqual("ApplicationRequired", monto["RequiredLevel"]["Value"])
        self.assertNotIn("Format", monto)  # DecimalAttributeMetadata no tiene formato
        self.assertEqual([1033, 3082], [l["LanguageCode"] for l in monto["DisplayName"]["LocalizedLabels"]])

    def test_el_json_del_cuerpo_lleva_el_monto_exacto(self):
        """El cuerpo se serializa con json.dumps: el límite 999999999.99 tiene que salir tal cual, no como 999999999.9900001."""
        import json
        from _comun import dividir_secciones, obtener_componente
        from fixtures import playbook_md
        datos = obtener_componente(dividir_secciones(playbook_md(tt.IDENT, CUOTA)), "tabla")  # leído como Decimal
        payload = tb.construir_payload(datos, tt.IDENT, {})
        monto = next(a for a in payload["Attributes"] if a["SchemaName"] == "sanic_monto")
        self.assertIn('"MaxValue": 999999999.99', json.dumps(monto))


class VerificacionDelDecimal(Base):
    def kw(self, **extra):
        return dict(verificadas=SIN_DECIMAL, permitir=True, **extra)

    def test_camino_feliz_crea_y_relee(self):
        cliente = tt.armar(ClienteSimulado(), CUOTA, existe=False)
        estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
        self.assertEqual(estado, "creado", detalle)
        post = next(l for l in cliente.llamadas if l["metodo"] == "POST" and l["ruta"] == "EntityDefinitions")
        monto = next(a for a in post["cuerpo"]["Attributes"] if a["SchemaName"] == "sanic_monto")
        self.assertEqual("Microsoft.Dynamics.CRM.DecimalAttributeMetadata", monto["@odata.type"])
        # y la lectura del tipo usó el cast del Web API del decimal, con lo que se compara
        consultas = [l["ruta"] for l in cliente.llamadas if "DecimalAttributeMetadata" in l["ruta"]]
        self.assertEqual(1, len(consultas))
        for propiedad in ("LogicalName", "Precision", "MinValue", "MaxValue"):
            self.assertIn(propiedad, consultas[0])

    def test_segunda_ejecucion_ya_existia_y_no_escribe(self):
        cliente = tt.armar(ClienteSimulado(), CUOTA)
        estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_cada_diferencia_difiere_y_nombra_la_columna_y_la_propiedad(self):
        pt = tt.filas_por_tipo(CUOTA)
        casos = [
            ("sanic_monto.Precision", con_cambio(pt, ["Decimal", 0, "Precision"], 4)),
            ("sanic_monto.MinValue", con_cambio(pt, ["Decimal", 0, "MinValue"], 1.0)),
            ("sanic_monto.MaxValue", con_cambio(pt, ["Decimal", 0, "MaxValue"], 999999999.9)),
            ("sanic_montopagado.Precision", con_cambio(pt, ["Decimal", 1, "Precision"], 3)),
            ("sanic_montopagado.MaxValue", con_cambio(pt, ["Decimal", 1, "MaxValue"], 100.0)),
        ]
        for esperado, por_tipo in casos:
            with self.subTest(esperado):
                cliente = tt.armar(ClienteSimulado(), CUOTA, por_tipo=por_tipo)
                estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_el_tipo_de_columna_equivocado_difiere(self):
        # En el entorno el monto es un entero: no se puede comparar como decimal y se dice.
        gen = tt.genericas_de(CUOTA)
        cliente = tt.armar(ClienteSimulado(), CUOTA, genericas=con_cambio(gen, [3, "AttributeTypeName"], {"Value": "IntegerType"}))
        estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_monto.tipo", detalle)
        self.assertIn("DecimalType", detalle)

    def test_la_comparacion_es_decimal_exacta_no_de_punto_flotante(self):
        # 0.1 + 0.2 != 0.3 en float; el entorno devuelve el JSON tal cual y el playbook se lee como Decimal.
        datos = con_cambio(CUOTA, ["columnas", POSICION_MONTO, "maximo"], 0.3)
        pt = tt.filas_por_tipo(datos)
        pt["Decimal"][0]["MaxValue"] = 0.1 + 0.2  # 0.30000000000000004: otro número
        cliente = tt.armar(ClienteSimulado(), datos, por_tipo=pt)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_monto.MaxValue", detalle)

    def test_el_entorno_devuelve_el_maximo_entero_como_entero_y_coincide(self):
        datos = con_cambio(con_cambio(CUOTA, ["columnas", POSICION_MONTO, "maximo"], 100), ["columnas", POSICION_MONTO, "minimo"], 0)
        pt = tt.filas_por_tipo(datos)
        pt["Decimal"][0]["MaxValue"] = 100       # el JSON del entorno trae un 100 entero
        pt["Decimal"][0]["MinValue"] = 0
        cliente = tt.armar(ClienteSimulado(), datos, por_tipo=pt)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)

    def test_forma_inesperada_es_error_y_nombra_el_campo(self):
        pt = tt.filas_por_tipo(CUOTA)
        casos = [
            ("value[0].Precision", con_cambio(pt, ["Decimal", 0, "Precision"], "2")),
            ("value[0].Precision", con_cambio(pt, ["Decimal", 0, "Precision"], True)),
            ("value[0].Precision", con_cambio(pt, ["Decimal", 0, "Precision"], None)),
            ("value[0].MinValue", con_cambio(pt, ["Decimal", 0, "MinValue"], "0")),
            ("value[0].MinValue", con_cambio(pt, ["Decimal", 0, "MinValue"], None)),
            ("value[0].MaxValue", con_cambio(pt, ["Decimal", 0, "MaxValue"], True)),
            ("value[1].MaxValue", con_cambio(pt, ["Decimal", 1, "MaxValue"], [1])),
        ]
        for campo, por_tipo in casos:
            with self.subTest(campo, por_tipo=por_tipo["Decimal"]):
                cliente = tt.armar(ClienteSimulado(), CUOTA, por_tipo=por_tipo)
                estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_un_decimal_que_la_plataforma_no_devuelve_en_su_familia_es_error_de_forma_no_ausencia_silenciosa(self):
        pt = copy.deepcopy(tt.filas_por_tipo(CUOTA))
        pt["Decimal"] = pt["Decimal"][:1]  # falta sanic_montopagado entre las columnas decimales
        cliente = tt.armar(ClienteSimulado(), CUOTA, por_tipo=pt)
        estado, _, detalle = self.con_cliente(cliente, CUOTA, **self.kw())
        self.assertEqual(estado, "error", detalle)
        self.assertIn("sanic_montopagado", detalle)
        self.assertIn("forma inesperada", detalle)


if __name__ == "__main__":
    unittest.main()
