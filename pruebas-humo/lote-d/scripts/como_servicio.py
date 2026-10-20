"""Lote D (2026-10-20): corre una herramienta de herramientas/construir HACIENDOSE PASAR por la cuenta de servicio de Dev
(admin@55xljh.onmicrosoft.com, System Administrator), con la cabecera `CallerObjectId` (impersonacion de Dataverse).
Hace falta porque la plataforma exige que quien asocia una conexion a una connection reference, o activa un flujo que la usa,
tenga permiso sobre esa conexion: la identidad de la herramienta (service principal) no lo tiene; la cuenta de servicio, que es
la duena de la conexion, si. Asi el flujo queda ADEMAS a nombre de la cuenta de servicio (13.6).

    python3 pruebas-humo/lote-d/scripts/como_servicio.py <herramienta> <playbook.md> [banderas...]

Nunca imprime secretos ni tokens."""
import importlib
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
sys.path.insert(0, os.path.join(RAIZ, "herramientas", "construir"))
sys.path.insert(0, os.path.join(RAIZ, "herramientas"))
from dataverse_api import Dataverse  # noqa: E402
from _comun import salida  # noqa: E402

UPN = "admin@55xljh.onmicrosoft.com"


class ComoServicio:
    def __init__(self):
        self._dv = Dataverse()
        est, b, _ = self._dv.call("GET", f"systemusers?$select=azureactivedirectoryobjectid&$filter=domainname eq '{UPN}'")
        self.oid = b["value"][0]["azureactivedirectoryobjectid"]
        self.api = self._dv.api

    def call(self, metodo, ruta, cuerpo=None, solucion=None, cabeceras=None, timeout=180):
        h = dict(cabeceras or {})
        h["CallerObjectId"] = self.oid
        return self._dv.call(metodo, ruta, cuerpo, solucion=solucion, cabeceras=h, timeout=timeout)

    def __getattr__(self, nombre):
        return getattr(self._dv, nombre)


herr, playbook, banderas = sys.argv[1], sys.argv[2], set(sys.argv[3:])
mod = importlib.import_module(herr)
kw = {"completar": "--completar" in banderas}
est, comp, det = mod.construir(playbook, "--solo-verificar" in banderas, ComoServicio, **kw)
sys.exit(salida(est, comp, det))
