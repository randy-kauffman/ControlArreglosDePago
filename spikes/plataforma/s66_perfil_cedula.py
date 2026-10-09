"""Q2c (preparacion): perfil de seguridad de columna 'SPK Perfil Cedula Lectura' con Read sobre spk_cliente.spk_cedula, en la solucion.
NO se asigna a ningun usuario ni equipo."""
import json
from dv import Dataverse, mostrar, SOLUCION
dv = Dataverse()
N = "SPK Perfil Cedula Lectura"
est, b, _ = dv.call("GET", f"fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq '{N}'")
if not b["value"]:
    est, b, h = dv.call("POST", "fieldsecurityprofiles", {"name": N, "description": "Spike 2: lectura de spk_cedula. Sin asignar."}, solucion=SOLUCION)
    mostrar("POST perfil", est, b)
est, b, _ = dv.call("GET", f"fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq '{N}'")
pid = b["value"][0]["fieldsecurityprofileid"]
est, b, _ = dv.call("GET", f"fieldpermissions?$select=fieldpermissionid,attributelogicalname,canread&$filter=_fieldsecurityprofileid_value eq {pid}")
if not b["value"]:
    est, b, h = dv.call("POST", "fieldpermissions", {"fieldsecurityprofileid@odata.bind": f"/fieldsecurityprofiles({pid})",
        "entityname": "spk_cliente", "attributelogicalname": "spk_cedula", "canread": 4, "cancreate": 0, "canupdate": 0})
    mostrar("POST permiso", est, b)
est, b, _ = dv.call("GET", f"fieldpermissions?$select=entityname,attributelogicalname,canread,cancreate,canupdate&$filter=_fieldsecurityprofileid_value eq {pid}")
print("RELEER permisos:", [(x["entityname"], x["attributelogicalname"], x["canread"], x["cancreate"], x["canupdate"]) for x in b["value"]])
est, b, _ = dv.call("GET", f"fieldsecurityprofiles({pid})/systemuserprofiles_association?$select=fullname")
est2, b2, _ = dv.call("GET", f"fieldsecurityprofiles({pid})/teamprofiles_association?$select=name")
print("RELEER asignados (usuarios, equipos):", b.get("value"), b2.get("value"))
est, b, _ = dv.call("GET", f"solutioncomponents?$select=componenttype&$filter=objectid eq {pid}")
print("RELEER pertenencia a solucion (componenttype):", [x["componenttype"] for x in b["value"]])
