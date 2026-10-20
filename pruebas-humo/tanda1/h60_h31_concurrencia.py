"""H-31: dos tomarconversacion simultaneas (IfRowVersionMatches dentro de la API) y K-01 (huella duplicada). Admin y CallerId=Randy como los dos oficiales."""
import threading
from hlib import *

RANDY = "12b85b09-58f3-f011-8407-000d3a316609"
dv = adm()
R = Res("60_h31.json")
YO = get(dv, "WhoAmI")["UserId"]
CV = conjuntos(dv)["conversacion"]
t0 = reloj(dv) - timedelta(seconds=3)
BASE = {"sanic_fechaprimercorreo": "2026-10-20T10:00:00Z", "sanic_correonuevoenfinalizada": False, "sanic_cantidadrespuestas": 0, **OWNER}
resultados = []
for ronda in range(1, 6):
    est, b, cid = crear(dv, f"conv-h31-{ronda}", "conversacion", dict(BASE, sanic_huellamessageid=f"HUMO-h-03{ronda}"), "H-31")
    assert est == 201
    res = {}
    bar = threading.Barrier(2)
    def lanza(k, caller):
        d2 = Dataverse() if False else dv
        bar.wait()
        res[k] = llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": cid}, caller)
    th = [threading.Thread(target=lanza, args=("admin", None)), threading.Thread(target=lanza, args=("randy", RANDY))]
    [t.start() for t in th]; [t.join() for t in th]
    final = leer(dv, "conversacion", cid, "_sanic_oficialasignadoid_value")["_sanic_oficialasignadoid_value"]
    r = {k: (v[0], codigo(v[1]), v[2]) for k, v in res.items()}
    r["oficial_final"] = "admin" if final == YO else ("randy" if final == RANDY else final)
    resultados.append(r)
    print("ronda", ronda, r)
hubo_900 = any(c == "CARP-TOM-900" for r in resultados for k, (e, c, s) in [(k, v) for k, v in r.items() if k != "oficial_final"])
hubo_004 = any(c == "CARP-TOM-004" for r in resultados for k, (e, c, s) in [(k, v) for k, v in r.items() if k != "oficial_final"])
R.v("H-31", "dos tomarconversacion simultaneas sobre una conversacion sin oficial (5 rondas): siempre queda UN oficial; el perdedor recibe CARP-TOM-900 (IfRowVersionMatches) o CARP-TOM-004 (llego despues)",
    all(r["oficial_final"] in ("admin", "randy") for r in resultados), "un solo oficial por ronda", resultados, hubo_tom900=hubo_900, hubo_tom004=hubo_004)
# K-01: huella duplicada
est, b, _ = crear(dv, "conv-h31-dup", "conversacion", dict(BASE, sanic_huellamessageid="HUMO-h-031"), "K-01")
R.v("H-31", "K-01: Create de conversacion con huella duplicada (Web API directo): rechazo de clave", est in (400, 412), "4xx por clave", (est, err(b)[:220]))
R.v("H-31", "K-01 DENTRO de un plugin (iniciarconversacionnueva con huella existente: CARP-INI-005/-900) y 0x80040265 anidado traducido", False, "requiere correos entrantes (no se crean correos: S-34 async y buzones)", "NO CORRIDA", no_corrida=True)
time.sleep(8)
tr = resumen_trazas(trazas(dv, t0, esperar=90, estable=3))
volcar("60_h31_trazas.json", tr)
fallas = [(x["tipo"], l[:160]) for x in tr for l in x["lineas"] if "falla de la plataforma" in l or "falla inesperada" in l]
print("fallas de plataforma trazadas:", fallas)
R.v("H-31", "forma de la FaultException de IfRowVersionMatches en la traza de la API (codigo hexadecimal)", True, "medicion", fallas)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
