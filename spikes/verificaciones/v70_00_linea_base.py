"""V-8: linea base ANTES de tocar nada (solucion, usuario no admin, perfil, config de org, paquetes, steps, filas) + metadatos de sanic_nombre en Arreglo. Solo lee."""
from v70_lib import *

dv = adm()
base = {}
sid, comps = componentes_solucion(dv)
print("solucion", sid, "componentes:", len(comps))
base["solucion_id"] = sid; base["componentes_solucion"] = comps

org = get(dv, "organizations?$select=organizationid,plugintracelogsetting,name")["value"][0]
print("org:", org["name"], "plugintracelogsetting:", org["plugintracelogsetting"])
base["org"] = {"organizationid": org["organizationid"], "plugintracelogsetting": org["plugintracelogsetting"]}

u = usuario_app(dv)
uid = u["systemuserid"]
uu = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value&$expand=systemuserroles_association($select=name,roleid),teammembership_association($select=name,teamid,teamtype)")
base["usuario"] = {"id": uid, "isdisabled": uu["isdisabled"], "accessmode": uu["accessmode"], "bu": uu["_businessunitid_value"],
                   "roles": sorted((r["name"], r["roleid"]) for r in uu["systemuserroles_association"]),
                   "teams": sorted((t["name"], t["teamid"], t["teamtype"]) for t in uu["teammembership_association"])}
print("usuario:", json.dumps(base["usuario"]))

perf = get(dv, "fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq 'CSP - CARP - Datos sensibles'")["value"]
base["perfil"] = perf
p = get(dv, f"fieldsecurityprofiles({perf[0]['fieldsecurityprofileid']})?$expand=systemuserprofiles_association($select=systemuserid),teamprofiles_association($select=teamid)")
base["perfil_miembros"] = {"usuarios": [x["systemuserid"] for x in p["systemuserprofiles_association"]], "teams": [x["teamid"] for x in p["teamprofiles_association"]]}
jef = get(dv, f"teams?$select=teamid&$filter=_businessunitid_value eq {BU_ID} and name eq 'Arreglos de Pago - Jefatura'")["value"][0]["teamid"]
base["jefatura_miembros"] = [m["systemuserid"] for m in get(dv, f"teams({jef})?$expand=teammembership_association($select=systemuserid)")["teammembership_association"]]
print("perfil miembros:", base["perfil_miembros"], "| jefatura:", base["jefatura_miembros"])

paq = get(dv, "pluginpackages?$select=pluginpackageid,uniquename,version")["value"]
base["paquetes"] = paq; print("paquetes:", [(x["uniquename"], x["version"]) for x in paq])
asm = get(dv, "pluginassemblies?$select=pluginassemblyid,name,ismanaged&$filter=ismanaged eq false")["value"]
base["ensamblados"] = asm; print("ensamblados no administrados:", [a["name"] for a in asm])
base["steps_ZZV"] = len(get(dv, "sdkmessageprocessingsteps?$select=name&$filter=startswith(name,'ZZV')")["value"])
base["trazas_Zzv"] = len(get(dv, "plugintracelogs?$select=plugintracelogid&$filter=startswith(typename,'Zzv.Probe')&$top=50")["value"])
# steps no ZZV ya registrados sobre Arreglo (plugins del proyecto, si existieran)
st = get(dv, "sdkmessageprocessingsteps?$select=name,stage&$expand=sdkmessagefilterid($select=primaryobjecttypecode)&$filter=ismanaged eq false")["value"]
base["steps_no_administrados"] = sorted((s["name"], s["stage"], (s.get("sdkmessagefilterid") or {}).get("primaryobjecttypecode")) for s in st)
print("steps no administrados (todos):", len(st), "| sobre arreglo:", [s for s in base["steps_no_administrados"] if s[2] == "sanic_carp_tbl_arreglo"])

base["tablas"] = {}
for t in ("conversacion", "arreglo", "versionplantilla", "cliente", "cuota", "generacionformato", "mapeocelda"):
    r = get(dv, f"EntityDefinitions(LogicalName='sanic_carp_tbl_{t}')?$select=EntitySetName,PrimaryIdAttribute")
    n = get(dv, f"{r['EntitySetName']}?$select={r['PrimaryIdAttribute']}&$top=5")
    base["tablas"][t] = {"set": r["EntitySetName"], "pk": r["PrimaryIdAttribute"], "filas(muestra)": len(n["value"])}
print("filas:", {k: v["filas(muestra)"] for k, v in base["tablas"].items()})

# metadatos de la primaria
m = get(dv, "EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')/Attributes(LogicalName='sanic_nombre')/Microsoft.Dynamics.CRM.StringAttributeMetadata"
            "?$select=LogicalName,AutoNumberFormat,MaxLength,RequiredLevel,IsValidForCreate,IsValidForUpdate,IsPrimaryName,IsAuditEnabled")
m.pop("@odata.context", None)
print("metadatos sanic_nombre:", json.dumps(m, default=str))
base["metadatos_sanic_nombre"] = m
guardar70({"linea_base": base})
volcar("70_linea_base.json", base)
