"""V-8: LIMPIEZA. Borra solo lo creado por el spike V-8 (registrado en estado_v70.json) y devuelve al usuario no administrador a su estado inicial (deshabilitado)."""
from v70_lib import *

dv = adm()
st = leer70(); base = st["linea_base"]
uid = base["usuario"]["id"]
out = {}


def borrar(ruta, etiqueta, reintentos=3):
    for _ in range(reintentos):
        est, b, _ = dv.call("DELETE", ruta, timeout=600)
        if est in (200, 204, 404):
            return est
        time.sleep(5)
    print("  !! no se pudo borrar", etiqueta, est, str(b)[:200])
    return est

# 1) usuario no administrador: deshabilitado como al inicio
print("deshabilitar usuario:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])

# 2) filas de prueba (arreglos primero)
orden = {ES["arreglo"]: 0, ES["cliente"]: 1, ES["versionplantilla"]: 2}
borradas = [(clave, conj, borrar(f"{conj}({rid})", clave)) for clave, conj, rid in sorted(st.get("filas", []), key=lambda f: orden[f[1]])]
out["filas_registradas_borradas"] = len(borradas); print("filas borradas:", len(borradas), sorted({b[2] for b in borradas}))
restantes = {t: len(get(dv, f"{c['set']}?$select={c['pk']}&$top=5")["value"]) for t, c in base["tablas"].items()}
out["filas_en_tablas_tras_borrar(esperado 0)"] = restantes; print("filas que quedan:", restantes)

# 3) steps (los 7 ZZV-, ninguno tiene imagen) y comprobacion de imagenes huerfanas
steps = get(dv, "sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name&$filter=startswith(name,'ZZV-')")["value"]
for s in steps:
    for im in get(dv, f"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid&$filter=_sdkmessageprocessingstepid_value eq {s['sdkmessageprocessingstepid']}")["value"]:
        borrar(f"sdkmessageprocessingstepimages({im['sdkmessageprocessingstepimageid']})", "imagen")
    borrar(f"sdkmessageprocessingsteps({s['sdkmessageprocessingstepid']})", s["name"])
out["steps_borrados"] = len(steps)
out["steps_ZZV_restantes"] = len(get(dv, "sdkmessageprocessingsteps?$select=name&$filter=startswith(name,'ZZV-')")["value"])
print("steps borrados:", len(steps), "restantes:", out["steps_ZZV_restantes"])

# 4) paquete (se lleva ensamblado y tipos)
p = st["paquete"]
print("borrar paquete:", borrar(f"pluginpackages({p['id']})", "paquete"))
time.sleep(5)
out["paquete_GET"] = dv.call("GET", f"pluginpackages({p['id']})?$select=name")[0]
out["ensamblado_GET"] = dv.call("GET", f"pluginassemblies({p['ensamblado']})?$select=name")[0]
out["tipos_GET"] = [dv.call("GET", f"plugintypes({t})?$select=name")[0] for t in p["tipos"].values()]
print("tras borrar: paquete", out["paquete_GET"], "ensamblado", out["ensamblado_GET"], "tipos", out["tipos_GET"])

# 5) trazas del spike
n = 0
while True:
    tr = get(dv, "plugintracelogs?$select=plugintracelogid&$filter=startswith(typename,'Zzv.Probe')&$top=200")["value"]
    if not tr:
        break
    for t in tr:
        borrar(f"plugintracelogs({t['plugintracelogid']})", "traza"); n += 1
out["trazas_borradas"] = n; print("trazas borradas:", n)
