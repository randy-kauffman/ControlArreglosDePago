"""Reasigna las trazas de un JSON de Corrida con la regla vigente (ultimo paso con t_ini <= inicio). Uso: python3 reasignar.py evidencia/<archivo>.json"""
import json, sys
from datetime import datetime, timezone
ruta = sys.argv[1]
d = json.load(open(ruta, encoding="utf-8"))
todas = [t for p in d["pasos"] for t in p["trazas"]] + d["trazas_sin_paso"]
f = lambda s: datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(timezone.utc) if "+" in s or s.endswith("Z") else datetime.fromisoformat(s).replace(tzinfo=timezone.utc)
for p in d["pasos"]:
    p["trazas"] = []
sin = []
for t in sorted(todas, key=lambda t: f(t["inicio"])):
    c = [p for p in d["pasos"] if f(p["t_ini"]) <= f(t["inicio"])][-1:]
    (c[0]["trazas"] if c else sin).append(t)
d["trazas_sin_paso"] = sin
json.dump(d, open(ruta, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("pasos", len(d["pasos"]), "trazas", len(todas), "sin paso", len(sin))
