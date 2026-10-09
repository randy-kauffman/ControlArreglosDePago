"""Preparacion 1 (evidencia): queues con direccion de correo y sus mailboxes (solo lectura, columnas no sensibles)."""
from lib import *
dv = Dataverse()
s, q, _ = dv.call("GET", "queues?$select=queueid,name,emailaddress,incomingemailfilteringmethod,incomingemaildeliverymethod,outgoingemaildeliverymethod,_ownerid_value,statecode,_defaultmailbox_value&$filter=emailaddress ne null", cabeceras=PREFER)
out = []
for x in q["value"]:
    s, m, _ = dv.call("GET", f"mailboxes?$filter=_regardingobjectid_value eq {x['queueid']}&$select=mailboxid,name,emailaddress,incomingemaildeliverymethod,outgoingemaildeliverymethod,incomingemailstatus,outgoingemailstatus,mailboxstatus,enabledforincomingemail,enabledforoutgoingemail,statecode,statuscode,_ownerid_value,lastsuccessfulsynccompletedon,processemailreceivedafter,lastsyncerror,isemailaddressapprovedbyo365admin,isforwardmailbox,_emailserverprofile_value", cabeceras=PREFER)
    out.append({"queue": {k: v for k, v in x.items() if not k.startswith("@odata")}, "mailbox": [{k: v for k, v in mm.items() if not k.startswith("@odata")} for mm in m.get("value", [])]})
volcar("preparacion_queues_mailboxes.json", out)
for o in out:
    qq = o["queue"]; mb = o["mailbox"][0] if o["mailbox"] else {}
    print(qq["name"], qq["emailaddress"], "| filtro", qq.get("incomingemailfilteringmethod@OData.Community.Display.V1.FormattedValue"), "| dueño queue:", qq.get("_ownerid_value@OData.Community.Display.V1.FormattedValue"),
          "| mailbox in/out:", mb.get("incomingemaildeliverymethod@OData.Community.Display.V1.FormattedValue"), "/", mb.get("outgoingemaildeliverymethod@OData.Community.Display.V1.FormattedValue"),
          "| estado in/out:", mb.get("incomingemailstatus@OData.Community.Display.V1.FormattedValue"), "/", mb.get("outgoingemailstatus@OData.Community.Display.V1.FormattedValue"),
          "| habilitado in/out:", mb.get("enabledforincomingemail"), mb.get("enabledforoutgoingemail"), "| aprobado admin O365:", mb.get("isemailaddressapprovedbyo365admin"), "| ultimo sync:", mb.get("lastsuccessfulsynccompletedon"))
