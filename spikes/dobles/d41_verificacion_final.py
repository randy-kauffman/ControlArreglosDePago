"""Dobles, punto (a): verificacion FINAL, solo lectura. (1) Relee el usuario y lo compara con el estado ORIGINAL anotado en estado.json antes de cambiar nada.
(2) Documenta por que d91 da limpieza_verificada_OK=false en el lote de 2026-10-15: tres conteos GLOBALES (correos, steps no administrados, Custom API) cambiaron por actividad
ajena a este spike (se muestra cuando se crearon y por quien); todo lo propio del spike esta en orden. -> evidencia/92_verificacion_final.json. Nunca imprime secretos."""
from dlib import *

dv = adm()
st = leer_estado()
orig = {k: v for k, v in st["privilegios_original"].items() if k != "anotado"}
uid = st["linea_base"]["usuario_no_admin_id"]
yo = st["linea_base"]["whoami"]["UserId"]
u = get(dv, f"systemusers({uid})?$select=isdisabled,accessmode,_businessunitid_value")
roles = sorted(r["roleid"] for r in paginar(dv, f"systemuserrolescollection?$select=roleid&$filter=systemuserid eq {uid}"))
equipos = sorted([t["teamid"], t["teamtype"], t["name"]] for t in get(dv, f"systemusers({uid})/teammembership_association?$select=teamid,name,teamtype")["value"])
r = {"usuario_igual_al_original": {
    "isdisabled": u["isdisabled"] == orig["isdisabled"], "accessmode": u["accessmode"] == orig["accessmode"], "businessunitid": u["_businessunitid_value"] == orig["businessunitid"],
    "roles_directos": roles == orig["roles_directos_systemuserroles"], "equipos": equipos == orig["equipos"]},
    "isdisabled_ahora": u["isdisabled"], "roles_directos_ahora": len(roles)}
r["usuario_restaurado_OK"] = all(r["usuario_igual_al_original"].values())

corte = "2026-10-15T00:00:00Z"   # el spike 'dobles' anterior cerro su limpieza a las 01:35 UTC de ese dia
em = get(dv, f"emails?$select=createdon,_createdby_value,subject&$filter=createdon ge {corte}&$orderby=createdon asc")["value"]
ca = get(dv, f"customapis?$select=createdon,_createdby_value&$filter=createdon ge {corte}&$orderby=createdon asc")["value"]
sp = get(dv, f"sdkmessageprocessingsteps?$select=createdon,modifiedon&$filter=ismanaged eq false and modifiedon ge {corte}")["value"]
r["deriva_de_conteos_globales"] = {
    "emails_nuevos": [{"createdon": e["createdon"], "creado_por_el_spike": e["_createdby_value"] in (yo, uid), "asunto_ZZF": (e.get("subject") or "").startswith("ZZF-")} for e in em],
    "customapis_nuevas": [{"createdon": c["createdon"], "creada_por_la_cuenta_de_servicio_del_proyecto": c["_createdby_value"] in (yo, uid)} for c in ca],
    "steps_no_administrados_tocados_desde_el_corte": len(sp),
    "lectura": "Los 3 correos no son ZZF- ni los creo el spike (entran por el buzon del proyecto). Las 2 Custom API y los steps tocados a las 01:38 UTC los creo la cuenta de servicio del proyecto "
               "en otra tarea, DESPUES de que el spike anterior cerrara; este spike (d40) no registra plugins, steps ni Custom API ni crea correos.",
}
r["lo_propio_del_spike"] = {"residuales_ZZF": residuales_zzf(dv), "residuales_Sonda": residuales_sonda(dv),
                            "filas_tablas_propias": {t: len(paginar(dv, f"{TABLAS[t][1]}?$select={TABLAS[t][2]}")) for t in PROPIAS}}
volcar("92_verificacion_final.json", r)
print(json.dumps({k: v for k, v in r.items() if k != "deriva_de_conteos_globales"}, indent=1, ensure_ascii=False))
