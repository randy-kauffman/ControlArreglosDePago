"""Paso 2: sube o actualiza el paquete de plugins en la solucion y VERIFICA por contenido (version y modifiedon del
ensamblado/paquete, tipos declarados vs registrados en los dos sentidos, bytes del paquete releido == bytes locales)."""
import base64, re, zipfile, sys
from sbx_lib import *

NUPKG = os.path.join(AQUI, "src", "Spk.Plugins", "bin", "Release", "spk_ArreglosPagoSandbox.1.0.0.nupkg")
CSPROJ = os.path.join(AQUI, "src", "Spk.Plugins", "Spk.Plugins.csproj")
TIPOS_DECLARADOS = {"Spk.Plugins.GenerarFormatoSpikePlugin"}
ENSAMBLADOS_DECLARADOS = {"Spk.Plugins"}

dv = Dataverse()
with open(NUPKG, "rb") as f:
    nupkg = f.read()
version_esperada = re.search(r"<AssemblyVersion>([^<]+)", open(CSPROJ, encoding="utf-8").read()).group(1)
z = zipfile.ZipFile(NUPKG)
dll = z.read("lib/net462/Spk.Plugins.dll")
for t in TIPOS_DECLARADOS:   # el tipo declarado esta de verdad dentro del .dll empaquetado (alm.md)
    assert t.split(".")[-1].encode() in dll, f"el tipo {t} no esta dentro del dll empaquetado: dotnet pack no reescribio el paquete"
print("nupkg local:", len(nupkg), "bytes | sha256", sha256(nupkg)[:16], "| AssemblyVersion esperada", version_esperada, "| lib:", [n for n in z.namelist() if n.startswith("lib/")])

est, b, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid,version,modifiedon&$filter=uniquename eq '{PAQUETE_UNICO}'")
antes = b["value"][0] if b.get("value") else None
if antes is None:
    est, b, h = dv.call("POST", "pluginpackages", {"name": PAQUETE_UNICO, "uniquename": PAQUETE_UNICO, "version": "1.0.0",
                                                   "content": base64.b64encode(nupkg).decode()}, solucion=SOLUCION, timeout=600)
    mostrar("POST pluginpackages", est, b)
    assert est in (201, 204), "no se creo el paquete"
    pid = dv.id_creado(h)
else:
    pid = antes["pluginpackageid"]
    est, ea, _ = dv.call("GET", f"pluginassemblies?$select=name,version,modifiedon&$filter=_packageid_value eq {pid}")
    ens_antes = {x["name"]: (x["version"], x["modifiedon"]) for x in ea["value"]}
    print("ensamblados antes del despliegue:", ens_antes)
    print("paquete existente", pid, "version", antes["version"], "modifiedon", antes["modifiedon"])
    est, b, h = dv.call("PATCH", f"pluginpackages({pid})", {"content": base64.b64encode(nupkg).decode()}, timeout=600)
    mostrar("PATCH pluginpackages", est, b)
    assert est in (200, 204), "no se actualizo el paquete"

# --- Verificacion por contenido (no por el 2xx) ---
est, p, _ = dv.call("GET", f"pluginpackages({pid})?$select=name,uniquename,version,modifiedon,createdon")
mostrar("RELEER paquete", est, p)
print("  modifiedon antes:", antes["modifiedon"] if antes else "(nuevo)", "-> despues:", p["modifiedon"])
est, a, _ = dv.call("GET", f"pluginassemblies?$select=pluginassemblyid,name,version,culture,publickeytoken,isolationmode,sourcetype,modifiedon,createdon&$filter=_packageid_value eq {pid}")
ens = a["value"]
for x in ens:
    print("  ensamblado:", x["name"], "version", x["version"], "isolationmode", x["isolationmode"], "sourcetype", x["sourcetype"], "modifiedon", x["modifiedon"])
nombres = {x["name"] for x in ens}
print("  ensamblados declarados que faltan:", ENSAMBLADOS_DECLARADOS - nombres, "| registrados que sobran:", nombres - ENSAMBLADOS_DECLARADOS)
tipos = set()
for x in ens:
    est, t, _ = dv.call("GET", f"plugintypes?$select=plugintypeid,typename,name&$filter=_pluginassemblyid_value eq {x['pluginassemblyid']}")
    tipos |= {y["typename"] for y in t["value"]}
    x["tipos"] = [y["plugintypeid"] for y in t["value"]]
print("  tipos declarados que faltan:", TIPOS_DECLARADOS - tipos, "| tipos registrados que sobran:", tipos - TIPOS_DECLARADOS)
if antes is not None:
    for x in ens:
        v0, m0 = ens_antes.get(x["name"], (None, ""))
        avanzo = x["modifiedon"] > m0
        print(f"  {x['name']}: version {v0} -> {x['version']}; modifiedon {m0} -> {x['modifiedon']} (avanzo: {avanzo})")
        if v0 != x["version"]:
            assert avanzo, "el ensamblado cambio de version pero su modifiedon no avanzo: el sandbox podria seguir con el binario anterior"
ok_version = all(x["version"] == version_esperada for x in ens if x["name"] in ENSAMBLADOS_DECLARADOS)
print("  version del ensamblado == esperada:", ok_version)
# bytes del paquete tal como quedo guardado
est, cont, _ = raw(dv, "GET", f"pluginpackages({pid})/package/$value")
print("  package/$value HTTP", est, "bytes", len(cont), "sha256 igual al local:", sha256(cont) == sha256(nupkg) if est == 200 else "no leible")
est, so, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
est, sc, _ = dv.call("GET", f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {so['value'][0]['solutionid']} and objectid eq {pid}")
print("  pertenece a la solucion (solutioncomponents):", [c["componenttype"] for c in sc["value"]])
e = leer_estado()
e["paquete"] = {"id": pid, "tipos": {x["name"]: x["tipos"] for x in ens}, "ensamblados": {x["name"]: x["pluginassemblyid"] for x in ens}}
guardar_estado(e)
assert not (ENSAMBLADOS_DECLARADOS - nombres) and not (nombres - ENSAMBLADOS_DECLARADOS) and not (TIPOS_DECLARADOS - tipos) and not (tipos - TIPOS_DECLARADOS) and ok_version, "VERIFICACION FALLIDA"
print("VERIFICACION OK")
