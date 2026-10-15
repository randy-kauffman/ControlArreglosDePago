"""Dobles: VERIFICA la limpieza RELEYENDO cada GUID registrado (filas y registros de plugins; debe dar 'no existe', HTTP 404), busca ZZF- y Sonda residuales
en todas las tablas que toca el spike y compara con la linea base (solucion del proyecto, correos, tablas del proyecto, paquetes, ensamblados, steps, Custom API).
Guarda la evidencia en evidencia/91_limpieza_verificada.json. El spike NO termina en verde si algo de esto falla."""
import sys
from dlib import *

dv = adm()
st = leer_estado()
base = st["linea_base"]
CONJ = {v[0]: v[1] for v in TABLAS.values()}
r = {}

relectura = []
for clave, tabla, rid in st.get("filas", []):
    est, _, _ = dv.call("GET", f"{CONJ[tabla]}({rid})?$select=createdon")
    relectura.append({"clave": clave, "tabla": tabla, "guid": rid, "http": est, "no_existe": est == 404})
for clave, conjunto, rid in st.get("registros", []):
    est, _, _ = dv.call("GET", f"{conjunto}({rid})")
    relectura.append({"clave": clave, "tabla": conjunto, "guid": rid, "http": est, "no_existe": est == 404})
r["guid_registrados_total"] = len(relectura)
r["guid_registrados_filas"] = len(st.get("filas", []))
r["guid_registrados_plugins"] = len(st.get("registros", []))
r["guid_que_no_existen_(404)"] = sum(1 for x in relectura if x["no_existe"])
r["guid_que_AUN_existen"] = [x for x in relectura if not x["no_existe"]]
r["relectura_por_guid"] = relectura

r["residuales_ZZF_por_tabla"] = {k: len(v) for k, v in residuales_zzf(dv).items()}
r["residuales_Sonda"] = {k: len(v) for k, v in residuales_sonda(dv).items()}
r["filas_en_tablas_del_proyecto_ahora"] = {}
for t in PROPIAS:
    r["filas_en_tablas_del_proyecto_ahora"][t] = len(paginar(dv, f"{TABLAS[t][1]}?$select={TABLAS[t][2]}"))
r["filas_en_tablas_del_proyecto_antes"] = base["filas_tablas_proyecto"]

comps = paginar(dv, f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {base['solucion_proyecto_id']}")
antes = {tuple(c) for c in base["solucion_proyecto_componentes"]}
ahora = {(c["componenttype"], c["objectid"]) for c in comps}
r["solucion_proyecto_componentes_antes"], r["solucion_proyecto_componentes_ahora"] = len(antes), len(ahora)
r["solucion_proyecto_componentes_nuevos"] = sorted(map(list, ahora - antes))
r["solucion_proyecto_componentes_que_faltan"] = sorted(map(list, antes - ahora))
r["emails_total_antes"], r["emails_total_ahora"] = base["emails_total"], len(paginar(dv, "emails?$select=activityid"))
r["environmentvariabledefinitions_sanic_zzf"] = len(paginar(dv, "environmentvariabledefinitions?$select=environmentvariabledefinitionid&$filter=startswith(schemaname,'sanic_zzf')"))
r["pluginpackages_antes_ahora"] = [base["pluginpackages_total"], len(paginar(dv, "pluginpackages?$select=pluginpackageid"))]
r["pluginassemblies_antes_ahora"] = [base["pluginassemblies_total"], len(paginar(dv, "pluginassemblies?$select=pluginassemblyid"))]
r["steps_no_administrados_antes_ahora"] = [base["steps_no_administrados_total"], len(paginar(dv, "sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid&$filter=ismanaged eq false"))]
r["customapis_antes_ahora"] = [base["customapis_total"], len(paginar(dv, "customapis?$select=customapiid"))]
r["plugintypes_Sonda"] = len(paginar(dv, "plugintypes?$select=plugintypeid&$filter=contains(typename,'Sonda')"))

ok = (r["guid_registrados_total"] == r["guid_que_no_existen_(404)"] and not r["residuales_ZZF_por_tabla"] and not r["residuales_Sonda"] and r["plugintypes_Sonda"] == 0
      and not r["solucion_proyecto_componentes_nuevos"] and not r["solucion_proyecto_componentes_que_faltan"] and r["emails_total_antes"] == r["emails_total_ahora"]
      and all(v == 0 for v in r["filas_en_tablas_del_proyecto_ahora"].values()) and r["environmentvariabledefinitions_sanic_zzf"] == 0
      and r["pluginpackages_antes_ahora"][0] == r["pluginpackages_antes_ahora"][1] and r["pluginassemblies_antes_ahora"][0] == r["pluginassemblies_antes_ahora"][1]
      and r["steps_no_administrados_antes_ahora"][0] == r["steps_no_administrados_antes_ahora"][1] and r["customapis_antes_ahora"][0] == r["customapis_antes_ahora"][1])
r["limpieza_verificada_OK"] = ok
volcar("91_limpieza_verificada.json", r)
resumen = {k: v for k, v in r.items() if k != "relectura_por_guid"}
print(json.dumps(resumen, indent=1))
sys.exit(0 if ok else 1)
