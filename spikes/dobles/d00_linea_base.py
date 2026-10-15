"""Dobles: LINEA BASE antes de tocar nada. Solo lee. Detiene el spike si: la organizacion no es la del exportador, hay filas ZZF- previas, hay pendientes en estado.json,
hay algo 'Sonda' previo (paquetes, ensamblados, tipos, steps, Custom API) o hay steps no administrados sobre las tablas propias.
Guarda en estado.json solo GUIDs, conteos y banderas (nunca textos reales ni secretos)."""
import sys
from dlib import *

dv = adm()
prev = leer_estado()
if prev.get("filas") or prev.get("registros"):
    sys.exit(f"DETENIDO: estado.json tiene pendientes de una corrida anterior. Correr d90_limpieza.py y d91_verificar_limpieza.py.")

base = {"fecha": time.strftime("%Y-%m-%d %H:%M:%S")}
org = get(dv, "organizations?$select=organizationid,plugintracelogsetting")["value"][0]
base["organizationid"] = org["organizationid"]
base["plugintracelogsetting"] = org["plugintracelogsetting"]   # 0 apagada, 1 solo excepciones, 2 todas. Solo se LEE
who = get(dv, "WhoAmI")
base["whoami"] = {"UserId": who["UserId"], "BusinessUnitId": who["BusinessUnitId"], "OrganizationId": who["OrganizationId"]}
assert who["OrganizationId"] == org["organizationid"]
org_exportador = json.load(open(ORG_FIXTURES, encoding="utf-8"))["organizationid"]
base["organizacion_igual_a_la_del_exportador"] = org["organizationid"] == org_exportador
print("entorno:", dv.org == ENTORNO, "| organizacion igual a la linea base del exportador:", base["organizacion_igual_a_la_del_exportador"], "| plugintracelogsetting:", org["plugintracelogsetting"])
if not base["organizacion_igual_a_la_del_exportador"]:
    volcar("00_linea_base_DETENIDO.json", base)
    sys.exit("DETENIDO: la organizacion no coincide con la de la linea base del exportador.")
base["bu_id"], base["team_def_id"] = BU_ID, TEAM_DEF

