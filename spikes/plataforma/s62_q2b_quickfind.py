"""Q2b: columna segura como 'find column' de la vista de busqueda rapida, en los dos sentidos.
Hallazgo previo: PATCH de fetchxml de la vista Quick Find exige mandar ademas layoutxml + name + returnedtypecode
(solo fetchxml da 400 'An unexpected error occurred', incluso sin cambios)."""
import json
from dv import Dataverse, mostrar
from spk_lib import *
dv = Dataverse()
VID = "90a86166-6276-471d-921d-bbe3c9c3edf4"
FX_ORIGINAL = ('<fetch version="1.0" mapping="logical" savedqueryid="90A86166-6276-471D-921D-BBE3C9C3EDF4"><entity name="spk_cliente">'
               '<attribute name="spk_clienteid" /><attribute name="spk_nombre" /><attribute name="createdon" />'
               '<order attribute="spk_nombre" descending="false" /><filter type="and"><condition attribute="statecode" operator="eq" value="0" /></filter>'
               '<filter type="or" isquickfindfields="1"><condition attribute="spk_nombre" operator="like" value="{0}" /></filter></entity></fetch>')
ANCHOR = '<condition attribute="spk_nombre" operator="like" value="{0}" />'
def cond(c): return f'<condition attribute="{c}" operator="like" value="{{0}}" />'
def leer():
    est, b, _ = dv.call("GET", f"savedqueries({VID})?$select=fetchxml,layoutxml,name,returnedtypecode"); return b
def poner(fx):
    v = leer()
    return dv.call("PATCH", f"savedqueries({VID})", {"fetchxml": fx, "layoutxml": v["layoutxml"], "name": v["name"], "returnedtypecode": v["returnedtypecode"]})
def publicar():
    return escribir(dv, "POST", "PublishXml", {"ParameterXml": "<importexportxml><entities><entity>spk_cliente</entity></entities></importexportxml>"}, solucion=None)[0]
def find_cols(fx):
    i = fx.index('isquickfindfields'); seg = fx[i:]
    import re
    return re.findall(r'attribute="([a-z_0-9]+)"', seg[:seg.index('</filter>')])

est, b, _ = poner(FX_ORIGINAL); print("restaurar original:", est, "| find columns:", find_cols(leer()["fetchxml"]))

# Sentido 1: cedula YA segura -> agregarla a las find columns
est, b, _ = poner(FX_ORIGINAL.replace(ANCHOR, ANCHOR + cond("spk_cedula")))
print("S1 PATCH (agregar spk_cedula SEGURA):", est, json.dumps(b)[:400])
print("   publicar:", publicar(), "| RELEER find columns:", find_cols(leer()["fetchxml"]))

# Sentido 2: columna comun nueva (spk_correo), a find columns, y DESPUES protegerla
est, a, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_correo')?$select=LogicalName")
if est == 404:
    est, b, _ = escribir(dv, "POST", "EntityDefinitions(LogicalName='spk_cliente')/Attributes", col_texto("spk_Correo", "Correo", 50)); print("alta spk_correo:", est)
est, b, _ = poner(leer()["fetchxml"].replace(ANCHOR, ANCHOR + cond("spk_correo")))
print("S2 PATCH (agregar spk_correo comun):", est, "| RELEER find columns:", find_cols(leer()["fetchxml"]))
publicar()
est, a, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_correo')/Microsoft.Dynamics.CRM.StringAttributeMetadata")
a["@odata.type"] = T + "StringAttributeMetadata"; a["IsSecured"] = True
est, b, _ = escribir(dv, "PUT", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_correo')", a, cabeceras={"MSCRM.MergeLabels": "true"})
mostrar("S2 PUT IsSecured=true sobre columna que YA es find column", est, b, 600)
est, a2, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_correo')?$select=LogicalName,IsSecured")
print("   RELEER spk_correo.IsSecured =", a2["IsSecured"], "| find columns:", find_cols(leer()["fetchxml"]))
