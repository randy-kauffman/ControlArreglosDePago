"""V-8: registra el paquete de prueba (con la clase nueva ZzvNombre) FUERA de la solucion (sin MSCRM.SolutionUniqueName), crea los steps y compara solutioncomponents antes/despues."""
import base64, zipfile, re
from v70_lib import *

NUPKG = os.path.join(AQUI, "src", "Zzv.Probe", "bin", "Release", "sanic_ZZVProbe.1.0.0.nupkg")
UNICO = "sanic_ZZVProbe"
TIPOS = {"Zzv.Probe.ZzvObserva", "Zzv.Probe.ZzvComando", "Zzv.Probe.ZzvNombre"}
dv = adm()
st = leer70()
base = st["linea_base"]
assert not any(p["uniquename"] == UNICO for p in base["paquetes"]), "el paquete ya existia en la linea base"

nup = open(NUPKG, "rb").read()
dll = zipfile.ZipFile(NUPKG).read("lib/net462/Zzv.Probe.dll")
for t in TIPOS:
    assert t.split(".")[-1].encode() in dll
ver_esperada = re.search(r"<AssemblyVersion>([^<]+)", open(os.path.join(AQUI, "src/Zzv.Probe/Zzv.Probe.csproj")).read()).group(1)
est, b, h = dv.call("POST", "pluginpackages", {"name": UNICO, "uniquename": UNICO, "version": "1.0.0", "content": base64.b64encode(nup).decode()}, timeout=600)
print("POST paquete", est, b if est >= 300 else ""); assert est in (201, 204)
pid = dv.id_creado(h)
st["paquete"] = {"id": pid}; guardar70(st)
time.sleep(5)
ens = get(dv, f"pluginassemblies?$select=pluginassemblyid,name,version&$filter=_packageid_value eq {pid}")["value"]
assert [e["name"] for e in ens] == ["Zzv.Probe"] and ens[0]["version"] == ver_esperada, ens
tipos = get(dv, f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {ens[0]['pluginassemblyid']}")["value"]
tp = {t["typename"]: t["plugintypeid"] for t in tipos}
assert set(tp) == TIPOS, tp
st["paquete"] = {"id": pid, "ensamblado": ens[0]["pluginassemblyid"], "tipos": tp, "version": ver_esperada}; guardar70(st)
print("paquete OK", pid, ver_esperada, sorted(tp))

msgs = {m["name"]: m["sdkmessageid"] for m in get(dv, "sdkmessages?$select=name&$filter=name eq 'Create' or name eq 'Update'")["value"]}


def filtro(msg, tabla):
    r = get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=_sdkmessageid_value eq {msgs[msg]} and primaryobjecttypecode eq '{tabla}'")["value"]
    assert len(r) == 1, (msg, tabla, r)
    return r[0]["sdkmessagefilterid"]


NOM = tp["Zzv.Probe.ZzvNombre"]
DEFS = [("ZZV-v8-arreglo-Create-PreVal", "Create", 10, None, "v8:create:preval10"),
        ("ZZV-v8-arreglo-Create-PreOp", "Create", 20, None, "v8:create:preop20"),
        ("ZZV-v8-arreglo-Create-PostOp", "Create", 40, None, "v8:create:postop40"),
        ("ZZV-v8-arreglo-Update-nombre-PreOp", "Update", 20, "sanic_nombre", "v8:update-nombre:preop20")]
ids = {}
for nombre, msg, etapa, filt, etiq in DEFS:
    cuerpo = {"name": nombre, "description": "Spike V-8 2026-10-11 (descartable)", "stage": etapa, "mode": 0, "rank": 1, "supporteddeployment": 0, "invocationsource": 0,
              "configuration": etiq, "sdkmessageid@odata.bind": f"/sdkmessages({msgs[msg]})",
              "sdkmessagefilterid@odata.bind": f"/sdkmessagefilters({filtro(msg, 'sanic_carp_tbl_arreglo')})",
              "plugintypeid@odata.bind": f"/plugintypes({NOM})", "eventhandler_plugintype@odata.bind": f"/plugintypes({NOM})"}
    if filt:
        cuerpo["filteringattributes"] = filt
    est, b, h = dv.call("POST", "sdkmessageprocessingsteps", cuerpo)
    print(nombre, est, b if est >= 300 else ""); assert est in (201, 204)
    ids[nombre] = dv.id_creado(h)
    st["steps"] = ids; guardar70(st)

filas = get(dv, "sdkmessageprocessingsteps?$select=name,stage,mode,filteringattributes,configuration,statecode&$expand=sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode)&$filter=startswith(name,'ZZV-')")["value"]
for f in sorted(filas, key=lambda x: x["name"]):
    print(f"{f['name']:40} {f['sdkmessageid']['name']:8} {f['sdkmessagefilterid']['primaryobjecttypecode']} stage={f['stage']} mode={f['mode']} filtro={f.get('filteringattributes')} state={f['statecode']}")
assert len(filas) == len(DEFS)

# el paquete y los steps NO deben estar en la solucion del proyecto
sid, comps = componentes_solucion(dv)
antes = {(c["componenttype"], c["objectid"]) for c in base["componentes_solucion"]}
ahora = {(c["componenttype"], c["objectid"]) for c in comps}
print("componentes solucion antes/ahora:", len(antes), len(ahora), "| nuevos:", sorted(ahora - antes), "| faltan:", sorted(antes - ahora))
assert ahora == antes
