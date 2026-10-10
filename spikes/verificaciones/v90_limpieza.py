"""LIMPIEZA del spike de verificaciones. Borra solo lo creado por el spike (prefijo ZZV / registrado en estado.json) y verifica releyendo contra la linea base."""
from vlib import *

dv = adm()
e = leer_estado()
base = e["linea_base"]
uid = base["usuario"]["id"]
out = {}
PERFIL = base["perfil"][0]["fieldsecurityprofileid"]
jef = get(dv, f"teams?$select=teamid&$filter=_businessunitid_value eq {BU_ID} and name eq 'Arreglos de Pago - Jefatura'")["value"][0]["teamid"]


def borrar(ruta, etiqueta, reintentos=3):
    for i in range(reintentos):
        est, b, _ = dv.call("DELETE", ruta, timeout=600)
        if est in (200, 204, 404):
            return est
        time.sleep(5)
    print("  !! no se pudo borrar", etiqueta, est, str(b)[:200])
    return est

# 1) membresias temporales (por si quedara alguna)
est, b, _ = dv.call("DELETE", f"fieldsecurityprofiles({PERFIL})/systemuserprofiles_association({uid})/$ref"); print("quitar del perfil (idempotente):", est)
est, b, _ = dv.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]}); print("quitar de Jefatura (idempotente):", est)

# 2) filas de prueba (arreglos primero), por registro y por prefijo
orden = {"sanic_carp_tbl_arreglos": 0, "sanic_carp_tbl_conversacions": 1, "sanic_carp_tbl_versionplantillas": 2, "sanic_carp_tbl_clientes": 3}
borradas = []
for clave, conj, rid in sorted(e.get("filas", []), key=lambda f: orden[f[1]]):
    st = borrar(f"{conj}({rid})", clave); borradas.append((clave, st))
restantes = {}
for conj, pk_, nom in (("sanic_carp_tbl_arreglos", "sanic_carp_tbl_arregloid", "sanic_nombre"), ("sanic_carp_tbl_conversacions", "sanic_carp_tbl_conversacionid", "sanic_nombre"),
                       ("sanic_carp_tbl_versionplantillas", "sanic_carp_tbl_versionplantillaid", "sanic_nombre"), ("sanic_carp_tbl_clientes", "sanic_carp_tbl_clienteid", "sanic_nombre")):
    r = get(dv, f"{conj}?$select={pk_}&$filter=startswith({nom},'ZZV')")["value"]
    for x in r:
        borrar(f"{conj}({x[pk_]})", conj)
    restantes[conj] = len(get(dv, f"{conj}?$select={pk_}&$top=5")["value"])
out["filas_borradas_registradas"] = len(borradas)
out["filas_en_tablas_tras_borrar(esperado 0)"] = restantes
print("filas borradas:", len(borradas), "| filas que quedan en las tablas:", restantes)

# 3) steps e imagenes
steps = get(dv, "sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name&$filter=startswith(name,'ZZV-')")["value"]
for s in steps:
    for im in get(dv, f"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid&$filter=_sdkmessageprocessingstepid_value eq {s['sdkmessageprocessingstepid']}")["value"]:
        borrar(f"sdkmessageprocessingstepimages({im['sdkmessageprocessingstepimageid']})", "imagen")
    borrar(f"sdkmessageprocessingsteps({s['sdkmessageprocessingstepid']})", s["name"])
out["steps_borrados"] = len(steps)
out["steps_ZZV_restantes"] = len(get(dv, "sdkmessageprocessingsteps?$select=name&$filter=startswith(name,'ZZV-')")["value"])
print("steps borrados:", len(steps), "restantes:", out["steps_ZZV_restantes"])

# 4) paquete (se lleva ensamblado y tipos)
pid = e["paquete"]["id"]
print("borrar paquete:", borrar(f"pluginpackages({pid})", "paquete"))
time.sleep(5)
out["paquete_GET"] = dv.call("GET", f"pluginpackages({pid})?$select=name")[0]
out["ensamblado_GET"] = dv.call("GET", f"pluginassemblies({e['paquete']['ensamblado']})?$select=name")[0]
out["tipos_GET"] = [dv.call("GET", f"plugintypes({t})?$select=name")[0] for t in e["paquete"]["tipos"].values()]
print("tras borrar: paquete", out["paquete_GET"], "ensamblado", out["ensamblado_GET"], "tipos", out["tipos_GET"])

# 5) usuario de prueba: deshabilitado como al inicio
print("deshabilitar usuario:", dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])

# 6) trazas del spike
n = 0
while True:
    tr = get(dv, "plugintracelogs?$select=plugintracelogid&$filter=startswith(typename,'Zzv.Probe')&$top=200")["value"]
    if not tr: break
    for t in tr:
        borrar(f"plugintracelogs({t['plugintracelogid']})", "traza"); n += 1
out["trazas_borradas"] = n
print("trazas borradas:", n)
