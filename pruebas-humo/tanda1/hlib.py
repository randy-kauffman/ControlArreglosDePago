"""Utilidades de la tanda 1 de pruebas de humo (2026-10-20). Nunca imprime secretos ni tokens.
Autentica con herramientas/dataverse_api.Dataverse (administrador, credenciales del archivo de secretos del puesto)."""
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from email.utils import parsedate_to_datetime

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "herramientas"))
from dataverse_api import Dataverse  # noqa: E402

ENTORNO = "https://org36e60d9d.crm.dynamics.com"
EVID = os.path.join(AQUI, "evidencia")
os.makedirs(EVID, exist_ok=True)
ESTADO = os.path.join(AQUI, "estado.json")  # solo GUIDs de filas HUMO, sin secretos

TEAM_DEF = "2ebc18e7-f9c3-f111-a05a-7ced8d3c456b"
OWNER = {"ownerid@odata.bind": f"/teams({TEAM_DEF})"}
PREFER_REP = {"Prefer": "return=representation"}

T = {"cliente": "sanic_carp_tbl_cliente", "conversacion": "sanic_carp_tbl_conversacion", "arreglo": "sanic_carp_tbl_arreglo",
     "version": "sanic_carp_tbl_versionplantilla", "mapeo": "sanic_carp_tbl_mapeocelda", "cuota": "sanic_carp_tbl_cuota",
     "generacion": "sanic_carp_tbl_generacionformato"}
# conjunto de entidades (plural) se resuelve por metadatos en conjuntos()
_CONJ = {}

# choices
VER_BORRADOR, VER_VIGENTE, VER_HISTORICA = 159460001, 159460002, 159460003
PROD_PRESTAMO, PROD_TARJETA = 159460001, 159460002
TIPO_TEMPORAL, TIPO_CANCELACION = 159460001, 159460002
MONEDA_COR, MONEDA_USD = 159460001, 159460002
CANAL_CORREO = 159460001


def adm():
    dv = Dataverse()
    assert dv.org == ENTORNO, "entorno no autorizado"
    return dv


