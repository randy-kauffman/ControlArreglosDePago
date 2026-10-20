"""Solo lectura: en que BU esta cada candidato a propietario (admin, Randy, Frederick) y quien es propietario de las filas t2."""
from t2lib import *

dv = adm()
YO = get(dv, "WhoAmI")["UserId"]
print("admin BU:", get(dv, f"systemusers({YO})?$select=fullname,_businessunitid_value"))
for x in get(dv, "systemusers?$select=systemuserid,fullname,_businessunitid_value,isdisabled,accessmode&$filter=contains(fullname,'Randy') or contains(fullname,'Frederick')")["value"]:
    print({k: v for k, v in x.items() if not k.startswith("@")})
bus = get(dv, "businessunits?$select=businessunitid,name,_parentbusinessunitid_value")["value"]
print([(b["name"], b["businessunitid"], b["_parentbusinessunitid_value"]) for b in bus])
for k, t in (("cliente-t2-adm", "cliente"), ("arreglo-t2-h32arr", "arreglo")):
    r = get(dv, f"{conjuntos(dv)[t]}({fila(k)})?$select=_ownerid_value,_owningbusinessunit_value")
    print(k, r.get("_ownerid_value"), r.get("_owningbusinessunit_value"))
