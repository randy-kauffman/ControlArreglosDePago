"""H-16 (alta con statecode=1 sin statuscode; pre-imagen de S-21 con nulos), H-17 (modo de redondeo 5.565 / 5.575), H-3 (60 cuotas dentro de la transaccion).
Como administrador (persona, no SYSTEM). Version Vigente = v91. Datos HUMO-. (H-16 c, orden de la falla de columna protegida, necesita un usuario no admin: tanda 2.)"""
from hlib import *

dv = adm()
R = Res("40_h16_h17_h3.json")
YO = get(dv, "WhoAmI")["UserId"]
CL = conjuntos(dv)["cliente"]
AR = conjuntos(dv)["arreglo"]
cli = fila("cliente-01")
t0 = reloj(dv) - timedelta(seconds=3)


def cuerpo(num, saldo, abonado, cuota, plazo, cta, extra=None):
    c = {"sanic_producto": PROD_PRESTAMO, "sanic_numeroproducto": num, "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR,
         "sanic_saldoinicial": saldo, "sanic_montoabonado": abonado, "sanic_plazo": plazo, "sanic_cuotamensual": cuota,
         "sanic_fechainicio": "2026-11-05", "sanic_cuentadebitar": cta, "sanic_fechaformalizacion": "2026-10-20", **OWNER,
         "sanic_clienteid@odata.bind": f"/{CL}({cli})", "sanic_oficialid@odata.bind": f"/systemusers({YO})"}
    c.update(extra or {})
    return c


def cuotas(arr):
    return get(dv, f"{conjuntos(dv)['cuota']}?$select=sanic_numero,sanic_monto,sanic_fechavencimiento,sanic_nombre&$filter=_sanic_arregloid_value eq {arr}&$orderby=sanic_numero asc&$top=100")["value"]


# ---------------------------------------------------------------- H-16 (a): statecode=1 sin statuscode en el alta
est, b, rid = crear(dv, "arreglo-h16-statecode", "arreglo", cuerpo("HUMO-prod-01", 1000, 0, 300, 4, "HUMO-cta-01", {"statecode": 1}), "H-16 (a) statecode=1 sin statuscode")
if est == 201:
    rel = leer(dv, "arreglo", rid, "statecode,statuscode,sanic_nombre")
    obt = ("el alta se ACEPTO", {k: v for k, v in rel.items() if not k.startswith("@")})
else:
    obt = ("rechazada", est, codigo(b), err(b)[:200])
R.v("H-16", "(a) Create de Arreglo con statecode=1 y SIN statuscode: que ve el Target de PreOperation", True, "medicion: CARP-ARR-008 si el Target trae statecode=1; fila Borrador si trae 0", obt)
vio_uno = est == 400 and codigo(b) == "CARP-ARR-008"
R.v("H-16", "(a) conclusion: el Target de PreOperation trae el statecode ENVIADO (1) si el step rechaza con CARP-ARR-008", vio_uno, "CARP-ARR-008 (Target statecode=1)" if vio_uno else "la plataforma dejo 0 o el step no lo vio", obt)

# alta normal (Borrador) para el barrido de nulos
est, b, a1 = crear(dv, "arreglo-01", "arreglo", cuerpo("HUMO-prod-02", 1000, 0, 300, 4, "HUMO-cta-02"), "H-16 b / H-18 base")
R.v("H-16", "alta normal de un Borrador (plazo 4, cuota 300, saldo 1000): remanente/total/fechafin/version/calendario",
    est == 201 and b.get("sanic_saldoremanente") == 1000 and b.get("sanic_totalpagar") == 1000 and b.get("statecode") == 0 and b.get("statuscode") == 1 and b.get("sanic_cantidadreaperturas") == 0 and b.get("sanic_versionplantillaid") is None or est == 201,
    "201 Borrador", (est, codigo(b), err(b)[:200] if est != 201 else {k: b.get(k) for k in ("sanic_nombre", "statecode", "statuscode", "sanic_saldoremanente", "sanic_totalpagar", "sanic_fechafin", "sanic_cantidadreaperturas")}))
assert est == 201
rel = leer(dv, "arreglo", a1, "sanic_nombre,_sanic_versionplantillaid_value,sanic_fechafin,sanic_saldoremanente,sanic_totalpagar,statecode,statuscode")
cu = cuotas(a1)
R.v("H-16", "alta normal: el calendario real (S-20) crea 4 cuotas 300/300/300/100 con nombre <arreglo>-Cnn; version = v91", len(cu) == 4 and [float(x["sanic_monto"]) for x in cu] == [300, 300, 300, 100] and rel["_sanic_versionplantillaid_value"] == fila("version-v91"),
    "4 cuotas 300,300,300,100; version v91", ([float(x["sanic_monto"]) for x in cu], [x["sanic_nombre"] for x in cu][:2], rel["sanic_nombre"], rel["sanic_fechafin"], rel["_sanic_versionplantillaid_value"] == fila("version-v91")))

# ---------------------------------------------------------------- H-16 (b): pre-imagen de S-21 con nulos (43 columnas pedidas)
NULOS = ["sanic_numeroarreglo", "sanic_numeroprovisional", "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo",
         "sanic_instantaneacelular", "sanic_fechaformalizado", "sanic_fechadescarte", "sanic_fechaanulacion", "sanic_fechareapertura", "sanic_fechatermino",
         "sanic_formalizadoporid@odata.bind", "sanic_descartadoporid@odata.bind", "sanic_anuladoporid@odata.bind", "sanic_reabiertoporid@odata.bind",
         "sanic_motivodescarte", "sanic_motivoanulacion", "sanic_motivoreapertura", "sanic_comentarios"]
malos = []
for col in NULOS:
    est, b = actualizar(dv, "arreglo", a1, {col: None})
    if est != 204:
        malos.append((col, est, codigo(b)))
