"""Inspecciona un Email de Dataverse: columnas clave, Queue Items, adjuntos, richtextfiles. Uso: s12_inspeccion.py <activityid> [etiqueta]"""
import sys
from lib import *
dv = Dataverse()
eid = sys.argv[1]; et = sys.argv[2] if len(sys.argv) > 2 else eid[:8]
e = email_completo(dv, eid)
res = {"email": e}
est, qi, _ = dv.call("GET", f"queueitems?$filter=_objectid_value eq {eid}&$select=queueitemid,title,_queueid_value,_workerid_value,_ownerid_value,enteredon,statecode,statuscode,objecttypecode", cabeceras=PREFER)
res["queueitems"] = qi.get("value", qi)
est, at, _ = dv.call("GET", f"activitymimeattachments?$filter=_objectid_value eq {eid}&$select=activitymimeattachmentid,filename,mimetype,filesize,attachmentnumber,attachmentcontentid,_objectid_value,objecttypecode,_attachmentid_value,_ownerid_value,activitysubject&$orderby=filename asc", cabeceras=PREFER)
res["attachments"] = at.get("value", at)
est, rt, _ = dv.call("GET", f"msdyn_richtextfiles?$select=msdyn_richtextfileid,msdyn_parentid,createdon,_ownerid_value&$filter=createdon ge 2026-10-08T19:00:00Z", cabeceras=PREFER)
res["richtextfiles_status"] = est
res["richtextfiles_recientes"] = (rt.get("value", []) if est == 200 else rt)
volcar(f"{et}_inspeccion.json", res)
print("estado", e.get("statuscode@OData.Community.Display.V1.FormattedValue"), "| regarding:", e.get("_regardingobjectid_value"), e.get("_regardingobjectid_value@Microsoft.Dynamics.CRM.lookuplogicalname"),
      "| owner:", e.get("_ownerid_value@OData.Community.Display.V1.FormattedValue"), "| attachmentcount:", e.get("attachmentcount"))
print("queueitems:", [(q.get("_queueid_value@OData.Community.Display.V1.FormattedValue"), q.get("_ownerid_value@OData.Community.Display.V1.FormattedValue"), q["statecode"]) for q in res["queueitems"]])
print("attachments:", [(a["filename"], a["mimetype"], a["filesize"]) for a in res["attachments"]] if isinstance(res["attachments"], list) else res["attachments"])
print("richtextfiles:", est, len(res["richtextfiles_recientes"]) if isinstance(res["richtextfiles_recientes"], list) else res["richtextfiles_recientes"])
