"""Utilidades de la tanda 2b de pruebas de humo (2026-10-20), usuario NO admin. Nunca imprime secretos ni tokens.
Reutiliza t2lib (tanda 2) y apunta el estado y la evidencia a tanda2b/. Los GUID de las filas HUMO salen del estado de la tanda 2."""
import json
import os
import sys

AQUI2B = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI2B, "..", "tanda2"))
from t2lib import *  # noqa: E402,F401,F403
import hlib as _h  # noqa: E402
import t2lib as _t  # noqa: E402

ESTADO2 = os.path.join(AQUI2B, "..", "tanda2", "estado.json")
_h.ESTADO = os.path.join(AQUI2B, "estado.json")
_h.EVID = os.path.join(AQUI2B, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
EVID = _h.EVID
ESTADO = _h.ESTADO
_t.EVID = EVID
_t.ESTADO = ESTADO


def fila2(clave):
    """GUID de una fila HUMO creada en la tanda 2."""
    with open(ESTADO2, encoding="utf-8") as f:
        for x in reversed(json.load(f)["filas"]):
            if x["clave"] == clave:
                return x["id"]
    raise KeyError(clave)
