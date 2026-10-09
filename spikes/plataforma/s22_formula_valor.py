"""Q4: releer el valor de la formula con Now() sobre filas reales, en dos lecturas separadas en el tiempo."""
import json, time, datetime as dt, sys
from dv import Dataverse, mostrar
dv = Dataverse()
COL = sys.argv[1] if len(sys.argv) > 1 else "spk_antignow_plano"
ahora = dt.datetime.now(dt.timezone.utc)
filas = {"hace5h": ahora - dt.timedelta(hours=5), "hace30h": ahora - dt.timedelta(hours=30), "hace10min": ahora - dt.timedelta(minutes=10)}
ids = {}
for n, t in filas.items():
    est, b, h = dv.call("POST", "spk_conversacions", {"spk_nombre": f"Q4-{n}", "spk_primercorreo": t.strftime("%Y-%m-%dT%H:%M:%SZ")})
    ids[n] = dv.id_creado(h)
    print("alta", n, est, ids[n])
json.dump(ids, open("/tmp/claude-1000/-home-gmaker-projects-ControlArreglosDePago/2a1c9540-94de-4279-b899-1e8a4eb38d2a/scratchpad/q4_ids.json", "w"))
for n, i in ids.items():
    est, b, _ = dv.call("GET", f"spk_conversacions({i})?$select=spk_nombre,spk_primercorreo,{COL}")
    print("RELEER", n, est, b.get("spk_primercorreo"), "->", COL, "=", b.get(COL) if est == 200 else b)
print("hora UTC de la primera lectura:", dt.datetime.now(dt.timezone.utc).strftime("%H:%M:%S"))
