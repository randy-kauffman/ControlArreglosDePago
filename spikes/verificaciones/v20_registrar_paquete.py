"""Registra el paquete de prueba FUERA de la solucion (sin MSCRM.SolutionUniqueName) y verifica por contenido."""
import base64, zipfile, hashlib, re
from vlib import *
NUPKG = os.path.join(AQUI, "src", "Zzv.Probe", "bin", "Release", "sanic_ZZVProbe.1.0.0.nupkg")
UNICO = "sanic_ZZVProbe"
TIPOS = {"Zzv.Probe.ZzvObserva", "Zzv.Probe.ZzvComando"}
dv = adm()
nup = open(NUPKG, "rb").read()
z = zipfile.ZipFile(NUPKG); dll = z.read("lib/net462/Zzv.Probe.dll")
for t in TIPOS: assert t.split(".")[-1].encode() in dll
ver_esperada = re.search(r"<AssemblyVersion>([^<]+)", open(os.path.join(AQUI, "src/Zzv.Probe/Zzv.Probe.csproj")).read()).group(1)
est, b, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid&$filter=uniquename eq '{UNICO}'")
ya = b["value"][0]["pluginpackageid"] if b["value"] else None
if ya is None:
    est, b, h = dv.call("POST", "pluginpackages", {"name": UNICO, "uniquename": UNICO, "version": "1.0.0", "content": base64.b64encode(nup).decode()}, timeout=600)
    print("POST", est, b if est >= 300 else ""); assert est in (201, 204)
    pid = dv.id_creado(h)
else:
    pid = ya
    est, b, h = dv.call("PATCH", f"pluginpackages({pid})", {"content": base64.b64encode(nup).decode()}, timeout=600)
    print("PATCH", est, b if est >= 300 else ""); assert est in (200, 204)
time.sleep(5)
ens = get(dv, f"pluginassemblies?$select=pluginassemblyid,name,version,isolationmode,sourcetype&$filter=_packageid_value eq {pid}")["value"]
print(ens)
assert [e["name"] for e in ens] == ["Zzv.Probe"] and ens[0]["version"] == ver_esperada, "ensamblado o version no coinciden"
tipos = get(dv, f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {ens[0]['pluginassemblyid']}")["value"]
tp = {t["typename"]: t["plugintypeid"] for t in tipos}
print(tp); assert set(tp) == TIPOS
est, cont, _ = dv.call("GET", f"pluginpackages({pid})?$select=name")
sid, comps = componentes_solucion(dv)
print("pertenece a la solucion del proyecto:", any(c["objectid"] == pid for c in comps), "| componentes:", len(comps))
e = leer_estado(); e["paquete"] = {"id": pid, "ensamblado": ens[0]["pluginassemblyid"], "tipos": tp, "version": ver_esperada}; guardar_estado(e)
print("OK", pid)
