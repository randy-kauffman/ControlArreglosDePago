"""Tarea C.1: agrega el TIMELINE (control de actividades, notescontrol) al formulario principal de spk_conversacion.
Patron: un formulario real del entorno (HB incident, Account): <control id="notescontrol" classid="{06375649-C143-495E-A496-C962E5B4488E}"/>
en una celda sin etiqueta y con rowspan. Relee el XML PUBLICADO despues de PublishXml (la lectura previa debe seguir mostrando el viejo)."""
import uuid, re
from lib import *
dv = DvAdmin(); est = cargar()
s, b, _ = dv.call("GET", "systemforms?$select=formid,name,type,formxml,ismanaged&$filter=objecttypecode eq 'spk_conversacion' and type eq 2")
assert s == 200 and len(b["value"]) == 1, b
f = b["value"][0]; fid = f["formid"]; xml = f["formxml"]
CLASS = "06375649-C143-495E-A496-C962E5B4488E"
print("formulario principal:", fid, "| ya tiene timeline:", CLASS.lower() in xml.lower())
if CLASS.lower() not in xml.lower():
    g = lambda: "{" + str(uuid.uuid4()).upper() + "}"
    celda = (f'<cell id="{g()}" showlabel="false" colspan="1" rowspan="14" auto="false"><labels><label description="Timeline" languagecode="1033" /></labels>'
             f'<control id="notescontrol" classid="{{{CLASS}}}" /></cell>')
    seccion = (f'<section name="timeline_section" showlabel="false" showbar="false" columns="1" id="{g()}" IsUserDefined="0"><labels><label description="Timeline" languagecode="1033" /></labels>'
               f'<rows><row>{celda}</row>' + "<row />" * 13 + '</rows></section>')
    assert xml.count('<column width="100%"><sections>') == 1 and xml.count("</sections></column></columns>") == 1
    nuevo = xml.replace('<column width="100%"><sections>', '<column width="55%"><sections>').replace(
        "</sections></column></columns>", f'</sections></column><column width="45%"><sections>{seccion}</sections></column></columns>')
    s, r, _ = dv.call("PATCH", f"systemforms({fid})", {"formxml": nuevo}, solucion=SOLUCION)
    print("PATCH formxml:", s, "" if s in (200, 204) else r)
    s, b2, _ = dv.call("GET", f"systemforms({fid})?$select=formxml"); print("capa publicada ANTES de publicar ya trae timeline:", CLASS.lower() in b2["formxml"].lower(), "(esperado False: GET lee lo publicado)")
    px = "<importexportxml><entities><entity>spk_conversacion</entity></entities></importexportxml>"
    s, r, _ = dv.call("POST", "PublishXml", {"ParameterXml": px}); print("PublishXml:", s, "" if s in (200, 204) else r)
s, b3, _ = dv.call("GET", f"systemforms({fid})?$select=formxml,name,type,formactivationstate,ismanaged,componentstate")
x3 = b3["formxml"]
print("RELEER publicado: timeline presente:", CLASS.lower() in x3.lower(), "| notescontrol:", 'id="notescontrol"' in x3, "| spk_nombre:", 'datafieldname="spk_Nombre"' in x3, "| activo:", b3["formactivationstate"])
assert CLASS.lower() in x3.lower()
volcar("c_formulario_publicado.xml.json", {"formid": fid, "formxml": x3})
s, b4, _ = dv.call("GET", f"solutioncomponents?$select=componenttype&$filter=objectid eq {fid}")
sol = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")[1]["value"][0]["solutionid"]
s, b5, _ = dv.call("GET", f"solutioncomponents?$select=componenttype&$filter=objectid eq {fid} and _solutionid_value eq {sol}")
print("formulario como componente de la solucion (tipo 60 esperado):", [c["componenttype"] for c in b5["value"]], "(vacio = viaja dentro de la tabla)")
est["C"] = {**est.get("C", {}), "formid": fid}; guardar(est)
