"""Tarea C.3: engancha las TABLAS a la app model-driven. AddAppComponents con Microsoft.Dynamics.CRM.entity no sirve (204 y deja siempre la
tabla de metadatos 'entity', ignora el GUID: trampa conocida y reproducida aqui, ver RESULTADO). Unico camino: ImportSolution.
Para no tocar el resto de la solucion (otro agente trabaja en el paquete de plugins, la Custom API y spk_versionplantilla) se importa un
ZIP MINIMO con la MISMA solucion (SpikesArreglosPago, misma version) que trae SOLO la app y su sitemap: una importacion no administrada
agrega/actualiza lo que viene y no quita lo que no viene. Se comprueba antes y despues que ningun componente de la solucion se perdio."""
import base64, io, re, uuid, zipfile
from lib import *
dv = DvAdmin(); est = cargar(); app_id = est["C"]["appid"]
UNIQUE = "spk_SpikesArreglosPago"; SM = "spk_SpikesArreglosPagoSitemap"
TABLAS = ["spk_conversacion", "email"]
def componentes_solucion():
    s, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'"); sid = b["value"][0]["solutionid"]
    out = set(); url = f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}"
    s, b, _ = dv.call("GET", url); assert s == 200, b
    return {(c["objectid"], c["componenttype"]) for c in b["value"]}
def pasos_estado():
    s, b, _ = dv.call("GET", "sdkmessageprocessingsteps?$select=name,statecode&$filter=contains(name,'Spk') or contains(name,'spk')"); 
    return sorted((x["name"], x["statecode"]) for x in b.get("value", [])) if s == 200 else None
antes = componentes_solucion(); pasos0 = pasos_estado(); print("componentes de la solucion ANTES:", len(antes), "| steps spk:", pasos0)
s, r, _ = dv.call("POST", "ExportSolution", {"SolutionName": SOLUCION, "Managed": False}, timeout=600); assert s == 200, r
z0 = zipfile.ZipFile(io.BytesIO(base64.b64decode(r["ExportSolutionFile"])))
sol = z0.read("solution.xml").decode("utf-8-sig"); cust = z0.read("customizations.xml").decode("utf-8-sig")
raiz = re.search(r"<ImportExportXml[^>]*>", cust).group(0)
def seccion(nombre):
    m = re.search(rf"[ \t]*<{nombre}>.*?</{nombre}>", cust, re.S); assert m, nombre; return m.group(0)
sitemaps = seccion("AppModuleSiteMaps"); apps = seccion("AppModules"); idiomas = seccion("Languages")
assert apps.count("<AppModule>") == 1 and UNIQUE in apps, "se esperaba exactamente la app del spike"
comp = ('<AppModuleComponents>\n' + "".join(f'        <AppModuleComponent type="1" schemaName="{t}" />\n' for t in TABLAS) +
        f'        <AppModuleComponent type="62" schemaName="{SM}" />\n      </AppModuleComponents>')
apps2, n = re.subn(r"<AppModuleComponents>.*?</AppModuleComponents>", comp, apps, flags=re.S); assert n == 1
cust2 = (f'<?xml version="1.0" encoding="utf-8"?>\n{raiz}\n  <Entities />\n  <Roles />\n  <Workflows />\n  <FieldSecurityProfiles />\n  <Templates />\n  <EntityMaps />\n'
         f'  <EntityRelationships />\n  <OrganizationSettings />\n  <optionsets />\n  <CustomControls />\n{sitemaps}\n{apps2}\n  <EntityDataProviders />\n{idiomas}\n</ImportExportXml>')
roots = f'<RootComponents>\n      <RootComponent type="62" schemaName="{SM}" behavior="0" />\n      <RootComponent type="80" schemaName="{UNIQUE}" behavior="0" />\n    </RootComponents>'
sol2, n = re.subn(r"<RootComponents>.*?</RootComponents>", roots, sol, flags=re.S); assert n == 1
buf = io.BytesIO()
with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
    z.writestr("[Content_Types].xml", z0.read("[Content_Types].xml")); z.writestr("solution.xml", sol2); z.writestr("customizations.xml", cust2)
print("zip minimo:", len(buf.getvalue()), "bytes; componentes del zip: sitemap + app; AppModuleComponents:", re.findall(r'type="(\d+)" schemaName="([^"]+)"', comp))
volcar("c_import_customizations.xml.json", {"customizations": cust2, "solution_roots": roots})
job = str(uuid.uuid4())
s, r, _ = dv.call("POST", "ImportSolution", {"OverwriteUnmanagedCustomizations": True, "PublishWorkflows": False,
                  "CustomizationFile": base64.b64encode(buf.getvalue()).decode(), "ImportJobId": job}, timeout=900)
print("ImportSolution:", s, "" if s in (200, 204) else str(r)[:600])
s, j, _ = dv.call("GET", f"importjobs({job})?$select=progress,completedon,startedon,solutionname")
print("importjob:", {k: v for k, v in j.items() if not k.startswith("@")})
s, jd, _ = dv.call("GET", f"importjobs({job})?$select=data"); data = jd.get("data", "")
print("resultados del import (result=...):", re.findall(r'result="(\w+)"', data)[:20], "| errores:", re.findall(r'errortext="([^"]+)"', data)[:5])
est["C"]["import_job"] = job; guardar(est)
despues = componentes_solucion()
print("componentes de la solucion DESPUES:", len(despues), "| perdidos:", sorted(antes - despues), "| nuevos:", sorted(despues - antes))
pasos1 = pasos_estado(); print("steps spk sin cambio de estado:", pasos0 == pasos1, pasos1 if pasos0 != pasos1 else "")
