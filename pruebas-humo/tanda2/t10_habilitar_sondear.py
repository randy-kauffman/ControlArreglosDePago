"""Habilita TEMPORALMENTE a SPIKE4 (autorizado) y sondea que ve: WhoAmI, lectura de systemuser de Randy, del perfil de columna, y de las filas HUMO.
La linea base ya esta en estado.json. La reversion es t99_revertir.py (correrla siempre al final)."""
from t2lib import *

dv = adm()
base = leer_estado()["linea_base"]
uid = base["usuario"]["systemuserid"]
YO = get(dv, "WhoAmI")["UserId"]
print("habilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[:2])
guardar_clave("habilitado", True)
time.sleep(30)
u = noadm()
est, b, _ = u.call("GET", "WhoAmI")
print("SPIKE4 WhoAmI:", est, (b.get("UserId") == uid) if est == 200 else b)
sonda = {}
est, b, _ = u.call("GET", f"systemusers({YO})?$select=fullname")
sonda["lee_systemuser_randy"] = (est, err(b) if est != 200 else b.get("fullname"))
est, b, _ = u.call("GET", f"systemusers({uid})?$select=fullname")
sonda["lee_su_systemuser"] = (est, err(b) if est != 200 else "ok")
CL, AR = conjuntos(dv)["cliente"], conjuntos(dv)["arreglo"]
est, b, _ = u.call("GET", f"{CL}({fila1('cliente-01')})?$select=sanic_nombre")
sonda["lee_cliente_humo"] = (est, err(b) if est != 200 else "ok")
est, b, _ = u.call("GET", f"{AR}({fila1('arreglo-01')})?$select=sanic_nombre,sanic_cuentadebitar,sanic_instantaneacedula,_sanic_oficialid_value,statecode,statuscode")
sonda["lee_arreglo_01"] = (est, err(b) if est != 200 else {k: v for k, v in b.items() if not k.startswith("@")})
# permisos del perfil de columna
pid = perfil_id(dv)
fp = get(dv, f"fieldpermissions?$select=entityname,attributelogicalname,canread,cancreate,canupdate&$filter=_fieldsecurityprofileid_value eq {pid}")["value"]
sonda["perfil_permisos"] = [{k: v for k, v in x.items() if not k.startswith("@")} for x in fp]
print(json.dumps(sonda, indent=1, ensure_ascii=False))
volcar("10_sonda_spike4.json", sonda)
