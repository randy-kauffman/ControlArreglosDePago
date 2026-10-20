"""Relectura final: perfil de columna (teams asociados) y SPIKE4."""
from t2blib import *

dv = adm()
pid = leer_estado()["linea_base"]["perfil"]["id"]
uid = leer_estado()["linea_base"]["usuario"]["systemuserid"]
fin = {"perfil": miembros_perfil(dv, pid), "spike4_isdisabled": get(dv, f"systemusers({uid})?$select=isdisabled")["isdisabled"], "jefatura_miembros": miembros_team(dv, jefatura_id(dv))}
print(json.dumps(fin, indent=1))
volcar("98_estado_final.json", fin)
guardar_clave("estado_final", fin)
