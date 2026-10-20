"""Pruebas de `herramientas/construir/referencia_conexion.py`. Nunca tocan la red.

Lo que más importa: la connection reference (nombre lógico, conector) viaja en
la solución; la CONEXION a la que apunta es de este entorno (la crea una persona
iniciando sesión). Por eso la conexión puede faltar en el playbook (`null`) y
la herramienta lo informa sin inventarla; y el conector no se puede cambiar.
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

import referencia_conexion as rc  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="referencia-conexion", inventario="10.5")

REF_ID = "77777777-7777-7777-7777-777777777777"

COMPONENTE = {
    "tipo": "referencia-conexion",
    "nombre": "sanic_carp_conr_outlook",
    "displayname": "CONR - CARP - Outlook",
    "descripcion": "Office 365 Outlook de la cuenta de servicio, para los avisos de los flujos.",
    "conector": "shared_office365",
    "conexion": "c0ffee00c0ffee00c0ffee00c0ffee00",
}


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None):
    componente = COMPONENTE if componente is None else componente
    return (
        "# Playbook: referencia-conexion · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(IDENT, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def fila(datos=None, **cambios):
    datos = COMPONENTE if datos is None else datos
    f = {
        "connectionreferenceid": REF_ID,
        "connectionreferencelogicalname": datos["nombre"],
        "connectionreferencedisplayname": datos["displayname"],
        "description": datos["descripcion"],
        "connectorid": rc.RAIZ_CONECTOR + datos["conector"],
        "connectionid": datos["conexion"],
        "ismanaged": False,
    }
    f.update(cambios)
    return f


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente))
        return self.ruta

    def armar(self, cliente, referencia=None, en_solucion=True):
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.referencia = referencia
        cliente.responder("GET", lambda r: r.startswith("connectionreferences?"),
                          lambda r, c, s: (200, {"value": [] if self.referencia is None else [self.referencia]}, {}))
        cliente.responder("GET", es_ruta_solutioncomponents,
                          (200, {"value": [{"componenttype": 10132, "objectid": REF_ID}] if en_solucion else []}, {}))
        return cliente

    def construir(self, componente=None, cliente=None, solo_verificar=False, completar=False):
        cliente = cliente if cliente is not None else self.armar(ClienteSimulado())
        return rc.construir(self.escribir(componente), solo_verificar, lambda: cliente, completar=completar)


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = rc.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_una_clave_de_mas_o_de_menos_se_rechaza(self):
        self.rechaza({k: v for k, v in COMPONENTE.items() if k != "conexion"}, "faltan ['conexion']")
        self.rechaza(comp(sobrante=1), "sobran ['sobrante']")

    def test_el_nombre_sigue_la_convencion_de_la_identidad(self):
        self.rechaza(comp(nombre="sanic_carp_ev_outlook"), "no sigue el patrón sanic_carp_conr_")
        self.rechaza(comp(nombre="sanic_carp_conr_out-look"), "letras y dígitos ASCII y guión bajo")

    def test_el_displayname_va_sin_tildes(self):
        self.rechaza(comp(displayname="CONR - CARP - Conexión"), "displayname")

    def test_el_conector_es_un_nombre_de_api_no_una_ruta(self):
        # La herramienta arma la ruta `/providers/Microsoft.PowerApps/apis/<conector>`.
        self.rechaza(comp(conector="/providers/Microsoft.PowerApps/apis/shared_office365"), "conector")
        self.rechaza(comp(conector="office365"), "shared_")

    def test_la_conexion_puede_ser_nula_pero_no_vacia(self):
        self.rechaza(comp(conexion=""), "conexion")


class ContraElEntorno(Base):
    def test_el_alta_crea_la_referencia_en_la_solucion_con_su_conexion(self):
        cliente = self.armar(ClienteSimulado())

        def crear(ruta, cuerpo, solucion):
            self.referencia = fila()
            return (204, {}, {})

        cliente.responder("POST", "connectionreferences", crear)
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("creado", estado, detalle)
        alta = next(l for l in cliente.llamadas if l["metodo"] == "POST")
        self.assertEqual(IDENT["solucion"], alta["solucion"])
        self.assertEqual("/providers/Microsoft.PowerApps/apis/shared_office365", alta["cuerpo"]["connectorid"])
        self.assertEqual(COMPONENTE["conexion"], alta["cuerpo"]["connectionid"])
        self.assertEqual("sanic_carp_conr_outlook", alta["cuerpo"]["connectionreferencelogicalname"])

    def test_sin_conexion_se_crea_igual_y_se_informa_bloqueado(self):
        # La conexión la crea una persona iniciando sesión: la referencia se
        # crea igual (viaja en la solución) y el estado dice qué falta.
        cliente = self.armar(ClienteSimulado())
        sin = comp(conexion=None)

        def crear(ruta, cuerpo, solucion):
            self.assertNotIn("connectionid", cuerpo)
            self.referencia = fila(sin)
            return (204, {}, {})

        cliente.responder("POST", "connectionreferences", crear)
        estado, _, detalle = self.construir(sin, cliente=cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("no tiene conexión", detalle)

    def test_si_ya_esta_igual_no_se_escribe_nada(self):
        cliente = self.armar(ClienteSimulado(), referencia=fila())
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_no_crea_nada(self):
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = self.construir(cliente=cliente, solo_verificar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_otra_conexion_se_informa_y_solo_se_corrige_con_completar(self):
        cliente = self.armar(ClienteSimulado(), referencia=fila(connectionid="otra"))
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("connectionid", detalle)
        self.assertFalse(cliente.hubo_escritura())

        def parchear(ruta, cuerpo, solucion):
            self.referencia = dict(self.referencia, **{"connectionid": cuerpo["connectionid"]})
            return (204, {}, {})

        cliente.responder("PATCH", lambda r: r.startswith("connectionreferences("), parchear)
        estado, _, detalle = self.construir(cliente=cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)

    def test_un_conector_distinto_es_inmutable_y_no_se_toca(self):
        cliente = self.armar(ClienteSimulado(), referencia=fila(connectorid=rc.RAIZ_CONECTOR + "shared_teams"))
        estado, _, detalle = self.construir(cliente=cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_fuera_de_la_solucion_se_informa(self):
        cliente = self.armar(ClienteSimulado(), referencia=fila(), en_solucion=False)
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no figura como componente de la solución", detalle)


if __name__ == "__main__":
    unittest.main()
