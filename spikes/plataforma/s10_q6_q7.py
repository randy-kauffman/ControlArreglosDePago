"""Q6 y Q7: solo lectura. Metadata de msdyn_richtextfile y configuracion de la organizacion."""
import json
from dv import Dataverse, mostrar
dv = Dataverse()
print("=== Q7: organization")
est, b, _ = dv.call("GET", "organizations?$select=maxuploadfilesize,blockedattachments,name")
o = b["value"][0]
print("maxuploadfilesize =", o["maxuploadfilesize"], "bytes =", o["maxuploadfilesize"]/1048576, "MiB")
print("blockedattachments =", o["blockedattachments"])
print("=== Q6: msdyn_richtextfile")
est, b, _ = dv.call("GET", "EntityDefinitions?$select=LogicalName,EntitySetName,OwnershipType,IsCustomEntity,SchemaName,IsActivity,PrimaryIdAttribute,PrimaryNameAttribute&$filter=contains(LogicalName,'richtext')")
print(est, json.dumps(b.get("value", b), indent=1))
