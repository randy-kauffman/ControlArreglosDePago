"""Tanda 4 de humo (2026-11-04), Dev, datos ficticios HUMO-D43-*. Sin correos. Despliegue 3: documento firmado (S-38), mensajes sin codigo, un solo Vigente por
credito y numero unico entre productos (D-43). Las acciones de personas se hacen como `# User 8` (Oficial) con MSCRMCallerID.
Nunca imprime secretos, tokens ni direcciones. No borra nada: las filas HUMO quedan en estado.json."""
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

AQUI4 = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI4, "..", "tanda1"))
import hlib as _h  # noqa: E402
from hlib import *  # noqa: E402,F401,F403

_h.ESTADO = os.path.join(AQUI4, "estado.json")
_h.EVID = os.path.join(AQUI4, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)

OFICIAL = "fab4b6ed-6116-f111-8342-000d3a316609"  # # User 8
API = "sanic_carp_capi_generarformato"
CAB = {"MSCRMCallerID": OFICIAL}
dv = adm()
CL, AR = conjuntos(dv)["cliente"], conjuntos(dv)["arreglo"]
RES = []


def v(paso, ok, esperado, obtenido):
    RES.append({"paso": paso, "ok": bool(ok), "esperado": esperado, "obtenido": obtenido})
    print(("OK   " if ok else "FALLA"), paso, "->", str(obtenido)[:230])


def msg(b):
    return (b.get("error") if isinstance(b, dict) else str(b)) or ""


def sin_codigo(t):
    return "CARP-" not in t


def subir(rid, nombre, contenido):
    h = {"Authorization": "Bearer " + dv._token, "Accept": "application/json", "OData-MaxVersion": "4.0", "OData-Version": "4.0",
         "Content-Type": "application/octet-stream", "x-ms-file-name": nombre, "MSCRMCallerID": OFICIAL}
    req = urllib.request.Request(dv.api + f"{AR}({rid})/sanic_documentofirmado", method="PATCH", headers=h, data=contenido)
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            return r.status, ""
    except urllib.error.HTTPError as e:
        t = e.read().decode()
        try:
            t = json.loads(t)["error"]["message"]
        except Exception:
            pass
        return e.code, t


def cuerpo(num, prod=PROD_PRESTAMO):
    return {"sanic_producto": prod, "sanic_numeroproducto": num, "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR, "sanic_saldoinicial": 1000,
            "sanic_montoabonado": 0, "sanic_plazo": 4, "sanic_cuotamensual": 300, "sanic_fechainicio": "2026-11-05", "sanic_cuentadebitar": "HUMO-cta-D43",
            "sanic_fechaformalizacion": "2026-11-04", **OWNER, "sanic_clienteid@odata.bind": f"/{CL}({CLIENTE})", "sanic_oficialid@odata.bind": f"/systemusers({OFICIAL})"}


def crear_como_oficial(clave, c):
    est, b, _ = dv.call("POST", AR, c, cabeceras={**PREFER_REP, **CAB})
    rid = b.get("sanic_carp_tbl_arregloid") if est == 201 else None
    if rid:
        registrar(clave, AR, rid, "HUMO D43")
    return est, b, rid


def estado(rid):
    r = get(dv, f"{AR}({rid})?$select=statecode,statuscode,sanic_numeroarreglo,sanic_fechacongelamiento,sanic_documentofirmado_name")
    return {k: x for k, x in r.items() if not k.startswith("@")}


