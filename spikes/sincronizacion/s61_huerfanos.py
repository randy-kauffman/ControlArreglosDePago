"""Caso 8: relee cada 30 s (max 25 min) si las filas de 'attachment' (el blob) de los correos borrados siguen existiendo."""
from lib import *
dv = Dataverse()
ids = {"6A foto": "81bd9ae9-4fc3-f111-a05a-000d3a35abca", "6A pdf": "7fbd9ae9-4fc3-f111-a05a-000d3a35abca", "6B logo (borrado via conversacion)": "99bd9ae9-4fc3-f111-a05a-000d3a35abca"}
t0 = time.time(); vistos = {}
while time.time() - t0 < 25 * 60:
    for n, i in ids.items():
        s, r, _ = dv.call("GET", f"attachments?$select=attachmentid&$filter=attachmentid eq {i}")
        if s == 200 and not r["value"] and n not in vistos:
            vistos[n] = int(time.time() - t0); print(n, "desaparecio a los", vistos[n], "s de empezar a vigilar", utc_iso(), flush=True)
    if len(vistos) == len(ids): break
    time.sleep(30)
print("final", vistos, "aun existen:", [n for n in ids if n not in vistos])
