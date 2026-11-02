"""V-1 (paso 3b): corrige lo que v12 dejó abierto. Failed (8) pertenece al estado Open (0): ¿PATCH con statecode 0 + statuscode 8 desde Pending Send?
¿`senton` es escribible en un correo que pasó a Sent a mano (S-35 usa senton y, si falta, actualend)?"""
from flib import *

dv = adm()
R = Res("v13_transiciones_2.json")
e = est()
idA, fid = e["v1_issuesend_true"]["id"], e["v1_failed"]["id"]


def leer(i):
    x = limpio(email(dv, i))
    return {k: x.get(k) for k in ("statuscode", "statecode", "senton", "actualend")}


f = {"antes": leer(fid)}
st, b, _ = dv.call("PATCH", f"emails({fid})", {"statecode": 0, "statuscode": 8})
f["a8"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(fid)}
R.v("V-1", "6 -> 8 Failed con statecode 0 explícito", f["a8"]["http"] == 204 and f["a8"]["despues"]["statuscode"] == 8, "204 y 8", f["a8"])
if f["a8"]["http"] >= 300:  # alternativa: pasar por 7 primero
    st, b, _ = dv.call("PATCH", f"emails({fid})", {"statuscode": 7})
    st2, b2, _ = dv.call("PATCH", f"emails({fid})", {"statecode": 0, "statuscode": 8})
    f["via7"] = {"http7": st, "http8": st2, "error": err(b2) if st2 >= 300 else None, "despues": leer(fid)}
    R.v("V-1", "6 -> 7 -> 8 Failed", st2 == 204, 204, f["via7"])
a = {"antes": leer(idA)}
ahora = reloj(dv)
st, b, _ = dv.call("PATCH", f"emails({idA})", {"senton": iso(ahora)})
a["senton"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(idA)}
R.v("V-1", "senton escribible en un correo Sent (informativo)", True, "informativo", a["senton"])
# Failed -> Pending Send (reintento manual)? informativo
st, b, _ = dv.call("PATCH", f"emails({fid})", {"statecode": 1, "statuscode": 6})
f["reintento_a6"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(fid)}
R.v("V-1", "Failed -> Pending Send (informativo)", True, "informativo", f["reintento_a6"])
volcar("v13_detalle.json", {"F": f, "A": a})
