"""Q6 (solo lectura): privilegios sobre msdyn_richtextfile y que roles los tienen y con que profundidad; ademas IsOptimisticConcurrencyEnabled de las tablas del spike."""
import json
from dv import Dataverse
dv = Dataverse()
DEPTH = {1: "Basic(usuario)", 2: "Local(unidad de negocio)", 4: "Deep(BU y subordinadas)", 8: "Global(organizacion)"}
est, b, _ = dv.call("GET", "privileges?$select=privilegeid,name,accessright&$filter=startswith(name,'prv') and contains(name,'msdyn_richtextfile')")
print(est, [(p["name"], p["accessright"]) for p in b.get("value", [])] if est == 200 else b)
if est == 200:
    for p in b["value"]:
        if p["name"] in ("prvReadmsdyn_richtextfile", "prvCreatemsdyn_richtextfile", "prvDeletemsdyn_richtextfile"):
            est2, rp, _ = dv.call("GET", f"roleprivilegescollection?$select=roleid,privilegedepthmask&$filter=privilegeid eq {p['privilegeid']}")
            out = []
            for x in rp["value"]:
                est3, r, _ = dv.call("GET", f"roles({x['roleid']})?$select=name")
                out.append((r.get("name"), DEPTH.get(x["privilegedepthmask"], x["privilegedepthmask"])))
            print(p["name"], "->", sorted(out))
for t in ("spk_arreglo", "msdyn_richtextfile", "email"):
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{t}')?$select=LogicalName,IsOptimisticConcurrencyEnabled")
    print(t, "IsOptimisticConcurrencyEnabled =", b.get("IsOptimisticConcurrencyEnabled") if est == 200 else (est, b))
