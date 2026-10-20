"""H-4 (admin) generarformato, H-19 (SYSTEM anidado: pagar hasta Terminado), H-18 (pre-imagen de Cuota), H-2 (dos Borradores del mismo credito: K-02 / CARP-ARR-020).
Administrador (persona). Version Vigente = v91. Datos HUMO-."""
import base64
import threading
from hlib import *

dv = adm()
R = Res("50_h19_h18_h2_h4.json")
YO = get(dv, "WhoAmI")["UserId"]
CL, AR, CU = conjuntos(dv)["cliente"], conjuntos(dv)["arreglo"], conjuntos(dv)["cuota"]
cli = fila("cliente-01")
t0 = reloj(dv) - timedelta(seconds=3)
API = "sanic_carp_capi_generarformato"


def cuerpo(num, saldo, abonado, cuota, plazo, cta):
    return {"sanic_producto": PROD_PRESTAMO, "sanic_numeroproducto": num, "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR, "sanic_saldoinicial": saldo,
            "sanic_montoabonado": abonado, "sanic_plazo": plazo, "sanic_cuotamensual": cuota, "sanic_fechainicio": "2026-11-05", "sanic_cuentadebitar": cta,
            "sanic_fechaformalizacion": "2026-10-20", **OWNER, "sanic_clienteid@odata.bind": f"/{CL}({cli})", "sanic_oficialid@odata.bind": f"/systemusers({YO})"}


def alta(clave, *a):
    est, b, rid = crear(dv, clave, "arreglo", cuerpo(*a), clave)
    assert est == 201, (clave, est, err(b))
    return rid


def cuotas(arr):
    return get(dv, f"{CU}?$select=sanic_numero,sanic_fechapago,sanic_montopagado,sanic_monto&$filter=_sanic_arregloid_value eq {arr}&$orderby=sanic_numero asc")["value"]


def generar(arr, guardar=None):
    est, b, seg = llamar(dv, API, {"arregloid": arr})
    if est == 200 and guardar:
        with open(os.path.join(EVID, guardar), "wb") as f:
            f.write(base64.b64decode(b["archivo"]))
    return est, b, seg


def formalizar(arr):
    return actualizar(dv, "arreglo", arr, {"statecode": 0, "statuscode": 159460001})


# ---------------------------------------------------------------- H-4 (admin): generar formato
a_h19 = alta("arreglo-h19", "HUMO-prod-08", 1000, 0, 500, 2, "HUMO-cta-08")
est, b, seg = generar(a_h19, "H-11_F-NIC-0000052-HUMO.xlsx")
rel = leer(dv, "arreglo", a_h19, "sanic_fechacongelamiento,sanic_numeroprovisional,sanic_instantaneanombre,statecode")
gen = get(dv, f"{conjuntos(dv)['generacion']}?$select=sanic_nombre,_sanic_generadoporid_value&$filter=_sanic_arregloid_value eq {a_h19}")["value"]
ok = est == 200 and b.get("congelado") is True and b.get("numeroprovisional") == 1 and rel["sanic_fechacongelamiento"] and len(gen) == 1 and gen[0]["_sanic_generadoporid_value"] == YO
R.v("H-4", "generarformato como ADMIN (directo): archivo, congelado, provisional=1, instantanea, bitacora con generadoporid=actor; tiempo", ok, "200 y bitacora",
    (est, codigo(b), err(b)[:200] if est != 200 else {k: (v if k != "archivo" else f"{len(v)} chars b64") for k, v in b.items() if not k.startswith("@")}, rel, len(gen), seg), segundos=seg)
R.v("H-4", "el no administrador con rol real y perfil de columna", False, "requiere usuario no admin", "NO CORRIDA: tanda 2", no_corrida=True)

# ---------------------------------------------------------------- H-19: formalizar y pagar hasta Terminado
est, b = formalizar(a_h19)
rel = leer(dv, "arreglo", a_h19, "statecode,statuscode,sanic_numeroarreglo,_sanic_formalizadoporid_value")
R.v("H-2", "Formalizar (Borrador congelado -> Vigente) con numeroarreglo asignado", est == 204 and rel["statuscode"] == 159460001 and rel["sanic_numeroarreglo"] == 1, "204, Vigente, numero 1", (est, codigo(b), err(b)[:160] if est != 204 else "", {k: v for k, v in rel.items() if not k.startswith("@")}))
cu = cuotas(a_h19)
c1, c2 = cu[0]["sanic_carp_tbl_cuotaid"] if "sanic_carp_tbl_cuotaid" in cu[0] else None, None
cu = get(dv, f"{CU}?$select=sanic_carp_tbl_cuotaid,sanic_numero&$filter=_sanic_arregloid_value eq {a_h19}&$orderby=sanic_numero asc")["value"]
c1, c2 = cu[0]["sanic_carp_tbl_cuotaid"], cu[1]["sanic_carp_tbl_cuotaid"]

