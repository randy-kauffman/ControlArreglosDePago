"""Pruebas de las columnas de ARCHIVO en `herramientas/construir/datos.py`. Nunca tocan la red.

El caso real es la versión de plantilla v07 (inventario 9.1): la fila lleva el
.xlsx aprobado en `sanic_archivo`. Un archivo no viaja en el cuerpo del alta:
se sube aparte (PATCH a `<conjunto>(<id>)/<columna>`), y la única forma honesta
de saber si el que está en el entorno es el del playbook es bajarlo y comparar
los bytes, no solo el nombre.
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

import datos as dt  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="datos", inventario="9.1")

TABLA = "sanic_carp_tbl_versionplantilla"
CONJUNTO = "sanic_carp_tbl_versionplantillas"
PRIMARIA = "sanic_carp_tbl_versionplantillaid"
ID = "id-v07"
CONTENIDO = b"PK\x03\x04 plantilla de mentira v07"
NOMBRE = "F-NIC-0000052_v07.xlsx"


def fila(**cambios):
    f = {"sanic_nombre": "v07", "sanic_capacidad": 60,
         "sanic_archivo": {"archivo": "plantilla.xlsx", "nombre": NOMBRE}}
    f.update(cambios)
    return f


def comp(filas=None, **cambios):
    d = {"tipo": "datos", "tabla": TABLA, "clave": ["sanic_nombre"], "filas": [fila()] if filas is None else filas}
    d.update(cambios)
    return d


def playbook(componente):
    return (
        "# Playbook: datos · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(IDENT, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")
        with open(os.path.join(self.dir.name, "plantilla.xlsx"), "wb") as f:
            f.write(CONTENIDO)
        self.guardada = None      # la fila en el entorno (sin el archivo)
        self.archivo = None       # (nombre, bytes) del archivo en el entorno

    def escribir(self, componente=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(comp() if componente is None else componente))
        return self.ruta

    def armar(self, guardada=None, archivo=None, bajada=None, subida=None):
        """`bajada`: bytes que devuelve la descarga, si tienen que ser otros que los guardados.
        `subida`: respuesta a la subida del archivo; por defecto la acepta y la guarda."""
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.guardada, self.archivo = guardada, archivo
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions("),
                          (200, {"EntitySetName": CONJUNTO, "PrimaryIdAttribute": PRIMARIA}, {}))

        def leer_filas(ruta, cuerpo, solucion):
            if self.guardada is None:
                return (200, {"value": []}, {})
            f = dict(self.guardada)
            f["sanic_archivo_name"] = self.archivo[0] if self.archivo else None
            return (200, {"value": [f]}, {})

        cliente.responder("GET", lambda r: r.startswith(CONJUNTO + "?"), leer_filas)

        def bajar(ruta, cuerpo, solucion):
            if bajada is not None:
                return (200, bajada, {})
            if self.archivo is None:
                return (404, {"error": "no hay archivo"}, {})
            return (200, self.archivo[1], {})

        cliente.responder("GET", f"{CONJUNTO}({ID})/sanic_archivo/$value", bajar)

        def crear(ruta, cuerpo, solucion):
            self.guardada = {PRIMARIA: ID, **cuerpo}
            return (204, {}, {})

        cliente.responder("POST", CONJUNTO, crear)

        def subir(ruta, cuerpo, solucion):
            if subida is not None:
                return subida
            self.archivo = (cuerpo["x-ms-file-name"], cuerpo["contenido"])
            return (204, {}, {})

        cliente.responder("PATCH", f"{CONJUNTO}({ID})/sanic_archivo", subir)
        return cliente

    def subidas(self, cliente):
        return [l for l in cliente.llamadas if l["metodo"] == "PATCH" and l["ruta"].endswith("/sanic_archivo")]


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = dt.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada, "no tenía que intentar construir el cliente de Dataverse")

    def test_un_archivo_que_no_existe_se_rechaza_sin_tocar_la_red(self):
        self.rechaza(comp([fila(sanic_archivo={"archivo": "no-esta.xlsx"})]), "no se pudo leer el archivo")

    def test_un_archivo_con_claves_desconocidas_se_rechaza(self):
        self.rechaza(comp([fila(sanic_archivo={"archivo": "plantilla.xlsx", "tipo": "xlsx"})]), "solo se admite {archivo, nombre}")

    def test_el_nombre_del_archivo_tiene_que_ser_texto(self):
        self.rechaza(comp([fila(sanic_archivo={"archivo": "plantilla.xlsx", "nombre": 7})]), "nombre")

    def test_una_columna_de_archivo_no_puede_ser_clave_de_negocio(self):
        self.rechaza(comp(clave=["sanic_archivo"]), "no puede ser parte de la clave")


class ContraElEntorno(Base):
    def test_el_alta_sube_el_archivo_aparte_y_lo_verifica_bajandolo(self):
        cliente = self.armar()
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        post = [l for l in cliente.llamadas if l["metodo"] == "POST"][0]
        self.assertNotIn("sanic_archivo", post["cuerpo"], "el archivo no viaja en el cuerpo del alta")
        self.assertEqual({"sanic_nombre": "v07", "sanic_capacidad": 60}, post["cuerpo"])
        subidas = self.subidas(cliente)
        self.assertEqual(1, len(subidas))
        self.assertEqual(NOMBRE, subidas[0]["cuerpo"]["x-ms-file-name"])
        self.assertEqual(CONTENIDO, subidas[0]["cuerpo"]["contenido"])
        self.assertTrue(any(l["ruta"].endswith("/$value") for l in cliente.llamadas), "tenía que bajarlo para comprobarlo")

    def test_sin_nombre_se_sube_con_el_nombre_del_archivo_local(self):
        cliente = self.armar()
        estado, _, detalle = dt.construir(self.escribir(comp([fila(sanic_archivo={"archivo": "plantilla.xlsx"})])), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        self.assertEqual("plantilla.xlsx", self.subidas(cliente)[0]["cuerpo"]["x-ms-file-name"])

    def test_correr_dos_veces_no_vuelve_a_subir_el_archivo(self):
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=(NOMBRE, CONTENIDO))
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_archivo_con_el_mismo_nombre_y_otros_bytes_se_denuncia_y_no_se_pisa(self):
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=(NOMBRE, b"otro contenido"))
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sanic_archivo", detalle)
        self.assertIn("contenido", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_fila_sin_archivo_se_denuncia(self):
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=None)
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no tiene archivo", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_completar_vuelve_a_subir_el_archivo_que_difiere(self):
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=("viejo.xlsx", b"viejo"))
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("actualizado", estado, detalle)
        self.assertEqual(1, len(self.subidas(cliente)))
        self.assertEqual((NOMBRE, CONTENIDO), self.archivo)

    def test_solo_verificar_con_completar_no_sube_nada(self):
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=("viejo.xlsx", b"viejo"))
        estado, _, detalle = dt.construir(self.escribir(), True, lambda: cliente, completar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_plugin_que_rechaza_la_subida_se_informa_con_su_mensaje(self):
        # El caso real: la versión ya es Vigente y el plugin no deja reemplazar el archivo (CARP-VER-003).
        cliente = self.armar(guardada={PRIMARIA: ID, "sanic_nombre": "v07", "sanic_capacidad": 60}, archivo=("viejo.xlsx", b"viejo"),
                             subida=(400, {"error": "CARP-VER-003 La version no es Borrador"}, {}))
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente, completar=True)
        self.assertEqual("error", estado, detalle)
        self.assertIn("CARP-VER-003", detalle)

    def test_si_lo_que_se_baja_no_es_lo_que_se_subio_es_error(self):
        cliente = self.armar(bajada=b"bytes cambiados")
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("sanic_archivo", detalle)

    def test_el_alta_rechazada_no_intenta_subir_el_archivo(self):
        cliente = self.armar()
        cliente._reglas = [r for r in cliente._reglas if r[0] != "POST"]
        cliente.responder("POST", CONJUNTO, (400, {"error": "CARP-VER-001 numero de version invalido"}, {}))
        estado, _, detalle = dt.construir(self.escribir(), False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("CARP-VER-001", detalle)
        self.assertEqual([], self.subidas(cliente))


if __name__ == "__main__":
    unittest.main()
