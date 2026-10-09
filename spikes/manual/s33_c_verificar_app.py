"""Tarea C: verificacion final de la app, SIEMPRE releyendo: capa sin publicar y publicada, clienttype, componentes (RetrieveAppComponents resuelto a
nombre logico), sitemap publicado (isappaware, subareas), formulario publicado (timeline), roles de la app, y pertenencia a la solucion."""
import re
from lib import *
dv = DvAdmin(); est = cargar(); app_id = est["C"]["appid"]; sm_id = est["C"]["sitemapid"]
px = f"<importexportxml><appmodules><appmodule>{app_id}</appmodule></appmodules></importexportxml>"
print("PublishXml(app):", dv.call("POST", "PublishXml", {"ParameterXml": px})[0])
s, b, _ = dv.call("GET", "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?$select=appmoduleid,name,uniquename,clienttype,componentstate,statecode&$filter=uniquename eq 'spk_SpikesArreglosPago'")
print("sin publicar:", [(a["name"], a["clienttype"], a["componentstate"], a["statecode"]) for a in b["value"]])
s, b, _ = dv.call("GET", "appmodules?$select=appmoduleid,name,uniquename,clienttype,componentstate,statecode,publishedon,url&$filter=uniquename eq 'spk_SpikesArreglosPago'")
pub = b["value"]; print("PUBLICADA:", [(a["name"], "clienttype", a["clienttype"], "estado", a["statecode"], "publicada", a.get("publishedon")) for a in pub]); assert len(pub) == 1 and pub[0]["clienttype"] == 4
s, b, _ = dv.call("GET", f"RetrieveAppComponents(AppModuleId={app_id})")
res = []
for c in b["value"]:
    if c["componenttype"] == 1:
        s2, e, _ = dv.call("GET", f"EntityDefinitions({c['objectid']})?$select=LogicalName"); res.append(("tabla", e.get("LogicalName")))
    else: res.append((c["componenttype"], c["objectid"]))
print("componentes PUBLICADOS:", res)
assert ("tabla", "spk_conversacion") in res and ("tabla", "email") in res and ("tabla", "entity") not in res, "componentes incorrectos"
s, v, _ = dv.call("GET", f"ValidateApp(AppModuleId={app_id})"); r = v["AppValidationResponse"]
print("ValidateApp: exito", r["ValidationSuccess"], "| avisos:", [i["Message"] or "(requeridos del sitemap)" for i in r["ValidationIssueList"]], "| errores:", [i for i in r["ValidationIssueList"] if i["ErrorType"] == "Error"])
s, sm, _ = dv.call("GET", f"sitemaps?$select=sitemapnameunique,isappaware,sitemapxml&$filter=sitemapnameunique eq 'spk_SpikesArreglosPagoSitemap'")
x = sm["value"][0]; print("sitemap: isappaware", x["isappaware"], "| entradas:", re.findall(r'<SubArea Id="(\w+)" Entity="(\w+)"', x["sitemapxml"]))
fid = est["C"]["formid"]; s, f, _ = dv.call("GET", f"systemforms({fid})?$select=formxml,formactivationstate"); print("formulario PUBLICADO con timeline (notescontrol):", 'id="notescontrol"' in f["formxml"], "| activo:", f["formactivationstate"])
s, rr, _ = dv.call("GET", f"appmodules({app_id})/appmoduleroles_association?$select=name"); print("roles con acceso a la app:", [x["name"] for x in rr.get("value", [])] if s == 200 else (s, str(rr)[:200]))
sol = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")[1]["value"][0]["solutionid"]
s, sc, _ = dv.call("GET", f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {sol} and (componenttype eq 80 or componenttype eq 62)")
print("pertenencia a la solucion (80=app, 62=sitemap):", sorted((c["componenttype"], c["objectid"] == (app_id if c["componenttype"] == 80 else sm_id)) for c in sc["value"]))
url = f"{dv.org}/main.aspx?appid={app_id}"; print("URL DE LA APP:", url)
est["C"]["url_app"] = url; guardar(est)