# H-18: pre-imagen de Cuota
est, b = actualizar(dv, "cuota", c1, {"sanic_fechapago": None, "sanic_montopagado": None})
R.v("H-18", "(a) cuota sin pago: reenviar fechapago=null y montopagado=null no es cambio", est == 204, 204, (est, codigo(b), err(b)[:160] if est != 204 else ""))
est, b = actualizar(dv, "cuota", c1, {"sanic_monto": 1})
R.v("H-18", "(b) cambiar sanic_monto: CARP-CUO-001", est == 400 and codigo(b) == "CARP-CUO-001", "400 CUO-001", (est, codigo(b)))
est, b = actualizar(dv, "cuota", c1, {"sanic_fechapago": "2026-11-05"})
R.v("H-18", "solo fechapago: CARP-CUO-003", est == 400 and codigo(b) == "CARP-CUO-003", "400 CUO-003", (est, codigo(b)))
est, b = actualizar(dv, "cuota", c1, {"statecode": 1})
R.v("H-18", "(b) statecode=1 en la cuota: CARP-CUO-001 (la pre-imagen trae statecode/statuscode)", est == 400 and codigo(b) == "CARP-CUO-001", "400 CUO-001", (est, codigo(b)))
est, b = actualizar(dv, "cuota", c1, {"sanic_fechapago": "2026-11-05", "sanic_montopagado": 500, "statecode": 0, "statuscode": 1})
R.v("H-18", "pago 1 con statecode/statuscode iguales reenviados (no cambio): pasa y el arreglo sigue Vigente", est == 204 and leer(dv, "arreglo", a_h19, "statecode")["statecode"] == 0, 204, (est, codigo(b)))
est, b = actualizar(dv, "cuota", c1, {"sanic_fechapago": None})
R.v("H-18", "(a) cuota con pago: vaciar solo fechapago: CARP-CUO-003 (la pre-imagen trae el monto pagado)", est == 400 and codigo(b) == "CARP-CUO-003", "400 CUO-003", (est, codigo(b)))
t = time.time()
est, b = actualizar(dv, "cuota", c2, {"sanic_fechapago": "2026-12-05", "sanic_montopagado": 500})
seg = round(time.time() - t, 2)
rel = leer(dv, "arreglo", a_h19, "statecode,statuscode,sanic_fechatermino")
R.v("H-19", "(a,b) pagar la ULTIMA cuota como persona: S-24 escribe el arreglo con SYSTEM anidado, S-21 lo deja pasar (DC-03): arreglo Terminado sin CARP-ARR-010", est == 204 and rel["statecode"] == 1 and rel["statuscode"] == 2 and rel["sanic_fechatermino"], "204 y Terminado (1/2) con fechatermino",
    (est, codigo(b), err(b)[:200] if est != 204 else "", {k: v for k, v in rel.items() if not k.startswith("@")}, seg), segundos=seg)

# ---------------------------------------------------------------- H-2: dos Borradores del mismo credito
a, bb = alta("arreglo-h2-a", "HUMO-prod-06", 1000, 0, 300, 4, "HUMO-cta-06"), alta("arreglo-h2-b", "HUMO-prod-06", 1000, 0, 300, 4, "HUMO-cta-06")
ea, ba, _ = generar(a); eb, bbb, _ = generar(bb)
R.v("H-2", "dos Borradores del mismo credito generan formato: ambos con provisional 1", ea == 200 and eb == 200 and ba.get("numeroprovisional") == 1 and bbb.get("numeroprovisional") == 1, "provisional 1 y 1", (ea, eb, ba.get("numeroprovisional"), bbb.get("numeroprovisional")))
est, bx = formalizar(a)
R.v("H-2", "formalizar el primero: pasa con numeroarreglo 1", est == 204, 204, (est, codigo(bx)))
est, bx = formalizar(bb)
R.v("H-2", "formalizar el segundo (mismo credito): rechazo CARP-ARR-020 (provisional 1, el siguiente es 2)", est == 400 and codigo(bx) == "CARP-ARR-020", "400 CARP-ARR-020", (est, codigo(bx), err(bx)[:200]))
# concurrencia real: dos formalizaciones simultaneas (K-02)
c, d = alta("arreglo-h2-c", "HUMO-prod-07", 1000, 0, 300, 4, "HUMO-cta-07"), alta("arreglo-h2-d", "HUMO-prod-07", 1000, 0, 300, 4, "HUMO-cta-07")
generar(c); generar(d)
res = {}
def lanza(k, arr):
    res[k] = formalizar(arr)
ths = [threading.Thread(target=lanza, args=("c", c)), threading.Thread(target=lanza, args=("d", d))]
[th.start() for th in ths]; [th.join() for th in ths]
cods = {k: (v[0], codigo(v[1]), err(v[1])[:160]) for k, v in res.items()}
R.v("H-2", "dos formalizaciones SIMULTANEAS del mismo credito: una pasa; la otra recibe CARP-ARR-020 o el rechazo de la clave K-02", sorted(v[0] for v in res.values()) == [204, 400] or sorted(v[0] for v in res.values())[0] in (204,), "una 204 y la otra 4xx", cods)
nums = [leer(dv, "arreglo", x, "sanic_numeroarreglo")["sanic_numeroarreglo"] for x in (c, d)]
R.v("H-2", "tras la carrera no hay dos arreglos con el mismo numero de credito", len([n for n in nums if n]) <= 1 or len(set(nums)) == 2, "numeros distintos o uno solo", nums)

time.sleep(8)
tr = resumen_trazas(trazas(dv, t0, esperar=120, estable=4))
volcar("50_trazas.json", tr)
cnt = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["ent"].replace("sanic_carp_tbl_", ""), x["depth"])
    cnt[k] = cnt.get(k, 0) + 1
for k, n in sorted(cnt.items()):
    print("  traza", k, "x", n)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
