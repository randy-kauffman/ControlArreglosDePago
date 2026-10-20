"""H-14 (autonumerico de Conversacion, S-36) y H-15 (pre-imagen de S-17 en Conversacion). Contra el paquete REAL, como administrador (persona, no SYSTEM).
Datos ficticios HUMO-. Cada fila creada se registra en estado.json."""
from hlib import *

dv = adm()
R = Res("10_h14_h15.json")
t0 = reloj(dv) - timedelta(seconds=3)
YO = get(dv, "WhoAmI")["UserId"]
CV = conjuntos(dv)["conversacion"]
CL = conjuntos(dv)["cliente"]
ISO = lambda s: s  # fechas ya en ISO

# ---------------------------------------------------------------- cliente HUMO
est, b, cli = crear(dv, "cliente-01", "cliente", {"sanic_nombrecompleto": "HUMO Cliente 01", "sanic_cedula": "HUMO-ced-01", **OWNER})
R.v("H-14", "alta del cliente HUMO (ClienteStep)", est == 201, 201, (est, err(b) if est != 201 else {"nombre": b.get("sanic_nombre"), "cedula": b.get("sanic_cedula")}))
assert est == 201

BASE = {"sanic_fechaprimercorreo": "2026-10-20T10:00:00Z", "sanic_correonuevoenfinalizada": False, "sanic_cantidadrespuestas": 0, **OWNER}


def conv(clave, huella, extra=None, nota=""):
    cuerpo = dict(BASE, sanic_huellamessageid=huella)
    cuerpo.update(extra or {})
    return crear(dv, clave, "conversacion", cuerpo, nota)


# ---------------------------------------------------------------- H-14
est, b, c1 = conv("conv-01", "HUMO-h-001", nota="H-14 (a) sin sanic_nombre")
nom1 = (b or {}).get("sanic_nombre") if est == 201 else None
import re
R.v("H-14", "(a) alta sin sanic_nombre: la fila guarda CONV-nnnnnn y el step S-36 la deja pasar", est == 201 and bool(re.fullmatch(r"CONV-\d{6,15}", nom1 or "")), "201 y CONV-nnnnnn", (est, nom1, err(b) if est != 201 else ""))

est, b, _ = conv("conv-x-null", "HUMO-h-002", {"sanic_nombre": None}, "H-14 nombre null")
nom2 = (b or {}).get("sanic_nombre") if est == 201 else None
R.v("H-14", "(a bis) alta con sanic_nombre=null: pasa y la plataforma completa el autonumerico", est == 201 and bool(re.fullmatch(r"CONV-\d{6,15}", nom2 or "")), "201 y CONV-nnnnnn", (est, nom2, err(b) if est != 201 else ""))

est, b, _ = conv("conv-x-vacio", "HUMO-h-003", {"sanic_nombre": ""}, "H-14 nombre vacio")
nom3 = (b or {}).get("sanic_nombre") if est == 201 else None
R.v("H-14", "(a ter) alta con sanic_nombre='': pasa", est == 201, 201, (est, nom3, err(b) if est != 201 else ""))

for val in ("ZZV-NOMBRE", "conv-000001", "CONV-12345", "CONV-000001 "):
    est, b, rid = conv("conv-x-rechazada", "HUMO-h-r-" + re.sub(r"\W", "", val)[:8], {"sanic_nombre": val}, "H-14 debe rechazarse")
    R.v("H-14", f"(b,d) alta con sanic_nombre={val!r}: S-36 rechaza con CARP-CNV-001 y no queda fila", est == 400 and codigo(b) == "CARP-CNV-001" and rid is None, "400 CARP-CNV-001", (est, codigo(b)))

est, b, c777 = conv("conv-777777", "HUMO-h-777", {"sanic_nombre": "CONV-777777"}, "H-14 (b) valor con formato valido")
R.v("H-14", "(b) alta con sanic_nombre='CONV-777777': se guarda tal cual", est == 201 and b.get("sanic_nombre") == "CONV-777777", "201 y CONV-777777", (est, (b or {}).get("sanic_nombre"), err(b) if est != 201 else ""))

