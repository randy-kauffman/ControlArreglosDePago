"""Pruebas de `herramientas/construir/icono_tabla.py`. Nunca tocan la red.

Lo que más importa: el ícono de una tabla es `EntityMetadata.IconVectorName`
(Learn, "Image web resources": SVG en lugar de IconSmall/Medium/Large). Se
cambia con un PUT de la definición COMPLETA de la tabla, partiendo de la que
devuelve el entorno, con `MSCRM.MergeLabels` (sin eso se pierden las etiquetas
de los otros idiomas), y después se publica la tabla. El web resource tiene
que existir antes: si falta, `bloqueado` sin escribir nada.
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

import icono_tabla as it  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="icono-tabla", inventario="12.27")

COMPONENTE = {
    "tipo": "icono-tabla",
    "tablas": [
        {"tabla": "sanic_carp_tbl_cliente", "icono": "sanic_carp_wr_svg_tbl_cliente"},
        {"tabla": "sanic_carp_tbl_arreglo", "icono": "sanic_carp_wr_svg_tbl_arreglo"},
    ],
}


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None):
    componente = COMPONENTE if componente is None else componente
    return (
        "# Playbook: icono-tabla · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(IDENT, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def definicion(tabla, icono=None, **cambios):
    d = {
        "@odata.context": "https://ensayo.crm.dynamics.com/api/data/v9.2/$metadata#EntityDefinitions/$entity",
        "MetadataId": "m-" + tabla,
        "LogicalName": tabla,
        "IconVectorName": icono,
        "IsManaged": False,
        "DisplayName": {"LocalizedLabels": [{"Label": "X", "LanguageCode": 1033}, {"Label": "X", "LanguageCode": 3082}]},
    }
    d.update(cambios)
    return d


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente))
        return self.ruta

    def armar(self, cliente, iconos=None, faltan_recursos=()):
        """`iconos`: {tabla: IconVectorName actual}. `faltan_recursos`: web resources que no existen."""
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.iconos = dict(iconos or {})

        def recurso(ruta, cuerpo, solucion):
            nombre = ruta.split("name eq '")[1].split("'")[0]
            return (200, {"value": [] if nombre in faltan_recursos else [{"webresourceid": "w-" + nombre, "name": nombre}]}, {})

        def tabla(ruta, cuerpo, solucion):
            nombre = ruta.split("LogicalName='")[1].split("'")[0]
            return (200, definicion(nombre, self.iconos.get(nombre)), {})

        cliente.responder("GET", lambda r: r.startswith("webresourceset?"), recurso)
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(LogicalName="), tabla)
        return cliente

    def con_escritura(self, cliente, publicar=204, put=204):
        self.puts = []

        def poner(ruta, cuerpo, solucion):
            self.puts.append((ruta, cuerpo, solucion))
            if put == 204:
                self.iconos[cuerpo["LogicalName"]] = cuerpo["IconVectorName"]
            return (put, {}, {})

        cliente.responder("PUT", lambda r: r.startswith("EntityDefinitions("), poner)
        cliente.responder("POST", "PublishXml", (publicar, {}, {}))
        return cliente

    def construir(self, componente=None, cliente=None, solo_verificar=False):
        cliente = cliente if cliente is not None else self.armar(ClienteSimulado())
        return it.construir(self.escribir(componente), solo_verificar, lambda: cliente)


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = it.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_claves_exactas(self):
        self.rechaza({"tipo": "icono-tabla"}, "faltan ['tablas']")
        self.rechaza(comp(sobrante=1), "sobran ['sobrante']")
        self.rechaza(comp(tablas=[{"tabla": "sanic_carp_tbl_cliente"}]), "icono")

    def test_la_lista_no_puede_venir_vacia(self):
        self.rechaza(comp(tablas=[]), "tablas")

    def test_la_tabla_es_propia_y_el_icono_un_svg_del_proyecto(self):
        self.rechaza(comp(tablas=[{"tabla": "account", "icono": "sanic_carp_wr_svg_tbl_cliente"}]), "sanic_carp_tbl_")
        self.rechaza(comp(tablas=[{"tabla": "sanic_carp_tbl_cliente", "icono": "sanic_carp_wr_js_comandos"}]),
                     "sanic_carp_wr_svg_")

    def test_una_tabla_no_se_repite(self):
        repetida = [{"tabla": "sanic_carp_tbl_cliente", "icono": "sanic_carp_wr_svg_a"},
                    {"tabla": "sanic_carp_tbl_cliente", "icono": "sanic_carp_wr_svg_b"}]
        self.rechaza(comp(tablas=repetida), "repetida")


class ContraElEntorno(Base):
    def test_si_ya_tienen_su_icono_no_se_escribe_nada(self):
        cliente = self.armar(ClienteSimulado(), iconos={
            "sanic_carp_tbl_cliente": "sanic_carp_wr_svg_tbl_cliente",
            "sanic_carp_tbl_arreglo": "sanic_carp_wr_svg_tbl_arreglo"})
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_informa_la_diferencia_sin_tocar(self):
        cliente = self.armar(ClienteSimulado(), iconos={"sanic_carp_tbl_cliente": "sanic_carp_wr_svg_lista"})
        estado, _, detalle = self.construir(cliente=cliente, solo_verificar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sanic_carp_tbl_cliente", detalle)
        self.assertIn("sanic_carp_wr_svg_lista", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_cambio_es_un_put_de_la_definicion_completa_con_mergelabels_y_se_publica(self):
        cliente = self.con_escritura(self.armar(ClienteSimulado(), iconos={
            "sanic_carp_tbl_arreglo": "sanic_carp_wr_svg_tbl_arreglo"}))
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("actualizado", estado, detalle)
        self.assertEqual(1, len(self.puts))
        ruta, cuerpo, solucion = self.puts[0]
        self.assertEqual("EntityDefinitions(m-sanic_carp_tbl_cliente)", ruta)
        self.assertEqual("sanic_carp_wr_svg_tbl_cliente", cuerpo["IconVectorName"])
        # La definición completa (las etiquetas siguen), sin el `@odata.context` que el PUT no admite.
        self.assertIn("DisplayName", cuerpo)
        self.assertNotIn("@odata.context", cuerpo)
        self.assertEqual(IDENT["solucion"], solucion)
        put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
        self.assertEqual("true", put["cabeceras"]["MSCRM.MergeLabels"])
        publicar = next(l for l in cliente.llamadas if l["metodo"] == "POST")
        self.assertIn("<entity>sanic_carp_tbl_cliente</entity>", publicar["cuerpo"]["ParameterXml"])

    def test_si_falta_el_web_resource_queda_bloqueado_sin_escribir(self):
        cliente = self.con_escritura(self.armar(ClienteSimulado(), faltan_recursos=("sanic_carp_wr_svg_tbl_arreglo",)))
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("sanic_carp_wr_svg_tbl_arreglo", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_put_rechazado_es_error(self):
        cliente = self.con_escritura(self.armar(ClienteSimulado()), put=400)
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HTTP 400", detalle)

    def test_una_tabla_managed_no_se_toca(self):
        cliente = self.con_escritura(self.armar(ClienteSimulado()))
        cliente._reglas.insert(0, ("GET", lambda r: r.startswith("EntityDefinitions(LogicalName='sanic_carp_tbl_cliente'"),
                                   (200, definicion("sanic_carp_tbl_cliente", IsManaged=True), {})))
        estado, _, detalle = self.construir(cliente=cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("managed", detalle)
        self.assertFalse(any(l["metodo"] == "PUT" and "cliente" in l["ruta"] for l in cliente.llamadas))


if __name__ == "__main__":
    unittest.main()
