#!/usr/bin/env python3
"""LIMPIEZA del spike 1b (NO se ejecuta sola). Ensayo en seco por defecto; con --si-borrar borra.

Borra SOLO lo que creo el spike, localizado por nombre exacto y comprobando antes que pertenece a la solucion
SpikesArreglosPago (solutioncomponents). No borra la solucion ni el publisher (los comparte el spike 2: su limpieza esta en
../plataforma/s99_limpieza.py). Orden exigido por la plataforma (un paquete no se borra con steps; una Custom API referencia el
tipo de plugin del paquete):
  1) steps de procesamiento de los tipos del paquete: el spike no crea ninguno; la plataforma crea uno interno por Custom API
     (nombre "CustomApi '<nombre>' implementation") que se va con la API; si apareciera otro, se informa y se borra
  2) propiedades de respuesta y parametros de entrada de la Custom API
  3) la Custom API
  4) el paquete de plugins (se llevan sus ensamblados y tipos)
  5) la tabla spk_versionplantilla (se lleva columnas y filas; unos 40 s; se relee hasta 404)
Uso:  python3 s99_limpieza.py [--si-borrar] [--conjunto spike|prueba-limpieza]
El conjunto 'prueba-limpieza' es un paquete y una Custom API minimos y descartables con los que se ensayo esta misma secuencia."""
import sys, time
from sbx_lib import *

CONJUNTOS = {
    "spike": {"api": API, "paquete": PAQUETE_UNICO, "tabla": TABLA},
    "prueba-limpieza": {"api": "spk_TmpLimpiezaApi", "paquete": "spk_TmpLimpiezaPkg", "tabla": None},
}
borrar = "--si-borrar" in sys.argv
nombre = sys.argv[sys.argv.index("--conjunto") + 1] if "--conjunto" in sys.argv else "spike"
c = CONJUNTOS[nombre]
dv = Dataverse()
print(("BORRADO REAL" if borrar else "ENSAYO EN SECO (no se borra nada)"), "| conjunto:", nombre)

est, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
sid = b["value"][0]["solutionid"]
est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}")
en_solucion = {x["objectid"] for x in b["value"]}
componentes_antes = len(en_solucion)

def verificar_pertenencia(que, rid):
    if rid not in en_solucion:
        sys.exit(f"ABORTO: {que} {rid} existe pero NO esta en la solucion {SOLUCION}: no lo creo este spike, no se borra.")

def borrar_fila(conjunto, rid, etiqueta):
    if not borrar:
        print(f"  BORRARIA {etiqueta} ({conjunto}/{rid})"); return
    est, r, _ = dv.call("DELETE", f"{conjunto}({rid})", timeout=600)
    print(f"  DELETE {etiqueta}: HTTP {est}", "" if est in (200, 204) else r)
    if est not in (200, 204):
        sys.exit("ABORTO: la plataforma rechazo el borrado; no se sigue.")
    est, _, _ = dv.call("GET", f"{conjunto}({rid})?$select=createdon")
    print(f"  releido: HTTP {est}", "(ya no existe)" if est == 404 else "(!! SIGUE EXISTIENDO)")
    if est != 404:
        sys.exit("ABORTO: el recurso sigue existiendo despues del DELETE.")

