"""Utilidades del lote B de pruebas de humo (2026-10-20). Nunca imprime secretos ni tokens.
Reutiliza hlib/hmap de la tanda 1 y apunta el estado y la evidencia a lote-b/. Los GUID de las filas HUMO de las tandas 1 y 2 salen de sus estado.json."""
import json
import os
import sys

AQUIB = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUIB, "..", "tanda1"))
import hlib as _h  # noqa: E402
from hmap import *  # noqa: E402,F401,F403

_h.ESTADO = os.path.join(AQUIB, "estado.json")
_h.EVID = os.path.join(AQUIB, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
EVID = _h.EVID
ESTADO = _h.ESTADO

# Estados (diseno/07 5.4; los mismos numeros que usa comandos.js)
CNV_ABIERTA, CNV_FINALIZADA = 1, 2
ARR_BORRADOR, ARR_TERMINADO, ARR_VIGENTE, ARR_ANULADO, ARR_DESCARTADO = 1, 2, 159460001 + 0, 159460002, 159460003
# OJO: ARR_VIGENTE = 159460001 (statuscode del Vigente); se deja explicito abajo
ARR_VIGENTE = 159460001


def fila_de(tanda, clave):
    """GUID de una fila HUMO creada en otra tanda (tanda1 o tanda2)."""
    ruta = os.path.join(AQUIB, "..", tanda, "estado.json")
    with open(ruta, encoding="utf-8") as f:
        for x in reversed(json.load(f)["filas"]):
            if x["clave"] == clave:
                return x["id"]
    raise KeyError(clave)


def registrar_cambio(clave, conj, rid, nota):
    """Anota en estado.json una fila EXISTENTE (de otra tanda) que este lote modifica."""
    e = leer_estado()
    e.setdefault("modificadas", []).append({"clave": clave, "conjunto": conj, "id": rid, "nota": nota})
    guardar_estado(e)


def sin_odata(b):
    return {k: v for k, v in (b or {}).items() if not k.startswith("@odata")}


def snap(dv, tabla_k, rid, cols):
    r = leer(dv, tabla_k, rid, cols)
    return {k: v for k, v in r.items() if not k.startswith("@")}
