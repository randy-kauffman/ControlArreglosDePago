"""H-10 / H-21 (a,b,c,e) / H-12 (a, c): rechazos de Publicar (trazas sin datos), pre-imagen con nulos, publicar v90, probar reemplazo y DELETE del archivo sobre la Vigente,
publicar v91 (v90 pasa a Historica). H-21 (d) NO se corre (borraria un Borrador). Queda v91 Vigente."""
from hmap import *

dv = adm()
R = Res("30_h10_h21_h12.json")
YO = get(dv, "WhoAmI")["UserId"]
VC = conjuntos(dv)["version"]
MC = conjuntos(dv)["mapeo"]
t0 = reloj(dv) - timedelta(seconds=3)
v90, v91, v92 = fila("version-v90"), fila("version-v91"), fila("version-sonda-v92")
VIG = "(sanic_estado eq 159460002)"


def publicar(ver):
    t = time.time()
    est, b = actualizar(dv, "version", ver, {"sanic_estado": VER_VIGENTE})
    return est, b, round(time.time() - t, 2)


def vigentes():
    return [x["sanic_nombre"] for x in get(dv, f"{VC}?$select=sanic_nombre&$filter=sanic_estado eq {VER_VIGENTE} and statecode eq 0")["value"]]


R.v("H-10", "linea base: ninguna Vigente antes de publicar", vigentes() == [], [], vigentes())

# ---------------------------------------------------------------- rechazos de Publicar (H-21 e: sin datos en las trazas)
t_rech = reloj(dv)
est, b, seg = publicar(v92)
R.v("H-10", "Publicar un Borrador sin archivo: CARP-VER-005", est == 400 and codigo(b) == "CARP-VER-005", "400 VER-005", (est, codigo(b), seg))
est, _, _ = dv.subir_archivo(f"{VC}({v92})/sanic_archivo", "HUMO-v92.xlsx", datos_plantilla())
est, b, seg = publicar(v92)
R.v("H-10", "Publicar con mapeo invalido (una sola fila suelta en una hoja inexistente): CARP-VER-007", est == 400 and codigo(b) == "CARP-VER-007", "400 VER-007", (est, codigo(b), err(b)[:260], seg))
v94 = alta_version(dv, "v94", hojas={"FORMATO": "FORMATO", "CONTROL": "CONTROL"})   # mapeo completo pero con las hojas sin el espacio inicial
est, b, seg = publicar(v94)
R.v("H-10", "Publicar con el mapeo completo pero la hoja 'FORMATO' sin el espacio inicial (PA-VER-08): CARP-VER-009 que lista las hojas reales", est == 400 and codigo(b) == "CARP-VER-009", "400 VER-009", (est, codigo(b), err(b)[:300], seg))
R.v("H-10", "los rechazos no dejan Vigente ni cambian el estado", vigentes() == [], [], vigentes())

# ---------------------------------------------------------------- H-21 (b): pre-imagen con nulos en Version y Mapeo (reenviar nulos no debe ser cambio)
for col in ("sanic_huella", "sanic_fechaaprobacion", "sanic_aprobadoporid@odata.bind"):
    est, b = actualizar(dv, "version", v92, {col: None})
    R.v("H-21", f"(b) Borrador: reenviar {col.split('@')[0]}=null (columna nula) no es cambio", est == 204, 204, (est, codigo(b), err(b)[:160] if est != 204 else ""))
est, b = actualizar(dv, "version", v92, {"sanic_nombre": "v92", "sanic_capacidad": 60})
R.v("H-21", "(b) Borrador: reenviar nombre y capacidad iguales no es cambio", est == 204, 204, (est, codigo(b)))
sonda = fila("mapeo-sonda-a")
for col, val in (("sanic_filasmaximas", None), ("sanic_hoja", "HUMO-hoja"), ("sanic_celda", "Z99"), ("sanic_tipo", SIMPLE)):
    est, b = actualizar(dv, "mapeo", sonda, {col: val})
    R.v("H-21", f"(b) Mapeo Borrador: reenviar {col}={val!r} (nulo o igual) no es cambio (S-29)", est == 204, 204, (est, codigo(b), err(b)[:160] if est != 204 else ""))
est, b = actualizar(dv, "mapeo", sonda, {"sanic_nombre": "otro"})
R.v("H-21", "(b) Mapeo: cambiar sanic_nombre a mano: CARP-MAP-008", est == 400 and codigo(b) == "CARP-MAP-008", "400 MAP-008", (est, codigo(b)))

# ---------------------------------------------------------------- H-10: publicar v90
t_v90 = reloj(dv)
est, b, seg = publicar(v90)
rel = leer(dv, "version", v90, "sanic_estado,statecode,sanic_huella,sanic_fechaaprobacion,_sanic_aprobadoporid_value,sanic_archivo_name")
ok = est == 204 and rel["sanic_estado"] == VER_VIGENTE and rel["sanic_huella"] == sha256_plantilla() and rel["_sanic_aprobadoporid_value"] == YO and rel["sanic_fechaaprobacion"] is not None
R.v("H-10", "Publicar v90 (primera, sin Vigente previa): estado Vigente, huella = SHA-256 del archivo, aprobador = actor, fecha; tiempo", ok, "204, Vigente, huella correcta, aprobador=yo",
    (est, codigo(b), err(b)[:200] if est != 204 else "", {k: v for k, v in rel.items() if not k.startswith("@")}, "huella==sha256 local" if rel["sanic_huella"] == sha256_plantilla() else "huella distinta", seg), segundos=seg)
