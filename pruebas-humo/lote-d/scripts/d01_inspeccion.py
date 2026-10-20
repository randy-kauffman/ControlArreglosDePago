"""Lote D (2026-10-20): SOLO LECTURA. Cuenta de servicio de Dev (13.6), roles CARP en la BU raiz, connection references,
variables 10.3-10.6, flujos CARP existentes y metadatos de bulkdeleteoperation / asyncoperation (D-09).
No crea ni modifica nada. Nunca imprime secretos ni tokens."""
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse  # noqa: E402

UPN = "admin@55xljh.onmicrosoft.com"
dv = Dataverse()
g = lambda r: dv.call("GET", r)[:2]
ev = {}
s, u = g(f"systemusers?$select=fullname,isdisabled,accessmode,islicensed,_businessunitid_value,azureactivedirectoryobjectid&$filter=domainname eq '{UPN}'")
u = u["value"]
ev["usuario"] = u
if u:
    uid = u[0]["systemuserid"]
    ev["bu_nombre"] = g(f"businessunits({u[0]['_businessunitid_value']})?$select=name,_parentbusinessunitid_value")[1]
    ev["roles_directos"] = [(r["name"], r["_businessunitid_value"]) for r in g(f"systemusers({uid})/systemuserroles_association?$select=name,_businessunitid_value")[1]["value"]]
    ev["teams_cantidad"] = len([t["name"] for t in g(f"systemusers({uid})/teammembership_association?$select=name")[1]["value"]])
raiz = g("businessunits?$select=businessunitid,name&$filter=_parentbusinessunitid_value eq null")[1]["value"]
ev["bu_raiz"] = raiz
ev["roles_carp"] = [(r["name"], r["roleid"], r["_businessunitid_value"]) for r in g("roles?$select=name,_businessunitid_value&$filter=startswith(name,'SR - CARP')")[1]["value"]]
ev["conr"] = g("connectionreferences?$select=connectionreferencelogicalname,connectionreferencedisplayname,connectorid,connectionid,statuscode")[1]["value"]
ev["ev"] = [(d["schemaname"], d["type"], d.get("displayname")) for d in g("environmentvariabledefinitions?$select=schemaname,type,displayname&$filter=startswith(schemaname,'sanic_')")[1]["value"]]
ev["flujos_carp"] = [(w["name"], w["statecode"], w["_ownerid_value"]) for w in g("workflows?$select=name,statecode,_ownerid_value&$filter=category eq 5 and contains(name,'CARP')")[1]["value"]]
# D-09: que privilegio lee los trabajos de BulkDelete
for ent in ("bulkdeleteoperation", "asyncoperation"):
    s, m = g(f"EntityDefinitions(LogicalName='{ent}')?$select=LogicalName,ObjectTypeCode,OwnershipType,IsPrivate")
    ev[f"meta_{ent}"] = m if s == 200 else s
ev["priv_bulk"] = [(p["name"], p["accessright"], p["canbebasic"], p["canbelocal"], p["canbedeep"], p["canbeglobal"]) for p in g("privileges?$select=name,accessright,canbebasic,canbelocal,canbedeep,canbeglobal&$filter=contains(name,'BulkDelete') or contains(name,'AsyncOperation')")[1]["value"]]
with open(os.path.join(os.path.dirname(AQUI), "evidencia", "01_inspeccion.json"), "w", encoding="utf-8") as f:
    json.dump(ev, f, ensure_ascii=False, indent=1, default=str)
print(json.dumps({k: v for k, v in ev.items() if k not in ("usuario", "bu_nombre")}, ensure_ascii=False, default=str))
