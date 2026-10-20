"""Linea base ANTES de cambiar nada: usuario SPIKE4, teams, perfil de columna, roles. Solo lecturas. Guarda en estado.json -> linea_base."""
from t2lib import *

dv = adm()
u = usuario_app(dv)
uid = u["systemuserid"]
base = {"fecha": "2026-10-20"}
base["usuario"] = {k: u.get(k) for k in ("systemuserid", "fullname", "isdisabled", "accessmode", "_businessunitid_value")}
full = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,fullname&$expand=systemuserroles_association($select=name,roleid),teammembership_association($select=name,teamid)")
base["usuario"]["roles_directos"] = sorted(r["name"] for r in full["systemuserroles_association"])
base["usuario"]["teams"] = sorted((t["name"], t["teamid"]) for t in full["teammembership_association"])
jef = jefatura_id(dv)
base["jefatura"] = {"teamid": jef, "miembros": miembros_team(dv, jef),
                    "roles": sorted(r["name"] for r in get(dv, f"teams({jef})?$expand=teamroles_association($select=name)")["teamroles_association"])}
base["team_default"] = {"teamid": TEAM_DEF, "miembros": miembros_team(dv, TEAM_DEF),
                        "roles": sorted(r["name"] for r in get(dv, f"teams({TEAM_DEF})?$expand=teamroles_association($select=name)")["teamroles_association"])}
pid = perfil_id(dv)
base["perfil"] = {"id": pid, **miembros_perfil(dv, pid)}
print(json.dumps(base, indent=1, ensure_ascii=False))
guardar_clave("linea_base", base)
volcar("00_linea_base.json", base)
