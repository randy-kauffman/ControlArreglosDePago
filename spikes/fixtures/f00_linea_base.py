"""Fixtures: LINEA BASE antes de tocar nada. Solo lee. Detiene el spike si hay filas ZZF- previas, filas pendientes en estado.json
o steps del proyecto registrados. Guarda en estado.json solo GUIDs, conteos y banderas (nunca textos reales ni secretos)."""
import sys
from flib import *

dv = adm()
prev = leer_estado()
if prev.get("filas"):
    sys.exit(f"DETENIDO: estado.json tiene {len(prev['filas'])} filas pendientes de una corrida anterior. Correr f90_limpieza.py y f91_verificar_limpieza.py.")

base = {}
org = get(dv, "organizations?$select=organizationid")["value"][0]
base["organizationid"] = org["organizationid"]
who = get(dv, "WhoAmI")
base["whoami"] = {"UserId": who["UserId"], "BusinessUnitId": who["BusinessUnitId"], "OrganizationId": who["OrganizationId"]}
assert who["OrganizationId"] == org["organizationid"]
base["bu_id"], base["team_def_id"] = BU_ID, TEAM_DEF

sid, comps = componentes_solucion(dv)
base["solucion_id"], base["componentes_solucion"] = sid, comps
print("componentes de la solucion:", len(comps))

# ---- residuales ZZF- (debe dar vacio)
res = residuales_zzf(dv)
base["residuales_zzf"] = res
print("residuales ZZF- por tabla:", {k: len(v) for k, v in res.items()} or "ninguno")
if res:
    volcar("00_linea_base_DETENIDO.json", base)
    sys.exit("DETENIDO: hay filas ZZF- previas (ver evidencia/00_linea_base_DETENIDO.json). No se toca nada.")

# ---- filas existentes en tablas del proyecto
base["filas_tablas_proyecto"] = {}
for t in ("conversacion", "arreglo", "cliente", "versionplantilla", "cuota", "generacionformato", "mapeocelda"):
    _, conj, pk = TABLAS[t]
    base["filas_tablas_proyecto"][t] = len(paginar(dv, f"{conj}?$select={pk}"))
print("filas en tablas del proyecto:", base["filas_tablas_proyecto"])

# ---- steps no administrados sobre las tablas del proyecto (deben ser 0: nada debe frenar las altas)
n_steps, n_plat = 0, 0
PLATAFORMA = {"ObjectModel Implementation", "External plug-in implementation"}
for t in ("conversacion", "arreglo", "cliente", "versionplantilla", "email"):
    for f in get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=primaryobjecttypecode eq '{TABLAS[t][0]}'")["value"]:
        for s in get(dv, f"sdkmessageprocessingsteps?$select=name&$filter=_sdkmessagefilterid_value eq {f['sdkmessagefilterid']} and ismanaged eq false")["value"]:
            if s["name"] in PLATAFORMA:   # pasos internos de la plataforma (se listan con ismanaged=false pero no son de nadie)
                n_plat += 1
            else:
                n_steps += 1
base["steps_plataforma_ignorados"] = n_plat
base["steps_no_administrados_sobre_tablas"] = n_steps
print("steps no administrados sobre conversacion/arreglo/cliente/version/email:", n_steps)
if n_steps:
    volcar("00_linea_base_DETENIDO.json", base)
    sys.exit("DETENIDO: hay steps no administrados sobre las tablas del spike.")

# ---- metadatos que el spike necesita
k = get(dv, "EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Keys?$select=SchemaName,LogicalName,EntityKeyIndexStatus")["value"]
base["claves_conversacion"] = [{"LogicalName": x["LogicalName"], "EntityKeyIndexStatus": x["EntityKeyIndexStatus"]} for x in k]
oc = get(dv, "EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')?$select=IsOptimisticConcurrencyEnabled")
base["conversacion_optimistic_concurrency"] = oc["IsOptimisticConcurrencyEnabled"]
print("claves de conversacion:", base["claves_conversacion"], "| concurrencia optimista:", base["conversacion_optimistic_concurrency"])

