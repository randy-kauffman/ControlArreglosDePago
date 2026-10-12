"""V-8 (segunda parte): actualiza el paquete de prueba (ZzvNombre con modos #quitar / #fijar) y registra 3 steps mutadores (rank 2, DESHABILITADOS) para probar
que puede hacer un plugin con sanic_nombre en el Create. Sigue FUERA de la solucion; compara solutioncomponents."""
import base64, zipfile, re
from v70_lib import *

NUPKG = os.path.join(AQUI, "src", "Zzv.Probe", "bin", "Release", "sanic_ZZVProbe.1.0.0.nupkg")
dv = adm()
st = leer70(); base = st["linea_base"]; pid = st["paquete"]["id"]
nup = open(NUPKG, "rb").read()
ver = re.search(r"<AssemblyVersion>([^<]+)", open(os.path.join(AQUI, "src/Zzv.Probe/Zzv.Probe.csproj")).read()).group(1)
est, b, h = dv.call("PATCH", f"pluginpackages({pid})", {"content": base64.b64encode(nup).decode()}, timeout=600)
print("PATCH paquete", est, b if est >= 300 else ""); assert est in (200, 204)
time.sleep(8)
ens = get(dv, f"pluginassemblies?$select=pluginassemblyid,name,version&$filter=_packageid_value eq {pid}")["value"]
print(ens); assert [e["name"] for e in ens] == ["Zzv.Probe"] and ens[0]["version"] == ver, "la version del ensamblado no se actualizo"
tp = {t["typename"]: t["plugintypeid"] for t in get(dv, f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {ens[0]['pluginassemblyid']}")["value"]}
print(tp); assert tp == st["paquete"]["tipos"] or set(tp) == set(st["paquete"]["tipos"])
st["paquete"].update({"ensamblado": ens[0]["pluginassemblyid"], "tipos": tp, "version": ver}); guardar70(st)

msgs = {m["name"]: m["sdkmessageid"] for m in get(dv, "sdkmessages?$select=name&$filter=name eq 'Create'")["value"]}
flt = get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=_sdkmessageid_value eq {msgs['Create']} and primaryobjecttypecode eq 'sanic_carp_tbl_arreglo'")["value"][0]["sdkmessagefilterid"]
NOM = tp["Zzv.Probe.ZzvNombre"]
DEFS = [("ZZV-v8-mut-PreVal-quitar", 10, "v8:mut:preval10#quitar"),
        ("ZZV-v8-mut-PreOp-quitar", 20, "v8:mut:preop20#quitar"),
        ("ZZV-v8-mut-PreOp-fijar", 20, "v8:mut:preop20#fijar=ZZV-FIJO")]
ids = st.get("steps", {})
for nombre, etapa, etiq in DEFS:
    if nombre in ids:
        continue
    cuerpo = {"name": nombre, "description": "Spike V-8 2026-10-11 (descartable)", "stage": etapa, "mode": 0, "rank": 2, "supporteddeployment": 0, "invocationsource": 0,
              "configuration": etiq, "sdkmessageid@odata.bind": f"/sdkmessages({msgs['Create']})", "sdkmessagefilterid@odata.bind": f"/sdkmessagefilters({flt})",
              "plugintypeid@odata.bind": f"/plugintypes({NOM})", "eventhandler_plugintype@odata.bind": f"/plugintypes({NOM})"}
    est, b, h = dv.call("POST", "sdkmessageprocessingsteps", cuerpo)
    print(nombre, est, b if est >= 300 else ""); assert est in (201, 204)
    ids[nombre] = dv.id_creado(h); st["steps"] = ids; guardar70(st)
    est = dv.call("PATCH", f"sdkmessageprocessingsteps({ids[nombre]})", {"statecode": 1, "statuscode": 2})[0]
    print("  deshabilitado:", est)
filas = get(dv, "sdkmessageprocessingsteps?$select=name,stage,rank,statecode,configuration&$filter=startswith(name,'ZZV-')")["value"]
for f in sorted(filas, key=lambda x: x["name"]):
    print(f"{f['name']:40} stage={f['stage']} rank={f['rank']} statecode={f['statecode']} cfg={f['configuration']}")
sid, comps = componentes_solucion(dv)
antes = {(c["componenttype"], c["objectid"]) for c in base["componentes_solucion"]}
ahora = {(c["componenttype"], c["objectid"]) for c in comps}
print("componentes solucion antes/ahora:", len(antes), len(ahora), "| nuevos:", sorted(ahora - antes), "| faltan:", sorted(antes - ahora))
assert ahora == antes