R.v("H-10", "queda exactamente una Vigente (v90)", vigentes() == ["v90"], ["v90"], vigentes())

# ---------------------------------------------------------------- H-12 (a): reemplazo con PATCH directo sobre la Vigente
antes = leer(dv, "version", v90, "sanic_archivo_name")
est, b, _ = dv.subir_archivo(f"{VC}({v90})/sanic_archivo", "HUMO-v90-reemplazo.xlsx", datos_plantilla())
despues = leer(dv, "version", v90, "sanic_archivo_name,sanic_archivo")
R.v("H-12", "(a) reemplazo del archivo por PATCH directo sobre una Vigente: rechazo CARP-VER-003 y el archivo queda como estaba (la 1a pasada, que deja null, se revierte)",
    est >= 400 and codigo(b) == "CARP-VER-003" and despues["sanic_archivo_name"] == antes["sanic_archivo_name"], "4xx VER-003 y archivo igual", (est, codigo(b), err(b)[:160], antes["sanic_archivo_name"], despues["sanic_archivo_name"], bool(despues.get("sanic_archivo"))))
est, b = actualizar(dv, "version", v90, {"sanic_capacidad": 59})
R.v("H-12", "Vigente: editar la capacidad: CARP-VER-003", est == 400 and codigo(b) == "CARP-VER-003", "400 VER-003", (est, codigo(b)))

# ---------------------------------------------------------------- H-12 (c): DELETE de la columna de archivo de la Vigente recien publicada
est, b, _ = dv.call("DELETE", f"{VC}({v90})/sanic_archivo")
despues = leer(dv, "version", v90, "sanic_archivo_name,sanic_archivo")
conserva = despues.get("sanic_archivo_name") == antes["sanic_archivo_name"] and bool(despues.get("sanic_archivo"))
R.v("H-12", "(c) DELETE .../sanic_archivo sobre la Vigente v90: S-27 lo ve? (rechazo = el archivo se conserva)", est >= 400 and conserva, "rechazo y archivo conservado",
    (est, codigo(b), err(b)[:200], "archivo CONSERVADO" if conserva else "archivo BORRADO", despues.get("sanic_archivo_name")), borrado=not conserva)

# ---------------------------------------------------------------- H-10 / H-21: publicar v91 (v90 -> Historica, SYSTEM anidado)
t_v91 = reloj(dv)
est, b, seg = publicar(v91)
r90 = leer(dv, "version", v90, "sanic_estado,statecode")
r91 = leer(dv, "version", v91, "sanic_estado,statecode,sanic_huella,_sanic_aprobadoporid_value")
ok = est == 204 and r90["sanic_estado"] == VER_HISTORICA and r90["statecode"] == 0 and r91["sanic_estado"] == VER_VIGENTE and r91["sanic_huella"] == sha256_plantilla()
R.v("H-10", "Publicar v91 con v90 Vigente: v90 pasa a Historica (escritura de SYSTEM anidada, S-26 profundidad 2 la deja pasar por DC-03 = H-21 a), v91 Vigente con huella; tiempo", ok,
    "204, v90 Historica, v91 Vigente", (est, codigo(b), err(b)[:200] if est != 204 else "", r90["sanic_estado"], r91["sanic_estado"], seg), segundos=seg)
R.v("H-10", "queda exactamente una Vigente (v91)", vigentes() == ["v91"], ["v91"], vigentes())
est, b = actualizar(dv, "version", v90, {"sanic_capacidad": 58})
R.v("H-10", "Historica: editar: CARP-VER-003", est == 400 and codigo(b) == "CARP-VER-003", "400 VER-003", (est, codigo(b)))

# ---------------------------------------------------------------- trazas
time.sleep(10)
tr = resumen_trazas(trazas(dv, t0, esperar=120, estable=4))
volcar("30_h10_h21_trazas.json", tr)
cuenta = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["ent"].replace("sanic_carp_tbl_", ""), x["depth"])
    cuenta[k] = cuenta.get(k, 0) + 1
for k, n in sorted(cuenta.items()):
    print("  traza", k, "x", n)
# H-21 (e): ninguna traza lleva nombres de hoja
sens = ["FORMATO", "CONTROL DE PAGOS", "HUMO-hoja", "ARP ELECTRONICO"]
hallazgos = [(x["tipo"], x["msg"], l[:200]) for x in tr for l in x["lineas"] if any(s in l for s in sens)]
exc = [(x["tipo"], x["exc"][:200]) for x in tr if x["exc"] and any(s in x["exc"] for s in sens)]
R.v("H-21", "(e) ITracingService no recibe el nombre de la hoja ni datos del archivo en ningun rechazo de Publicar", not hallazgos and not exc, "0 trazas con nombres de hoja", (len(hallazgos), hallazgos[:3], exc[:2]))
R.v("H-21", "(a) traza del Update anidado de v90 a Historica: VersionPlantillaActualizarStep a profundidad 2 y salida correcta (identidad SYSTEM)",
    any(x["tipo"] == "VersionPlantillaActualizarStep" and x["depth"] == 2 and any("salida correcta" in l for l in x["lineas"]) for x in tr), "depth 2 + salida correcta",
    [(x["depth"], x["lineas"][-1][:60]) for x in tr if x["tipo"] == "VersionPlantillaActualizarStep" and x["depth"] == 2])
dur = [(x["tipo"], x["msg"], x["depth"], x["dur_ms"]) for x in tr if x["tipo"] in ("VersionPlantillaActualizarStep",) and x["dur_ms"] and x["dur_ms"] > 150]
print("pasos lentos de VersionPlantillaActualizarStep:", dur)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