# ---- usuarios: SYSTEM, el no administrador (PP_MAIL_*), el conectado
sysu = get(dv, "systemusers?$select=systemuserid&$filter=fullname eq 'SYSTEM'")["value"]
base["system_ids"] = [u["systemuserid"] for u in sysu]
sec = leer_secretos(requeridas=("PP_MAIL_CLIENT_ID", "PP_MAIL_BUZON_1", "PP_MAIL_BUZON_2", "PP_MAIL_BUZON_3", "PP_MAIL_BUZON_4"))
na = get(dv, f"systemusers?$select=systemuserid,isdisabled&$filter=applicationid eq {sec['PP_MAIL_CLIENT_ID']}")["value"]
base["usuario_no_admin_id"] = na[0]["systemuserid"] if na else None
base["usuario_no_admin_isdisabled"] = na[0]["isdisabled"] if na else None
print("SYSTEM encontrados:", len(sysu), "| usuario no admin:", bool(na))

# ---- team por defecto de la BU y colas
base["teams_isdefault_en_bu"] = [t["teamid"] for t in get(dv, f"teams?$select=teamid&$filter=isdefault eq true and _businessunitid_value eq {BU_ID}")["value"]]
base["teams_isdefault_total"] = len(get(dv, "teams?$select=teamid&$filter=isdefault eq true")["value"])
q = paginar(dv, "queues?$select=queueid,emailaddress")
base["colas_total"] = len(q)
base["colas_con_emailaddress"] = [x["queueid"] for x in q if x.get("emailaddress")]
print("teams isdefault en la BU:", len(base["teams_isdefault_en_bu"]), "| colas:", len(q), "con emailaddress:", len(base["colas_con_emailaddress"]))

# ---- variables de entorno del proyecto
ev = paginar(dv, "environmentvariabledefinitions?$select=environmentvariabledefinitionid,schemaname&$filter=startswith(schemaname,'sanic_carp_ev_')")
base["ev_definiciones_proyecto"] = [e["environmentvariabledefinitionid"] for e in ev]
print("definiciones sanic_carp_ev_*:", len(ev))

# ---- correos existentes entre los buzones de prueba (en memoria se compara con los buzones; solo se guardan GUIDs y conteos)
buzones = [sec[f"PP_MAIL_BUZON_{i}"].lower() for i in range(1, 5)]
mails = paginar(dv, "emails?$select=activityid,statuscode,statecode,directioncode,sender,torecipients,messageid,inreplyto,_regardingobjectid_value")
base["emails_total"] = len(mails)
base["emails_por_estado"] = {}
cand = {"entrante": [], "saliente": []}
for m in mails:
    k2 = f"statuscode={m['statuscode']},directioncode={m['directioncode']},statecode={m['statecode']}"
    base["emails_por_estado"][k2] = base["emails_por_estado"].get(k2, 0) + 1
    texto = ((m.get("sender") or "") + ";" + (m.get("torecipients") or "")).lower()
    if not any(b in texto for b in buzones):
        continue
    puntos = (2 if m.get("_regardingobjectid_value") else 0) + (1 if m.get("inreplyto") else 0) + (1 if "<" in (m.get("messageid") or "") else 0)
    if m["statuscode"] == 4 and m["directioncode"] is False:
        cand["entrante"].append((puntos, m["activityid"]))
    elif m["statuscode"] == 3 and m["directioncode"] is True:
        cand["saliente"].append((puntos, m["activityid"]))
base["emails_candidatos_conteo"] = {k3: len(v) for k3, v in cand.items()}
base["emails_candidatos"] = {k3: [g for _, g in sorted(v, key=lambda x: -x[0])[:2]] for k3, v in cand.items()}
base["emails_candidatos_puntos"] = {k3: [p for p, _ in sorted(v, key=lambda x: -x[0])[:2]] for k3, v in cand.items()}
print("emails totales:", len(mails), "| por estado:", base["emails_por_estado"])
print("candidatos entre buzones de prueba (conteo):", base["emails_candidatos_conteo"], "| puntos elegidos:", base["emails_candidatos_puntos"])

guardar_estado({"linea_base": base, "filas": []})
volcar("00_linea_base.json", base)
print("OK: linea base guardada")
