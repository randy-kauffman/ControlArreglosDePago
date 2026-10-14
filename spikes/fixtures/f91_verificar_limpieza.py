"""Fixtures: VERIFICA la limpieza RELEYENDO cada GUID registrado (debe dar 'no existe', HTTP 404), busca ZZF- residual en todas las tablas que toca el spike
y compara con la linea base (solucion, correos, tablas del proyecto). Guarda la evidencia en evidencia/91_limpieza_verificada.json."""
import sys
from flib import *

dv = adm()
st = leer_estado()
base = st["linea_base"]
CONJ = {v[0]: v[1] for v in TABLAS.values()}
r = {}

relectura = []
for clave, tabla, rid in st.get("filas", []):
    est, _, _ = dv.call("GET", f"{CONJ[tabla]}({rid})?$select=createdon")
    relectura.append({"clave": clave, "tabla": tabla, "guid": rid, "http": est, "no_existe": est == 404})
r["filas_registradas"] = len(relectura)
r["filas_que_no_existen_(404)"] = sum(1 for x in relectura if x["no_existe"])
r["filas_que_AUN_existen"] = [x for x in relectura if not x["no_existe"]]
r["relectura_por_guid"] = relectura

r["residuales_ZZF_por_tabla"] = {k: len(v) for k, v in residuales_zzf(dv).items()}
r["filas_en_tablas_del_proyecto_ahora"] = {}
for t in ("conversacion", "arreglo", "cliente", "versionplantilla", "cuota", "generacionformato", "mapeocelda"):
    r["filas_en_tablas_del_proyecto_ahora"][t] = len(paginar(dv, f"{TABLAS[t][1]}?$select={TABLAS[t][2]}"))
r["filas_en_tablas_del_proyecto_antes"] = base["filas_tablas_proyecto"]

sid, comps = componentes_solucion(dv)
antes = {(c["componenttype"], c["objectid"]) for c in base["componentes_solucion"]}
ahora = {(c["componenttype"], c["objectid"]) for c in comps}
r["solucion_componentes_antes"], r["solucion_componentes_ahora"] = len(antes), len(ahora)
r["solucion_componentes_nuevos"] = sorted(map(list, ahora - antes))
r["solucion_componentes_que_faltan"] = sorted(map(list, antes - ahora))
r["emails_total_antes"] = base["emails_total"]
r["emails_total_ahora"] = len(paginar(dv, "emails?$select=activityid"))
r["environmentvariabledefinitions_sanic_zzf"] = len(paginar(dv, "environmentvariabledefinitions?$select=environmentvariabledefinitionid&$filter=startswith(schemaname,'sanic_zzf')"))

ok = (r["filas_registradas"] == r["filas_que_no_existen_(404)"] and not r["residuales_ZZF_por_tabla"]
      and not r["solucion_componentes_nuevos"] and not r["solucion_componentes_que_faltan"] and r["emails_total_antes"] == r["emails_total_ahora"]
      and all(v == 0 for v in r["filas_en_tablas_del_proyecto_ahora"].values()) and r["environmentvariabledefinitions_sanic_zzf"] == 0)
r["limpieza_verificada_OK"] = ok
volcar("91_limpieza_verificada.json", r)
resumen = {k: v for k, v in r.items() if k != "relectura_por_guid"}
print(json.dumps(resumen, indent=1))
sys.exit(0 if ok else 1)
