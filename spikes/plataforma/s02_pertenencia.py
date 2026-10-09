"""Verifica por relectura que las tablas del spike pertenecen a la solucion (tipo 1, rootcomponentbehavior 0)."""
from dv import Dataverse, SOLUCION
dv = Dataverse()
est, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
sid = b["value"][0]["solutionid"]
est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype,rootcomponentbehavior&$filter=_solutionid_value eq {sid}")
comps = b["value"]
print("componentes en la solucion:", len(comps))
for t in ("spk_arreglo", "spk_cuota", "spk_cliente", "spk_conversacion"):
    est, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{t}')?$select=MetadataId")
    filas = [c for c in comps if c["objectid"] == e["MetadataId"]]
    print(t, [(c["componenttype"], c["rootcomponentbehavior"]) for c in filas])
