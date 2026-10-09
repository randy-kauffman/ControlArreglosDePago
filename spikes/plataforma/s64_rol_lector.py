"""Q2c (preparacion): rol propio 'SPK Lector Cliente' con Read sobre spk_cliente, en la solucion del spike.
NO se asigna a nadie (asignarlo es cambiar los roles de un usuario: lo decide el dueño del entorno)."""
import json
from dv import Dataverse, mostrar, SOLUCION
dv = Dataverse()
NOMBRE = "SPK Lector Cliente"
est, w, _ = dv.call("GET", "WhoAmI"); bu = w["BusinessUnitId"]
est, b, _ = dv.call("GET", f"roles?$select=roleid,name&$filter=name eq '{NOMBRE}'")
if not b["value"]:
    est, b, h = dv.call("POST", "roles", {"name": NOMBRE, "businessunitid@odata.bind": f"/businessunits({bu})"}, solucion=SOLUCION)
    mostrar("POST rol", est, b)
est, b, _ = dv.call("GET", f"roles?$select=roleid,name,_businessunitid_value&$filter=name eq '{NOMBRE}'")
rid = b["value"][0]["roleid"]; print("rol:", rid)
est, p, _ = dv.call("GET", "privileges?$select=privilegeid,name&$filter=name eq 'prvReadspk_cliente' or name eq 'prvReadspk_arreglo'")
privs = [{"PrivilegeId": x["privilegeid"], "Depth": "Global", "BusinessUnitId": bu} for x in p["value"]]
est, b, _ = dv.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {"Privileges": privs})
mostrar("AddPrivilegesRole", est, b)
est, b, _ = dv.call("GET", f"roles({rid})/roleprivileges_association?$select=name")
print("RELEER privilegios del rol:", sorted(x["name"] for x in b["value"]))
est, b, _ = dv.call("GET", f"solutioncomponents?$select=componenttype&$filter=objectid eq {rid}")
print("RELEER pertenencia a solucion (componenttype):", [(x["componenttype"]) for x in b["value"]])
est, b, _ = dv.call("GET", f"systemusers?$select=fullname&$filter=_roleid_value eq {rid}") if False else (0, {}, 0)
est, b, _ = dv.call("GET", f"roles({rid})/systemuserroles_association?$select=fullname")
print("RELEER usuarios con el rol (debe ser vacio):", b.get("value"))
