"""Lote D (2026-10-20), duda D-09: con que privilegio lee la cuenta de servicio los trabajos de eliminacion masiva.
SR - CARP - Servicio y SR - CARP - Oficial tienen el mismo prvReadAsyncOperation (Basic) y ningun otro privilegio sobre
AsyncOperation / BulkDeleteOperation que importe para leer. Se mide con SPIKE4 (no admin, Oficial por el team del sistema):
lo habilita TEMPORALMENTE, consulta bulkdeleteoperations y asyncoperations de tipo 13, y lo deshabilita al final (finally).
No cambia roles ni teams. Nunca imprime secretos ni tokens."""
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
EVID_D = os.path.join(os.path.dirname(AQUI), "evidencia")  # no EVID: t2blib lo pisa con el de la tanda 2b
sys.path.insert(0, os.path.join(AQUI, "..", "..", "tanda2b"))
from t2blib import *  # noqa: E402,F401,F403

dv = adm()
base = leer_estado()["linea_base"]
uid = base["usuario"]["systemuserid"]
jef = base["jefatura"]["teamid"]
CONSULTAS = {
    "bulkdeleteoperations_fallidas_24h": "bulkdeleteoperations?$select=bulkdeleteoperationid&$filter=statuscode eq 31 and createdon ge "
                                          + time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() - 86400)),
    "bulkdeleteoperations_todas": "bulkdeleteoperations?$select=bulkdeleteoperationid&$top=50",
    "asyncoperations_tipo13": "asyncoperations?$select=asyncoperationid&$filter=operationtype eq 13&$top=50",
}


def medir(cli):
    r = {}
    for k, ruta in CONSULTAS.items():
        est, b, _ = cli.call("GET", ruta)
        r[k] = (est, err(b)[:200] if est != 200 else {"filas": len(b.get("value", []))})
    return r


out = {"fecha": "2026-10-20", "duda": "D-09", "admin": medir(dv)}
antes = get(dv, f"systemusers({uid})?$select=isdisabled")
out["antes"] = {"isdisabled": antes["isdisabled"], "en_jefatura": uid in miembros_team(dv, jef)}
assert not out["antes"]["en_jefatura"], out["antes"]
try:
    if antes["isdisabled"]:
        print("habilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0])
    out["no_admin"] = []
    for espera in (30, 60):
        time.sleep(espera)
        m = medir(noadm())
        out["no_admin"].append({"espera_s": espera, **m})
        print(json.dumps(m, default=str))
        if all(v[0] in (200, 403) for v in m.values()):
            break
finally:
    if antes["isdisabled"]:
        print("deshabilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])
    time.sleep(5)
    d = get(dv, f"systemusers({uid})?$select=isdisabled")
    out["despues"] = {"isdisabled": d["isdisabled"], "igual_que_antes": d["isdisabled"] == antes["isdisabled"],
                      "en_jefatura": uid in miembros_team(dv, jef)}
    with open(os.path.join(EVID_D, "02_d09_purga.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False, default=str)
    print(json.dumps({k: out[k] for k in ("admin", "antes", "despues")}, default=str))
