"""Tanda 4, continuacion (2026-11-04): el .txt del borrador A trabo el reemplazo por el .pdf (la regla de S-38 lee el nombre ANTERIOR del archivo al subir). Se prueba
DELETE del archivo (H-36) como Oficial, el reemplazo, y que Formalizar de A con C vigente (mismo numero) se rechace (D-43). Datos HUMO; sin borrados de filas."""
import sys
sys.argv = [sys.argv[0]]
exec(open(__file__.replace("t20_humo_d43_continuacion", "t10_humo_d43")).read().split("# --- datos base")[0])
RES.clear()
est_ = leer_estado()
f = {x["clave"]: x["id"] for x in est_["filas"]}
A, C, CLIENTE = f["arreglo-d43-A"], f["arreglo-d43-C"], f["cliente-d43"]
NUM = "HUMO-D43-0001"

print("A:", estado(A)); print("C:", estado(C))
# H-36: borrar el archivo como Oficial (DELETE sobre la columna)
est, b, _ = dv.call("DELETE", f"{AR}({A})/sanic_documentofirmado", cabeceras=CAB)
e = estado(A)
v("H-36 Oficial borra el archivo de A (DELETE columna) estando congelado", est in (200, 204), "2xx", (est, msg(b)[:160], e.get("sanic_documentofirmado_name")))
est, t = subir(A, "firmado.pdf", b"%PDF-1.4 HUMO ficticio\n%%EOF\n")
v("reemplazo por .pdf tras borrar: pasa", est in (200, 204), "2xx", (est, t[:160]))
# D-43: A es Borrador congelado con documento; C ya es Vigente del mismo numero
est, b = actualizar(dv, "arreglo", A, {"statecode": 0, "statuscode": 159460001}, cabeceras=CAB)
v("D-43: Formalizar A con C vigente (mismo numero): rechazo con el numero del otro, sin codigo", est >= 400 and "ya tiene un arreglo vigente (1)" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))
v("A sigue Borrador", estado(A)["statuscode"] == 1, 1, estado(A)["statuscode"])
# desbloquear A exige quitar el documento (ARR-028) y luego descartar
est, b = actualizar(dv, "arreglo", A, {"sanic_fechacongelamiento": None}, cabeceras=CAB)
v("Desbloquear A con documento: rechazo ARR-028 sin codigo", est >= 400 and "Quite el documento firmado" in msg(b) and sin_codigo(msg(b)), "4xx", (est, msg(b)[:200]))
est, b, _ = dv.call("DELETE", f"{AR}({A})/sanic_documentofirmado", cabeceras=CAB)
est, b = actualizar(dv, "arreglo", A, {"sanic_fechacongelamiento": None}, cabeceras=CAB)
v("Desbloquear A sin documento", est == 204, 204, (est, msg(b)[:160]))
est, b = actualizar(dv, "arreglo", A, {"statecode": 1, "statuscode": 159460003, "sanic_motivodescarte": "HUMO D43 prueba"}, cabeceras=CAB)
e = estado(A)
v("Descartar A con motivo", est == 204 and e["statuscode"] == 159460003, "204 Descartado", (est, msg(b)[:160], e["statuscode"]))
# C vigente: reemplazar rechazado; anular
est, t = subir(C, "otro.pdf", b"HUMO2")
v("C Vigente recien formalizado: reemplazar el documento rechazado ARR-027 sin codigo", est >= 400 and "solo se puede adjuntar o reemplazar" in t and sin_codigo(t), "4xx", (est, t[:200]))
est, b = actualizar(dv, "arreglo", C, {"statecode": 1, "statuscode": 159460002, "sanic_motivoanulacion": "HUMO D43 prueba"}, cabeceras=CAB)
e = estado(C)
v("Anular C", est == 204 and e["statuscode"] == 159460002, "204 Anulado", (est, msg(b)[:160], e["statuscode"]))
volcar("20_humo_d43_continuacion.json", RES)
print("RESUMEN:", sum(1 for r in RES if r["ok"]), "ok de", len(RES))
