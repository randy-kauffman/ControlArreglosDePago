"""Dobles: CONTROL DE ANONIMIZACION (adaptado de spikes/fixtures/f20). Recorre salida/*.json (y, para secretos, todos los archivos de texto de spikes/dobles) y falla si encuentra:
- una direccion de correo con dominio distinto de example.com,
- la URL de la organizacion o su host,
- cualquiera de PP_MAIL_BUZON_1..4, el dominio del tenant (derivado de los buzones), el id de tenant, los id de las aplicaciones o los secretos,
- los GUID reales de usuarios, teams, BU, colas, organizacion y correos exportados,
- los nombres, correos y nombres de usuario reales de usuarios, teams, BU y colas.
Los valores se comparan EN MEMORIA y solo se informan CONTEOS por categoria, nunca el valor."""
import re
import sys
from dlib import *

sec = leer_secretos()
dv = adm()
base = leer_estado()["linea_base"]

salida = sorted(f for f in os.listdir(SALIDA) if f.endswith(".json"))
textos = {f: open(os.path.join(SALIDA, f), encoding="utf-8").read() for f in salida}
todo = "\n".join(textos.values())
todo_l = todo.lower()


def cuenta(valor, texto_l=None):
    v = valor.lower()
    return (texto_l if texto_l is not None else todo_l).count(v) if v else 0


res = {"archivos_revisados": len(salida)}

# 1) correos
correos = re.findall(r"[A-Za-z0-9._%+\-']+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}", todo)
res["correos_encontrados"] = len(correos)
res["correos_con_dominio_distinto_de_example.com"] = sum(1 for c in correos if not c.lower().endswith("@example.com"))

# 2) URL de la organizacion y host
host = ENTORNO.split("//")[1]
res["url_de_la_organizacion"] = cuenta(ENTORNO) + cuenta(host) + cuenta(host.split(".")[0])
res["cualquier_crm.dynamics.com"] = cuenta("crm.dynamics.com")

# 3) buzones, dominio del tenant, ids de tenant/app, secretos
buz = [sec[f"PP_MAIL_BUZON_{i}"] for i in range(1, 5)]
dominios = {b.split("@")[1] for b in buz if "@" in b}
res["buzones_PP_MAIL_BUZON_1a4"] = sum(cuenta(b) for b in buz)
res["partes_locales_de_los_buzones"] = sum(cuenta(b.split("@")[0]) for b in buz if len(b.split("@")[0]) >= 4)
res["dominio_del_tenant"] = sum(cuenta(d) for d in dominios) + sum(cuenta(d.split(".")[0]) for d in dominios if len(d.split(".")[0]) >= 4)
res["ids_tenant_y_aplicaciones"] = sum(cuenta(sec[k]) for k in ("PP_TENANT_ID", "PP_CLIENT_ID", "PP_MAIL_CLIENT_ID", "PP_MAIL_TENANT_ID") if k in sec)

# 4) GUID reales (consulta de solo lectura; los valores no se imprimen ni guardan)
reales = {}
for ent, conj, pk in (("systemuser", "systemusers", "systemuserid"), ("team", "teams", "teamid"), ("businessunit", "businessunits", "businessunitid"),
                      ("queue", "queues", "queueid"), ("organization", "organizations", "organizationid")):
    reales[ent] = [x[pk] for x in paginar(dv, f"{conj}?$select={pk}")]
reales["usuarios_del_spike_y_system"] = [base["whoami"]["UserId"], base["usuario_no_admin_id"]] + base["system_ids"]
reales["bu_team_org_de_la_linea_base"] = [base["bu_id"], base["team_def_id"], base["organizationid"], base["whoami"]["BusinessUnitId"]]
reales["mailbox_y_roles"] = [x["mailboxid"] for x in paginar(dv, "mailboxes?$select=mailboxid")] + [x["roleid"] for x in paginar(dv, "roles?$select=roleid")]
res["guid_reales_por_categoria"] = {k: sum(cuenta(g) for g in v) for k, v in reales.items()}

# 5) nombres y correos reales de usuarios, teams, BU y colas
nombres = {"systemuser": set(), "team": set(), "businessunit": set(), "queue": set(), "correos_usuarios_y_colas": set()}
for u in paginar(dv, "systemusers?$select=fullname,domainname,internalemailaddress,firstname,lastname"):
    for k in ("fullname", "firstname", "lastname"):
        if u.get(k):
            nombres["systemuser"].add(u[k])
    for k in ("domainname", "internalemailaddress"):
        if u.get(k):
            nombres["correos_usuarios_y_colas"].add(u[k])
for t in paginar(dv, "teams?$select=name"):
    nombres["team"].add(t["name"])
