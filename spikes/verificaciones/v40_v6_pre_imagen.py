"""V-6: la pre-imagen de un step de Update en Arreglo trae las columnas con seguridad por columna (sanic_cuentadebitar, sanic_instantaneacedula) con su valor REAL
para (a) el administrador, (b) un usuario no administrador CON el perfil CSP - CARP - Datos sensibles, (c) uno SIN el perfil. El usuario no administrador es el de
aplicacion de correo (PP_MAIL_*): se habilita, se agrega al perfil y se devuelve al estado inicial (el estado inicial esta en estado.json -> linea_base). Datos ficticios."""
from vlib import *

dv = adm()
base = leer_estado()["linea_base"]
uid = base["usuario"]["id"]
PERFIL = base["perfil"][0]["fieldsecurityprofileid"]
yo = mi_id(dv)
C = Corrida(dv, '40_v6_pre_imagen.json')
paso = C.paso


cli = crear_cliente(dv, "v6b")
ver = crear_version(dv, "v6b")
a = crear_arreglo(dv, "v6b", cli, ver, yo, cuenta="ZZV-cta-REAL-6", cedula="ZZV-ced-REAL-6")
AR = ES["arreglo"]
SEG = "sanic_cuentadebitar,sanic_instantaneacedula"
# lo que lee cada uno directamente
e, b, _ = dv.call("GET", f"{AR}({a})?$select={SEG}")
print("admin lee directo:", e, b if e != 200 else {k: b[k] for k in SEG.split(',')})
lectura = {"admin_lee_directo": {k: b.get(k) for k in SEG.split(',')} if e == 200 else b}

# --- habilitar al usuario no administrador (estado inicial: deshabilitado)
print("habilitar:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0])
time.sleep(30)
yo2 = correo()
e, b, _ = yo2.call("GET", "WhoAmI"); print("mail WhoAmI:", e)
assert e == 200, b
e, b, _ = yo2.call("GET", f"{AR}({a})?$select={SEG},sanic_comentarios")
print("no admin SIN perfil lee directo:", e, b if e != 200 else {k: b.get(k) for k in SEG.split(',')})
lectura["sin_perfil_lee_directo"] = {k: b.get(k) for k in SEG.split(',')} if e == 200 else b

paso("(a) ADMIN: PATCH comentarios", lambda: dv.call("PATCH", f"{AR}({a})", {"sanic_comentarios": "ZZV-v6-admin"})[:2])
paso("(c) NO ADMIN SIN perfil: PATCH comentarios", lambda: yo2.call("PATCH", f"{AR}({a})", {"sanic_comentarios": "ZZV-v6-sinperfil"})[:2])

# --- agregar al perfil y esperar a que la cache de FLS lo vea
est, b, _ = dv.call("POST", f"fieldsecurityprofiles({PERFIL})/systemuserprofiles_association/$ref", {"@odata.id": f"{dv.api}systemusers({uid})"})
print("agregar al perfil:", est, b if est >= 300 else "")
miembros = get(dv, f"fieldsecurityprofiles({PERFIL})?$expand=systemuserprofiles_association($select=systemuserid)")["systemuserprofiles_association"]
print("miembros del perfil ahora:", [m["systemuserid"] for m in miembros])
lectura["con_perfil_lee_directo"] = None
for i in range(8):
    time.sleep(15)
    e, b, _ = yo2.call("GET", f"{AR}({a})?$select={SEG}")
    vals = {k: b.get(k) for k in SEG.split(',')} if e == 200 else b
    print(f"  intento {i+1}: no admin CON perfil lee directo:", e, vals)
    lectura["con_perfil_lee_directo"] = vals
    if e == 200 and b.get("sanic_cuentadebitar") is not None:
        break
paso("(b) NO ADMIN CON perfil: PATCH comentarios", lambda: yo2.call("PATCH", f"{AR}({a})", {"sanic_comentarios": "ZZV-v6-conperfil"})[:2])
# (b2) ademas: el no admin con perfil cambia la propia columna protegida: el Target trae la columna protegida
paso("(b2) NO ADMIN CON perfil: PATCH cuentadebitar y comentarios (el Target trae la protegida)", lambda: yo2.call("PATCH", f"{AR}({a})", {"sanic_comentarios": "ZZV-v6-conperfil-2", "sanic_cuentadebitar": "ZZV-cta-NUEVA-6"})[:2])


# --- V-2 (extra): el Oficial (no admin) sobre filas INACTIVAS
cv_i = crear_conversacion(dv, "v6i")
ar_i = crear_arreglo(dv, "v6i", cli, ver, yo)
print("inactivar:", dv.call("PATCH", f"{ES['conversacion']}({cv_i})", {"statecode": 1, "statuscode": 2})[0], dv.call("PATCH", f"{AR}({ar_i})", {"statecode": 1, "statuscode": 2})[0])
paso("(V-2 extra) NO ADMIN (Oficial) PATCH detalle sobre Conversacion INACTIVA", lambda: yo2.call("PATCH", f"{ES['conversacion']}({cv_i})", {"sanic_detallesinrespuesta": "ZZV-v2-oficial"})[:2],
     lambda: dv.call("GET", f"{ES['conversacion']}({cv_i})?$select=statecode,sanic_detallesinrespuesta")[1])
paso("(V-2 extra) NO ADMIN (Oficial) PATCH comentarios sobre Arreglo INACTIVO", lambda: yo2.call("PATCH", f"{AR}({ar_i})", {"sanic_comentarios": "ZZV-v2-oficial"})[:2],
     lambda: dv.call("GET", f"{AR}({ar_i})?$select=statecode,sanic_comentarios")[1])
paso("(V-2 extra) NO ADMIN (Oficial) PATCH estado Arreglo INACTIVO -> Vigente (reapertura)", lambda: yo2.call("PATCH", f"{AR}({ar_i})", {"statecode": 0, "statuscode": 159460001})[:2],
     lambda: dv.call("GET", f"{AR}({ar_i})?$select=statecode,statuscode")[1])
paso("(V-1 extra) NO ADMIN (Oficial) PATCH estado Conversacion INACTIVA -> Abierta (formulario / Xrm.WebApi de un Oficial)", lambda: yo2.call("PATCH", f"{ES['conversacion']}({cv_i})", {"statecode": 0, "statuscode": 1})[:2],
     lambda: dv.call("GET", f"{ES['conversacion']}({cv_i})?$select=statecode,statuscode")[1])

# --- devolver el perfil al estado inicial
est, b, _ = dv.call("DELETE", f"fieldsecurityprofiles({PERFIL})/systemuserprofiles_association({uid})/$ref")
print("quitar del perfil:", est, b if est >= 300 else "")
miembros = get(dv, f"fieldsecurityprofiles({PERFIL})?$expand=systemuserprofiles_association($select=systemuserid),teamprofiles_association($select=teamid)")
print("miembros del perfil tras quitar: usuarios", len(miembros["systemuserprofiles_association"]), "teams", len(miembros["teamprofiles_association"]))
assert not miembros["systemuserprofiles_association"] and not miembros["teamprofiles_association"], "el perfil no quedo como al inicio"
volcar("40_v6_lecturas_directas.json", lectura)
C.cerrar()
print("Usuario sigue HABILITADO hasta v90 (lo usa V-7); el perfil quedo vacio como al inicio.")