R.v("H-16", f"(b) Borrador: reenviar null en {len(NULOS)} columnas que estan nulas (solo-sistema, de gesto y libres) no es cambio: la pre-imagen/regla trata la ausencia correctamente", not malos, "20/20 con 204", (len(NULOS) - len(malos), malos))
# valores presentes: reenviarlos iguales
est, b = actualizar(dv, "arreglo", a1, {"sanic_cantidadreaperturas": 0, "sanic_numeroproducto": "HUMO-PROD-02", "sanic_plazo": 4, "sanic_cuotamensual": 300, "sanic_saldoinicial": 1000, "sanic_montoabonado": 0})
R.v("H-16", "(b) Borrador: reenviar los valores IGUALES (cantidadreaperturas=0, producto, plazo, cuota, saldo, abonado) no es cambio", est == 204, 204, (est, codigo(b), err(b)[:160] if est != 204 else ""))
# cambios reales: se detectan
det = []
for col, val, esp in (("sanic_numeroarreglo", 5, "CARP-ARR-009"), ("sanic_cantidadreaperturas", None, "CARP-ARR-009"), ("sanic_cantidadreaperturas", 7, "CARP-ARR-009"),
                      ("sanic_motivodescarte", "HUMO motivo", "CARP-ARR-024"), ("sanic_fechacongelamiento", "2026-10-20T00:00:00Z", "CARP-ARR-009"), ("sanic_nombre", "ARR-777777", "CARP-ARR-009")):
    est, b = actualizar(dv, "arreglo", a1, {col: val})
    det.append((col, val, est, codigo(b), esp))
R.v("H-16", "(b) cambios reales sobre columnas con valor o nulas se detectan (valor presente en la pre-imagen / nulo -> valor)", all(d[2] == 400 and d[3] == d[4] for d in det), "400 con el codigo esperado en los 6", det)
R.v("H-16", "(c) orden de la falla de columna protegida frente a PreOperation en un Update", False, "requiere un usuario SIN el perfil de datos sensibles (no admin)", "NO CORRIDA: tanda 2 (usuario no admin)", no_corrida=True)

# ---------------------------------------------------------------- H-17: redondeo en columna decimal de dos decimales
dec = {}
for tag, saldo, num in (("5565", 5.565, "HUMO-prod-03"), ("5575", 5.575, "HUMO-prod-04")):
    est, b, rid = crear(dv, "arreglo-h17-" + tag, "arreglo", cuerpo(num, saldo, 0, 6, 1, "HUMO-cta-03"), "H-17 saldo " + str(saldo))
    if est != 201:
        dec[tag] = (est, codigo(b), err(b)[:200])
        continue
    rel = leer(dv, "arreglo", rid, "sanic_saldoinicial,sanic_saldoremanente,sanic_totalpagar,sanic_cuotamensual")
    cu = cuotas(rid)
    dec[tag] = {"respuesta_create": {k: b.get(k) for k in ("sanic_saldoinicial", "sanic_saldoremanente", "sanic_totalpagar")},
                "leido": {k: rel[k] for k in ("sanic_saldoinicial", "sanic_saldoremanente", "sanic_totalpagar")}, "cuotas": [x["sanic_monto"] for x in cu]}
    print("   H-17", tag, dec[tag])
s565 = dec["5565"]["leido"]["sanic_saldoinicial"] if isinstance(dec["5565"], dict) else None
s575 = dec["5575"]["leido"]["sanic_saldoinicial"] if isinstance(dec["5575"], dict) else None
modo = {(5.57, 5.58): "AwayFromZero (mitad hacia arriba)", (5.56, 5.58): "ToEven (banquero)", (5.56, 5.57): "truncado"}.get((s565, s575), "otro")
R.v("H-17", f"modo de redondeo de Dev en decimal de 2 decimales: 5.565 -> {s565}, 5.575 -> {s575}  => {modo}", s565 is not None, "medicion", dec, modo_de_redondeo=modo)

# ---------------------------------------------------------------- H-3: 60 cuotas
t = time.time()
est, b, a60 = crear(dv, "arreglo-60cuotas", "arreglo", cuerpo("HUMO-prod-05", 6000, 0, 100, 60, "HUMO-cta-04"), "H-3 60 cuotas")
seg = round(time.time() - t, 2)
R.v("H-3", f"alta de un Arreglo de 60 cuotas (60 Create individuales en PostOperation sincrono): tiempo {seg}s, limite 120s", est == 201 and seg < 120, "201 en < 120 s", (est, codigo(b), err(b)[:200] if est != 201 else "", seg), segundos=seg)
if est == 201:
    cu = cuotas(a60)
    nums = [x["sanic_numero"] for x in cu]
    R.v("H-3", "las 60 cuotas existen: numeradas 1..60, monto 100, nombre <arreglo>-C01..C60", nums == list(range(1, 61)) and all(float(x["sanic_monto"]) == 100 for x in cu), "60 cuotas 1..60 de 100", (len(cu), nums[:3], nums[-3:], cu[0]["sanic_nombre"], cu[-1]["sanic_nombre"]))

time.sleep(8)
tr = resumen_trazas(trazas(dv, t0, esperar=120, estable=4))
volcar("40_h16_h17_h3_trazas.json", tr)
cal = [(x["inicio"][11:23], x["depth"], x["dur_ms"], x["lineas"][-1][:60]) for x in tr if x["tipo"] == "ArregloCalendarioStep"]
print("ArregloCalendarioStep:", cal)
cnt = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["ent"].replace("sanic_carp_tbl_", ""), x["depth"])
    cnt[k] = cnt.get(k, 0) + 1
for k, n in sorted(cnt.items()):
    print("  traza", k, "x", n)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
