"""Tarea C.2: app model-driven 'Spikes Arreglos de Pago' (Unified Interface, clienttype 4) con sitemap (Conversaciones + Correos).
Cada paso se verifica releyendo (capa SIN publicar con RetrieveUnpublishedMultiple, componentes con RetrieveAppComponents, y la capa publicada).
Idempotente: si la app ya existe en cualquier capa, la reutiliza."""
import urllib.parse
from lib import *
dv = DvAdmin(); est = cargar()
UNIQUE = "spk_SpikesArreglosPago"; NOMBRE = "Spikes Arreglos de Pago"; SM_UNIQUE = "spk_SpikesArreglosPagoSitemap"
def titulos(t): return f'<Titles><Title LCID="1033" Title="{t}" /><Title LCID="3082" Title="{t}" /></Titles>'
SITEMAP = ('<SiteMap IntroducedVersion="7.0.0.0"><Area Id="area_spikes" ShowGroups="true" IntroducedVersion="7.0.0.0">' + titulos("Spikes Arreglos de Pago") +
           '<Group Id="grp_spikes" IsProfile="false" IntroducedVersion="7.0.0.0">' + titulos("Pruebas") +
           '<SubArea Id="sub_conversaciones" Entity="spk_conversacion" Client="All" PassParams="false">' + titulos("Conversaciones") + '</SubArea>'
           '<SubArea Id="sub_correos" Entity="email" Client="All" PassParams="false">' + titulos("Correos") + '</SubArea>'
           '</Group></Area></SiteMap>')
def sin_publicar():
    s, b, _ = dv.call("GET", "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?$select=appmoduleid,name,uniquename,clienttype,componentstate,statecode,webresourceid,publishedon,descriptor&$filter=uniquename eq '%s'" % UNIQUE)
    assert s == 200, (s, b); return b["value"]
def publicada():
    s, b, _ = dv.call("GET", "appmodules?$select=appmoduleid,name,uniquename,clienttype,componentstate,statecode,publishedon&$filter=uniquename eq '%s'" % UNIQUE)
    assert s == 200, (s, b); return b["value"]
def componentes(app_id):
    s, b, _ = dv.call("GET", f"RetrieveAppComponents(AppModuleId={app_id})")
    if s == 404: return []   # app sin publicar: RetrieveAppComponents solo ve la capa publicada
    assert s == 200, (s, b)
    return b.get("value", b.get("AppComponents", []))
print("capa sin publicar:", [(a["name"], a["clienttype"], a["componentstate"]) for a in sin_publicar()], "| capa publicada:", [(a["name"], a["clienttype"]) for a in publicada()])
ex = sin_publicar()
if not ex:
    cuerpo = {"name": NOMBRE, "uniquename": UNIQUE, "description": "Spike 4: app minima para las pruebas manuales (conversaciones y correos). Descartable.",
              "clienttype": 4, "webresourceid": "953b9fac-1e5e-e611-80d6-00155ded156f", "navigationtype": 0}
    s, b, h = dv.call("POST", "appmodules", cuerpo, solucion=SOLUCION); print("POST appmodules:", s, "" if s in (200, 201, 204) else b)
    ex = sin_publicar()
assert len(ex) == 1; app = ex[0]; app_id = app["appmoduleid"]
print("RELEER sin publicar:", {k: app[k] for k in ("appmoduleid", "name", "uniquename", "clienttype", "componentstate", "statecode")}); assert app["clienttype"] == 4
# sitemap
s, b, _ = dv.call("GET", f"sitemaps?$select=sitemapid,sitemapnameunique,isappaware&$filter=sitemapnameunique eq '{SM_UNIQUE}'"); assert s == 200
if not b["value"]:
    s, r, h = dv.call("POST", "sitemaps", {"sitemapname": NOMBRE, "sitemapnameunique": SM_UNIQUE, "sitemapxml": SITEMAP, "isappaware": True}, solucion=SOLUCION)
    print("POST sitemaps:", s, "" if s in (200, 201, 204) else r)
    s, b, _ = dv.call("GET", f"sitemaps?$select=sitemapid,sitemapnameunique,isappaware&$filter=sitemapnameunique eq '{SM_UNIQUE}'")
assert len(b["value"]) == 1; sm_id = b["value"][0]["sitemapid"]; print("sitemap:", sm_id, "isappaware:", b["value"][0]["isappaware"])
# entidades
ids = {}
for t in ("spk_conversacion", "email"):
    s, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{t}')?$select=MetadataId,LogicalName"); assert s == 200, e; ids[t] = e["MetadataId"]
def nombres_componentes():
    out = []
    for c in componentes(app_id):
        t = c.get("componenttype"); oid = c.get("objectid")
        if t == 1:
            s, e, _ = dv.call("GET", f"EntityDefinitions({oid})?$select=LogicalName"); out.append(("entity", e.get("LogicalName") if s == 200 else f"?{oid}"))
        elif t == 62: out.append(("sitemap", oid))
        else: out.append((t, oid))
    return out
antes = nombres_componentes(); print("componentes ANTES:", antes)
faltan = [t for t in ids if ("entity", t) not in antes]
comps = [{"@odata.type": "Microsoft.Dynamics.CRM.entity", "entityid": ids[t]} for t in faltan]
if ("sitemap", sm_id) not in antes and not any(k == "sitemap" for k, _ in antes): comps.append({"@odata.type": "Microsoft.Dynamics.CRM.sitemap", "sitemapid": sm_id})
if comps:
    s, r, _ = dv.call("POST", "AddAppComponents", {"AppId": app_id, "Components": comps}); print("AddAppComponents:", s, "(un 204 NO prueba nada, se relee)", "" if s in (200, 204) else r)
s, v, _ = dv.call("GET", f"ValidateApp(AppModuleId={app_id})"); print("ValidateApp:", s, json.dumps(v, ensure_ascii=False)[:600])
px = f"<importexportxml><appmodules><appmodule>{app_id}</appmodule></appmodules></importexportxml>"
s, r, _ = dv.call("POST", "PublishXml", {"ParameterXml": px}); print("PublishXml(app):", s, "" if s in (200, 204) else r)
despues = nombres_componentes(); print("RELEER componentes PUBLICADOS (lo que quedo):", despues)
for t in ids: assert ("entity", t) in despues, f"falta {t} en la app publicada"
assert any(k == "sitemap" for k, _ in despues), "falta el sitemap en la app publicada"
pub = publicada(); print("RELEER capa publicada:", [(a["name"], a["clienttype"], a["componentstate"], a["statecode"], a.get("publishedon")) for a in pub])
assert len(pub) == 1 and pub[0]["clienttype"] == 4
s, smx, _ = dv.call("GET", f"sitemaps({sm_id})?$select=sitemapxml,isappaware,sitemapnameunique"); print("sitemap publicado: isappaware", smx["isappaware"], "| subareas:", [m for m in ("spk_conversacion", "email") if f'Entity="{m}"' in smx["sitemapxml"]])
est["C"] = {**est.get("C", {}), "appid": app_id, "sitemapid": sm_id, "app_unique": UNIQUE}; guardar(est)
print("URL de la app:", f"{dv.org}/main.aspx?appid={app_id}")