# (c) el Update nunca agrega sanic_nombre al Target: un Update libre (clienteid) de persona sobre una Abierta pasa y el nombre no cambia
nav_cli = "sanic_clienteid@odata.bind"
est, b = actualizar(dv, "conversacion", c1, {nav_cli: f"/{CL}({cli})"})
rel = leer(dv, "conversacion", c1, "sanic_nombre,_sanic_clienteid_value")
R.v("H-14", "(c) Update de persona sobre sanic_clienteid: pasa (si la plataforma agregara un sanic_nombre distinto, daria CARP-CNV-001) y el nombre no cambia", est == 204 and rel["sanic_nombre"] == nom1, "204 y nombre igual", (est, codigo(b), rel["sanic_nombre"] == nom1))
est, b = actualizar(dv, "conversacion", c1, {"sanic_nombre": "CONV-000001"})
R.v("H-14", "(c bis) Update de persona que cambia sanic_nombre: CARP-CNV-001", est == 400 and codigo(b) == "CARP-CNV-001", "400 CARP-CNV-001", (est, codigo(b)))

# ---------------------------------------------------------------- H-15
est, b, c0 = conv("conv-aux-unida", "HUMO-h-010", nota="H-15: destino del lookup sanic_unidaaid")
usr = f"/systemusers({YO})"
POBLADA = {
    "sanic_asunto": "HUMO asunto", "sanic_remitente": "humo@example.invalid", "sanic_sinrespuestadesde": "2026-10-20T11:00:00Z",
    "sanic_correonuevoenfinalizada": True, "sanic_cantidadrespuestas": 3, "sanic_tiempoprimerarespuesta": 5, "sanic_tiempototal": 10,
    "sanic_fechafinalizacion": "2026-10-20T12:00:00Z", "sanic_resultado": 159460003, "sanic_motivosinrespuesta": 159460001, "sanic_detallesinrespuesta": "HUMO detalle",
    "sanic_unidaaid@odata.bind": f"/{CV}({c0})", "sanic_finalizadoporid@odata.bind": usr, "sanic_clienteid@odata.bind": f"/{CL}({cli})",
    "sanic_oficialasignadoid@odata.bind": usr,
}
est, b, c4 = conv("conv-04-poblada", "HUMO-h-004", POBLADA, "H-15: Abierta con las 21 columnas vigiladas pobladas")
R.v("H-15", "alta de una Abierta con todas las columnas vigiladas pobladas", est == 201, 201, (est, err(b) if est != 201 else "ok"))
assert est == 201

SOLO_SIS = {  # columna -> valor distinto del guardado
    "sanic_huellamessageid": None, "sanic_fechaprimercorreo": None, "sanic_asunto": None, "sanic_remitente": None, "sanic_sinrespuestadesde": None,
    "sanic_correonuevoenfinalizada": False, "sanic_fechafinalizacion": None, "sanic_tiempoprimerarespuesta": None, "sanic_tiempototal": None,
    "sanic_cantidadrespuestas": None, "sanic_unidaaid@odata.bind": None, "sanic_finalizadoporid@odata.bind": None, "sanic_nombre": None,
}
ok_all = 0
for col, val in SOLO_SIS.items():
    est, b = actualizar(dv, "conversacion", c4, {col: val})
    base = col.split("@")[0]
    c = codigo(b)
    txt = err(b)
    ok = est == 400 and c == "CARP-CNV-001" and base in txt
    ok_all += ok
    R.v("H-15", f"(a) Abierta con valor: PATCH {base}={val!r} se detecta como cambio", ok, "400 CARP-CNV-001 nombrando la columna", (est, c, base in txt, txt[:120] if not ok else ""))
for col in ("sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta"):
    est, b = actualizar(dv, "conversacion", c4, {col: None})
    R.v("H-15", f"(a) Abierta con valor: PATCH {col}=null se detecta (gesto, CARP-CNV-005)", est == 400 and codigo(b) == "CARP-CNV-005", "400 CARP-CNV-005", (est, codigo(b)))
