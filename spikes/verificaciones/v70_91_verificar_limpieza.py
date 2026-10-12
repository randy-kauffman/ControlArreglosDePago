"""V-8: verifica la limpieza RELEYENDO todo contra la linea base de estado_v70.json (evidencia/70_linea_base.json)."""
from v70_lib import *

dv = adm()
base = leer70()["linea_base"]
r = {}
uid = base["usuario"]["id"]
PERFIL = base["perfil"][0]["fieldsecurityprofileid"]

for _ in range(2):  # trazas que lleguen tarde
    for t in get(dv, "plugintracelogs?$select=plugintracelogid&$filter=startswith(typename,'Zzv.Probe')&$top=200")["value"]:
        dv.call("DELETE", f"plugintracelogs({t['plugintracelogid']})")
    time.sleep(25)
r["trazas_Zzv_restantes"] = len(get(dv, "plugintracelogs?$select=plugintracelogid&$filter=startswith(typename,'Zzv.Probe')&$top=50")["value"])

sid, comps = componentes_solucion(dv)
antes = {(c["componenttype"], c["objectid"]) for c in base["componentes_solucion"]}
ahora = {(c["componenttype"], c["objectid"]) for c in comps}
r["solucion_componentes_antes"] = len(antes); r["solucion_componentes_ahora"] = len(ahora)
r["solucion_componentes_nuevos"] = sorted(map(list, ahora - antes)); r["solucion_componentes_que_faltan"] = sorted(map(list, antes - ahora))

org = get(dv, "organizations?$select=plugintracelogsetting")["value"][0]
r["plugintracelogsetting_antes"] = base["org"]["plugintracelogsetting"]; r["plugintracelogsetting_ahora"] = org["plugintracelogsetting"]

uu = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value&$expand=systemuserroles_association($select=name,roleid),teammembership_association($select=name,teamid,teamtype)")
ahora_u = {"id": uid, "isdisabled": uu["isdisabled"], "accessmode": uu["accessmode"], "bu": uu["_businessunitid_value"],
           "roles": sorted((x["name"], x["roleid"]) for x in uu["systemuserroles_association"]),
           "teams": sorted((x["name"], x["teamid"], x["teamtype"]) for x in uu["teammembership_association"])}
r["usuario_antes"] = base["usuario"]; r["usuario_ahora"] = ahora_u
r["usuario_igual"] = json.loads(json.dumps(base["usuario"], sort_keys=True)) == json.loads(json.dumps(ahora_u, sort_keys=True))

p = get(dv, f"fieldsecurityprofiles({PERFIL})?$expand=systemuserprofiles_association($select=systemuserid),teamprofiles_association($select=teamid)")
r["perfil_miembros_ahora"] = {"usuarios": len(p["systemuserprofiles_association"]), "teams": len(p["teamprofiles_association"])}
r["perfil_miembros_antes"] = {"usuarios": len(base["perfil_miembros"]["usuarios"]), "teams": len(base["perfil_miembros"]["teams"])}
jef = get(dv, f"teams?$select=teamid&$filter=_businessunitid_value eq {BU_ID} and name eq 'Arreglos de Pago - Jefatura'")["value"][0]["teamid"]
r["jefatura_miembros_ahora"] = [m["systemuserid"] for m in get(dv, f"teams({jef})?$expand=teammembership_association($select=systemuserid)")["teammembership_association"]]
r["jefatura_miembros_antes"] = base["jefatura_miembros"]

paq = get(dv, "pluginpackages?$select=pluginpackageid,uniquename,version")["value"]
r["paquetes_igual_que_antes"] = sorted((x["pluginpackageid"], x["uniquename"], x["version"]) for x in paq) == sorted((x["pluginpackageid"], x["uniquename"], x["version"]) for x in base["paquetes"])
asm = get(dv, "pluginassemblies?$select=pluginassemblyid,name&$filter=ismanaged eq false")["value"]
r["ensamblados_igual_que_antes"] = sorted(x["pluginassemblyid"] for x in asm) == sorted(x["pluginassemblyid"] for x in base["ensamblados"])
r["ensamblados_Zzv"] = [x["name"] for x in asm if x["name"].startswith("Zzv")]
r["steps_ZZV"] = len(get(dv, "sdkmessageprocessingsteps?$select=name&$filter=startswith(name,'ZZV')")["value"])
n = 0
for f in get(dv, "sdkmessagefilters?$select=sdkmessagefilterid&$filter=primaryobjecttypecode eq 'sanic_carp_tbl_arreglo'")["value"]:
    n += len(get(dv, f"sdkmessageprocessingsteps?$select=name&$filter=_sdkmessagefilterid_value eq {f['sdkmessagefilterid']}")["value"])
r["steps_sobre_arreglo_antes"] = base["steps_sobre_arreglo"]; r["steps_sobre_arreglo_ahora"] = n
r["customapis_ZZV"] = len(get(dv, "customapis?$select=uniquename&$filter=startswith(uniquename,'ZZV') or startswith(uniquename,'zzv')")["value"])
for t, c in base["tablas"].items():
    r[f"filas_{t}"] = len(get(dv, f"{c['set']}?$select={c['pk']}&$top=5")["value"])
volcar("70_limpieza_verificada.json", r)
print(json.dumps(r, indent=1, default=str))
