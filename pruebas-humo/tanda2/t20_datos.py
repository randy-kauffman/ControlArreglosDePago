"""Datos HUMO-T2 de la tanda 2 (admin). Todos con oficial = SPIKE4 salvo indicacion. Registra cada GUID en estado.json.
- cliente-t2-adm: Cliente propiedad del usuario admin (raiz) => SPIKE4 no lo ve (H-32).
- arreglo-t2-gen: Borrador (H-4, H-13, H-16 c).
- arreglo-t2-h32cli: Borrador legible, con el Cliente ilegible (H-32 via GenerarFormato -> clientes.Leer).
- arreglo-t2-h32arr: Borrador propiedad del admin => ilegible (H-32 via GenerarFormato -> arreglo).
- arreglo-t2-h8: Vigente congelado (H-8: reabrir). arreglo-t2-h19: Vigente de 2 cuotas (H-19 b)."""
from t2lib import *

dv = adm()
R = Res("20_datos.json")
base = leer_estado()["linea_base"]
UID = base["usuario"]["systemuserid"]
YO = get(dv, "WhoAmI")["UserId"]
CL, AR, CU = conjuntos(dv)["cliente"], conjuntos(dv)["arreglo"], conjuntos(dv)["cuota"]
cli = fila1("cliente-01")
OWNER_ADM = {"ownerid@odata.bind": f"/systemusers({YO})"}
API = "sanic_carp_capi_generarformato"


def cuerpo(num, saldo, abonado, cuota, plazo, cta, cliente, owner=None, oficial=UID):
    return {"sanic_producto": PROD_PRESTAMO, "sanic_numeroproducto": num, "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR, "sanic_saldoinicial": saldo,
            "sanic_montoabonado": abonado, "sanic_plazo": plazo, "sanic_cuotamensual": cuota, "sanic_fechainicio": "2026-11-05", "sanic_cuentadebitar": cta,
            "sanic_fechaformalizacion": "2026-10-20", **(owner or OWNER), "sanic_clienteid@odata.bind": f"/{CL}({cliente})", "sanic_oficialid@odata.bind": f"/systemusers({oficial})"}


def alta(clave, *a, **k):
    est, b, rid = crear(dv, clave, "arreglo", cuerpo(*a, **k), clave)
    R.v("datos", clave, est == 201, 201, (est, codigo(b), err(b)[:200] if est != 201 else rid))
    return rid


est, b, cli_adm = crear(dv, "cliente-t2-adm", "cliente", {"sanic_nombrecompleto": "HUMO Cliente T2 Admin", "sanic_cedula": "HUMO-ced-t2-adm", **OWNER_ADM}, "H-32 cliente ilegible")
R.v("datos", "cliente-t2-adm (propiedad del admin)", est == 201, 201, (est, codigo(b), err(b)[:200] if est != 201 else cli_adm))
assert est == 201
a_gen = alta("arreglo-t2-gen", "HUMO-prod-T201", 1000, 0, 300, 4, "HUMO-cta-T201", cli)
a_cli = alta("arreglo-t2-h32cli", "HUMO-prod-T202", 1000, 0, 300, 4, "HUMO-cta-T202", cli_adm)
a_arr = alta("arreglo-t2-h32arr", "HUMO-prod-T203", 1000, 0, 300, 4, "HUMO-cta-T203", cli, owner=OWNER_ADM)

# H-8: Vigente congelado (admin genera y formaliza)
a_h8 = alta("arreglo-t2-h8", "HUMO-prod-T204", 1000, 0, 500, 2, "HUMO-cta-T204", cli)
a_h19 = alta("arreglo-t2-h19", "HUMO-prod-T205", 1000, 0, 500, 2, "HUMO-cta-T205", cli)
for nombre, a in (("h8", a_h8), ("h19", a_h19)):
    est, b, seg = llamar(dv, API, {"arregloid": a})
    R.v("datos", f"{nombre}: generar formato (admin)", est == 200, 200, (est, codigo(b), err(b)[:200] if est != 200 else {k: v for k, v in b.items() if k in ("numeroprovisional", "congelado")}))
    est, b = actualizar(dv, "arreglo", a, {"statecode": 0, "statuscode": 159460001})
    rel = leer(dv, "arreglo", a, "statecode,statuscode,sanic_numeroarreglo,sanic_fechacongelamiento")
    R.v("datos", f"{nombre}: formalizar -> Vigente congelado", est == 204 and rel["statuscode"] == 159460001, 204, (est, codigo(b), err(b)[:200] if est != 204 else {k: v for k, v in rel.items() if not k.startswith("@")}))