# ---- solucion del proyecto (no se toca) y solucion descartable de los spikes
sol = get(dv, "solutions?$select=solutionid,uniquename&$filter=uniquename eq 'sanic_carp_sol_controlarreglospago'")["value"][0]["solutionid"]
comps = paginar(dv, f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {sol}")
base["solucion_proyecto_id"] = sol
base["solucion_proyecto_componentes"] = sorted([[c["componenttype"], c["objectid"]] for c in comps])
print("componentes de la solucion del proyecto:", len(comps))
spk = get(dv, f"solutions?$select=solutionid,uniquename,ismanaged&$filter=uniquename eq '{SOLUCION_SPK}'")["value"]
base["solucion_spk_existe"] = bool(spk)
if spk:
    base["solucion_spk_id"] = spk[0]["solutionid"]
    sc = paginar(dv, f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {spk[0]['solutionid']}")
    base["solucion_spk_componentes"] = sorted([[c["componenttype"], c["objectid"]] for c in sc])
    print(f"solucion {SOLUCION_SPK}: existe, {len(sc)} componentes")
else:
    print(f"solucion {SOLUCION_SPK}: NO existe (el paquete se registraria sin solucion)")

# ---- residuales ZZF- (debe dar vacio)
res = residuales_zzf(dv)
base["residuales_zzf"] = res
print("residuales ZZF- por tabla:", {k: len(v) for k, v in res.items()} or "ninguno")
sond = residuales_sonda(dv)
base["residuales_sonda"] = sond
print("residuales Sonda:", {k: len(v) for k, v in sond.items()} or "ninguno")
if res or sond:
    volcar("00_linea_base_DETENIDO.json", base)
    sys.exit("DETENIDO: hay residuales (ver evidencia/00_linea_base_DETENIDO.json). No se toca nada.")

# ---- filas existentes en tablas del proyecto
base["filas_tablas_proyecto"] = {}
for t in PROPIAS:
    base["filas_tablas_proyecto"][t] = len(paginar(dv, f"{TABLAS[t][1]}?$select={TABLAS[t][2]}"))
print("filas en tablas del proyecto:", base["filas_tablas_proyecto"])

# ---- steps no administrados sobre las tablas del proyecto (deben ser 0)
PLATAFORMA = {"ObjectModel Implementation", "External plug-in implementation"}
n_steps, n_plat, detalle = 0, 0, {}
for t in PROPIAS + ("email",):
    for f in get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=primaryobjecttypecode eq '{TABLAS[t][0]}'")["value"]:
        for s in get(dv, f"sdkmessageprocessingsteps?$select=name&$filter=_sdkmessagefilterid_value eq {f['sdkmessagefilterid']} and ismanaged eq false")["value"]:
            if s["name"] in PLATAFORMA:
                n_plat += 1
            else:
                n_steps += 1
                detalle[t] = detalle.get(t, 0) + 1
base["steps_plataforma_ignorados"] = n_plat
base["steps_no_administrados_sobre_tablas"] = n_steps
print("steps no administrados sobre tablas propias y email:", n_steps, detalle or "")
if n_steps:
    volcar("00_linea_base_DETENIDO.json", base)
    sys.exit("DETENIDO: hay steps no administrados sobre las tablas del spike.")

# ---- inventario de plugins para comparar al final
base["pluginpackages_total"] = len(paginar(dv, "pluginpackages?$select=pluginpackageid"))
base["pluginassemblies_total"] = len(paginar(dv, "pluginassemblies?$select=pluginassemblyid"))
base["steps_no_administrados_total"] = len(paginar(dv, "sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid&$filter=ismanaged eq false"))
base["customapis_total"] = len(paginar(dv, "customapis?$select=customapiid"))
print("paquetes:", base["pluginpackages_total"], "| ensamblados:", base["pluginassemblies_total"], "| steps no administrados:", base["steps_no_administrados_total"], "| custom api:", base["customapis_total"])

# ---- usuarios
sysu = get(dv, "systemusers?$select=systemuserid&$filter=fullname eq 'SYSTEM'")["value"]
base["system_ids"] = [u["systemuserid"] for u in sysu]
sec = leer_secretos(requeridas=("PP_MAIL_CLIENT_ID",))
na = get(dv, f"systemusers?$select=systemuserid,isdisabled,accessmode&$filter=applicationid eq {sec['PP_MAIL_CLIENT_ID']}")["value"]
base["usuario_no_admin_id"] = na[0]["systemuserid"] if na else None
base["usuario_no_admin_isdisabled"] = na[0]["isdisabled"] if na else None
base["usuario_no_admin_accessmode"] = na[0]["accessmode"] if na else None
print("usuario de aplicacion no admin encontrado:", bool(na), "| deshabilitado:", base["usuario_no_admin_isdisabled"])
if na:
    roles = paginar(dv, f"systemuserrolescollection?$select=roleid&$filter=systemuserid eq {na[0]['systemuserid']}") if False else []
    r = get(dv, f"systemusers({na[0]['systemuserid']})?$expand=systemuserroles_association($select=name)")
    base["usuario_no_admin_roles_conteo"] = len(r.get("systemuserroles_association", []))
    print("roles del usuario no admin (conteo):", base["usuario_no_admin_roles_conteo"])

# ---- correos y variables de entorno
base["emails_total"] = len(paginar(dv, "emails?$select=activityid"))
base["ev_zzf"] = len(paginar(dv, "environmentvariabledefinitions?$select=environmentvariabledefinitionid&$filter=startswith(schemaname,'sanic_zzf')"))
print("emails totales:", base["emails_total"], "| definiciones sanic_zzf:", base["ev_zzf"])

# ---- colas/team por defecto de la BU
base["teams_isdefault_en_bu"] = [t["teamid"] for t in get(dv, f"teams?$select=teamid&$filter=isdefault eq true and _businessunitid_value eq {BU_ID}")["value"]]
assert base["teams_isdefault_en_bu"] == [TEAM_DEF], "el team por defecto de la BU no es el esperado"

guardar_estado({"linea_base": base, "filas": [], "registros": []})
volcar("00_linea_base.json", base)
print("OK: linea base guardada")
