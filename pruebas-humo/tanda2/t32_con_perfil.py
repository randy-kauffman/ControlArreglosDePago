"""CON el perfil asociado por team: H-13 (efecto observable en S-21), H-16 (c), H-4 como Oficial no admin (rol real + perfil)."""
import base64
import zipfile
from t2lib import *

dv = adm()
u = noadm()
R = Res("32_con_perfil.json")
UID = leer_estado()["linea_base"]["usuario"]["systemuserid"]
AR = conjuntos(dv)["arreglo"]
API = "sanic_carp_capi_generarformato"
a_gen = fila("arreglo-t2-gen")

# ---- H-13: lectura directa y S-21
est, b, _ = u.call("GET", f"{AR}({a_gen})?$select=sanic_cuentadebitar,sanic_instantaneacedula")
R.v("H-13", "CON perfil por team: lectura directa trae la cuenta real", est == 200 and b.get("sanic_cuentadebitar") == "HUMO-cta-T201", "HUMO-cta-T201", {k: b.get(k) for k in ("sanic_cuentadebitar", "sanic_instantaneacedula")} if est == 200 else err(b))
t5 = reloj(dv)
codigos = []
for i in range(6):
    est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_comentarios": f"HUMO-T2-conperfil-{i}"})
    codigos.append(est)
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201"})
iguales = (est, codigo(b), err(b)[:200] if est >= 300 else "")
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201-B"})
cambio = (est, codigo(b), err(b)[:200] if est >= 300 else "")
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201"})
vuelta = (est, codigo(b), err(b)[:200] if est >= 300 else "")
R.v("H-13", "CON perfil: reenviar la cuenta igual = no cambio (204); cambiarla = permitido en Borrador (el perfil da update); restaurarla", iguales[0] == 204 and cambio[0] == 204 and vuelta[0] == 204 and all(c == 204 for c in codigos), "204 en todos", {"comentarios": codigos, "igual": iguales, "cambio": cambio, "vuelta": vuelta})
tr = resumen_trazas(trazas(dv, t5, esperar=90, estable=3))
volcar("32_trazas_h13.json", tr)
d21 = [x["dur_ms"] for x in tr if x["tipo"] == "ArregloActualizarStep"]
R.v("H-13", "CON perfil: duraciones de S-21 (ms), a comparar con la linea base sin perfil (31,46,15,15,31,31,15,31)", True, "medicion", d21)

# ---- H-16 (c) con perfil
t3 = reloj(dv)
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201", "sanic_numeroarreglo": 5})
r1 = (est, codigo(b), err(b)[:300])
R.v("H-16", "(c) CON perfil, columna protegida + solo-sistema: la falla viene de S-21 (CARP-ARR-009)", est == 400 and codigo(b) == "CARP-ARR-009", "400 ARR-009", r1)
tr3 = resumen_trazas(trazas(dv, t3, esperar=90, estable=3))
volcar("32_trazas_h16c.json", tr3)
print([ (x["tipo"], x["lineas"]) for x in tr3 if x["tipo"] == "ArregloActualizarStep"])

# ---- H-4: generar el formato como Oficial no admin
t4 = reloj(dv)
est, b, seg = llamar(u, API, {"arregloid": a_gen})
if est == 200:
    datos = base64.b64decode(b["archivo"])
    with open(os.path.join(EVID, "H-4_no_admin_" + b["nombrearchivo"]), "wb") as f:
        f.write(datos)
    try:
        z = zipfile.ZipFile(os.path.join(EVID, "H-4_no_admin_" + b["nombrearchivo"]))
        zip_ok = z.testzip() is None and "xl/workbook.xml" in z.namelist()
    except Exception as e:  # noqa: BLE001
        zip_ok = str(e)
else:
    zip_ok = None
rel = leer(dv, "arreglo", a_gen, "sanic_fechacongelamiento,sanic_numeroprovisional,sanic_instantaneanombre,sanic_instantaneacedula,statecode,statuscode")
gen = get(dv, f"{conjuntos(dv)['generacion']}?$select=sanic_nombre,_sanic_generadoporid_value&$filter=_sanic_arregloid_value eq {a_gen}")["value"]
ok = est == 200 and b.get("congelado") is True and b.get("numeroprovisional") == 1 and rel["sanic_fechacongelamiento"] and len(gen) == 1 and gen[0]["_sanic_generadoporid_value"] == UID and zip_ok is True
R.v("H-4", "Oficial NO admin (rol Oficial por el team default + perfil por el team): generarformato OK, congela, provisional 1, bitacora con generadoporid = SPIKE4, xlsx valido",
    ok, "200, congelado, bitacora con SPIKE4",
    (est, codigo(b), err(b)[:200] if est != 200 else {k: (v if k != "archivo" else f"{len(v)} chars b64") for k, v in b.items() if not k.startswith("@")}, {k: v for k, v in rel.items() if not k.startswith("@")}, len(gen), "zip_ok=" + str(zip_ok), seg), segundos=seg)
R.v("H-4", "la instantanea de la cedula quedo escrita (SYSTEM) y el Oficial la lee (perfil: lectura)", rel.get("sanic_instantaneacedula") is not None, "cedula del cliente", "presente" if rel.get("sanic_instantaneacedula") else None)
est, b, _ = u.call("GET", f"{AR}({a_gen})?$select=sanic_instantaneacedula,sanic_instantaneanombre")
R.v("H-13", "CON perfil, arreglo congelado: SPIKE4 lee sanic_instantaneacedula (el efecto de la pre-imagen de S-21 sobre la columna de solo lectura)", est == 200 and b.get("sanic_instantaneacedula") is not None, "valor presente", {k: ("presente" if b.get(k) else None) for k in ("sanic_instantaneacedula", "sanic_instantaneanombre")} if est == 200 else err(b))
# reenviar la cuenta igual en un arreglo congelado: no es cambio (pre-imagen o lectura SYSTEM)
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201", "sanic_instantaneacedula": rel.get("sanic_instantaneacedula")})
R.v("H-13", "Congelado: reenviar cuenta e instantanea de cedula IGUALES no es cambio (S-21 con pre-imagen completa)", est in (204,) or (est == 403), "204 (o 403 de columna sin update en instantaneacedula)", (est, codigo(b), err(b)[:240]))
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_cuentadebitar": "HUMO-cta-T201-C"})
R.v("H-13", "Congelado: cambiar la cuenta (dato del acuerdo) se rechaza CARP-ARR-011 (S-21 ve el valor anterior)", est == 400 and codigo(b) == "CARP-ARR-011", "400 ARR-011", (est, codigo(b), err(b)[:200]))