for b in paginar(dv, "businessunits?$select=name"):
    nombres["businessunit"].add(b["name"])
for q in paginar(dv, "queues?$select=name,emailaddress"):
    nombres["queue"].add(q["name"])
    if q.get("emailaddress"):
        nombres["correos_usuarios_y_colas"].add(q["emailaddress"])
for m in paginar(dv, "mailboxes?$select=name,emailaddress"):
    for k in ("name", "emailaddress"):
        if m.get(k):
            nombres["correos_usuarios_y_colas"].add(m[k])
for r in paginar(dv, "roles?$select=name"):
    nombres["businessunit"].add(r["name"])
nombres["organizacion"] = {o["name"] for o in paginar(dv, "organizations?$select=name") if o.get("name")}
# se excluyen los valores que el diseno conserva a proposito (SYSTEM) y los que empiezan con ZZF
# Fragmentos de nombres de usuarios INTEGRADOS de la plataforma ("Microsoft Dynamics 365 for Outlook"): coinciden con nombres de tipo .NET
# (Microsoft.Xrm.Sdk...) y con etiquetas tecnicas (formatted values de incomingemaildeliverymethod). No son datos de nadie.
# "User" (2026-10-15): etiqueta de plataforma, el tipo de principal en ErrorDetails de las fallas de privilegio (22-privilegios.json, valor 'user'); coincide con un usuario cuyo nombre es "User".
excluir = {"SYSTEM", "Microsoft", "for Outlook", "User"}


def hojas(nodo, out):
    """Todas las cadenas que son VALORES (no claves) del JSON: las claves son nombres de columna y daban falsos positivos (p. ej. 'isintegrationuser')."""
    if isinstance(nodo, dict):
        for v in nodo.values():
            hojas(v, out)
    elif isinstance(nodo, list):
        for v in nodo:
            hojas(v, out)
    elif isinstance(nodo, str):
        out.append(nodo.lower())
    return out


valores_l = []
for _f in salida:
    hojas(json.loads(textos[_f]), valores_l)
bloque_valores = "\n".join(valores_l)
res["cadenas_valor_revisadas"] = len(valores_l)


def en_json(valor):
    """Coincidencia como valor completo o, si el valor es largo, como subcadena de algun valor (las claves del JSON no cuentan)."""
    v = valor.lower()
    if len(v) >= 8:
        return bloque_valores.count(v)
    return sum(1 for x in valores_l if x == v)


res["nombres_reales_por_categoria"] = {}
for k, vals in nombres.items():
    vals = {v for v in vals if v and v not in excluir and not v.startswith("ZZF") and len(v) >= 3}
    res["nombres_reales_por_categoria"][k] = {"valores_comparados": len(vals), "coincidencias": sum(en_json(v) for v in vals)}
res["nombres_excluidos_por_ser_etiquetas_de_plataforma"] = sorted(excluir)

# 6) secretos en CUALQUIER archivo de texto del spike (incluye estado, evidencia, logs y los json de obj/bin)
secretos = [sec[k] for k in ("PP_CLIENT_SECRET", "PP_MAIL_CLIENT_SECRET") if sec.get(k)]
n_arch, n_sec = 0, 0
for raiz, dirs, archivos in os.walk(AQUI):
    dirs[:] = [d for d in dirs if d != "__pycache__"]
    for a in archivos:
        if a.endswith((".dll", ".pdb", ".exe", ".so", ".nupkg", ".cache", ".pyc")):
            continue
        try:
            t = open(os.path.join(raiz, a), encoding="utf-8").read()
        except (UnicodeDecodeError, OSError):
            continue
        n_arch += 1
        n_sec += sum(t.count(s) for s in secretos)
res["secretos_en_archivos_del_spike"] = {"archivos_de_texto_revisados": n_arch, "apariciones": n_sec}

def total(r):
    t = r["correos_con_dominio_distinto_de_example.com"] + r["url_de_la_organizacion"] + r["cualquier_crm.dynamics.com"] + r["buzones_PP_MAIL_BUZON_1a4"]
    t += r["partes_locales_de_los_buzones"] + r["dominio_del_tenant"] + r["ids_tenant_y_aplicaciones"] + sum(r["guid_reales_por_categoria"].values())
    t += sum(v["coincidencias"] for v in r["nombres_reales_por_categoria"].values()) + r["secretos_en_archivos_del_spike"]["apariciones"]
    return t

res["total_hallazgos"] = total(res)
res["control_OK"] = res["total_hallazgos"] == 0
volcar("20_control_anonimizacion.json", res)
print(json.dumps(res, indent=1))
sys.exit(0 if res["control_OK"] else 1)
