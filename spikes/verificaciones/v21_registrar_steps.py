"""Registra los steps de prueba (fuera de la solucion) y los verifica releyendolos. Idempotente por nombre."""
from vlib import *
dv = adm()
est_ = leer_estado()
TP = est_["paquete"]["tipos"]
OBS, CMD = TP["Zzv.Probe.ZzvObserva"], TP["Zzv.Probe.ZzvComando"]
msgs = {m["name"]: m["sdkmessageid"] for m in get(dv, "sdkmessages?$select=name&$filter=name eq 'Update' or name eq 'SetState' or name eq 'SetStateDynamicEntity' or name eq 'CommitFileBlocksUpload'")["value"]}


def filtro(msg, tabla):
    r = get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=_sdkmessageid_value eq {msgs[msg]} and primaryobjecttypecode eq '{tabla}'")["value"]
    assert len(r) == 1, (msg, tabla, r)
    return r[0]["sdkmessagefilterid"]


PRE_ARR = "statecode,statuscode,sanic_comentarios,sanic_cuentadebitar,sanic_instantaneacedula,sanic_numeroproducto"
PRE_CNV = "statecode,statuscode,sanic_detallesinrespuesta,sanic_asunto"
PRE_VER = "statecode,statuscode,sanic_nombre,sanic_huella,sanic_archivo"
# (nombre, plugintype, mensaje, tabla, etapa, filtro_attrs, etiqueta, preimagen_attrs)
DEFS = []
for t, pre in (("conversacion", PRE_CNV), ("arreglo", PRE_ARR)):
    tb = f"sanic_carp_tbl_{t}"
    DEFS += [
        (f"ZZV-{t}-Update-estado-Pre", OBS, "Update", tb, 20, "statecode,statuscode", f"{t}:upd-estado:pre20", pre),
        (f"ZZV-{t}-Update-estado-Post", OBS, "Update", tb, 40, "statecode,statuscode", f"{t}:upd-estado:post40", pre),
        (f"ZZV-{t}-SetState-Pre", OBS, "SetState", tb, 20, None, f"{t}:setstate:pre20", None),
        (f"ZZV-{t}-SetState-Post", OBS, "SetState", tb, 40, None, f"{t}:setstate:post40", None),
        (f"ZZV-{t}-SetStateDyn-Pre", OBS, "SetStateDynamicEntity", tb, 20, None, f"{t}:setstatedyn:pre20", None),
        (f"ZZV-{t}-SetStateDyn-Post", OBS, "SetStateDynamicEntity", tb, 40, None, f"{t}:setstatedyn:post40", None),
    ]
DEFS += [
    ("ZZV-arreglo-Update-comentarios-Pre", OBS, "Update", "sanic_carp_tbl_arreglo", 20, "sanic_comentarios", "arreglo:upd-comentarios:pre20", PRE_ARR),
    ("ZZV-conversacion-Update-detalle-Pre", OBS, "Update", "sanic_carp_tbl_conversacion", 20, "sanic_detallesinrespuesta", "conversacion:upd-detalle:pre20", PRE_CNV),
    ("ZZV-version-Update-sinfiltro-Post", OBS, "Update", "sanic_carp_tbl_versionplantilla", 40, None, "version:upd-sinfiltro:post40", PRE_VER),
    ("ZZV-version-Update-sinfiltro-Pre", OBS, "Update", "sanic_carp_tbl_versionplantilla", 20, None, "version:upd-sinfiltro:pre20", PRE_VER),
    ("ZZV-cliente-Update-comando-Post", CMD, "Update", "sanic_carp_tbl_cliente", 40, "sanic_nombrecompleto", "cmd", None),
]
ids = est_.get("steps", {})
for nombre, ptid, msg, tabla, etapa, filt, etiq, pre in DEFS:
    ya = get(dv, f"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid&$filter=name eq '{nombre}'")["value"]
    if ya:
        ids[nombre] = ya[0]["sdkmessageprocessingstepid"]; continue
    cuerpo = {"name": nombre, "description": "Spike verificaciones 2026-10-10 (descartable)", "stage": etapa, "mode": 0, "rank": 1, "supporteddeployment": 0, "invocationsource": 0,
              "configuration": etiq,
              "sdkmessageid@odata.bind": f"/sdkmessages({msgs[msg]})", "sdkmessagefilterid@odata.bind": f"/sdkmessagefilters({filtro(msg, tabla)})",
              "plugintypeid@odata.bind": f"/plugintypes({ptid})", "eventhandler_plugintype@odata.bind": f"/plugintypes({ptid})"}
    if filt: cuerpo["filteringattributes"] = filt
    est, b, h = dv.call("POST", "sdkmessageprocessingsteps", cuerpo)
    print(nombre, est, b if est >= 300 else "")
    assert est in (201, 204)
    sid = dv.id_creado(h); ids[nombre] = sid
    if pre:
        est, b, h = dv.call("POST", "sdkmessageprocessingstepimages", {"name": "PreImage", "entityalias": "PreImage", "imagetype": 0, "messagepropertyname": "Target", "attributes": pre,
                                                                       "sdkmessageprocessingstepid@odata.bind": f"/sdkmessageprocessingsteps({sid})"})
        print("  imagen", est, b if est >= 300 else ""); assert est in (201, 204)
est_["steps"] = ids; guardar_estado(est_)

# verificacion releyendo
filas = get(dv, "sdkmessageprocessingsteps?$select=name,stage,mode,filteringattributes,configuration,statecode&$expand=sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode)&$filter=startswith(name,'ZZV-')")["value"]
for f in sorted(filas, key=lambda x: x["name"]):
    print(f"{f['name']:45} {f['sdkmessageid']['name']:22} {f['sdkmessagefilterid']['primaryobjecttypecode'] if f.get('sdkmessagefilterid') else None} stage={f['stage']} filtro={f.get('filteringattributes')} state={f['statecode']}")
imgs = get(dv, "sdkmessageprocessingstepimages?$select=name,attributes,imagetype&$filter=_sdkmessageprocessingstepid_value ne null&$top=200")["value"]
print("n steps ZZV:", len(filas), "(esperado", len(DEFS), ")")
assert len(filas) == len(DEFS)
