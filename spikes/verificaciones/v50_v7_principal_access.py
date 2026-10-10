"""V-7: RetrievePrincipalAccess responde WriteAccess sobre una Version de plantilla para (1) el administrador, (2) un usuario con rol Jefe, (3) uno con solo Oficial
(que NO deberia tener Write). Se contrasta cada respuesta con un PATCH real de la fila (la verdad). Usuario: el de aplicacion de correo; para el caso Jefe se agrega
temporalmente al team 'Arreglos de Pago - Jefatura' y se saca al final."""
from vlib import *

dv = adm()
base = leer_estado()["linea_base"]
uid = base["usuario"]["id"]
VER = ES["versionplantilla"]
res = {}
ver = crear_version(dv, "v7")


def rpa(llamador, principal_id, rid):
    """RetrievePrincipalAccess con Target = la version y el principal dado (funcion ligada a systemuser)."""
    tgt = json.dumps({"@odata.id": f"{VER}({rid})"})
    est, b, _ = llamador.call("GET", f"systemusers({principal_id})/Microsoft.Dynamics.CRM.RetrievePrincipalAccess(Target=@t)?@t={tgt}")
    return est, b.get("AccessRights") if est == 200 else b


def patch_real(llamador, rid, texto):
    est, b, _ = llamador.call("PATCH", f"{VER}({rid})", {"sanic_huella": texto})
    return est, b.get("error", "")[:300] if isinstance(b, dict) else b


yo = mi_id(dv)
jef = get(dv, f"teams?$select=teamid&$filter=_businessunitid_value eq {BU_ID} and name eq 'Arreglos de Pago - Jefatura'")["value"][0]["teamid"]
jef_roles = [r["name"] for r in get(dv, f"teams({jef})?$expand=teamroles_association($select=name)")["teamroles_association"]]
res["team_jefatura_roles"] = jef_roles
miembros_antes = [m["systemuserid"] for m in get(dv, f"teams({jef})?$expand=teammembership_association($select=systemuserid)")["teammembership_association"]]
res["jefatura_miembros_antes"] = miembros_antes
print("Jefatura roles:", jef_roles, "| miembros antes:", miembros_antes)

# 1) administrador
res["admin_RPA"] = rpa(dv, yo, ver)
res["admin_PATCH"] = patch_real(dv, ver, "ZZV-v7-admin")
print("ADMIN  RPA:", res["admin_RPA"], "| PATCH real:", res["admin_PATCH"])

# 2) solo Oficial (el usuario de correo habilitado, fuera de Jefatura)
d = get(dv, f"systemusers({uid})?$select=isdisabled")
print("usuario deshabilitado?", d["isdisabled"])
if d["isdisabled"]:
    print("habilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0]); time.sleep(30)
yo2 = correo()
assert uid not in miembros_antes
res["oficial_RPA_por_admin"] = rpa(dv, uid, ver)
res["oficial_RPA_autollamada"] = rpa(yo2, uid, ver)
res["oficial_PATCH"] = patch_real(yo2, ver, "ZZV-v7-oficial")
res["oficial_lectura"] = yo2.call("GET", f"{VER}({ver})?$select=sanic_nombre")[0]
print("OFICIAL RPA (por admin):", res["oficial_RPA_por_admin"], "| autollamada:", res["oficial_RPA_autollamada"], "| PATCH real:", res["oficial_PATCH"], "| GET:", res["oficial_lectura"])

# 3) Jefe: agregar a Jefatura
print("agregar a Jefatura:", dv.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.AddMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]})[0])
try:
    for i in range(6):
        time.sleep(20)
        r = rpa(dv, uid, ver)
        print(f"  intento {i+1}: RPA por admin para el usuario como Jefe:", r)
        if r[0] == 200 and "WriteAccess" in str(r[1]):
            break
    res["jefe_RPA_por_admin"] = r
    res["jefe_RPA_autollamada"] = rpa(yo2, uid, ver)
    res["jefe_PATCH"] = patch_real(yo2, ver, "ZZV-v7-jefe")
    res["jefe_lectura"] = yo2.call("GET", f"{VER}({ver})?$select=sanic_nombre")[0]
    print("JEFE RPA (por admin):", res["jefe_RPA_por_admin"], "| autollamada:", res["jefe_RPA_autollamada"], "| PATCH real:", res["jefe_PATCH"], "| GET:", res["jefe_lectura"])
finally:
    print("quitar de Jefatura:", dv.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]})[0])
    despues = [m["systemuserid"] for m in get(dv, f"teams({jef})?$expand=teammembership_association($select=systemuserid)")["teammembership_association"]]
    res["jefatura_miembros_despues"] = despues
    print("Jefatura miembros despues:", despues, "(antes:", miembros_antes, ")")
volcar("50_v7_principal_access.json", res)
