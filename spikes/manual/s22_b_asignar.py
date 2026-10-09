"""Asigna (o quita) roles al usuario de aplicacion y relee. Uso: python3 s22_b_asignar.py +'SPK Oficial Correo' -'SPK Lector Cliente'"""
import sys
from lib import *
dv = DvAdmin(); est = cargar(); uid = est["usuario_app_id"]; bu = est["bu_raiz"]
for a in sys.argv[1:]:
    op, nombre = a[0], a[1:]
    r = rol_por_nombre(dv, nombre, bu); assert len(r) == 1, (nombre, r)
    rid = r[0]["roleid"]
    if op == "+":
        s, b, _ = dv.call("POST", f"systemusers({uid})/systemuserroles_association/$ref", {"@odata.id": f"{dv.api}roles({rid})"})
    else:
        s, b, _ = dv.call("DELETE", f"systemusers({uid})/systemuserroles_association/$ref?$id={dv.api}roles({rid})")
    print(op, nombre, s, "" if s in (200, 204) else b)
print("RELEER roles:", roles_de_usuario(dv, uid))
