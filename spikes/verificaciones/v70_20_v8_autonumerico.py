"""V-8: la primaria autonumerica sanic_nombre (ARR-{SEQNUM:6}) de Arreglo en el Create. Observa el Target en PreValidation (10), PreOperation (20) y PostOperation (40)
con y sin valor del llamador, y RELEE la fila. Corre como administrador y como el usuario de aplicacion no administrador (se habilita y se devuelve a deshabilitado en v70_90).
Datos ficticios ZZV-. El Update con sanic_nombre es un extra (step de Update con filtro sanic_nombre)."""
from v70_lib import *

dv = adm()
st = leer70(); base = st["linea_base"]
uid = base["usuario"]["id"]
yo = mi_id(dv)
C = Corrida(dv, "70_v8_trazas.json")
paso = C.paso

cli = cliente70(dv, "v8")
ver = version70(dv, "v8")
ult = {}


def leer(rid):
    return dv.call("GET", f"{AR}({rid})?$select=sanic_nombre,statecode,statuscode")[1]


def crear_paso(nombre, svc, cuerpo, sufijo):
    def accion():
        est, b = crear_arreglo_raw(svc, cuerpo, sufijo)
        ult["id"] = b.get("sanic_carp_tbl_arregloid") if est == 201 else None
        ult["resp"] = b.get("sanic_nombre") if est == 201 else None
        return est, b
    est, b = paso(nombre, accion, lambda: ({"respuesta_sanic_nombre": ult.get("resp"), **{k: v for k, v in leer(ult["id"]).items() if k == "sanic_nombre"}} if ult.get("id") else None))
    return ult.get("id")


# ------------------------------------------------------------------ administrador
a1 = crear_paso("A1 ADMIN: Create SIN sanic_nombre", dv, cuerpo_arreglo(dv, cli, ver, yo, "a1"), "a1")
a2 = crear_paso("A2 ADMIN: Create CON sanic_nombre='ZZV-NOMBRE'", dv, cuerpo_arreglo(dv, cli, ver, yo, "a2", nombre="ZZV-NOMBRE"), "a2")
a3 = crear_paso("A3 ADMIN: Create SIN sanic_nombre (otra vez; secuencia tras el caso A2)", dv, cuerpo_arreglo(dv, cli, ver, yo, "a3"), "a3")
a4 = crear_paso("A4 ADMIN: Create con sanic_nombre=null", dv, {**cuerpo_arreglo(dv, cli, ver, yo, "a4"), "sanic_nombre": None}, "a4")
a5 = crear_paso("A5 ADMIN: Create con sanic_nombre='' (vacio)", dv, cuerpo_arreglo(dv, cli, ver, yo, "a5", nombre=""), "a5")
if a1:
    paso("A6 ADMIN (extra): Update sanic_nombre='ZZV-UPD' sobre la fila de A1", lambda: dv.call("PATCH", f"{AR}({a1})", {"sanic_nombre": "ZZV-UPD"})[:2], lambda: leer(a1))

# ------------------------------------------------------------------ no administrador
print("habilitar usuario:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0])
st = leer70(); st["usuario_habilitado_por_v70"] = True; guardar70(st)
time.sleep(30)
yo2 = correo()
e, b, _ = yo2.call("GET", "WhoAmI"); print("mail WhoAmI:", e); assert e == 200, b
n1 = crear_paso("N1 NO ADMIN: Create SIN sanic_nombre", yo2, cuerpo_arreglo(dv, cli, ver, uid, "n1"), "n1")
n2 = crear_paso("N2 NO ADMIN: Create CON sanic_nombre='ZZV-NOMBRE'", yo2, cuerpo_arreglo(dv, cli, ver, uid, "n2", nombre="ZZV-NOMBRE"), "n2")

pasos = C.cerrar(espera_min=45)
volcar("70_v8_autonumerico.json", {"metadatos_sanic_nombre": base["metadatos_sanic_nombre"], "pasos": [dict(p, t_ini=iso(p["t_ini"]), t_fin=iso(p["t_fin"])) for p in pasos]})
