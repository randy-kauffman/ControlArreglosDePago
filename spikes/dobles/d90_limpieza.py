"""Dobles: LIMPIEZA de seguridad por Web API. Borra solo lo registrado en estado.json (filas y registros de plugins, en orden inverso) y despues
cualquier fila ZZF- / sanic_zzf residual y todo lo 'Sonda' residual (steps, Custom API, paquete). Normalmente el finally de la consola ya borro las filas."""
from dlib import *

dv = adm()
st = leer_estado()
r = {"filas_registradas": 0, "filas_borradas": 0, "filas_ya_no_existian": 0, "registros_registrados": 0, "registros_borrados": 0, "registros_ya_no_existian": 0,
     "fallos": 0, "residuales_borrados": 0}
CONJ = {v[0]: v[1] for v in TABLAS.values()}


def borrar(conj, rid):
    est = None
    for _ in range(3):
        est, b, _h = dv.call("DELETE", f"{conj}({rid})", timeout=600)
        if est in (200, 204, 404):
            return est
        time.sleep(5)
    print("  !! DELETE", conj, "->", est, str(b)[:300])
    return est


# 1) registros de plugins (steps, Custom API, paquete) en orden inverso al de su nacimiento
for clave, conjunto, rid in reversed(st.get("registros", [])):
    r["registros_registrados"] += 1
    est = borrar(conjunto, rid)
    if est == 404:
        r["registros_ya_no_existian"] += 1
    elif est in (200, 204):
        r["registros_borrados"] += 1
    else:
        r["fallos"] += 1
        print("  !! no se pudo borrar", clave, est)

# 2) filas registradas (orden inverso)
for clave, tabla, rid in reversed(st.get("filas", [])):
    r["filas_registradas"] += 1
    est = borrar(CONJ[tabla], rid)
    if est == 404:
        r["filas_ya_no_existian"] += 1
    elif est in (200, 204):
        r["filas_borradas"] += 1
    else:
        r["fallos"] += 1
        print("  !! no se pudo borrar", clave, est)

# 3) residuales Sonda: steps, Custom API, paquete (ensamblados y tipos se van con el paquete)
for _ in range(2):
    sond = residuales_sonda(dv)
    for clase, conjunto in (("sdkmessageprocessingsteps", "sdkmessageprocessingsteps"), ("customapis", "customapis"), ("pluginpackages", "pluginpackages")):
        for rid in sond.get(clase, []):
            est = borrar(conjunto, rid)
            if est in (200, 204):
                r["residuales_borrados"] += 1
            elif est != 404:
                r["fallos"] += 1

# 4) residuales ZZF- (orden de dependencia: arreglos, conversaciones, cliente/version, correos, variables)
for _ in range(3):
    res = residuales_zzf(dv)
    for t in ("cuota", "mapeocelda", "arreglo", "conversacion", "email", "cliente", "versionplantilla", "environmentvariablevalue", "environmentvariabledefinition"):
        for rid in res.get(t, []):
            est = borrar(TABLAS[t][1], rid)
            if est in (200, 204):
                r["residuales_borrados"] += 1
            elif est != 404:
                r["fallos"] += 1
print(json.dumps(r))
volcar("90_limpieza.json", r)