# nulos: reenviar null sobre columna nula no es cambio (c1 no tiene asunto)
est, b = actualizar(dv, "conversacion", c1, {"sanic_asunto": None, "sanic_remitente": None, "sanic_unidaaid@odata.bind": None, "sanic_tiempototal": None})
R.v("H-15", "(a) columnas NULAS reenviadas como null: no son cambio (ausente = nulo)", est == 204, 204, (est, codigo(b), err(b)[:150] if est != 204 else ""))
est, b = actualizar(dv, "conversacion", c4, {"sanic_asunto": "HUMO asunto", "sanic_cantidadrespuestas": 3, "sanic_correonuevoenfinalizada": True})
R.v("H-15", "(a) mismo valor reenviado sobre columnas con valor: no es cambio", est == 204, 204, (est, codigo(b)))

# Finalizar (statecode, statuscode y pre-imagen de estado)
est, b = actualizar(dv, "conversacion", c4, {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460003, "sanic_motivosinrespuesta": 159460001, "sanic_detallesinrespuesta": "HUMO detalle"})
rel = leer(dv, "conversacion", c4, "statecode,statuscode,_sanic_finalizadoporid_value,sanic_fechafinalizacion,sanic_tiempototal,sanic_sinrespuestadesde")
R.v("H-15", "Finalizar (statecode=1/statuscode=2): efectos del step (finalizadopor=actor, tiempototal, sinrespuestadesde=null)",
    est == 204 and rel["statecode"] == 1 and rel["statuscode"] == 2 and rel["_sanic_finalizadoporid_value"] == YO and rel["sanic_sinrespuestadesde"] is None,
    "204, Finalizada, finalizadopor=yo, sinrespuestadesde=null", (est, codigo(b), {k: v for k, v in rel.items() if not k.startswith("@")}))

# Finalizada: la pre-imagen debe traer las 21 columnas con valor
fin_ok = 0
for col, val in list(SOLO_SIS.items()) + [("sanic_resultado", None), ("sanic_motivosinrespuesta", None), ("sanic_detallesinrespuesta", None)]:
    est, b = actualizar(dv, "conversacion", c4, {col: val})
    c = codigo(b)
    ok = est == 400 and c == "CARP-CNV-009"
    fin_ok += ok
    R.v("H-15", f"(b) Finalizada: PATCH {col.split('@')[0]}={val!r} se detecta (CARP-CNV-009)", ok, "400 CARP-CNV-009", (est, c))
for col, val, nombre in (("sanic_clienteid@odata.bind", None, "clienteid=null"), ("sanic_oficialasignadoid@odata.bind", None, "oficialasignadoid=null"), ("sanic_nombre", "ZZV-X", "nombre cambiado")):
    est, b = actualizar(dv, "conversacion", c4, {col: val})
    R.v("H-15", f"(b) Finalizada: {nombre} se detecta (CARP-CNV-009)", est == 400 and codigo(b) == "CARP-CNV-009", "400 CARP-CNV-009", (est, codigo(b)))
est, b = actualizar(dv, "conversacion", c4, {"sanic_clienteid@odata.bind": f"/{CL}({cli})", "sanic_oficialasignadoid@odata.bind": usr, "ownerid@odata.bind": f"/teams({TEAM_DEF})"})
R.v("H-15", "(b) Finalizada: reenviar el MISMO cliente, oficial y propietario no es cambio (pre-imagen trae sus valores)", est == 204, 204, (est, codigo(b), err(b)[:120] if est != 204 else ""))
est, b = actualizar(dv, "conversacion", c4, {"statecode": 0, "statuscode": 1})
rel = leer(dv, "conversacion", c4, "statecode,statuscode,sanic_resultado,sanic_fechafinalizacion,sanic_correonuevoenfinalizada,sanic_sinrespuestadesde")
R.v("H-15", "Reabrir (statecode=0/statuscode=1): efectos del step y estado Abierta", est == 204 and rel["statecode"] == 0 and rel["sanic_resultado"] is None and rel["sanic_correonuevoenfinalizada"] is False,
    "204, Abierta, resultado=null, indicador=false", (est, codigo(b), {k: v for k, v in rel.items() if not k.startswith("@")}))

# ---------------------------------------------------------------- trazas
time.sleep(5)
tr = resumen_trazas(trazas(dv, t0, esperar=60))
volcar("10_h14_h15_trazas.json", tr)
print("trazas del paquete:", len(tr))
mostrar([x for x in tr if x["tipo"] in ("ConversacionCrearStep",)][:6])
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
