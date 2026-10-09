"""Crea un paquete minimo y una Custom API minima (conjunto 'prueba-limpieza') para ensayar con ellos s99_limpieza.py --si-borrar
sin tocar el conjunto real del spike. Todo en la solucion SpikesArreglosPago. Idempotente."""
import base64
from sbx_lib import *
NUPKG = os.path.join(AQUI, "tools", "PruebaLimpieza", "bin", "Release", "spk_TmpLimpiezaPkg.1.0.0.nupkg")
PKG, API_TMP = "spk_TmpLimpiezaPkg", "spk_TmpLimpiezaApi"
dv = Dataverse()
est, b, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid&$filter=uniquename eq '{PKG}'")
if not b["value"]:
    est, b, h = dv.call("POST", "pluginpackages", {"name": PKG, "uniquename": PKG, "version": "1.0.0",
                        "content": base64.b64encode(open(NUPKG, "rb").read()).decode()}, solucion=SOLUCION, timeout=600)
    print("POST paquete", est, "" if est in (201, 204) else b); assert est in (201, 204)
est, p, _ = dv.call("GET", f"pluginpackages?$select=pluginpackageid&$filter=uniquename eq '{PKG}'")
pid = p["value"][0]["pluginpackageid"]
est, ens, _ = dv.call("GET", f"pluginassemblies?$select=pluginassemblyid,name,version&$filter=_packageid_value eq {pid}")
print("ensamblados:", [(x["name"], x["version"]) for x in ens["value"]])
est, t, _ = dv.call("GET", f"plugintypes?$select=plugintypeid,typename&$filter=typename eq 'Spk.Tmp.TmpPlugin'")
tid = t["value"][0]["plugintypeid"]
est, a, _ = dv.call("GET", f"customapis?$select=customapiid&$filter=uniquename eq '{API_TMP}'")
if not a["value"]:
    est, r, h = dv.call("POST", "customapis", {"uniquename": API_TMP, "name": "TmpLimpiezaApi", "displayname": "Tmp limpieza", "description": "Descartable",
                        "bindingtype": 0, "isfunction": False, "isprivate": False, "allowedcustomprocessingsteptype": 0, "workflowsdkstepenabled": False,
                        "PluginTypeId@odata.bind": f"/plugintypes({tid})"}, solucion=SOLUCION)
    print("POST api", est, "" if est in (201, 204) else r); assert est in (201, 204)
est, a, _ = dv.call("GET", f"customapis?$select=customapiid&$filter=uniquename eq '{API_TMP}'")
cid = a["value"][0]["customapiid"]
for ent, conj, nom, ty in (("customapirequestparameter", "customapirequestparameters", "Entrada", 10), ("customapiresponseproperty", "customapiresponseproperties", "Salida", 10)):
    est, r, _ = dv.call("GET", f"{conj}?$select=uniquename&$filter=_customapiid_value eq {cid}")
    if not r["value"]:
        est, r, _ = dv.call("POST", conj, {"uniquename": nom, "name": f"{API_TMP}.{nom}", "displayname": nom, "type": ty,
                            "CustomAPIId@odata.bind": f"/customapis({cid})", **({"isoptional": True} if ent.endswith("parameter") else {})}, solucion=SOLUCION)
        print("POST", ent, est); assert est in (201, 204)
print("OK conjunto prueba-limpieza listo")
