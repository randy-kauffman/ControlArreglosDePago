"""Dobles: registra el paquete de plugins SONDA y sus steps en Dev y los VERIFICA POR CONTENIDO (no por el 2xx), como spikes/sandbox/s10_paquete.py.
Cada objeto se anota en estado.json["registros"] ANTES de crearse (GUID asignado por el cliente), para que d90/d91 lo borren/verifiquen pase lo que pase.
No hay solucion SpikesArreglosPago en Dev (linea base): el paquete se registra SIN solucion. Steps SOLO sobre tablas propias sanic_carp_tbl_*."""
import base64, hashlib, re, sys, uuid, zipfile
from dlib import *

st = leer_estado()
base = st["linea_base"]
if base.get("solucion_spk_existe"):
    sys.exit("DETENIDO: la solucion SpikesArreglosPago existe ahora; el spike la usaria (cambiar d30 antes de seguir)")
dv = adm()
if residuales_sonda(dv):
    sys.exit("DETENIDO: ya hay algo 'Sonda' vivo en Dev (correr d90 y d91 antes)")
CSPROJ = os.path.join(PROYECTO_SONDA, "Spk.SondaDobles.csproj")
version_esperada = re.search(r"<AssemblyVersion>([^<]+)", open(CSPROJ, encoding="utf-8").read()).group(1)
nupkg = open(NUPKG, "rb").read()
z = zipfile.ZipFile(NUPKG)
dll = z.read(f"lib/net462/{ENSAMBLADO}.dll")
assert TIPO_PLUGIN.split(".")[-1].encode() in dll, "el tipo declarado no esta dentro del dll empaquetado"
print("nupkg local:", len(nupkg), "bytes | sha256", hashlib.sha256(nupkg).hexdigest()[:16], "| AssemblyVersion esperada", version_esperada, "| lib:", [n for n in z.namelist() if n.startswith("lib/")])


def registrar(clave, conjunto, rid):
    s = leer_estado()
    s.setdefault("registros", []).append([clave, conjunto, rid])
    guardar_estado(s)


# ---------------------------------------------------------------- paquete
pid = str(uuid.uuid4())
registrar("paquete " + PAQUETE, "pluginpackages", pid)
est, b, h = dv.call("POST", "pluginpackages", {"pluginpackageid": pid, "name": PAQUETE, "uniquename": PAQUETE, "version": "1.0.0",
                                              "content": base64.b64encode(nupkg).decode()}, timeout=600)
print("POST pluginpackages ->", est, "" if est in (201, 204) else b)
assert est in (201, 204), "no se creo el paquete"
creado = dv.id_creado(h)
assert creado.lower() == pid.lower(), "la plataforma no respeto el GUID asignado por el cliente"
time.sleep(5)

