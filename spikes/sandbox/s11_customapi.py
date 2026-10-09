"""Paso 3: Custom API spk_GenerarFormatoSpike (Accion global) + parametros, atada al tipo de plugin del paquete.
Idempotente, nunca destructiva. Verifica releyendo y comparando en los dos sentidos (declarado vs presente)."""
from sbx_lib import *

TIPO_PLUGIN = "Spk.Plugins.GenerarFormatoSpikePlugin"
ENTRADA = {"VersionId": 12, "Plazo": 7}                                   # Guid, Integer
SALIDA = {"ArchivoBase64": 10, "Milisegundos": 7, "TamanoBytes": 7, "Detalle": 10}   # String, Integer, Integer, String
dv = Dataverse()
e = leer_estado()

est, t, _ = dv.call("GET", f"plugintypes?$select=plugintypeid,typename,_pluginassemblyid_value&$filter=typename eq '{TIPO_PLUGIN}'")
assert len(t["value"]) == 1, ("tipo de plugin no registrado", t)
tid = t["value"][0]["plugintypeid"]

est, b, _ = dv.call("GET", f"customapis?$select=customapiid,uniquename,isfunction,bindingtype,_plugintypeid_value&$filter=uniquename eq '{API}'")
if b["value"]:
    api = b["value"][0]
    print("API ya existia", api["customapiid"])
    assert api["isfunction"] is False and api["bindingtype"] == 0 and api["_plugintypeid_value"] == tid, ("difiere", api)
else:
    est, r, h = dv.call("POST", "customapis", {
        "uniquename": API, "name": "GenerarFormatoSpike", "displayname": "Generar formato (spike)",
        "description": "Spike 1b. Devuelve el F-NIC-0000052 generado con datos ficticios, en Base64.",
        "bindingtype": 0, "isfunction": False, "isprivate": False, "allowedcustomprocessingsteptype": 0,
        "workflowsdkstepenabled": False, "PluginTypeId@odata.bind": f"/plugintypes({tid})"}, solucion=SOLUCION)
    mostrar("POST customapi", est, r)
    assert est in (201, 204)
    api = {"customapiid": dv.id_creado(h)}
cid = api["customapiid"]

def asegurar(ent, nav, nombre, tipo, opcional=None):
    conj = ent + "s" if ent.endswith("parameter") else ent[:-1] + "ies"
    est, r, _ = dv.call("GET", f"{conj}?$select=uniquename,type&$filter=_customapiid_value eq {cid} and uniquename eq '{nombre}'")
    if r["value"]:
        assert r["value"][0]["type"] == tipo, ("difiere el tipo", nombre)
        return
    cuerpo = {"uniquename": nombre, "name": f"{API}.{nombre}", "displayname": nombre, "type": tipo, "CustomAPIId@odata.bind": f"/customapis({cid})"}
    if opcional is not None:
        cuerpo["isoptional"] = opcional
    est, r, _ = dv.call("POST", conj, cuerpo, solucion=SOLUCION)
    print("  POST", ent, nombre, est, "" if est in (201, 204) else r)
    assert est in (201, 204)

for n, ty in ENTRADA.items():
    asegurar("customapirequestparameter", "CustomAPIId", n, ty, opcional=False)
for n, ty in SALIDA.items():
    asegurar("customapiresponseproperty", "CustomAPIId", n, ty)

# Verificacion por contenido, en los dos sentidos
est, a, _ = dv.call("GET", f"customapis({cid})?$select=uniquename,name,isfunction,bindingtype,isprivate,allowedcustomprocessingsteptype,executeprivilegename,modifiedon,_plugintypeid_value")
mostrar("RELEER customapi", est, a)
assert a["_plugintypeid_value"] == tid and a["isfunction"] is False and a["bindingtype"] == 0
est, rq, _ = dv.call("GET", f"customapirequestparameters?$select=uniquename,type,isoptional&$filter=_customapiid_value eq {cid}")
est, rs, _ = dv.call("GET", f"customapiresponseproperties?$select=uniquename,type&$filter=_customapiid_value eq {cid}")
pr = {x["uniquename"]: x["type"] for x in rq["value"]}; ps = {x["uniquename"]: x["type"] for x in rs["value"]}
print("entrada presente:", pr, "| faltan:", set(ENTRADA) - set(pr), "| sobran:", set(pr) - set(ENTRADA), "| tipos ok:", all(pr.get(k) == v for k, v in ENTRADA.items()))
print("salida presente:", ps, "| faltan:", set(SALIDA) - set(ps), "| sobran:", set(ps) - set(SALIDA), "| tipos ok:", all(ps.get(k) == v for k, v in SALIDA.items()))
assert pr == ENTRADA and ps == SALIDA, "VERIFICACION FALLIDA"
est, so, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
est, sc, _ = dv.call("GET", f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {so['value'][0]['solutionid']}")
ids = {cid: "customapi"} | {x["customapirequestparameterid"]: "param-entrada" for x in dv.call("GET", f"customapirequestparameters?$select=customapirequestparameterid&$filter=_customapiid_value eq {cid}")[1]["value"]} \
      | {x["customapiresponsepropertyid"]: "propiedad-salida" for x in dv.call("GET", f"customapiresponseproperties?$select=customapiresponsepropertyid&$filter=_customapiid_value eq {cid}")[1]["value"]}
print("pertenencia a la solucion (tipo de componente que puso la plataforma):", [(ids[c["objectid"]], c["componenttype"]) for c in sc["value"] if c["objectid"] in ids])
e["customapi"] = {"id": cid, "plugintypeid": tid}
guardar_estado(e)
print("VERIFICACION OK")
