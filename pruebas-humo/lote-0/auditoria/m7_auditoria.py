"""M7 (2026-10-20): repite la prueba de tanda2b/t60 despues de agregar prvReadRecordAuditHistory (Global) a SR - CARP - Oficial.
Habilita TEMPORALMENTE a SPIKE4 (no admin, Oficial por el team por defecto, fuera de Jefatura), mide y lo deshabilita al final
(siempre, en finally). No cambia roles ni teams. Nunca imprime secretos ni tokens."""
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "tanda2b"))
from t2blib import *  # noqa: E402,F401,F403

EVID_M7 = os.path.join(AQUI, "evidencia")
dv = adm()
base = leer_estado()["linea_base"]  # linea base de la tanda 2b (solo lectura)
uid = base["usuario"]["systemuserid"]
jef = base["jefatura"]["teamid"]
AR = conjuntos(dv)["arreglo"]
a = fila2("arreglo-t2-gen")
out = {"fecha": "2026-10-20", "arreglo": "arreglo-t2-gen (HUMO, tanda 2)"}
antes = get(dv, f"systemusers({uid})?$select=isdisabled")
out["antes"] = {"isdisabled": antes["isdisabled"], "en_jefatura": uid in miembros_team(dv, jef)}
assert out["antes"]["isdisabled"] is True and not out["antes"]["en_jefatura"], out["antes"]
ruta = 'RetrieveRecordChangeHistory(Target=@t)?@t={"@odata.id":"' + f"{AR}({a})" + '"}'


def medir(cli):
    r = {}
    est, b, _ = cli.call("GET", ruta)
    det = ((b.get("AuditDetailCollection") or {}).get("AuditDetails")) if est == 200 else None
    r["RetrieveRecordChangeHistory"] = (est, err(b)[:200] if est != 200 else {"cambios": len(det or [])})
    est, b, _ = cli.call("GET", f"audits?$select=auditid,operation&$filter=_objectid_value eq {a}&$top=20")
    r["audits"] = (est, err(b)[:160] if est != 200 else {"filas": len(b.get("value", []))})
    est, b, _ = cli.call("GET", f"{AR}({a})?$select=sanic_nombre")
    r["lee_el_arreglo"] = est
    return r


try:
    print("habilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0])
    out["intentos_oficial"] = []
    for espera in (30, 60, 120):
        time.sleep(espera)
        u = noadm()
        m = medir(u)
        out["intentos_oficial"].append({"espera_s": espera, **m})
        print(json.dumps(m, default=str))
        if m["RetrieveRecordChangeHistory"][0] == 200:
            break
    out["admin"] = medir(dv)
finally:
    print("deshabilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])
    time.sleep(5)
    d = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value&$expand=systemuserroles_association($select=name),teammembership_association($select=teamid)")
    out["despues"] = {"isdisabled": d["isdisabled"], "en_jefatura": uid in miembros_team(dv, jef),
                      "roles_directos": sorted(r["name"] for r in d["systemuserroles_association"]),
                      "teams": sorted(t["teamid"] for t in d["teammembership_association"])}
    out["despues"]["identico_a_linea_base"] = (d["isdisabled"] is True and not out["despues"]["en_jefatura"]
        and d["accessmode"] == base["usuario"]["accessmode"] and d["_businessunitid_value"] == base["usuario"]["_businessunitid_value"]
        and out["despues"]["roles_directos"] == base["usuario"]["roles_directos"]
        and out["despues"]["teams"] == sorted(t[1] for t in base["usuario"]["teams"]))
    with open(os.path.join(EVID_M7, "m7_auditoria.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False, default=str)
    print(json.dumps(out["despues"], indent=1))
