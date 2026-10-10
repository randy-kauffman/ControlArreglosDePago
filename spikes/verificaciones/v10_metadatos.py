"""Metadatos de solo lectura: V-3 (email), V-4 (filtros de mensaje), mensajes de estado, columnas de archivo y choices necesarios."""
from vlib import *
dv = adm()
out = {}

# ---- V-3: StatusAttributeMetadata de email y directioncode
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='email')/Attributes(LogicalName='statuscode')/Microsoft.Dynamics.CRM.StatusAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)")
assert est == 200, b
out["email_statuscode"] = [(o["Value"], o["Label"]["UserLocalizedLabel"]["Label"] if o["Label"].get("UserLocalizedLabel") else None, o.get("State")) for o in b["OptionSet"]["Options"]]
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='email')/Attributes(LogicalName='directioncode')/Microsoft.Dynamics.CRM.BooleanAttributeMetadata?$select=LogicalName,AttributeType&$expand=OptionSet($select=TrueOption,FalseOption)")
assert est == 200, b
os_ = b["OptionSet"]
out["email_directioncode"] = {"false": os_["FalseOption"]["Label"]["UserLocalizedLabel"]["Label"], "true": os_["TrueOption"]["Label"]["UserLocalizedLabel"]["Label"]}
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='email')/Attributes(LogicalName='statecode')/Microsoft.Dynamics.CRM.StateAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)")
out["email_statecode"] = [(o["Value"], o["Label"]["UserLocalizedLabel"]["Label"], o.get("DefaultStatus")) for o in b["OptionSet"]["Options"]]
print(json.dumps(out, indent=1))

# emails reales (solo lectura): contraste de statuscode/directioncode
emails = []
for sc in (4, 3, 1, 2, 6, 7, 8):
    est, b, _ = dv.call("GET", f"emails?$select=statuscode,directioncode,statecode&$filter=statuscode eq {sc}&$top=1000&$count=true")
    if est == 200:
        from collections import Counter
        c = Counter((x["statecode"], x["statuscode"], x["directioncode"]) for x in b["value"])
        emails.append({"statuscode": sc, "n_devueltas": len(b["value"]), "combinaciones(statecode,statuscode,directioncode)": [[list(k), v] for k, v in c.items()]})
out["emails_reales"] = emails
print(json.dumps(emails, indent=1))

# ---- V-4: mensajes y filtros
tabla = "sanic_carp_tbl_versionplantilla"
est, b, _ = dv.call("GET", "sdkmessages?$select=sdkmessageid,name&$filter=contains(name,'FileBlocks') or name eq 'SetState' or name eq 'SetStateDynamicEntity' or name eq 'Update' or name eq 'InitializeFileBlocksUpload' or name eq 'UploadBlock' or name eq 'CommitFileBlocksUpload' or name eq 'InitializeFileBlocksDownload'")
msgs = {m["name"]: m["sdkmessageid"] for m in b["value"]}
out["mensajes"] = msgs; print(msgs)
filtros = {}
for n, mid in msgs.items():
    est, f, _ = dv.call("GET", f"sdkmessagefilters?$select=sdkmessagefilterid,primaryobjecttypecode,secondaryobjecttypecode,isvisible,iscustomprocessingstepallowed,availability&$filter=_sdkmessageid_value eq {mid} and primaryobjecttypecode eq '{tabla}'")
    filtros[n] = f["value"] if est == 200 else (est, f)
out["filtros_versionplantilla"] = filtros
print(json.dumps(filtros, indent=1))
# filtros de TODOS los mensajes para la tabla que tengan que ver con archivo
est, f, _ = dv.call("GET", f"sdkmessagefilters?$select=sdkmessagefilterid,_sdkmessageid_value,iscustomprocessingstepallowed&$filter=primaryobjecttypecode eq '{tabla}'&$expand=sdkmessageid($select=name)")
out["todos_los_mensajes_con_filtro_versionplantilla"] = sorted((x["sdkmessageid"]["name"], x["iscustomprocessingstepallowed"]) for x in f["value"])
print("mensajes con filtro en versionplantilla:", out["todos_los_mensajes_con_filtro_versionplantilla"])
# SetState en conversacion y arreglo
for t in ("conversacion", "arreglo"):
    for n in ("SetState", "SetStateDynamicEntity", "Update"):
        if n in msgs:
            est, f, _ = dv.call("GET", f"sdkmessagefilters?$select=sdkmessagefilterid,iscustomprocessingstepallowed&$filter=_sdkmessageid_value eq {msgs[n]} and primaryobjecttypecode eq 'sanic_carp_tbl_{t}'")
            out.setdefault("filtros_estado", {})[f"{t}:{n}"] = f["value"]
print(json.dumps(out.get("filtros_estado"), indent=1))

# ---- columnas de archivo de la version, y atributos de interes
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{tabla}')/Attributes?$select=LogicalName,AttributeType,RequiredLevel&$filter=AttributeType eq Microsoft.Dynamics.CRM.AttributeTypeCode'Virtual' or AttributeType eq Microsoft.Dynamics.CRM.AttributeTypeCode'Uniqueidentifier'")
out["version_attrs_virtuales"] = [(a["LogicalName"], a["AttributeType"]) for a in b.get("value", [])] if est == 200 else (est, b)
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{tabla}')/Attributes/Microsoft.Dynamics.CRM.FileAttributeMetadata?$select=LogicalName,MaxSizeInKB")
out["version_archivo"] = b.get("value") if est == 200 else (est, b)
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{tabla}')/Attributes?$select=LogicalName,AttributeType,RequiredLevel&$filter=RequiredLevel/Value eq Microsoft.Dynamics.CRM.AttributeRequiredLevel'ApplicationRequired'")
out["version_requeridas"] = [a["LogicalName"] for a in b["value"]] if est == 200 else (est, b)
print(out["version_attrs_virtuales"], out["version_archivo"], out["version_requeridas"])
volcar("10_metadatos.json", out)
