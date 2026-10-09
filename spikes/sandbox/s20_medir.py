"""Mide la Custom API en el sandbox: por plazo, N llamadas seguidas. Guarda cada llamada en salida/mediciones.jsonl
(sin el Base64) y resume. Uso: python3 s20_medir.py [--n 30] [--etiqueta texto] [--plazos 6,30,60]"""
import argparse, time, base64, statistics
from sbx_lib import *

ap = argparse.ArgumentParser(); ap.add_argument("--n", type=int, default=30); ap.add_argument("--etiqueta", default="")
ap.add_argument("--plazos", default="6,30,60"); ap.add_argument("--fila", default="v07 borrador (huella correcta)"); a = ap.parse_args()
dv = Dataverse(); e = leer_estado()
os.makedirs(os.path.join(AQUI, "salida"), exist_ok=True)
log = open(os.path.join(AQUI, "salida", "mediciones.jsonl"), "a", encoding="utf-8")

def kv(det):
    d = {}
    for p in det.split(";"):
        k, _, v = p.partition("=")
        d[k] = v
    return d

for plazo in [int(x) for x in a.plazos.split(",")]:
    filas = []
    for i in range(a.n):
        t0 = time.perf_counter()
        est, b, _ = dv.call("POST", API, {"VersionId": e["filas"][a.fila], "Plazo": plazo})
        http = (time.perf_counter() - t0) * 1000
        assert est == 200, (est, b)
        d = kv(b["Detalle"])
        raw_len = len(base64.b64decode(b["ArchivoBase64"]))
        assert raw_len == b["TamanoBytes"], "el tamano declarado no coincide con el archivo decodificado"
        r = {"etiqueta": a.etiqueta, "fila": a.fila, "plazo": plazo, "i": i + 1, "http_ms": round(http, 1), "plugin_ms": b["Milisegundos"],
             "descarga_ms": int(d["descargaMs"]), "generacion_ms": int(d["generacionMs"]), "base64_ms": int(d["base64Ms"]),
             "llamada_en_proceso": int(d["llamadaEnElProceso"]), "proceso_vivo_ms": int(d["procesoVivoMs"]),
             "bytes": b["TamanoBytes"], "base64_chars": len(b["ArchivoBase64"]), "mem_antes_kb": int(d["memAntesKB"]), "mem_despues_kb": int(d["memDespuesKB"]),
             "ts": time.strftime("%Y-%m-%dT%H:%M:%S")}
        log.write(json.dumps(r) + "\n"); log.flush(); filas.append(r)
    pl = [f["plugin_ms"] for f in filas]; ht = [f["http_ms"] for f in filas]
    print(f"plazo {plazo:2d} | primera: plugin {filas[0]['plugin_ms']} ms (llamada nro {filas[0]['llamada_en_proceso']} del proceso, gen {filas[0]['generacion_ms']} ms, descarga {filas[0]['descarga_ms']} ms), http {filas[0]['http_ms']:.0f} ms"
          f" | siguientes ({len(filas)-1}): plugin mediana {statistics.median(pl[1:]):.0f} min {min(pl[1:])} max {max(pl[1:])} ms; gen mediana {statistics.median([f['generacion_ms'] for f in filas[1:]]):.0f} ms; http mediana {statistics.median(ht[1:]):.0f} max {max(ht[1:]):.0f} ms"
          f" | {filas[0]['bytes']} bytes, {filas[0]['base64_chars']} chars b64")
