"""LIMPIEZA del Spike 4 (NO se ejecuta sola). Ensayo en seco por defecto; con --si-borrar borra.
Borra SOLO lo que creo el Spike 4, localizado por nombre/id exacto y comprobando que pertenece a la solucion SpikesArreglosPago:
  1. msdyn_richtextfile vinculadas (por texto) a los Emails [SPIKE4] y Emails [SPIKE4]/Regarding de las conversaciones del spike (uno por uno: la
     cascada desde la conversacion deja huerfana la fila 'attachment', ver spike 3)
  2. conversaciones del spike 4 ('[SPIKE4] ...' y 'Prueba manual SPIKE4')
  3. la app 'Spikes Arreglos de Pago' (spk_SpikesArreglosPago) y su sitemap
  4. el usuario de aplicacion de correo: se le quitan los roles de prueba y la pertenencia al perfil de columna y se DESACTIVA
  5. el rol 'SPK Oficial Correo'
EJECUTAR ANTES de ../plataforma/s99_limpieza.py (que borra las tablas, el rol 'SPK Lector Cliente', el perfil y la solucion).
No toca los correos de los buzones de Exchange ni la queue Spike, ni los emails [SPIKE3]. Uso:  python3 s99_limpieza.py [--si-borrar]"""
import sys
from lib import *
BORRAR = "--si-borrar" in sys.argv
dv = DvAdmin(); est = cargar()
def hacer(txt, fn=None):
    if BORRAR and fn:
        r = fn(); print(f"  [BORRADO] {txt} -> {r}"); return r
    print(f"  [ensayo ] {txt}")
print("MODO:", "BORRAR" if BORRAR else "ENSAYO EN SECO (agregar --si-borrar para borrar)")
s, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'"); sid = b["value"][0]["solutionid"]
s, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}")
en_sol = {c["objectid"] for c in b["value"]}

# 1) emails
CONVS_ID = {x for x in [est.get("B", {}).get("conversacion"), est.get("D", {}).get("conversacion")] if x}
s, b, _ = dv.call("GET", "spk_conversacions?$select=spk_nombre,spk_conversacionid")
convs = [c for c in b.get("value", []) if c["spk_conversacionid"] in CONVS_ID or (c["spk_nombre"] or "").startswith("[SPIKE4]") or c["spk_nombre"] == "Prueba manual SPIKE4"] if s == 200 else []
conv_ids = {c["spk_conversacionid"] for c in convs}
s, b, _ = dv.call("GET", "emails?$select=activityid,subject,_regardingobjectid_value,createdon,statuscode&$filter=createdon ge 2026-10-08T20:00:00Z&$orderby=createdon asc")
emails = [e for e in b.get("value", []) if "[SPIKE4]" in (e["subject"] or "") or e["_regardingobjectid_value"] in conv_ids]
print(f"\nEmails del spike 4: {len(emails)}")
ids_email = {e["activityid"] for e in emails}
s, b, _ = dv.call("GET", "msdyn_richtextfiles?$select=msdyn_richtextfileid,msdyn_parentid,msdyn_parententityname,createdon&$filter=createdon ge 2026-10-08T20:00:00Z")
rtf = [r for r in b.get("value", []) if (r.get("msdyn_parentid") or "").strip("{}").lower() in {i.lower() for i in ids_email}] if s == 200 else []
otros_rtf = [r for r in (b.get("value", []) if s == 200 else []) if r not in rtf]
print(f"msdyn_richtextfile vinculadas a esos Emails: {len(rtf)} | otras desde las 20:00 UTC (no se borran, revisar a mano): {len(otros_rtf)}")
for r in rtf: hacer(f"msdyn_richtextfile {r['msdyn_richtextfileid']}", lambda r=r: dv.call("DELETE", f"msdyn_richtextfiles({r['msdyn_richtextfileid']})")[0])
for e in emails: hacer(f"Email {e['activityid'][:8]} '{e['subject'][:50]}' ({e['createdon']})", lambda e=e: dv.call("DELETE", f"emails({e['activityid']})")[0])
if BORRAR:
    s, b, _ = dv.call("GET", "emails?$select=activityid,subject&$filter=createdon ge 2026-10-08T20:00:00Z")
    quedan = [e for e in b["value"] if e["activityid"] in ids_email]; print("  releido: Emails del spike 4 que quedan:", len(quedan)); assert not quedan