# --- localizar ---
est, p, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid,uniquename,version&$filter=uniquename eq '{c['paquete']}'")
paquete = p["value"][0] if p["value"] else None
est, a, _ = dv.call("GET", f"customapis?$select=customapiid,uniquename&$filter=uniquename eq '{c['api']}'")
api = a["value"][0] if a["value"] else None
tipos = []
if paquete:
    verificar_pertenencia("paquete", paquete["pluginpackageid"])
    est, ens, _ = dv.call("GET", f"pluginassemblies?$select=pluginassemblyid,name&$filter=_packageid_value eq {paquete['pluginpackageid']}")
    for e in ens["value"]:
        est, t, _ = dv.call("GET", f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {e['pluginassemblyid']}")
        tipos += [x["plugintypeid"] for x in t["value"]]
if api:
    verificar_pertenencia("Custom API", api["customapiid"])
pasos = []
# 1) steps
steps = []
for t in tipos:
    est, s, _ = dv.call("GET", f"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name&$filter=_plugintypeid_value eq {t}")
    steps += s["value"]
internos = [x for x in steps if x["name"].startswith("CustomApi '")]
propios = [x for x in steps if x not in internos]
print(f"1) steps de los tipos del paquete: {len(steps)} ({len(internos)} interno(s) que la plataforma crea para la Custom API y borra con ella; {len(propios)} propio(s))")
for x in internos:
    print(f"  (interno, no se borra a mano) {x['name']}")
for x in propios:
    borrar_fila("sdkmessageprocessingsteps", x["sdkmessageprocessingstepid"], f"step {x['name']}")
# 2) y 3) Custom API
if api:
    est, rs, _ = dv.call("GET", f"customapiresponseproperties?$select=customapiresponsepropertyid,uniquename&$filter=_customapiid_value eq {api['customapiid']}")
    est, rq, _ = dv.call("GET", f"customapirequestparameters?$select=customapirequestparameterid,uniquename&$filter=_customapiid_value eq {api['customapiid']}")
    print(f"2) Custom API {c['api']}: {len(rs['value'])} propiedades de respuesta y {len(rq['value'])} parametros de entrada")
    for x in rs["value"]:
        borrar_fila("customapiresponseproperties", x["customapiresponsepropertyid"], f"propiedad de respuesta {x['uniquename']}")
    for x in rq["value"]:
        borrar_fila("customapirequestparameters", x["customapirequestparameterid"], f"parametro de entrada {x['uniquename']}")
    print("3) la Custom API")
    borrar_fila("customapis", api["customapiid"], f"Custom API {c['api']}")
    if borrar:
        for x in internos:
            est, _, _ = dv.call("GET", f"sdkmessageprocessingsteps({x['sdkmessageprocessingstepid']})?$select=name")
            print(f"  step interno {x['name']}: HTTP {est}", "(se fue con la API)" if est == 404 else "(!! SIGUE EXISTIENDO)")
else:
    print(f"2-3) Custom API {c['api']}: no existe")
# 4) paquete
if paquete:
    print(f"4) paquete {c['paquete']} v{paquete['version']} con {len(tipos)} tipo(s) de plugin")
    borrar_fila("pluginpackages", paquete["pluginpackageid"], f"paquete {c['paquete']}")
    if borrar:
        est, ens2, _ = dv.call("GET", f"pluginassemblies?$select=name&$filter=_packageid_value eq {paquete['pluginpackageid']}")
        print("  ensamblados que quedaron del paquete:", len(ens2["value"]))
        restos = [t for t in tipos if dv.call("GET", f"plugintypes({t})?$select=typename")[0] != 404]
        print("  tipos de plugin que quedaron:", len(restos))
else:
    print(f"4) paquete {c['paquete']}: no existe")
# 5) tabla
if c["tabla"]:
    est, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{c['tabla']}')?$select=MetadataId")
    if est == 404:
        print(f"5) tabla {c['tabla']}: no existe")
    else:
        verificar_pertenencia("tabla", e["MetadataId"])
        print(f"5) tabla {c['tabla']}")
        if not borrar:
            est, n, _ = dv.call("GET", f"{CONJUNTO}?$select=spk_versionplantillaid")
            print(f"  BORRARIA la tabla {c['tabla']} con sus {len(n['value'])} filas y columnas")
        else:
            est, r, _ = dv.call("DELETE", f"EntityDefinitions(LogicalName='{c['tabla']}')", timeout=600)
            print("  DELETE tabla: HTTP", est, "" if est in (200, 204) else r)
            for _ in range(30):
                est, _, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{c['tabla']}')?$select=MetadataId")
                if est == 404:
                    print("  releido: ya no existe"); break
                time.sleep(10)
            else:
                sys.exit("ABORTO: la tabla sigue existiendo.")
if borrar:
    est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid&$filter=_solutionid_value eq {sid}")
    print(f"componentes de la solucion antes: {componentes_antes}, despues: {len(b['value'])} (la solucion y el publisher NO se tocan)")