# --- datos base (segunda corrida, con 1.0.4.0: el tipo ya no se valida al subir)
est, b, CLIENTE = crear(dv, "cliente-d43b", "cliente", {"sanic_nombrecompleto": "HUMO D43 Cliente B", "sanic_cedula": "HUMO-ced-d43b", **OWNER}, "HUMO D43")
v("cliente HUMO (admin)", est == 201, 201, est)
NUM = "HUMO-D43-1001"
est, b, A = crear_como_oficial("arreglo-d43b-A", cuerpo(NUM)); v("1 Oficial crea borrador A (prestamo)", est == 201, 201, est)
est, b, _ = crear_como_oficial("arreglo-d43b-tarjeta", cuerpo(NUM, PROD_TARJETA))
v("2 mismo numero como Tarjeta: rechazo sin codigo", est >= 400 and "ya está registrado como préstamo" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:160]))
est, b, C = crear_como_oficial("arreglo-d43b-C", cuerpo(NUM)); v("2b borrador C del mismo credito (los borradores son libres)", est == 201, 201, est)
for n, r in (("A", A), ("C", C)):
    est, b, _ = llamar(dv, API, {"arregloid": r}, caller=OFICIAL); v(f"3 generar formato {n}", est == 200, 200, est if est == 200 else (est, msg(b)[:160]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("3b Formalizar A sin documento: texto sin codigo", est >= 400 and "Adjunte el documento firmado" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:160]))
est, t = subir(A, "firmado.txt", b"HUMO"); v("4 subir .txt: pasa (el tipo se revisa al formalizar)", est in (200, 204), "2xx", (est, t[:160]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("4b Formalizar con .txt: rechazo ARR-026 sin codigo", est >= 400 and "PDF, JPG o PNG" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:160]))
est, t = subir(A, "firmado.pdf", b"%PDF-1.4 HUMO ficticio\n%%EOF\n"); v("4c reemplazar por .pdf: pasa (antes quedaba atrapado)", est in (200, 204), "2xx", (est, t[:160]))
est, b, _ = dv.call("DELETE", f"{AR}({C})/sanic_documentofirmado", cabeceras=CAB)
v("4d DELETE de la columna sobre C sin archivo: sin error de tipo", est in (200, 204, 404) or "PDF, JPG o PNG" not in msg(b), "sin ARR-026", (est, msg(b)[:120]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
e = estado(A)
v("5 Formalizar A con .pdf: Vigente con numero 1", est == 204 and e["statuscode"] == 159460001 and e.get("sanic_numeroarreglo") == 1, "204 Vigente 1", (est, msg(b)[:120], e.get("statuscode"), e.get("sanic_numeroarreglo")))
est, t = subir(A, "otro.pdf", b"HUMO2")
v("6 Vigente recien formalizado: reemplazar rechazado ARR-027 sin codigo", est >= 400 and "solo se puede adjuntar o reemplazar" in t and sin_codigo(t), "4xx", (est, t[:160]))
est, t = subir(C, "firmado.pdf", b"%PDF-1.4 HUMO ficticio\n%%EOF\n"); v("7 subir documento a C", est in (200, 204), "2xx", (est, t[:120]))
est, b = actualizar(dv, "arreglo", C, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("7b D-43 Formalizar C con A vigente: rechazo con el numero del otro, sin codigo", est >= 400 and "ya tiene un arreglo vigente (1)" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:160]))
est, b = actualizar(dv, "arreglo", C, {"sanic_fechacongelamiento": None}, cabeceras=CAB)
v("8 desbloquear C con documento: ARR-028 sin codigo", est >= 400 and "Quite el documento firmado" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:160]))
est, b = actualizar(dv, "arreglo", C, {"statecode": 1, "statuscode": 159460003, "sanic_motivodescarte": "HUMO D43 prueba"}, cabeceras=CAB)
v("8b descartar C con motivo", est == 204 and estado(C)["statuscode"] == 159460003, "204", (est, msg(b)[:120]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 1, "statuscode": 159460002, "sanic_motivoanulacion": "HUMO D43 prueba"}, cabeceras=CAB)
v("9 anular A", est == 204 and estado(A)["statuscode"] == 159460002, "204", (est, msg(b)[:120]))
volcar("30_humo_d43_corrida2.json", RES)
print("RESUMEN:", sum(1 for r in RES if r["ok"]), "ok de", len(RES))
