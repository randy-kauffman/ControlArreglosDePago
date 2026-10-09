"""Solo lectura: inventario final de lo que el spike 1b dejo en el entorno, con pertenencia a la solucion."""
from sbx_lib import *
dv = Dataverse(); e = leer_estado()
est, so, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
sid = so["value"][0]["solutionid"]
est, sc, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}")
comp = {x["objectid"]: x["componenttype"] for x in sc["value"]}
print("componentes en la solucion:", len(comp))
est, t, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')?$select=MetadataId")
print("tabla", TABLA, "en solucion:", comp.get(t["MetadataId"]))
est, p, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid,version,modifiedon&$filter=uniquename eq '{PAQUETE_UNICO}'")
pk = p["value"][0]; print("paquete", PAQUETE_UNICO, pk["version"], pk["modifiedon"], "en solucion:", comp.get(pk["pluginpackageid"]))
est, a, _ = dv.call("GET", f"pluginassemblies?$select=name,version,modifiedon,isolationmode&$filter=_packageid_value eq {pk['pluginpackageid']}")
print("ensamblados:", [(x["name"], x["version"], x["modifiedon"], x["isolationmode"]) for x in a["value"]])
est, c, _ = dv.call("GET", f"customapis?$select=customapiid,uniquename,isfunction,bindingtype&$filter=uniquename eq '{API}'")
print("custom api:", c["value"][0]["uniquename"], "isfunction", c["value"][0]["isfunction"], "en solucion:", comp.get(c["value"][0]["customapiid"]))
est, f, _ = dv.call("GET", f"{CONJUNTO}?$select=spk_nombre,spk_huella,spk_archivo_name")
for x in f["value"]:
    print("  fila:", x["spk_nombre"], "|", x.get("spk_archivo_name"), "|", (x["spk_huella"] or "")[:12])
for nombre in ("spk_TmpLimpiezaApi", "spk_TmpLimpiezaPkg"):
    print("restos de la prueba de limpieza", nombre, ":", len(dv.call("GET", f"customapis?$select=uniquename&$filter=uniquename eq '{nombre}'")[1].get("value", [])) + len(dv.call("GET", f"pluginpackages?$select=uniquename&$filter=uniquename eq '{nombre}'")[1].get("value", [])))
