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


# --- datos base
est, b, CLIENTE = crear(dv, "cliente-d43", "cliente", {"sanic_nombrecompleto": "HUMO D43 Cliente", "sanic_cedula": "HUMO-ced-d43", **OWNER}, "HUMO D43")
v("cliente HUMO (admin)", est == 201, 201, est)
NUM = "HUMO-D43-0001"

# 1. Oficial crea el Borrador A (Prestamo)
est, b, A = crear_como_oficial("arreglo-d43-A", cuerpo(NUM))
v("1 Oficial crea borrador A (prestamo)", est == 201, 201, est if est == 201 else (est, msg(b)[:200]))

# 2. numero unico entre productos: Tarjeta con el mismo numero -> rechazo sin codigo
est, b, _ = crear_como_oficial("arreglo-d43-tarjeta", cuerpo(NUM, PROD_TARJETA))
v("2 Oficial crea el mismo numero como Tarjeta: rechazo, texto sin codigo", est >= 400 and "ya está registrado como préstamo" in msg(b) and sin_codigo(msg(b)), "4xx y texto en espanol sin CARP-", (est, msg(b)[:200]))
est, b, T2 = crear_como_oficial("arreglo-d43-tarjeta2", cuerpo("HUMO-D43-0002", PROD_TARJETA))
v("2b Tarjeta con otro numero pasa", est == 201, 201, est)
est, b = actualizar(dv, "arreglo", T2, {"sanic_numeroproducto": NUM}, cabeceras=CAB)
v("2c cambiar el numero del borrador Tarjeta a HUMO-D43-0001: rechazo sin codigo", est >= 400 and "ya está registrado como préstamo" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))

# 3. Formalizar sin documento
est, b, seg = llamar(dv, API, {"arregloid": A}, caller=OFICIAL)
v("3 generar formato A (Oficial)", est == 200, 200, est if est == 200 else (est, msg(b)[:200]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("3b Formalizar sin documento: ARR-025 sin codigo", est >= 400 and "Adjunte el documento firmado" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))

# 4. tipo no permitido y permitido
est, t = subir(A, "firmado.txt", b"HUMO")
v("4 subir .txt: rechazo ARR-026 sin codigo", est >= 400 and "PDF, JPG o PNG" in t and sin_codigo(t), "4xx", (est, t[:200]))
est, t = subir(A, "firmado.pdf", b"%PDF-1.4 HUMO ficticio\n%%EOF\n")
v("4b subir .pdf: pasa", est in (200, 204), "2xx", (est, t[:200]))
print(estado(A))

# 5. Formalizar A con documento -> Vigente
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
e = estado(A)
v("5 Formalizar A con documento: Vigente con numero", est == 204 and e["statuscode"] == 159460001 and e.get("sanic_numeroarreglo") == 1, "204 Vigente numero 1", (est, msg(b)[:160], e))

# 6. columna segura: el Oficial (con perfil) lee el nombre; vigente: no se reemplaza
est, t = subir(A, "otro.pdf", b"HUMO2")
v("6 Vigente recien formalizado: reemplazar el documento rechazado ARR-027 sin codigo", est >= 400 and "solo se puede adjuntar o reemplazar" in t and sin_codigo(t), "4xx", (est, t[:200]))

# 7. un solo Vigente por credito
est, b, C = crear_como_oficial("arreglo-d43-C", cuerpo(NUM))
v("7 borrador C del mismo credito (los borradores son libres)", est == 201, 201, est)
est, b, seg = llamar(dv, API, {"arregloid": C}, caller=OFICIAL)
v("7b generar formato C", est == 200, 200, est if est == 200 else (est, msg(b)[:200]))
est, t = subir(C, "firmado.pdf", b"%PDF-1.4 HUMO ficticio\n%%EOF\n")
v("7c subir documento a C", est in (200, 204), "2xx", (est, t[:160]))
est, b = actualizar(dv, "arreglo", C, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("7d Formalizar C con A vigente: rechazo ARR-030 con el numero del otro, sin codigo", est >= 400 and "ya tiene un arreglo vigente (1)" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))
e = estado(C)
v("7e C sigue Borrador", e["statuscode"] == 1, "statuscode 1", e)

# 8. descartar C con motivo (Oficial)
est, b = actualizar(dv, "arreglo", C, {"sanic_fechacongelamiento": None}, cabeceras=CAB)
v("8 desbloquear C con documento: rechazo ARR-028 sin codigo", est >= 400 and "Quite el documento firmado" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))
est, b = actualizar(dv, "arreglo", C, {"statecode": 1, "statuscode": 159460003, "sanic_motivodescarte": "HUMO D43 prueba"}, cabeceras=CAB)
e = estado(C)
v("8b descartar C con motivo", est == 204 and e["statuscode"] == 159460003, "204 Descartado", (est, msg(b)[:160], e))

# 9. Terminar/anular A y reabrir: ARR-031 requiere un Terminado; se cubre en pruebas de dominio. Aqui se anula A (Oficial) para dejar el credito libre.
est, b = actualizar(dv, "arreglo", A, {"statecode": 1, "statuscode": 159460002, "sanic_motivoanulacion": "HUMO D43 prueba"}, cabeceras=CAB)
e = estado(A)
v("9 anular A", est == 204 and e["statuscode"] == 159460002, "204 Anulado", (est, msg(b)[:160], e))

volcar("10_humo_d43.json", RES)
print("RESUMEN:", sum(1 for r in RES if r["ok"]), "ok de", len(RES))
