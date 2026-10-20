"""Reversion del usuario SPIKE4 al estado de linea_base: fuera de Jefatura, deshabilitado. Idempotente. Verifica releyendo."""
from t2lib import *

dv = adm()
base = leer_estado()["linea_base"]
uid = base["usuario"]["systemuserid"]
jef = base["jefatura"]["teamid"]
res = {}
if uid in miembros_team(dv, jef):
    est, b, _ = dv.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]})
    print("quitar de Jefatura:", est)
est, b, _ = dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})
print("deshabilitar:", est, b if est >= 300 else "")
time.sleep(5)
u = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value&$expand=systemuserroles_association($select=name),teammembership_association($select=name,teamid)")
res["usuario_despues"] = {"isdisabled": u["isdisabled"], "accessmode": u["accessmode"], "bu": u["_businessunitid_value"],
                          "roles_directos": sorted(r["name"] for r in u["systemuserroles_association"]),
                          "teams": sorted((t["name"], t["teamid"]) for t in u["teammembership_association"])}
res["jefatura_miembros_despues"] = miembros_team(dv, jef)
ok = (res["usuario_despues"]["isdisabled"] is True and res["usuario_despues"]["accessmode"] == base["usuario"]["accessmode"]
      and res["usuario_despues"]["bu"] == base["usuario"]["_businessunitid_value"] and res["usuario_despues"]["roles_directos"] == base["usuario"]["roles_directos"]
      and [list(x) for x in res["usuario_despues"]["teams"]] == [list(x) for x in base["usuario"]["teams"]] and res["jefatura_miembros_despues"] == base["jefatura"]["miembros"])
res["identico_a_linea_base"] = ok
print(json.dumps(res, indent=1, ensure_ascii=False))
volcar("99_reversion_verificada.json", res)
guardar_clave("reversion", res)
assert ok, "el usuario NO quedo como al inicio"
