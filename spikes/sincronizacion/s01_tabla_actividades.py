"""Preparacion 2: habilita actividades en spk_conversacion (sustituto de Conversacion) dentro de SpikesArreglosPago.
La tabla ya existia del spike 2 (HasActivities=false). HasActivities es de un solo sentido: no se puede deshabilitar despues."""
import json, time
from dv import Dataverse, SOLUCION
from spk_lib import escribir
dv = Dataverse()
est, e, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_conversacion')")
print("antes HasActivities =", e.get("HasActivities"))
if not e.get("HasActivities"):
    e["HasActivities"] = True
    est, r, _ = escribir(dv, "PUT", f"EntityDefinitions({e['MetadataId']})", e, cabeceras={"MSCRM.MergeLabels": "true"})
    print("PUT", est, "" if est in (200, 204) else r)
for _ in range(12):
    est, e2, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_conversacion')?$select=HasActivities,HasNotes,PrimaryIdAttribute,EntitySetName,ObjectTypeCode")
    print("releido", {k: v for k, v in e2.items() if not k.startswith('@')})
    if e2.get("HasActivities"): break
    time.sleep(10)
# prueba real: un Email puede apuntar a la tabla como Regarding?
est, r, _ = dv.call("GET", "EntityDefinitions(LogicalName='email')/ManyToOneRelationships?$select=SchemaName,ReferencedEntity&$filter=ReferencedEntity eq 'spk_conversacion'")
print("relaciones email->spk_conversacion:", est, [x["SchemaName"] for x in r.get("value", [])])
