"""Restaura el mailbox 1 (salida SSS) al valor de la línea base guardada en estado.json -> ventana_salida.original, y lo verifica.
Úsese si v60_salida_e2e.py se cortó con la ventana abierta. Idempotente. No toca nada más."""
from flib import *

dv = adm()
W = est().get("ventana_salida")
if not W or not W.get("original"):
    sys.exit("no hay línea base guardada: no se hace nada")
o = W["original"]
st, b, _ = dv.call("PATCH", f"mailboxes({AREA_MAILBOX})", {"outgoingemaildeliverymethod": o["outgoingemaildeliverymethod"], "incomingemaildeliverymethod": o["incomingemaildeliverymethod"]})
time.sleep(5)
cols = ",".join(o.keys())
m = {k: v for k, v in get(dv, f"mailboxes({AREA_MAILBOX})?$select={cols}").items() if not k.startswith("@")}
dif = {k: [o[k], m[k]] for k in o if o[k] != m[k]}
W["abierta"] = bool(dif)
poner("ventana_salida", W)
print("PATCH", st, "| diferencias contra la línea base:", dif or "ninguna")
