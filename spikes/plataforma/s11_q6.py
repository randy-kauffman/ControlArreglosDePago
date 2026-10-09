"""Q6: solo lectura de metadata de msdyn_richtextfile (existencia, columnas, relaciones, ownership)."""
import json
from dv import Dataverse
dv = Dataverse()
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='msdyn_richtextfile')?$select=LogicalName,SchemaName,EntitySetName,OwnershipType,IsCustomEntity,IsManaged,PrimaryIdAttribute,PrimaryNameAttribute,IsActivity,IsIntersect,IsValidForAdvancedFind,IsAuditEnabled,ObjectTypeCode,CanCreateAttributes,CanBeInManyToMany")
print("existe:", est)
print(json.dumps(b, indent=1)[:1800])
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='msdyn_richtextfile')/Attributes?$select=LogicalName,AttributeTypeName,AttributeOf,IsCustomAttribute,RequiredLevel,IsValidForCreate,IsValidForRead,IsValidForUpdate,IsPrimaryId,IsPrimaryName,IsSecured")
print("columnas:", est, len(b.get("value", [])))
for a in b.get("value", []):
    print("  ", a["LogicalName"].ljust(34), a["AttributeTypeName"]["Value"].ljust(22), "of=" + str(a["AttributeOf"]), "req=" + a["RequiredLevel"]["Value"], "C/R/U=%s/%s/%s" % (a["IsValidForCreate"], a["IsValidForRead"], a["IsValidForUpdate"]))
for tipo in ("ManyToOneRelationships", "OneToManyRelationships", "ManyToManyRelationships"):
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='msdyn_richtextfile')/{tipo}")
    print(tipo, est, len(b.get("value", [])))
    for r in b.get("value", []):
        if tipo == "ManyToManyRelationships":
            print("  ", r["SchemaName"], r["Entity1LogicalName"], r["Entity2LogicalName"])
        else:
            print("  ", r["SchemaName"], "|", r["ReferencingEntity"] + "." + r["ReferencingAttribute"], "->", r["ReferencedEntity"] + "." + r["ReferencedAttribute"], "| delete:", r["CascadeConfiguration"]["Delete"])
