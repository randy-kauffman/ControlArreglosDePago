"""Linea base ANTES de tocar nada: solucion, usuario de prueba, teams, roles, perfil de columnas, config de la org, paquetes y steps existentes."""
from vlib import *

dv = adm()
est, b, _ = dv.call("GET", "WhoAmI"); print("WhoAmI admin:", est, b.get("UserId"))
sid, comps = componentes_solucion(dv)
print("solucion", sid, "componentes:", len(comps))
base = {"solucion_id": sid, "componentes_solucion": comps}

org = get(dv, "organizations?$select=organizationid,plugintracelogsetting,name")["value"][0]
print("org:", org["name"], "plugintracelogsetting:", org["plugintracelogsetting"])
base["org"] = {"organizationid": org["organizationid"], "plugintracelogsetting": org["plugintracelogsetting"]}

u = usuario_app(dv)
uid = u["systemuserid"]; print("usuario app:", {k: u[k] for k in ("systemuserid", "isdisabled", "accessmode", "_businessunitid_value")})
uu = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value&$expand=systemuserroles_association($select=name,roleid),teammembership_association($select=name,teamid,teamtype)")
base["usuario"] = {"id": uid, "isdisabled": uu["isdisabled"], "accessmode": uu["accessmode"], "bu": uu["_businessunitid_value"],
                   "roles": sorted((r["name"], r["roleid"]) for r in uu["systemuserroles_association"]),
                   "teams": sorted((t["name"], t["teamid"], t["teamtype"]) for t in uu["teammembership_association"])}
print("usuario:", json.dumps(base["usuario"], indent=1))

perf = get(dv, "fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq 'CSP - CARP - Datos sensibles'")["value"]
print("perfil:", perf)
base["perfil"] = perf
if perf:
    pid = perf[0]["fieldsecurityprofileid"]
    p = get(dv, f"fieldsecurityprofiles({pid})?$expand=systemuserprofiles_association($select=systemuserid,fullname),teamprofiles_association($select=teamid,name)")
    base["perfil_miembros"] = {"usuarios": [(x["systemuserid"], x["fullname"]) for x in p["systemuserprofiles_association"]],
                               "teams": [(x["teamid"], x["name"]) for x in p["teamprofiles_association"]]}
    print("perfil miembros:", base["perfil_miembros"])

paq = get(dv, "pluginpackages?$select=pluginpackageid,uniquename,version")["value"]
print("paquetes:", paq); base["paquetes"] = paq
asm = get(dv, "pluginassemblies?$select=pluginassemblyid,name,ismanaged&$filter=ismanaged eq false")["value"]
print("ensamblados no administrados:", [(a["name"]) for a in asm]); base["ensamblados"] = asm
steps = get(dv, "sdkmessageprocessingsteps?$select=name,sdkmessageprocessingstepid&$filter=ismanaged eq false and startswith(name,'ZZV')")["value"]
print("steps ZZV:", steps)
for t in ("conversacion", "arreglo", "versionplantilla", "cliente", "cuota", "generacionformato", "mapeocelda"):
    r = get(dv, f"EntityDefinitions(LogicalName='sanic_carp_tbl_{t}')?$select=EntitySetName,PrimaryIdAttribute,ObjectTypeCode")
    n = dv.call("GET", f"{r['EntitySetName']}?$select={r['PrimaryIdAttribute']}&$top=5")[1]
    print(t, r["EntitySetName"], r["ObjectTypeCode"], "filas(muestra):", len(n.get("value", [])))
    base.setdefault("tablas", {})[t] = {"set": r["EntitySetName"], "pk": r["PrimaryIdAttribute"], "otc": r["ObjectTypeCode"]}
guardar_estado({"linea_base": base})
volcar("00_linea_base.json", base)
