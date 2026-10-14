"""Fixtures: LIMPIEZA de seguridad por Web API. Borra solo lo registrado en estado.json (en orden inverso) y despues cualquier fila ZZF- / sanic_zzf residual.
Normalmente el finally del exportador ya borro todo y esto no encuentra nada."""
from flib import *

dv = adm()
st = leer_estado()
r = {"registradas": 0, "borradas": 0, "ya_no_existian": 0, "fallos": 0, "residuales_borrados": 0}
CONJ = {v[0]: v[1] for v in TABLAS.values()}


def borrar(conj, rid):
    for _ in range(3):
        est, b, _ = dv.call("DELETE", f"{conj}({rid})", timeout=600)
        if est in (200, 204, 404):
            return est
        time.sleep(5)
    return est


import time  # noqa: E402
for clave, tabla, rid in reversed(st.get("filas", [])):
    r["registradas"] += 1
    est = borrar(CONJ[tabla], rid)
    if est == 404:
        r["ya_no_existian"] += 1
    elif est in (200, 204):
        r["borradas"] += 1
    else:
        r["fallos"] += 1
        print("  !! no se pudo borrar", clave, est)

# residuales ZZF- (orden de dependencia: arreglos, conversaciones, cliente/version, correos, variables)
for _ in range(2):
    res = residuales_zzf(dv)
    for t in ("arreglo", "conversacion", "cliente", "versionplantilla", "email", "environmentvariablevalue", "environmentvariabledefinition"):
        for rid in res.get(t, []):
            est = borrar(TABLAS[t][1], rid)
            if est in (200, 204):
                r["residuales_borrados"] += 1
            elif est != 404:
                r["fallos"] += 1
print(json.dumps(r))
volcar("90_limpieza.json", r)
