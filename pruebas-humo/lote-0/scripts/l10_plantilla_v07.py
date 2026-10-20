"""Lote 0, plantilla real v07 (inventario 9.1 y 9.2): publica v07 con el gesto normal (PATCH sanic_estado = Vigente, el mismo que arma
comandos.js), crea un arreglo HUMO y genera el formato dos veces: en Borrador (PA-GEN-01) y ya Vigente con la cuota 1 pagada (PA-GEN-04),
para que las 28 celdas de MAP_V07 tengan dato. Caller = administrador de Dev. Nunca imprime secretos. Todo dato es HUMO-.
La version y su mapeo los carga antes herramientas/construir/datos.py con playbooks/datos/versionplantilla.md y mapeocelda.md."""
import base64
import io
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
L0 = os.path.join(AQUI, "..")
sys.path.insert(0, os.path.join(L0, "..", "tanda1"))
import hlib as _h  # noqa: E402
from hmap import *  # noqa: E402,F401,F403
import openpyxl  # noqa: E402

_h.ESTADO = os.path.join(L0, "estado-plantilla.json")
_h.EVID = os.path.join(L0, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
EVID = _h.EVID

dv = adm()
YO = get(dv, "WhoAmI")["UserId"]
VC, AR, CU, CL = (conjuntos(dv)[k] for k in ("version", "arreglo", "cuota", "cliente"))
R = Res("10_plantilla_v07.json")
API = "sanic_carp_capi_generarformato"


def tanda1(clave):
    with open(os.path.join(L0, "..", "tanda1", "estado.json"), encoding="utf-8") as f:
        for x in reversed(json.load(f)["filas"]):
            if x["clave"] == clave:
                return x["id"]
    raise KeyError(clave)


def version(nombre):
    v = get(dv, f"{VC}?$select=sanic_carp_tbl_versionplantillaid,sanic_estado,sanic_huella,sanic_fechaaprobacion,_sanic_aprobadoporid_value,sanic_archivo_name"
                f"&$filter=sanic_nombre eq '{nombre}'")["value"]
    assert len(v) == 1, (nombre, len(v))
    return v[0]


def vigentes():
    return [x["sanic_nombre"] for x in get(dv, f"{VC}?$select=sanic_nombre&$filter=sanic_estado eq {VER_VIGENTE}")["value"]]


# ---------------------------------------------------------------- 1. publicar v07
v07 = version("v07")
vid = v07["sanic_carp_tbl_versionplantillaid"]
antes = vigentes()
if v07["sanic_estado"] == VER_BORRADOR:
    est, b = actualizar(dv, "version", vid, {"sanic_estado": VER_VIGENTE})
    R.v("9.1", "Publicar v07 (gesto Publicar: PATCH sanic_estado = Vigente)", est == 204, 204, (est, codigo(b), err(b)[:300] if est != 204 else ""))
v07 = version("v07")
R.v("9.1", "v07 Vigente, huella = SHA-256 del archivo aprobado, aprobador = actor, fecha de aprobacion",
    v07["sanic_estado"] == VER_VIGENTE and v07["sanic_huella"] == sha256_plantilla() and v07["_sanic_aprobadoporid_value"] == YO and bool(v07["sanic_fechaaprobacion"]),
    "Vigente con huella", {"vigentes_antes": antes, "huella_ok": v07["sanic_huella"] == sha256_plantilla(), "archivo": v07["sanic_archivo_name"], "fecha": v07["sanic_fechaaprobacion"]})
R.v("9.1", "queda exactamente una Vigente (v07); la anterior paso a Historica", vigentes() == ["v07"], ["v07"], vigentes())

# ---------------------------------------------------------------- 2. arreglo HUMO sobre v07
# El cliente-01 de la tanda 1 no tiene correo ni celular (primera corrida: B9 y F9 vacias). Se crea un cliente HUMO completo;
# el dominio example.com es reservado (RFC 2606) y a ese correo nunca se manda nada.
try:
    cli = fila("cliente-l0-v07")
except KeyError:
    est, b, cli = crear(dv, "cliente-l0-v07", "cliente", {"sanic_nombrecompleto": "HUMO Cliente L0 Plantilla", "sanic_cedula": "HUMO-ced-l0-v07",
                                                          "sanic_correo": "humo.cliente.l0@example.com", "sanic_celular": "8888-0007", **OWNER}, "cliente HUMO completo")
    assert est == 201, (est, err(b))
c = get(dv, f"{CL}({cli})?$select=sanic_nombrecompleto,sanic_correo,sanic_celular")
R.v("9.2", "el cliente HUMO tiene correo y celular (para que sus celdas tengan dato)", bool(c.get("sanic_correo")) and bool(c.get("sanic_celular")), True,
    {"nombre_humo": (c.get("sanic_nombrecompleto") or "").startswith("HUMO")})
try:
    arr = fila("arreglo-l0-v07b")
except KeyError:
    cuerpo = {"sanic_producto": PROD_PRESTAMO, "sanic_numeroproducto": "HUMO-prod-L0-07B", "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR,
              "sanic_saldoinicial": 3000, "sanic_montoabonado": 300, "sanic_plazo": 3, "sanic_cuotamensual": 900, "sanic_fechainicio": "2026-11-05",
              "sanic_cuentadebitar": "HUMO-cta-L0-07B", "sanic_fechaformalizacion": "2026-10-20", "sanic_comentarios": "HUMO comentario de prueba de la plantilla v07",
              **OWNER, "sanic_clienteid@odata.bind": f"/{CL}({cli})", "sanic_oficialid@odata.bind": f"/systemusers({YO})"}
    est, b, arr = crear(dv, "arreglo-l0-v07b", "arreglo", cuerpo, "arreglo HUMO de la prueba de la plantilla v07")
    assert est == 201, (est, err(b))
rel = leer(dv, "arreglo", arr, "_sanic_versionplantillaid_value,statecode,statuscode")
R.v("9.2", "el arreglo nuevo toma v07 (la Vigente) al alta", rel["_sanic_versionplantillaid_value"] == vid, vid, rel["_sanic_versionplantillaid_value"])


def generar(nombre):
    est, b, seg = llamar(dv, API, {"arregloid": arr})
    assert est == 200, (est, codigo(b), err(b)[:300])
    datos = base64.b64decode(b["archivo"])
    with open(os.path.join(EVID, nombre), "wb") as f:
        f.write(datos)
    return openpyxl.load_workbook(io.BytesIO(datos)), seg


def revisar(libro, plazo, con_pago):
    """Por cada fila de MAP_V07: la celda (o, en bloque, la primera fila) tiene dato. Las columnas de pago solo si hay una cuota pagada."""
    filas = []
    for h, ce, campo, tipo in MAP_V07:
        hoja = libro[HOJAS[h]]
        val = hoja[ce].value
        if tipo == BLOQUE:
            col = "".join(ch for ch in ce if ch.isalpha())
            ini = int(ce[len(col):])
            val = [hoja[f"{col}{ini + i}"].value for i in range(plazo)]
            pago = campo in ("cuotaFechaPago", "cuotaMontoPagado")
            ok = (val[0] not in (None, "")) if (con_pago or not pago) else True
            if not pago:
                ok = all(v not in (None, "") for v in val)
        else:
            ok = val not in (None, "")
        filas.append({"hoja": h, "celda": ce, "campo": campo, "lleno": ok, "tipo_valor": type(val).__name__})
    return filas


libro, seg = generar("10_F-NIC-0000052-v07-HUMO-borrador.xlsx")
r1 = revisar(libro, 3, con_pago=False)
pend = [f"{x['hoja']}!{x['celda']}" for x in r1 if not x["lleno"]]
R.v("9.2", "formato en Borrador (PA-GEN-01): las 26 celdas que no son de pago tienen dato y el bloque tiene las 3 cuotas", not pend, "0 vacias", {"vacias": pend, "seg": seg})

# ---------------------------------------------------------------- 3. formalizar, pagar la cuota 1 y volver a generar (PA-GEN-04)
if leer(dv, "arreglo", arr, "statuscode")["statuscode"] != 159460001:
    est, b = actualizar(dv, "arreglo", arr, {"statecode": 0, "statuscode": 159460001})
    assert est == 204, (est, codigo(b), err(b)[:200])
c1 = get(dv, f"{CU}?$select=sanic_carp_tbl_cuotaid,sanic_montopagado&$filter=_sanic_arregloid_value eq {arr} and sanic_numero eq 1")["value"][0]
if c1.get("sanic_montopagado") is None:
    est, b = actualizar(dv, "cuota", c1["sanic_carp_tbl_cuotaid"], {"sanic_fechapago": "2026-11-05", "sanic_montopagado": 900})
    assert est == 204, (est, codigo(b), err(b)[:200])
libro, seg = generar("10_F-NIC-0000052-v07-HUMO-vigente.xlsx")
r2 = revisar(libro, 3, con_pago=True)
pend = [f"{x['hoja']}!{x['celda']}" for x in r2 if not x["lleno"]]
R.v("9.2", "formato del arreglo Vigente con la cuota 1 pagada (PA-GEN-04): las 28 celdas de MAP_V07 tienen dato", not pend and len(r2) == 28, "28 de 28",
    {"llenas": sum(x["lleno"] for x in r2), "vacias": pend, "seg": seg, "detalle": r2})
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
