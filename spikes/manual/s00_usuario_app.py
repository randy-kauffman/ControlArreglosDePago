"""Tarea A.1: alta del usuario de aplicacion para PP_MAIL_CLIENT_ID en la BU raiz, solo con el rol 'SPK Lector Cliente'.
Idempotente. Relee roles al final. No imprime secretos."""
from lib import *
dv = DvAdmin(); est = cargar()
bu = raiz_bu(dv)
u = usuario_app(dv)
print("usuario existente:", u)
if not u:
    cuerpo = {"applicationid": id_mail_client(), "businessunitid@odata.bind": f"/businessunits({bu})",
              "firstname": "SPIKE4", "lastname": "Correo (usuario de aplicacion de prueba)",
              "internalemailaddress": "spike4-app@example.invalid"}
    s, b, h = dv.call("POST", "systemusers", cuerpo)
    print("POST systemusers:", s, "" if s in (200, 201, 204) else b)
    u = usuario_app(dv)
assert u, "no se pudo releer el usuario"
uid = u["systemuserid"]; est["usuario_app_id"] = uid; est["bu_raiz"] = bu
print("releido:", {k: u[k] for k in ("systemuserid", "fullname", "isdisabled", "accessmode")}, "BU raiz?", u["_businessunitid_value"] == bu)
print("roles actuales:", roles_de_usuario(dv, uid))
rol = rol_por_nombre(dv, "SPK Lector Cliente", bu)
assert len(rol) == 1, rol
est["rol_lector"] = rol[0]["roleid"]
if "SPK Lector Cliente" not in roles_de_usuario(dv, uid):
    s, b, _ = dv.call("POST", f"systemusers({uid})/systemuserroles_association/$ref",
                      {"@odata.id": f"{dv.api}roles({rol[0]['roleid']})"})
    print("asociar rol lector:", s, "" if s in (200, 204) else b)
print("RELEER roles:", roles_de_usuario(dv, uid))
guardar(est)
