"""Lote 0 (2026-10-20), inventario 13.7: SOLO LECTURA. Qué usuarios están en la BU `Arreglos de Pago`, con qué roles,
y quiénes son miembros del team `Arreglos de Pago - Jefatura`. No crea, no mueve, no modifica. Nunca imprime secretos."""
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse  # noqa: E402

dv = Dataverse()
bu = dv.call("GET", "businessunits?$select=businessunitid&$filter=name eq 'Arreglos de Pago'")[1]["value"][0]["businessunitid"]
jef = dv.call("GET", f"teams?$select=teamid&$filter=name eq 'Arreglos de Pago - Jefatura' and _businessunitid_value eq {bu}")[1]["value"][0]["teamid"]
miembros = {u["systemuserid"] for u in dv.call("GET", f"teams({jef})/teammembership_association?$select=systemuserid")[1]["value"]}
us = dv.call("GET", f"systemusers?$select=fullname,isdisabled,accessmode,applicationid,azureactivedirectoryobjectid&$filter=_businessunitid_value eq {bu}")[1]["value"]
out = []
for u in us:
    roles = [r["name"] for r in dv.call("GET", f"systemusers({u['systemuserid']})/systemuserroles_association?$select=name")[1]["value"]]
    out.append({"id": u["systemuserid"], "fullname": u["fullname"], "deshabilitado": u["isdisabled"], "accessmode": u["accessmode"],
                "es_aplicacion": bool(u.get("applicationid")), "roles_directos": roles, "en_jefatura": u["systemuserid"] in miembros})
fuera = sorted(miembros - {u["systemuserid"] for u in us})
ev = {"bu": bu, "team_jefatura": jef, "usuarios_en_la_bu": out, "miembros_de_jefatura_fuera_de_la_bu": fuera}
with open(os.path.join(os.path.dirname(AQUI), "evidencia", "03_usuarios_bu.json"), "w", encoding="utf-8") as f:
    json.dump(ev, f, ensure_ascii=False, indent=1)
print(json.dumps(ev, ensure_ascii=False, indent=1))
