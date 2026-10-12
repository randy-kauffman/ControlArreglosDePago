"""V-8 (segunda parte). (1) valor 'falsificado' con la forma de la plataforma (ARR-777777): se guarda? (2) que pasa si un plugin QUITA sanic_nombre del Target en PreValidation / en PreOperation,
o lo FIJA en PreOperation (los mutadores son steps rank 2 que se habilitan de a uno; los observadores rank 1 ven el estado antes de mutar en la misma etapa y el resultado en las siguientes).
(3) el no administrador (sin las dos columnas protegidas, que su rol no puede escribir). Datos ficticios ZZV-."""
from v70_lib import *

dv = adm()
st = leer70(); base = st["linea_base"]; uid = base["usuario"]["id"]; yo = mi_id(dv)
S = st["steps"]
C = Corrida(dv, "70_v8_trazas_2.json")
paso = C.paso
cli = cliente70(dv, "v8b")
ver = version70(dv, "v8b")
ult = {}


def leer(rid):
    return {k: v for k, v in dv.call("GET", f"{AR}({rid})?$select=sanic_nombre")[1].items() if k == "sanic_nombre"}


def crear_paso(nombre, svc, cuerpo, sufijo):
    def accion():
        est, b = crear_arreglo_raw(svc, cuerpo, sufijo)
        ult["id"] = b.get("sanic_carp_tbl_arregloid") if est == 201 else None
        ult["resp"] = b.get("sanic_nombre") if est == 201 else None
        return est, b
    paso(nombre, accion, lambda: ({"respuesta_sanic_nombre": ult.get("resp"), **leer(ult["id"])} if ult.get("id") else None))


def activar(nombre, si):
    est = dv.call("PATCH", f"sdkmessageprocessingsteps({S[nombre]})", {"statecode": 0 if si else 1, "statuscode": 1 if si else 2})[0]
    assert est in (200, 204), est
    time.sleep(10)
    print("  step", nombre, "habilitado" if si else "deshabilitado", est, flush=True)


MUT = ["ZZV-v8-mut-PreVal-quitar", "ZZV-v8-mut-PreOp-quitar", "ZZV-v8-mut-PreOp-fijar"]
try:
    # (1) valor con la forma de la plataforma
    crear_paso("B0 ADMIN: Create CON sanic_nombre='ARR-777777' (forma de la plataforma)", dv, cuerpo_arreglo(dv, cli, ver, yo, "b0", nombre="ARR-777777"), "b0")
    crear_paso("B0b ADMIN: Create SIN sanic_nombre (la secuencia sigue o salta?)", dv, cuerpo_arreglo(dv, cli, ver, yo, "b0b"), "b0b")
    # (2) mutadores
    activar(MUT[0], True)
    crear_paso("B1 ADMIN + plugin QUITA en PreValidation: Create CON 'ZZV-NOMBRE'", dv, cuerpo_arreglo(dv, cli, ver, yo, "b1", nombre="ZZV-NOMBRE"), "b1")
    crear_paso("B1b ADMIN + plugin QUITA en PreValidation: Create SIN sanic_nombre", dv, cuerpo_arreglo(dv, cli, ver, yo, "b1b"), "b1b")
    activar(MUT[0], False)
    activar(MUT[1], True)
    crear_paso("B2 ADMIN + plugin QUITA en PreOperation: Create CON 'ZZV-NOMBRE'", dv, cuerpo_arreglo(dv, cli, ver, yo, "b2", nombre="ZZV-NOMBRE"), "b2")
    crear_paso("B2b ADMIN + plugin QUITA en PreOperation: Create SIN sanic_nombre", dv, cuerpo_arreglo(dv, cli, ver, yo, "b2b"), "b2b")
    activar(MUT[1], False)
    activar(MUT[2], True)
    crear_paso("B3 ADMIN + plugin FIJA 'ZZV-FIJO' en PreOperation: Create SIN sanic_nombre", dv, cuerpo_arreglo(dv, cli, ver, yo, "b3"), "b3")
    crear_paso("B3b ADMIN + plugin FIJA 'ZZV-FIJO' en PreOperation: Create CON 'ZZV-NOMBRE'", dv, cuerpo_arreglo(dv, cli, ver, yo, "b3b", nombre="ZZV-NOMBRE"), "b3b")
    activar(MUT[2], False)
finally:
    for m in MUT:
        dv.call("PATCH", f"sdkmessageprocessingsteps({S[m]})", {"statecode": 1, "statuscode": 2})
    print("mutadores deshabilitados (final):", [get(dv, f"sdkmessageprocessingsteps({S[m]})?$select=statecode")["statecode"] for m in MUT])

# (3) no administrador (el usuario sigue habilitado desde v70_20)
yo2 = correo()
e, b, _ = yo2.call("GET", "WhoAmI"); print("mail WhoAmI:", e); assert e == 200, b


def sin_protegidas(c):
    return {k: v for k, v in c.items() if k not in ("sanic_cuentadebitar", "sanic_instantaneacedula")}


crear_paso("N3 NO ADMIN: Create SIN sanic_nombre (sin columnas protegidas)", yo2, sin_protegidas(cuerpo_arreglo(dv, cli, ver, uid, "n3")), "n3")
crear_paso("N4 NO ADMIN: Create CON sanic_nombre='ZZV-NOMBRE' (sin columnas protegidas)", yo2, sin_protegidas(cuerpo_arreglo(dv, cli, ver, uid, "n4", nombre="ZZV-NOMBRE")), "n4")
crear_paso("N5 NO ADMIN: Create CON sanic_nombre='ARR-777778' (forma de la plataforma)", yo2, sin_protegidas(cuerpo_arreglo(dv, cli, ver, uid, "n5", nombre="ARR-777778")), "n5")

pasos = C.cerrar(espera_min=45)
ev = json.load(open(os.path.join(EVID, "70_v8_autonumerico.json"), encoding="utf-8"))
ev["parte2_pasos"] = [dict(p, t_ini=iso(p["t_ini"]), t_fin=iso(p["t_fin"])) for p in pasos]
volcar("70_v8_autonumerico.json", ev)
