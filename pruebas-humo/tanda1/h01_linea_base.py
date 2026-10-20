"""Linea base de la tanda 1 (solo GET): quien soy, usuarios, EV, version Vigente, filas existentes, candidatos de reconciliacion, trace log."""
from hlib import *

dv = adm()
out = {}
out["whoami"] = get(dv, "WhoAmI")
uid = out["whoami"]["UserId"]
out["yo"] = get(dv, f"systemusers({uid})?$select=fullname,applicationid,isdisabled,accessmode,_businessunitid_value")
out["humanos"] = get(dv, "systemusers?$select=systemuserid,fullname,isdisabled,accessmode,applicationid,_businessunitid_value&$filter=contains(fullname,'Kauffman') or contains(fullname,'Alguera')")["value"]
out["ev"] = get(dv, "environmentvariabledefinitions?$select=schemaname,defaultvalue&$filter=startswith(schemaname,'sanic_carp_ev')")["value"]
for k in ("cliente", "conversacion", "arreglo", "version", "mapeo", "cuota", "generacion"):
    conj = conjuntos(dv)[k]
    est, b, h = dv.call("GET", f"{conj}?$select={T[k]}id&$top=5")
    out["filas_" + k] = len(b.get("value", [])) if est == 200 else (est, err(b))
bu = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"
est, b, _ = dv.call("GET", f"emails?$select=activityid&$filter=_owningbusinessunit_value eq {bu} and directioncode eq false and statuscode eq 4 and _regardingobjectid_value eq null&$top=5")
out["candidatos_reconciliar_en_la_BU_del_area"] = len(b["value"]) if est == 200 else (est, err(b))
est, b, _ = dv.call("GET", "organizations?$select=plugintracelogsetting")
out["tracelog_setting"] = b.get("value", b)
est, b, _ = dv.call("GET", "plugintracelogs?$select=typename&$filter=startswith(typename,'Sanic.Carp')&$top=3")
out["trazas_previas_del_paquete"] = len(b.get("value", []))
for k, v in out.items():
    print(k, "=", json.dumps(v, ensure_ascii=False, default=str)[:700])
volcar("01_linea_base.json", {k: v for k, v in out.items() if k != "humanos"})