# 2) conversaciones
print(f"\nConversaciones del spike 4: {len(convs)}")
for c in convs: hacer(f"conversacion {c['spk_nombre']} ({c['spk_conversacionid'][:8]})", lambda c=c: dv.call("DELETE", f"spk_conversacions({c['spk_conversacionid']})")[0])

# 3) app y sitemap
print("\nApp y sitemap")
s, b, _ = dv.call("GET", "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?$select=appmoduleid,name&$filter=uniquename eq 'spk_SpikesArreglosPago'")
for a in b.get("value", []):
    assert a["appmoduleid"] in en_sol, "la app NO esta en la solucion: no se borra"
    hacer(f"app '{a['name']}' ({a['appmoduleid'][:8]})", lambda a=a: dv.call("DELETE", f"appmodules({a['appmoduleid']})")[0])
s, b, _ = dv.call("GET", "sitemaps?$select=sitemapid&$filter=sitemapnameunique eq 'spk_SpikesArreglosPagoSitemap'")
for m in b.get("value", []):
    assert m["sitemapid"] in en_sol, "el sitemap NO esta en la solucion"
    hacer(f"sitemap {m['sitemapid'][:8]}", lambda m=m: dv.call("DELETE", f"sitemaps({m['sitemapid']})")[0])

# 4) usuario de aplicacion
print("\nUsuario de aplicacion de correo")
u = usuario_app(dv)
if u:
    uid = u["systemuserid"]; bu = u["_businessunitid_value"]
    print("  roles actuales:", roles_de_usuario(dv, uid), "| deshabilitado:", u["isdisabled"])
    for nombre in ("SPK Oficial Correo", "SPK Lector Cliente"):
        r = rol_por_nombre(dv, nombre, bu)
        if r and nombre in roles_de_usuario(dv, uid):
            hacer(f"quitar rol '{nombre}' al usuario", lambda r=r: dv.call("DELETE", f"systemusers({uid})/systemuserroles_association/$ref?$id={dv.api}roles({r[0]['roleid']})")[0])
    s, p, _ = dv.call("GET", "fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter=name eq 'SPK Perfil Cedula Lectura'")
    for pr in p.get("value", []):
        s2, mm, _ = dv.call("GET", f"fieldsecurityprofiles({pr['fieldsecurityprofileid']})/systemuserprofiles_association?$select=systemuserid")
        if uid in [x["systemuserid"] for x in mm.get("value", [])]:
            hacer("quitar al usuario del perfil de columna 'SPK Perfil Cedula Lectura'", lambda pr=pr: dv.call("DELETE", f"fieldsecurityprofiles({pr['fieldsecurityprofileid']})/systemuserprofiles_association/$ref?$id={dv.api}systemusers({uid})")[0])
    if not u["isdisabled"]:
        hacer("DESACTIVAR el usuario de aplicacion (isdisabled = true; no se borra: tiene huellas de auditoria en los correos)", lambda: dv.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])
    if BORRAR:
        u2 = usuario_app(dv); print("  releido: deshabilitado =", u2["isdisabled"], "| roles:", roles_de_usuario(dv, uid))
else:
    print("  no existe")

# 5) rol
print("\nRol 'SPK Oficial Correo'")
bu = est.get("bu_raiz") or raiz_bu(dv)
for r in rol_por_nombre(dv, "SPK Oficial Correo", bu):
    assert r["roleid"] in en_sol, "el rol NO esta en la solucion: no se borra"
    hacer(f"rol {r['roleid'][:8]}", lambda r=r: dv.call("DELETE", f"roles({r['roleid']})")[0])
print("\nQuedan para ../plataforma/s99_limpieza.py --si-borrar: tablas spk_*, rol 'SPK Lector Cliente', perfil de columna, solucion y publisher (y con las tablas se va el timeline agregado al formulario).")
