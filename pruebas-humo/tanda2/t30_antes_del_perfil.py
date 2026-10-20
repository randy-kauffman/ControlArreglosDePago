"""ANTES de asociar el perfil de columna a los teams (SPIKE4 = Oficial por el team default, SIN perfil = el 'fuera del perfil' de H-13):
H-32 (Retrieve que falla por falta de acceso, atrapado dentro del plugin), H-4 sin perfil (CARP-GEN-012), H-16 (c) orden de la falla de columna protegida, H-13 linea base."""
from t2lib import *

dv = adm()
u = noadm()
R = Res("30_antes_del_perfil.json")
UID = leer_estado()["linea_base"]["usuario"]["systemuserid"]
CL, AR = conjuntos(dv)["cliente"], conjuntos(dv)["arreglo"]
API = "sanic_carp_capi_generarformato"
t0 = reloj(dv) - timedelta(seconds=3)
a_gen, a_cli, a_arr = fila("arreglo-t2-gen"), fila("arreglo-t2-h32cli"), fila("arreglo-t2-h32arr")
cli_adm = fila("cliente-t2-adm")

# ---- H-32: lo que SPIKE4 ve directamente
est, b, _ = u.call("GET", f"{CL}({cli_adm})?$select=sanic_nombrecompleto")
R.v("H-32", "lectura directa (Web API) del Cliente propiedad del admin: sin acceso", est >= 400, "4xx", (est, err(b)[:200]))
est, b, _ = u.call("GET", f"{AR}({a_arr})?$select=sanic_nombre")
R.v("H-32", "lectura directa del Arreglo propiedad del admin: sin acceso", est >= 400, "4xx", (est, err(b)[:200]))
est, b, _ = u.call("GET", f"{AR}({a_cli})?$select=sanic_nombre,_sanic_clienteid_value")
R.v("H-32", "lectura directa del Arreglo h32cli (propiedad del team): legible", est == 200, 200, (est, err(b)[:200] if est != 200 else "ok"))

# ---- H-32: el mismo Retrieve dentro del plugin (GenerarFormato -> arreglosComoLlamador.Leer y clientes.Leer, via LeerOMenos)
t1 = reloj(dv)
est, b, seg = llamar(u, API, {"arregloid": a_arr})
R.v("H-32", "GenerarFormato sobre un Arreglo que el llamador no puede leer: el Retrieve falla, LeerOMenos lo atrapa y sale CARP-GEN-002 (no GEN-900)", est >= 400 and codigo(b) == "CARP-GEN-002", "4xx CARP-GEN-002", (est, codigo(b), err(b)[:200], seg))
est, b, seg = llamar(u, API, {"arregloid": a_cli})
R.v("H-32", "GenerarFormato con el Cliente ilegible (arreglo legible): el Retrieve del Cliente falla, se atrapa y sale CARP-GEN-002", est >= 400 and codigo(b) == "CARP-GEN-002", "4xx CARP-GEN-002", (est, codigo(b), err(b)[:200], seg))
fz = leer(dv, "arreglo", a_cli, "sanic_fechacongelamiento")["sanic_fechacongelamiento"]
R.v("H-32", "tras el rechazo no queda congelamiento ni bitacora (la transaccion se revirtio entera)", fz is None, "sin congelar", fz)
tr1 = resumen_trazas(trazas(dv, t1, esperar=90, estable=3))
volcar("30_trazas_h32.json", tr1)
print("--- trazas H-32 ---"); mostrar(tr1)

# ---- H-4 sin perfil
t2 = reloj(dv)
est, b, seg = llamar(u, API, {"arregloid": a_gen})
R.v("H-4", "SIN perfil de columna: generarformato del Oficial sobre un Borrador legible rechaza con CARP-GEN-012 (la cuenta falta en su lectura y SYSTEM la ve)", est >= 400 and codigo(b) == "CARP-GEN-012", "4xx CARP-GEN-012", (est, codigo(b), err(b)[:240], seg))
fz = leer(dv, "arreglo", a_gen, "sanic_fechacongelamiento")["sanic_fechacongelamiento"]
R.v("H-4", "el rechazo GEN-012 no congela el arreglo", fz is None, "sin congelar", fz)

# ---- H-13 linea base (sin perfil) y H-16 (c)
est, b, _ = u.call("GET", f"{AR}({a_gen})?$select=sanic_cuentadebitar,sanic_instantaneacedula,sanic_comentarios")
sin = {k: b.get(k) for k in ("sanic_cuentadebitar", "sanic_instantaneacedula")} if est == 200 else err(b)
R.v("H-13", "linea base SIN perfil: lectura directa de las dos columnas protegidas viene nula", est == 200 and all(v is None for v in sin.values()), "ambas null", sin)
t3 = reloj(dv)
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-NUEVA-T2", "sanic_numeroarreglo": 5})
r1 = (est, codigo(b), err(b)[:300])
R.v("H-16", "(c) SIN perfil, Update con la columna protegida Y una solo-sistema (CARP-ARR-009 de S-21): que falla primero", True, "medicion", r1)
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-NUEVA-T2"})
r2 = (est, codigo(b), err(b)[:300])
R.v("H-16", "(c) SIN perfil, Update solo de la columna protegida", True, "medicion", r2)
# pausa para que lleguen las trazas de S-21
tr3 = resumen_trazas(trazas(dv, t3, esperar=90, estable=3))
volcar("30_trazas_h16c.json", tr3)
print("--- trazas H-16 (c) ---"); mostrar(tr3)
s21 = [x for x in tr3 if x["tipo"] == "ArregloActualizarStep"]
R.v("H-16", "(c) S-21 se ejecuto en el Update con columna protegida + solo-sistema? (si hay traza 'rechazo CARP-ARR-009' el step corrio antes que el chequeo de columna)",
    True, "medicion", {"trazas_S21": len(s21), "lineas": [x["lineas"] for x in s21]})
# durations of S-21 for plain updates (baseline para comparar con y sin perfil)
durs = []
t5 = reloj(dv)
for i in range(6):
    est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_comentarios": f"HUMO-T2-sinperfil-{i}"})
    durs.append(est)
tr5 = resumen_trazas(trazas(dv, t5, esperar=90, estable=3))
volcar("30_trazas_comentarios_sin_perfil.json", tr5)
d21 = [x["dur_ms"] for x in tr5 if x["tipo"] == "ArregloActualizarStep"]
R.v("H-13", "SIN perfil: 6 PATCH de comentarios pasan; duracion de S-21 (ms) como linea base", all(e == 204 for e in durs), "204 x6", {"http": durs, "s21_ms": d21})