def volcar(nombre, obj):
    with open(os.path.join(EVID, nombre), "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=1, ensure_ascii=False, default=str)


def leer_estado():
    try:
        with open(ESTADO, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return {"filas": []}


def guardar_estado(d):
    with open(ESTADO, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=1, ensure_ascii=False)


def registrar(clave, conj, rid, nota=""):
    e = leer_estado()
    e.setdefault("filas", []).append({"clave": clave, "conjunto": conj, "id": rid, "nota": nota})
    guardar_estado(e)


def fila(clave):
    """GUID registrado con esa clave (la ultima)."""
    for f in reversed(leer_estado().get("filas", [])):
        if f["clave"] == clave:
            return f["id"]
    raise KeyError(clave)


def get(dv, ruta):
    est, b, _ = dv.call("GET", ruta)
    assert est == 200, (ruta, est, b)
    return b


def conjuntos(dv):
    if not _CONJ:
        for k, ln in T.items():
            _CONJ[k] = get(dv, f"EntityDefinitions(LogicalName='{ln}')?$select=EntitySetName")["EntitySetName"]
    return _CONJ


def navs(dv, tabla):
    r = get(dv, f"EntityDefinitions(LogicalName='{tabla}')/ManyToOneRelationships?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName")
    return {x["ReferencingAttribute"]: x["ReferencingEntityNavigationPropertyName"] for x in r["value"]}


def reloj(dv):
    _, _, h = dv.call("GET", "WhoAmI")
    return parsedate_to_datetime(h.get("Date") or h.get("date")).astimezone(timezone.utc)


def iso(t):
    return t.strftime("%Y-%m-%dT%H:%M:%SZ")


def crear(dv, clave, tabla_k, cuerpo, nota=""):
    """POST con return=representation. Devuelve (http, cuerpo_o_error, id). Registra el GUID si se creo."""
    conj = conjuntos(dv)[tabla_k]
    est, b, h = dv.call("POST", conj, cuerpo, cabeceras=PREFER_REP)
    rid = None
    if est == 201:
        rid = b.get(T[tabla_k] + "id")
        registrar(clave, conj, rid, nota)
    return est, b, rid


def actualizar(dv, tabla_k, rid, cuerpo, cabeceras=None):
    conj = conjuntos(dv)[tabla_k]
    est, b, _ = dv.call("PATCH", f"{conj}({rid})", cuerpo, cabeceras=cabeceras)
    return est, b


def leer(dv, tabla_k, rid, select):
    conj = conjuntos(dv)[tabla_k]
    return get(dv, f"{conj}({rid})?$select={select}")


def llamar(dv, api, cuerpo, caller=None, timeout=300):
    """Ejecuta una Custom API (accion global). `caller` = systemuserid para MSCRMCallerID."""
    cab = {"MSCRMCallerID": caller} if caller else None
    t0 = time.time()
    est, b, h = dv.call("POST", api, cuerpo, cabeceras=cab, timeout=timeout)
    return est, b, round(time.time() - t0, 2)


def trazas(dv, desde, tipos=None, esperar=45, estable=2):
    """Trazas del paquete (typename Sanic.Carp.*) creadas desde `desde`. Espera a que lleguen y dejen de crecer.
    Devuelve filas con los campos utiles (sin messageblock de datos: los plugins solo trazan ids y codigos)."""
    t0, ult, rep = time.time(), -1, 0
    filas = []
    flt = "startswith(typename,'Sanic.Carp') and createdon ge " + iso(desde)
    while time.time() - t0 < esperar:
        filas = get(dv, "plugintracelogs?$select=typename,messagename,primaryentity,depth,mode,operationtype,correlationid,requestid,messageblock,createdon,"
                        "exceptiondetails,performanceexecutionduration,performanceexecutionstarttime,issystemcreated,_createdby_value,_createdonbehalfby_value"
                        f"&$filter={flt}&$orderby=performanceexecutionstarttime asc&$top=500")["value"]
        if len(filas) == ult:
            rep += 1
            if rep >= estable:
                break
        else:
            rep = 0
        ult = len(filas)
        time.sleep(5)
    return filas


def resumen_trazas(filas):
    out = []
    for f in filas:
        out.append({"inicio": f.get("performanceexecutionstarttime"), "tipo": f["typename"].split(",")[0].split(".")[-1], "msg": f["messagename"], "ent": f["primaryentity"],
                    "depth": f["depth"], "mode": f["mode"], "dur_ms": f.get("performanceexecutionduration"), "corr": (f["correlationid"] or "")[:8],
                    "lineas": [l[:300] for l in (f["messageblock"] or "").splitlines() if l.strip()][:8],
                    "exc": (f.get("exceptiondetails") or "")[:300]})
    return out


def mostrar(res):
    for r in res:
        print(f"  [{(r['inicio'] or '')[11:23]}] {r['tipo']} {r['msg']}/{r['ent']} d={r['depth']} m={r['mode']} {r['dur_ms']}ms c={r['corr']}")
        for l in r["lineas"]:
            print("       ", l)
        if r["exc"]:
            print("       EXC:", r["exc"][:200])


class Res:
    """Acumula veredictos por sub-prueba y los guarda en evidencia/<archivo>."""

    def __init__(self, archivo):
        self.archivo, self.filas = archivo, []

    def v(self, prueba, sub, ok, esperado, obtenido, **extra):
        r = {"prueba": prueba, "sub": sub, "ok": bool(ok), "esperado": esperado, "obtenido": obtenido}
        r.update(extra)
        self.filas.append(r)
        print(f"[{'OK ' if ok else 'XX '}] {prueba} {sub}: esperado={esperado} | obtenido={str(obtenido)[:300]}", flush=True)
        self.guardar()
        return ok

    def guardar(self):
        volcar(self.archivo, self.filas)


def codigo(b):
    """Codigo CARP-XXX-NNN que lleva un mensaje de error, o None."""
    import re
    m = re.search(r"CARP-[A-Z]{3}-\d{3}", err(b) if isinstance(b, dict) else str(b))
    return m.group(0) if m else None


def err(b):
    return (b.get("error") or "")[:400] if isinstance(b, dict) else str(b)[:400]
