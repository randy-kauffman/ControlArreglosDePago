"""Solo lectura: privilegios de los roles SR - CARP - Oficial y SR - CARP - Jefe sobre las tablas del area y systemuser/email/queue (profundidad)."""
from t2lib import *

dv = adm()
roles = get(dv, f"roles?$select=roleid,name&$filter=_businessunitid_value eq {BU_ID} and startswith(name,'SR - CARP')")["value"]
PROF = {1: "Basic(usuario)", 2: "Local(BU)", 4: "Deep(BU+hijas)", 8: "Global(org)"}
out = {}
for r in roles:
    ids = {}
    for frag in ("sanic_carp", "SystemUser", "Email", "Queue", "ActivityPointer", "Team", "User"):
        for x in get(dv, f"privileges?$select=privilegeid,name&$filter=contains(name,'{frag}')&$top=500")["value"]:
            ids[x["privilegeid"]] = x["name"]
    # profundidad: RetrieveRolePrivilegesRole
    est, b, _ = dv.call("GET", f"RetrieveRolePrivilegesRole(RoleId={r['roleid']})")
    assert est == 200, (est, b)
    prof = [{"privilegeid": x["PrivilegeId"], "privilegedepthmask": {"Basic": 1, "Local": 2, "Deep": 4, "Global": 8}.get(x["Depth"], x["Depth"])} for x in b["RolePrivileges"]]
    print(r["name"], len(ids), len(prof))
    filas = {}
    for p in prof:
        n = ids.get(p["privilegeid"])
        if n and (n.startswith("prv") and any(t in n for t in ("sanic_carp", "SystemUser", "systemuser", "Email", "Queue", "ActivityPointer", "Team"))):
            filas[n] = PROF.get(p["privilegedepthmask"], p["privilegedepthmask"])
    out[r["name"]] = dict(sorted(filas.items()))
    print("==", r["name"])
    for k, v in sorted(filas.items()):
        print("  ", k, v)
volcar("12_privilegios_roles.json", out)
