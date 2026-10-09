"""Lista todos los Email [SPIKE3] de Dataverse (lectura)."""
import sys
from lib import *
dv = Dataverse()
est, b, _ = dv.call("GET", f"emails?$select=activityid,subject,createdon,senton,actualend,statuscode,directioncode,messageid,inreplyto,correlationmethod,_regardingobjectid_value,_parentactivityid_value,_correlatedactivityid_value,_receivingmailboxid_value,_ownerid_value,sender,torecipients,correlatedsubjectchanged&$filter=createdon ge 2026-10-08T19:38:00Z&$orderby=createdon asc", cabeceras=PREFER)
for e in b["value"]:
    if "[SPIKE3]" not in e["subject"]: continue
    print(e["createdon"], e["activityid"][:8], "OUT" if e["directioncode"] else "IN ", e["statuscode@OData.Community.Display.V1.FormattedValue"], "|", e["subject"][:60], "|",
          "reg:", (e["_regardingobjectid_value"] or "")[:8], "| corr:", e["correlationmethod@OData.Community.Display.V1.FormattedValue"], "| parent:", (e["_parentactivityid_value"] or "")[:8],
          "| rcvmb:", e.get("_receivingmailboxid_value@OData.Community.Display.V1.FormattedValue"), "| from:", e["sender"], "| owner:", e.get("_ownerid_value@OData.Community.Display.V1.FormattedValue"))
