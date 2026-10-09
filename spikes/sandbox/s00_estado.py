"""Solo lectura: estado de base del entorno antes de construir (que hay en la solucion, paquetes, ensamblados, APIs)."""
from sbx_lib import *
dv = Dataverse()
est, b, _ = dv.call("GET", f"solutions?$select=solutionid,uniquename,version,modifiedon&$filter=uniquename eq '{SOLUCION}'")
mostrar("solucion", est, b)
sid = b["value"][0]["solutionid"]
est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}")
from collections import Counter
print("componentes por tipo en la solucion:", dict(Counter(c["componenttype"] for c in b["value"])))
for ent, sel in [("pluginpackages", "name,uniquename,version,modifiedon"), ("pluginassemblies", "name,version,modifiedon,ismanaged,_packageid_value,isolationmode"),
                 ("customapis", "uniquename,isfunction,modifiedon,ismanaged")]:
    est, b, _ = dv.call("GET", f"{ent}?$select={sel}&$filter=ismanaged eq false" if ent != "pluginpackages" else f"{ent}?$select={sel}")
    print(ent, est, len(b.get("value", [])), [ (x.get("uniquename") or x.get("name")) for x in b.get("value", [])][:15] if est==200 else b)
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')?$select=LogicalName")
print(TABLA, est)
est, b, _ = dv.call("GET", "WhoAmI")
mostrar("whoami", est, {k: v for k, v in b.items() if k in ("UserId", "BusinessUnitId")})
