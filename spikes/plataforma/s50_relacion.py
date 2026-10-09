"""Q5 prerequisito: relacion 1:N spk_arreglo -> spk_cuota con su lookup; se verifica releyendo."""
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
NOM = "spk_arreglo_spk_cuota"
est, b, _ = dv.call("GET", f"RelationshipDefinitions(SchemaName='{NOM}')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata?$select=SchemaName,ReferencedEntity,ReferencingEntity,ReferencingAttribute,IsCustomRelationship")
if est == 404:
    cuerpo = {"@odata.type": T + "OneToManyRelationshipMetadata", "SchemaName": NOM,
              "ReferencedEntity": "spk_arreglo", "ReferencedAttribute": "spk_arregloid", "ReferencingEntity": "spk_cuota",
              "CascadeConfiguration": {"Assign": "NoCascade", "Delete": "Cascade", "Merge": "NoCascade", "Reparent": "Cascade",
                                       "Share": "Cascade", "Unshare": "Cascade", "RollupView": "NoCascade"},
              "AssociatedMenuConfiguration": {"Behavior": "UseCollectionName", "Group": "Details", "Order": 10000},
              "Lookup": {"@odata.type": T + "LookupAttributeMetadata", "AttributeType": "Lookup",
                         "AttributeTypeName": {"Value": "LookupType"}, "SchemaName": "spk_ArregloId",
                         "DisplayName": etiqueta("Arreglo"), "Description": etiqueta("Arreglo padre"),
                         "RequiredLevel": REQ_NONE, "IsAuditEnabled": AUD_OFF}}
    est, b, _ = escribir(dv, "POST", "RelationshipDefinitions", cuerpo)
    mostrar("POST relacion", est, b)
    for t in ("spk_arreglo", "spk_cuota"):
        est, b, _ = escribir(dv, "POST", "PublishXml", {"ParameterXml": f"<importexportxml><entities><entity>{t}</entity></entities></importexportxml>"}, solucion=None)
        print("PublishXml", t, est)
est, b, _ = dv.call("GET", f"RelationshipDefinitions(SchemaName='{NOM}')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata?$select=SchemaName,ReferencedEntity,ReferencingEntity,ReferencingAttribute,IsCustomRelationship,IsManaged")
mostrar("RELEER relacion", est, b)
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cuota')/Attributes(LogicalName='spk_arregloid')/Microsoft.Dynamics.CRM.LookupAttributeMetadata?$select=LogicalName,Targets")
mostrar("RELEER lookup", est, b)
