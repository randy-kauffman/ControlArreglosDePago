"""V-4: (1) sdkmessagefilter de CommitFileBlocksUpload sobre sanic_carp_tbl_versionplantilla; (2) intento de registrar un step; (3) que mensajes ve un plugin
cuando se sube un archivo a sanic_archivo (InitializeFileBlocksUpload/UploadBlock/CommitFileBlocksUpload y PATCH directo). Datos ficticios."""
import base64
from vlib import *

dv = adm()
est_ = leer_estado()
OBS = est_["paquete"]["tipos"]["Zzv.Probe.ZzvObserva"]
TABLA, VER = LN["versionplantilla"], ES["versionplantilla"]
res = {}
FILTRO_NONE = "d0e93b42-8d0e-4d4f-a9cc-23b3623ba818"
MSG = {m["name"]: m["sdkmessageid"] for m in get(dv, "sdkmessages?$select=name&$filter=name eq 'CommitFileBlocksUpload' or name eq 'InitializeFileBlocksUpload' or name eq 'UploadBlock'")["value"]}

# (1) filtros
for n, mid in MSG.items():
    fl = get(dv, f"sdkmessagefilters?$select=primaryobjecttypecode,iscustomprocessingstepallowed,availability&$filter=_sdkmessageid_value eq {mid}")["value"]
    res[f"filtros_{n}"] = [{k: f[k] for k in ("primaryobjecttypecode", "iscustomprocessingstepallowed", "availability")} for f in fl]
    res[f"filtro_para_versionplantilla_{n}"] = [f for f in fl if f["primaryobjecttypecode"] == TABLA]
    print(n, "filtros:", res[f"filtros_{n}"], "| para la version:", len(res[f"filtro_para_versionplantilla_{n}"]))

# (2) intento de registrar el step
ids = []
def intento(nombre, con_filtro):
    cuerpo = {"name": nombre, "description": "Spike V-4", "stage": 20, "mode": 0, "rank": 1, "supporteddeployment": 0, "invocationsource": 0, "configuration": "v4:commit:pre20",
              "sdkmessageid@odata.bind": f"/sdkmessages({MSG['CommitFileBlocksUpload']})", "plugintypeid@odata.bind": f"/plugintypes({OBS})", "eventhandler_plugintype@odata.bind": f"/plugintypes({OBS})"}
    if con_filtro:
        cuerpo["sdkmessagefilterid@odata.bind"] = f"/sdkmessagefilters({FILTRO_NONE})"
    est, b, h = dv.call("POST", "sdkmessageprocessingsteps", cuerpo)
    r = {"http": est, "error": (b.get("error") or "")[:500] if isinstance(b, dict) else None}
    if est in (201, 204):
        ids.append(dv.id_creado(h)); r["id"] = ids[-1]
    return r
res["registrar_step_commit_con_filtro_none"] = intento("ZZV-v4-commit-con-filtro-none", True)
res["registrar_step_commit_sin_filtro"] = intento("ZZV-v4-commit-sin-filtro", False)
print("registrar con filtro 'none':", res["registrar_step_commit_con_filtro_none"])
print("registrar sin filtro:", res["registrar_step_commit_sin_filtro"])
for i in ids:
    leido = dv.call("GET", f"sdkmessageprocessingsteps({i})?$select=name,stage,statecode")[1]
    print("  creado, releido:", {k: leido.get(k) for k in ("name", "stage", "statecode")})
    res.setdefault("steps_creados_releidos", []).append({k: leido.get(k) for k in ("name", "stage", "statecode")})


C = Corrida(dv, '60_v4_archivo_trazas.json')

# (3) subidas
ver = crear_version(dv, "v4")
tgt = {"@odata.type": "Microsoft.Dynamics.CRM." + TABLA, "sanic_carp_tbl_versionplantillaid": ver}
datos = b"ZZV-contenido-de-prueba\n" * 50
b64 = lambda x: base64.b64encode(x).decode()

def bloques():
    out = {}
    e, b, _ = dv.call("POST", "InitializeFileBlocksUpload", {"Target": tgt, "FileAttributeName": "sanic_archivo", "FileName": "zzv.txt"})
    out["init"] = (e, "token" if e == 200 else b); assert e == 200, b
    tok = b["FileContinuationToken"]
    bid = b64(b"zzv-block-0001")
    e, b, _ = dv.call("POST", "UploadBlock", {"BlockId": bid, "BlockData": b64(datos), "FileContinuationToken": tok})
    out["upload"] = e; assert e in (200, 204), b
    e, b, _ = dv.call("POST", "CommitFileBlocksUpload", {"FileName": "zzv.txt", "MimeType": "text/plain", "BlockList": [bid], "FileContinuationToken": tok})
    out["commit"] = (e, {k: b.get(k) for k in ("FileSizeInBytes",)} if e == 200 else b)
    return out

C.paso("S1 subida por bloques (Initialize/UploadBlock/Commit) como administrador", lambda: (res.__setitem__("subida_por_bloques", bloques()) or 200, {}))
r = dv.call("GET", f"{VER}({ver})?$select=sanic_archivo_name,sanic_archivo")[1]
res["relectura_tras_bloques"] = {k: r.get(k) for k in ("sanic_archivo_name", "sanic_archivo")}
print("relectura:", res["relectura_tras_bloques"])

C.paso("S2 subida directa por PATCH a la columna de archivo", lambda: (res.__setitem__("subida_patch_http", dv.subir_archivo(f"{VER}({ver})/sanic_archivo", "zzv2.txt", datos + b"2")[0]) or res["subida_patch_http"], {}))
r = dv.call("GET", f"{VER}({ver})?$select=sanic_archivo_name")[1]
res["relectura_tras_patch"] = r.get("sanic_archivo_name")
print("relectura:", res["relectura_tras_patch"])
C.paso("S3 control: PATCH de sanic_huella (columna normal)", lambda: dv.call("PATCH", f"{VER}({ver})", {"sanic_huella": "ZZV-v4-huella"})[:2])

# limpieza de lo creado en esta prueba
for i in ids:
    print("borrar step", dv.call("DELETE", f"sdkmessageprocessingsteps({i})")[0])
volcar("60_v4_archivo.json", res)
C.cerrar()
