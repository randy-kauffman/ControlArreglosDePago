"""Q5 (continuacion): combinacion olderthan-x-days 1 OR yesterday, filtro relacionado (link-type any) y vista guardada."""
import json, urllib.parse, datetime as dt
from dv import Dataverse, mostrar, SOLUCION
dv = Dataverse()
est, b, _ = dv.call("GET", "timezonedefinitions?$select=standardname,userinterfacename,bias&$filter=timezonecode eq 92")
print("zona horaria del principal (code 92):", b.get("value"))

def run(etiqueta, fx, campo="spk_nombre"):
    est, b, _ = dv.call("GET", "spk_cuotas?fetchXml=" + urllib.parse.quote(fx, safe=""))
    print(f"  {etiqueta.ljust(52)} HTTP {est}", [r[campo] for r in b["value"]] if est == 200 else str(b)[:300])
OR_VENCIDA = ("<filter type='or'><condition attribute='spk_fechavencimiento' operator='olderthan-x-days' value='1'/>"
              "<condition attribute='spk_fechavencimiento' operator='yesterday'/></filter>")
print("Cuotas (hijas), condicion 'vencida' = olderthan-x-days 1 OR yesterday, y sin pago:")
run("vencida OR + sin pago", "<fetch><entity name='spk_cuota'><attribute name='spk_nombre'/><filter type='and'><condition attribute='spk_nombre' operator='like' value='Q5-%'/>"
    + OR_VENCIDA + "<condition attribute='spk_fechapago' operator='null'/></filter></entity></fetch>")

# Filtro relacionado: arreglos con AL MENOS UNA cuota vencida sin pago (link-type any)
def run_padre(etiqueta, fx):
    est, b, _ = dv.call("GET", "spk_arreglos?fetchXml=" + urllib.parse.quote(fx, safe=""))
    print(f"  {etiqueta.ljust(52)} HTTP {est}", sorted(r["spk_nombre"] for r in b["value"]) if est == 200 else str(b)[:400])
LINK = ("<fetch><entity name='spk_arreglo'><attribute name='spk_nombre'/><filter><condition attribute='spk_nombre' operator='like' value='Q5-P%'/></filter>"
        "<link-entity name='spk_cuota' from='spk_arregloid' to='spk_arregloid' link-type='any'><filter type='and'>"
        + OR_VENCIDA + "<condition attribute='spk_fechapago' operator='null'/></filter></link-entity></entity></fetch>")
print("\nArreglos con alguna cuota vencida sin pago (esperado: Q5-PA solo; PA tiene 2 cuotas vencidas -> aparece UNA vez):")
run_padre("link-type=any, vencida OR + sin pago", LINK)
LINK_OLD = LINK.replace(OR_VENCIDA, "<condition attribute='spk_fechavencimiento' operator='olderthan-x-days' value='1'/>")
run_padre("link-type=any, SOLO olderthan-x-days 1 + sin pago", LINK_OLD)

# Vista guardada (savedquery) con ese fetch, en la solucion; se ejecuta con savedQuery=
fx_vista = LINK.replace("<filter><condition attribute='spk_nombre' operator='like' value='Q5-P%'/></filter>", "").replace(
    "<attribute name='spk_nombre'/>", "<attribute name='spk_nombre'/><attribute name='spk_arregloid'/>")
layout = ("<grid name='resultset' object='%s' jump='spk_nombre' select='1' icon='1' preview='1'><row name='result' id='spk_arregloid'>"
          "<cell name='spk_nombre' width='200'/></row></grid>")
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_arreglo')?$select=ObjectTypeCode")
otc = b["ObjectTypeCode"]
est, b, _ = dv.call("GET", "savedqueries?$select=savedqueryid,name&$filter=name eq 'SPK Arreglos en riesgo'")
if not b["value"]:
    est, b, h = dv.call("POST", "savedqueries", {"name": "SPK Arreglos en riesgo", "returnedtypecode": "spk_arreglo", "querytype": 0,
                                                  "fetchxml": fx_vista, "layoutxml": layout % otc, "isdefault": False}, solucion=SOLUCION)
    mostrar("POST savedquery", est, b)
est, b, _ = dv.call("GET", "savedqueries?$select=savedqueryid,name,fetchxml&$filter=name eq 'SPK Arreglos en riesgo'")
vid = b["value"][0]["savedqueryid"]; print("vista releida:", vid, "| fetch contiene yesterday:", "yesterday" in b["value"][0]["fetchxml"], "| link any:", "link-type=\"any\"" in b["value"][0]["fetchxml"] or "link-type='any'" in b["value"][0]["fetchxml"])
est, b, _ = dv.call("POST", "PublishXml", {"ParameterXml": "<importexportxml><entities><entity>spk_arreglo</entity></entities></importexportxml>"})
print("PublishXml", est)
est, b, _ = dv.call("GET", f"spk_arreglos?savedQuery={vid}")
print("EJECUTAR vista guardada: HTTP", est, [r["spk_nombre"] for r in b["value"]] if est == 200 else str(b)[:300])