ev = {"fecha": time.strftime("%Y-%m-%d %H:%M:%S"), "paquete": {}, "steps": [], "imagenes": []}
p = get(dv, f"pluginpackages({pid})?$select=name,uniquename,version,modifiedon,createdon")
ev["paquete"].update({"uniquename": p["uniquename"], "version_paquete": p["version"], "modifiedon": p["modifiedon"]})
ens = get(dv, f"pluginassemblies?$select=pluginassemblyid,name,version,isolationmode,sourcetype,modifiedon&$filter=_packageid_value eq {pid}")["value"]
ev["ensamblados"] = [{"name": x["name"], "version": x["version"], "isolationmode": x["isolationmode"], "sourcetype": x["sourcetype"], "modifiedon": x["modifiedon"]} for x in ens]
nombres = {x["name"] for x in ens}
tipos = {}
for x in ens:
    for t in get(dv, f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {x['pluginassemblyid']}")["value"]:
        tipos[t["typename"]] = t["plugintypeid"]
ev["tipos_registrados"] = sorted(tipos)
# el cliente del Web API no devuelve binarios: se relee con una peticion cruda
import urllib.request, urllib.parse
req = urllib.request.Request(dv.api + f"pluginpackages({pid})/package/$value", headers={"Authorization": "Bearer " + dv._token, "Accept": "application/octet-stream"})
try:
    with urllib.request.urlopen(req, timeout=120) as r:
        guardado = r.read()
    ev["paquete"]["sha256_igual_al_local"] = hashlib.sha256(guardado).hexdigest() == hashlib.sha256(nupkg).hexdigest()
except Exception as e:
    ev["paquete"]["sha256_igual_al_local"] = f"no se pudo releer ({type(e).__name__})"
sol_proy = get(dv, f"solutioncomponents?$select=solutionid&$filter=objectid eq {pid}")["value"]
ev["paquete"]["pertenece_a_alguna_solucion"] = len(sol_proy) > 0
print("ensamblados:", [(x['name'], x['version'], x['isolationmode'], x['sourcetype']) for x in ens], "| tipos:", sorted(tipos))
assert nombres == {ENSAMBLADO}, ("ensamblados distintos de los declarados", nombres)
assert set(tipos) == {TIPO_PLUGIN}, ("tipos distintos de los declarados", set(tipos))
assert all(x["version"] == version_esperada for x in ens), "la version del ensamblado no es la esperada"
assert ev["paquete"]["sha256_igual_al_local"] is True, "el paquete guardado no es el local"

# ---------------------------------------------------------------- steps
sec = leer_secretos(requeridas=("PP_CLIENT_ID",))
system_id = base["system_ids"][0]
cfg = f"u={base['whoami']['UserId']};s={system_id}"   # GUID reales: viven solo en el step de Dev, nunca en un archivo del repo
msgs = {n: get(dv, f"sdkmessages?$select=sdkmessageid&$filter=name eq '{n}'")["value"][0]["sdkmessageid"] for n in ("Create", "Update")}
ETAPA = {10: "PreValidation", 20: "PreOperation", 40: "PostOperation"}
STEPS = [("cliente", "Create", 10, None), ("cliente", "Create", 20, None), ("cliente", "Create", 40, None),
         ("conversacion", "Create", 10, None), ("conversacion", "Create", 20, None), ("conversacion", "Create", 40, None),
         ("arreglo", "Create", 20, None), ("arreglo", "Create", 40, None),
         ("arreglo", "Update", 20, ["sanic_numeroproducto", "sanic_fechainicio", "sanic_fechafin", "sanic_fechacongelamiento", "sanic_saldoinicial", "sanic_plazo", "sanic_tipo", "sanic_clienteid", "ownerid", "createdon"])]
esperados = {}
for corto, mensaje, etapa, imagen in STEPS:
    tabla = TABLAS[corto][0]
    assert tabla.startswith("sanic_carp_tbl_"), "la sonda solo se registra sobre tablas propias"
    nombre = f"STEP - SPK - Sonda - {tabla} - {mensaje} - {ETAPA[etapa]}"
    filtros = get(dv, f"sdkmessagefilters?$select=sdkmessagefilterid&$filter=primaryobjecttypecode eq '{tabla}' and _sdkmessageid_value eq {msgs[mensaje]}")["value"]
    assert len(filtros) == 1, ("filtro de mensaje", tabla, mensaje, len(filtros))
    sid = str(uuid.uuid4())
    registrar(nombre, "sdkmessageprocessingsteps", sid)
    cuerpo = {
        "sdkmessageprocessingstepid": sid, "name": nombre, "description": "Sonda descartable del mini spike de los dobles", "stage": etapa, "mode": 0, "rank": 1,
        "supporteddeployment": 0, "invocationsource": 0, "configuration": cfg,
        "sdkmessageid@odata.bind": f"/sdkmessages({msgs[mensaje]})", "sdkmessagefilterid@odata.bind": f"/sdkmessagefilters({filtros[0]['sdkmessagefilterid']})",
        "plugintypeid@odata.bind": f"/plugintypes({tipos[TIPO_PLUGIN]})", "eventhandler_plugintype@odata.bind": f"/plugintypes({tipos[TIPO_PLUGIN]})",
    }
    est, b, h = dv.call("POST", "sdkmessageprocessingsteps", cuerpo, timeout=300)
    print("POST step", nombre[7:], "->", est, "" if est in (201, 204) else b)
    assert est in (201, 204), "no se creo el step"
    esperados[sid] = {"nombre": nombre, "stage": etapa, "tabla": tabla, "mensaje": mensaje, "imagen": imagen}
    if imagen:
        iid = str(uuid.uuid4())
        registrar("imagen PreImagen de " + nombre, "sdkmessageprocessingstepimages", iid)
        est, b, h = dv.call("POST", "sdkmessageprocessingstepimages", {
            "sdkmessageprocessingstepimageid": iid, "name": "PreImagen", "entityalias": "PreImagen", "imagetype": 0, "messagepropertyname": "Target",
            "attributes": ",".join(sorted(imagen)), "sdkmessageprocessingstepid@odata.bind": f"/sdkmessageprocessingsteps({sid})"}, timeout=300)
        print("POST imagen ->", est, "" if est in (201, 204) else b)
        assert est in (201, 204), "no se creo la imagen"
        esperados[sid]["imagen_id"] = iid
time.sleep(5)

# ---------------------------------------------------------------- verificacion por contenido de los steps (los dos sentidos)
leidos = get(dv, f"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,stage,mode,rank,configuration,statecode,_plugintypeid_value,_sdkmessagefilterid_value&$filter=contains(name,'Sonda')")["value"]
por_id = {x["sdkmessageprocessingstepid"]: x for x in leidos}
faltan = set(esperados) - set(por_id)
sobran = set(por_id) - set(esperados)
for sid, e in esperados.items():
    x = por_id.get(sid)
    if not x:
        continue
    ok = (x["name"] == e["nombre"] and x["stage"] == e["stage"] and x["mode"] == 0 and x["rank"] == 1 and x["statecode"] == 0 and x["configuration"] == cfg
          and x["_plugintypeid_value"] == tipos[TIPO_PLUGIN])
    filtro = get(dv, f"sdkmessagefilters({x['_sdkmessagefilterid_value']})?$select=primaryobjecttypecode")["primaryobjecttypecode"]
    ok = ok and filtro == e["tabla"]
    img = None
    if e.get("imagen"):
        im = get(dv, f"sdkmessageprocessingstepimages?$select=name,imagetype,attributes,entityalias&$filter=_sdkmessageprocessingstepid_value eq {sid}")["value"]
        img = len(im) == 1 and im[0]["name"] == "PreImagen" and im[0]["imagetype"] == 0 and set(im[0]["attributes"].split(",")) == set(e["imagen"])
        ev["imagenes"].append({"step": e["nombre"], "coincide": img})
        ok = ok and img
    ev["steps"].append({"nombre": e["nombre"], "tabla": filtro, "stage": x["stage"], "mode": x["mode"], "rank": x["rank"], "configuracion_coincide": x["configuration"] == cfg, "coincide": bool(ok)})
ev["steps_que_faltan"], ev["steps_que_sobran"] = len(faltan), len(sobran)
ev["verificacion_OK"] = not faltan and not sobran and all(s["coincide"] for s in ev["steps"]) and len(ev["steps"]) == len(STEPS)
volcar("30_paquete_verificado.json", ev)
print(json.dumps({k: v for k, v in ev.items() if k not in ("steps",)}, indent=1))
print("steps verificados:", sum(1 for s in ev["steps"] if s["coincide"]), "de", len(STEPS))
sys.exit(0 if ev["verificacion_OK"] else 1)
