"""Sondeo: que systemusers puede leer SPIKE4 con Retrieve puntual (para elegir el caso real de H-32). Solo lecturas."""
from t2lib import *

dv = adm()
u = noadm()
todos = get(dv, "systemusers?$select=systemuserid,fullname,isdisabled,accessmode,_businessunitid_value&$top=200")["value"]
res = []
for x in todos:
    est, b, _ = u.call("GET", f"systemusers({x['systemuserid']})?$select=fullname")
    res.append({"id": x["systemuserid"], "fullname": x["fullname"], "isdisabled": x["isdisabled"], "accessmode": x["accessmode"], "bu": x["_businessunitid_value"], "http": est,
                "error": err(b)[:160] if est != 200 else ""})
print(len(res), "usuarios;", sum(1 for r in res if r["http"] != 200), "no legibles por SPIKE4")
for r in res:
    if r["http"] != 200:
        print(r)
volcar("11_visibilidad_usuarios.json", res)
