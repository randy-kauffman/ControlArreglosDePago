"""Preparacion 1: queue del buzon 1, su mailbox, estados, propietario. Solo lectura."""
import json
from dv import Dataverse
from graph import Graph
dv = Dataverse(); g = Graph(); b1 = g.buzon[1]
est, q, _ = dv.call("GET", f"queues?$filter=emailaddress eq '{b1}'&$select=queueid,name,emailaddress,incomingemailfilteringmethod,incomingemaildeliverymethod,outgoingemaildeliverymethod,_ownerid_value,_owningbusinessunit_value,statecode,queuetypecode,_defaultmailbox_value")
print(est, json.dumps(q, indent=1, ensure_ascii=False))
for x in q.get("value", []):
    est, mb, _ = dv.call("GET", f"mailboxes?$filter=_regardingobjectid_value eq {x['queueid']}")
    print(est, json.dumps(mb, indent=1, ensure_ascii=False))
    est, o, _ = dv.call("GET", f"{'systemusers' if False else 'owners'}({x['_ownerid_value']})")
    print("owner", est, json.dumps(o)[:400])
