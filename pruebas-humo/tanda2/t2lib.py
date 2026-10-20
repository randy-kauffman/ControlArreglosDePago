"""Utilidades de la tanda 2 de pruebas de humo (2026-10-20), usuario NO admin. Nunca imprime secretos ni tokens.
Reutiliza hlib (tanda 1, administrador) y spikes/manual/lib.DvCorreo (usuario de aplicacion SPIKE4, PP_MAIL_*)."""
import os
import sys

AQUI2 = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI2, "..", "tanda1"))
sys.path.insert(0, os.path.join(AQUI2, "..", "..", "spikes", "manual"))
import hlib  # noqa: E402
from hlib import *  # noqa: E402,F401,F403
import hlib as _h  # noqa: E402

# El estado y la evidencia de esta tanda son propios (hlib apunta a tanda1 por defecto).
_h.ESTADO = os.path.join(AQUI2, "estado.json")
_h.EVID = os.path.join(AQUI2, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
EVID = _h.EVID
ESTADO = _h.ESTADO
ESTADO1 = os.path.join(AQUI2, "..", "tanda1", "estado.json")

from lib import DvCorreo, usuario_app  # noqa: E402,F401

BU_ID = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"
PERFIL_NOMBRE = "CSP - CARP - Datos sensibles"
JEF_NOMBRE = "Arreglos de Pago - Jefatura"


def fila1(clave):
    """GUID de la tanda 1 (clientes, versiones, ...)."""
    with open(ESTADO1, encoding="utf-8") as f:
        for x in reversed(json.load(f)["filas"]):
            if x["clave"] == clave:
                return x["id"]
    raise KeyError(clave)


def noadm():
    return DvCorreo()


def guardar_clave(k, v):
    e = leer_estado()
    e[k] = v
    guardar_estado(e)


def evidencia(nombre):
    """Reexporta volcar apuntando a la carpeta de la tanda 2."""
    return os.path.join(EVID, nombre)


def jefatura_id(dv):
    return get(dv, f"teams?$select=teamid&$filter=_businessunitid_value eq {BU_ID} and name eq '{JEF_NOMBRE}'")["value"][0]["teamid"]


def miembros_team(dv, tid):
    return sorted(m["systemuserid"] for m in get(dv, f"teams({tid})?$expand=teammembership_association($select=systemuserid)")["teammembership_association"])


def perfil_id(dv):
    r = get(dv, f"fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq '{PERFIL_NOMBRE}'")["value"]
    assert len(r) == 1, r
    return r[0]["fieldsecurityprofileid"]


def miembros_perfil(dv, pid):
    r = get(dv, f"fieldsecurityprofiles({pid})?$expand=systemuserprofiles_association($select=systemuserid),teamprofiles_association($select=teamid)")
    return {"usuarios": sorted(x["systemuserid"] for x in r["systemuserprofiles_association"]), "teams": sorted(x["teamid"] for x in r["teamprofiles_association"])}
