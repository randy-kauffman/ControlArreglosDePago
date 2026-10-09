"""Inventario releido de lo que contiene la solucion SpikesArreglosPago (solo lectura)."""
from dv import Dataverse, SOLUCION
dv = Dataverse()
est, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
sid = b["value"][0]["solutionid"]
est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype,rootcomponentbehavior&$filter=_solutionid_value eq {sid}")
TIPOS = {1: "Tabla", 20: "Rol de seguridad", 70: "Perfil de seguridad de columna", 26: "Vista (savedquery)"}
for c in b["value"]:
    print(TIPOS.get(c["componenttype"], c["componenttype"]), c["objectid"], "root-behavior", c["rootcomponentbehavior"])
for t in ("spk_arreglo", "spk_cuota", "spk_cliente", "spk_conversacion"):
    est, r, _ = dv.call("GET", f"{t.replace('conversacion','conversacion')}s?$count=true&$top=1") if t != "spk_conversacion" else dv.call("GET", "spk_conversacions?$count=true&$top=1")
    print(t, "filas:", r.get("@odata.count"))
