"""Solo lectura: opciones de resultado/motivo de Conversacion, columnas y Custom API generarformato."""
from blib import *

dv = adm()
for col in ("sanic_resultado", "sanic_motivosinrespuesta"):
    r = get(dv, f"EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Attributes(LogicalName='{col}')/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)")
    print(col, [(o["Value"], o["Label"]["UserLocalizedLabel"]["Label"] if o["Label"].get("UserLocalizedLabel") else None) for o in r["OptionSet"]["Options"]])
r = get(dv, "EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Attributes(LogicalName='sanic_detallesinrespuesta')?$select=AttributeType,LogicalName")
print("detalle", r["AttributeType"])
for api in ("generarformato", "unirconversaciones", "iniciarconversacionnueva", "tomarconversacion"):
    ps = get(dv, f"customapirequestparameters?$select=uniquename,type,isoptional&$filter=customapiid/uniquename eq 'sanic_carp_capi_{api}'")["value"]
    rs = get(dv, f"customapiresponseproperties?$select=uniquename,type&$filter=customapiid/uniquename eq 'sanic_carp_capi_{api}'")["value"]
    print(api, [(p["uniquename"], p["type"], p["isoptional"]) for p in ps], "->", [(p["uniquename"], p["type"]) for p in rs])
