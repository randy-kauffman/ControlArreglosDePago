"""Vuelca las trazas del paquete desde una hora UTC (argumento 1, ISO) a evidencia/<argumento 2>. Espera a que el volumen se estabilice (las trazas llegan por lotes, hasta ~60 s tarde).
Uso: python3 hT_trazas.py 2026-10-20T14:14:00Z 11_h29_trazas_completas.json [filtro_tipo]"""
from hlib import *
from datetime import datetime

dv = adm()
desde = datetime.strptime(sys.argv[1], "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
tr = resumen_trazas(trazas(dv, desde, esperar=120, estable=4))
filtro = sys.argv[3] if len(sys.argv) > 3 else None
if filtro:
    tr = [x for x in tr if filtro in x["tipo"]]
volcar(sys.argv[2], tr)
print(len(tr), "trazas")
cuenta = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["depth"], x["mode"])
    cuenta[k] = cuenta.get(k, 0) + 1
for k, n in sorted(cuenta.items()):
    print(" ", k, "x", n)
