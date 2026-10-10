"""Extras de V-4 y V-6.
E1 (V-4): un step de Update con filtro sanic_archivo en la version: se dispara al subir un archivo por bloques y NO con un PATCH de otra columna.
E2 (V-6): step con 'ejecutar en el contexto de' un usuario administrador (impersonatinguserid) y llamador no administrador SIN perfil: que ve la pre-imagen.
Se restablece el step al final (se quita la suplantacion y se relee)."""
import base64
from vlib import *

dv = adm()
est_ = leer_estado()
OBS = est_["paquete"]["tipos"]["Zzv.Probe.ZzvObserva"]
steps = est_["steps"]
res = {}
base = est_["linea_base"]
uid = base["usuario"]["id"]
yo = mi_id(dv)
C = Corrida(dv, "61_extras_trazas.json")

# ---------------- E1
msg = get(dv, "sdkmessages?$select=sdkmessageid&$filter=name eq 'Update'")["value"][0]["sdkmessageid"]
flt = get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=_sdkmessageid_value eq {msg} and primaryobjecttypecode eq '{LN['versionplantilla']}'")["value"][0]["sdkmessagefilterid"]
est, b, h = dv.call("POST", "sdkmessageprocessingsteps", {"name": "ZZV-version-Update-archivo-Pre", "description": "Spike V-4 E1", "stage": 20, "mode": 0, "rank": 1, "supporteddeployment": 0, "invocationsource": 0,
                    "configuration": "version:upd-archivo:pre20", "filteringattributes": "sanic_archivo", "sdkmessageid@odata.bind": f"/sdkmessages({msg})", "sdkmessagefilterid@odata.bind": f"/sdkmessagefilters({flt})",
                    "plugintypeid@odata.bind": f"/plugintypes({OBS})", "eventhandler_plugintype@odata.bind": f"/plugintypes({OBS})"})
assert est in (201, 204), b
steps["ZZV-version-Update-archivo-Pre"] = dv.id_creado(h)
est_["steps"] = steps; guardar_estado(est_)
# desactivar los dos steps sin filtro de la version para que el rastro de E1 sea limpio
for n in ("ZZV-version-Update-sinfiltro-Pre", "ZZV-version-Update-sinfiltro-Post"):
    print("desactivar", n, dv.call("PATCH", f"sdkmessageprocessingsteps({steps[n]})", {"statecode": 1, "statuscode": 2})[0])
ver = crear_version(dv, "v4b")
TABLA, VER = LN["versionplantilla"], ES["versionplantilla"]
tgt = {"@odata.type": "Microsoft.Dynamics.CRM." + TABLA, "sanic_carp_tbl_versionplantillaid": ver}
b64 = lambda x: base64.b64encode(x).decode()


def bloques():
    e, b, _ = dv.call("POST", "InitializeFileBlocksUpload", {"Target": tgt, "FileAttributeName": "sanic_archivo", "FileName": "zzv3.txt"}); assert e == 200, b
    tok = b["FileContinuationToken"]; bid = b64(b"zzv-block-0001")
    e, b, _ = dv.call("POST", "UploadBlock", {"BlockId": bid, "BlockData": b64(b"ZZV-datos" * 20), "FileContinuationToken": tok}); assert e in (200, 204), b
    e, b, _ = dv.call("POST", "CommitFileBlocksUpload", {"FileName": "zzv3.txt", "MimeType": "text/plain", "BlockList": [bid], "FileContinuationToken": tok})
    return e, b


C.paso("E1a subida por bloques con step filtrado por sanic_archivo", bloques)
C.paso("E1b control: PATCH sanic_huella con el mismo step filtrado", lambda: dv.call("PATCH", f"{VER}({ver})", {"sanic_huella": "ZZV-e1b"})[:2])
C.paso("E1c subida por bloques como NO admin Oficial?", lambda: (0, {}))   # marcador, se completa abajo si el usuario esta habilitado

# ---------------- E2
yo2 = correo()
arr_v6 = [f for f in est_["filas"] if f[0] == "arreglo-v6b"][0][2]
AR = ES["arreglo"]
paso_id = steps["ZZV-arreglo-Update-comentarios-Pre"]
print("suplantacion ->", dv.call("PATCH", f"sdkmessageprocessingsteps({paso_id})", {"impersonatinguserid@odata.bind": f"/systemusers({yo})"})[0])
rel = get(dv, f"sdkmessageprocessingsteps({paso_id})?$select=name&$expand=impersonatinguserid($select=systemuserid)")
res["step_con_suplantacion"] = (rel.get("impersonatinguserid") or {}).get("systemuserid")
print("releido impersonatinguserid:", res["step_con_suplantacion"])
C.paso("E2 step suplantando al admin; llamador NO admin SIN perfil: PATCH comentarios", lambda: yo2.call("PATCH", f"{AR}({arr_v6})", {"sanic_comentarios": "ZZV-e2-suplantado"})[:2])
time.sleep(1)
print("quitar suplantacion ->", dv.call("DELETE", f"sdkmessageprocessingsteps({paso_id})/impersonatinguserid/$ref")[0])
rel = get(dv, f"sdkmessageprocessingsteps({paso_id})?$select=name&$expand=impersonatinguserid($select=systemuserid)")
res["step_suplantacion_tras_restaurar"] = (rel.get("impersonatinguserid") or {}).get("systemuserid")
print("releido tras restaurar:", res["step_suplantacion_tras_restaurar"])
assert res["step_suplantacion_tras_restaurar"] is None
for n in ("ZZV-version-Update-sinfiltro-Pre", "ZZV-version-Update-sinfiltro-Post"):
    print("reactivar", n, dv.call("PATCH", f"sdkmessageprocessingsteps({steps[n]})", {"statecode": 0, "statuscode": 1})[0])
volcar("61_extras.json", res)
C.cerrar()
print("FIN")
