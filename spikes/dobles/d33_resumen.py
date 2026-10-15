"""Dobles: RESUMENES derivados de salida/*.json (sin red). Produce:
 - salida/40-resumen-fallas.json : cada codigo de falla visto, su tipo exterior, HRESULT, profundidad de InnerFault, claves de ErrorDetails y los casos que lo produjeron (punto (i))
 - salida/41-sonda-resumen.json  : lo que vio el plugin sonda, ordenado por pregunta (punto (f), (g), (11) y la parte de adentro de (16))"""
import glob
import re
from collections import defaultdict
from dlib import *

# ---------------------------------------------------------------- fallas
fallas = defaultdict(lambda: {"veces": 0, "exterior": set(), "HResult_exterior": set(), "largo_cadena": set(), "niveles_InnerFault": set(), "claves_ErrorDetails": set(), "mensajes": [], "casos": []})
GUID = re.compile(r"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}")


def nivel_inner(f):
    n = 0
    while f and f.get("InnerFault"):
        n += 1
        f = f["InnerFault"]
    return n


def recorrer(nodo, archivo):
    if isinstance(nodo, dict):
        if nodo.get("resultado") == "falla" and "falla" in nodo:
            fa = nodo["falla"]
            f = fa.get("fault")
            clave = f["ErrorCode_hex"] if f else "sin-Fault:" + fa["exterior"].split(".")[-1]
            r = fallas[clave]
            r["veces"] += 1
            r["exterior"].add(fa["exterior"].split("[[")[0])
            r["HResult_exterior"].add(fa["cadena"][0]["HResult"])
            r["largo_cadena"].add(len(fa["cadena"]))
            if f:
                r["ErrorCode_decimal"] = f["ErrorCode_decimal"]
                r["niveles_InnerFault"].add(nivel_inner(f))
                r["claves_ErrorDetails"].update(f["ErrorDetails"].keys())
                m = GUID.sub("<guid>", f["Message"]).replace("\n", " ")[:170]
            else:
                m = GUID.sub("<guid>", fa["cadena"][0]["mensaje"])[:170]
            if m not in r["mensajes"] and len(r["mensajes"]) < 3:
                r["mensajes"].append(m)
            if len(r["casos"]) < 8:
                r["casos"].append(archivo + ": " + nodo.get("caso", "")[:110])
        for v in nodo.values():
            recorrer(v, archivo)
    elif isinstance(nodo, list):
        for v in nodo:
            recorrer(v, archivo)


for ruta in sorted(glob.glob(os.path.join(SALIDA, "[0-3]*.json"))):
    nombre = os.path.basename(ruta)
    if nombre.startswith(("31-", "32-")):
        continue
    recorrer(json.load(open(ruta, encoding="utf-8")), nombre)
# las fallas vistas DENTRO del plugin (trazas) tambien cuentan
resumen = {}
for k, r in sorted(fallas.items()):
    resumen[k] = {x: (sorted(y) if isinstance(y, set) else y) for x, y in r.items()}
with open(os.path.join(SALIDA, "40-resumen-fallas.json"), "w", encoding="utf-8") as f:
    json.dump({"cabecera": {"fuente": "salida/[0-3]*.json (fallas capturadas por la consola con ServiceClient 1.2.26)", "nota": "una entrada por ErrorCode; los mensajes no llevan GUID"}, "contenido": resumen}, f, indent=1, ensure_ascii=False)
print("codigos de falla distintos:", len(resumen))
for k, v in resumen.items():
    print(" ", k, v["veces"], "veces |", v["exterior"][0][-45:], "| HResult", v["HResult_exterior"], "| InnerFault", v.get("niveles_InnerFault"), "|", v["mensajes"][0][:90] if v["mensajes"] else "")

# ---------------------------------------------------------------- sonda
tr = json.load(open(os.path.join(SALIDA, "31-sonda-trazas.json"), encoding="utf-8"))["contenido"]
web = json.load(open(os.path.join(SALIDA, "32-ids-webapi.json"), encoding="utf-8"))["contenido"]["etapas"]
CTX = ("stage", "mode", "depth", "UserId_igual_a_InitiatingUserId", "UserId_es_el_usuario_del_spike", "UserId_es_SYSTEM", "InitiatingUserId_es_el_usuario_del_spike", "PrimaryEntityId_vacio", "Target_Id_vacio",
       "Target_Id_igual_a_PrimaryEntityId", "Target_tiene_atributo_id_primario", "OutputParameters_id", "ParentContext", "IsInTransaction", "IsolationMode")
por_esc = defaultdict(list)
for t in tr:
    for l in t["lineas"]:
        por_esc[l["escenario"]].append({"correlacion": t["correlacion"], "creado": t["creado"], **l})

out = {}
# (f) ids
out["f_ids"] = {}
for esc in ("ids", "idsenv"):
    out["f_ids"][esc] = [{"paso": l["paso"], **{k: l[k] for k in CTX if k in l}} for l in sorted(por_esc[esc], key=lambda x: int(x["stage"]) if "stage" in x else 0) if l["paso"].startswith("ctx:")]
out["f_ids"]["idsweb (Web API, POST sin id)"] = sorted(web, key=lambda x: int(x["stage"]))
# (f) SharedVariables
out["f_shared_variables"] = {}
for esc in ("sv", "sys"):
    filas = []
    ctx_actual = None
    for t in sorted([x for x in tr if x["lineas"] and x["lineas"][0]["escenario"] == esc], key=lambda x: x["creado"]):
        pasos = t["lineas"]
        ctx = next((p for p in pasos if p["paso"].startswith("ctx:")), None)
        sh = next((p for p in pasos if p["paso"] == "shared"), None)
        if ctx and sh:
            filas.append({"step": ctx["paso"][4:], "depth": ctx["depth"], "UserId_es_el_usuario_del_spike": ctx["UserId_es_el_usuario_del_spike"], "tiene_la_variable_puesta_en_PreOperation_del_cliente": sh["tiene_spk.sondaPreOp"],
                          "tiene_la_variable_en_ParentContext": sh["padre_tiene_spk.sondaPreOp"], "claves_propias": sh["propias"], "claves_del_padre": sh["padre"]})
        elif ctx:
            filas.append({"step": ctx["paso"][4:], "depth": ctx["depth"], "sin_linea_shared": True})
    ORDEN = ["Create.PreValidation.cliente", "Create.PreOperation.cliente", "Create.PreValidation.conversacion", "Create.PreOperation.conversacion", "Create.PostOperation.conversacion", "Create.PostOperation.cliente"]
    filas.sort(key=lambda f: ORDEN.index(f["step"]) if f["step"] in ORDEN else 99)
    out["f_shared_variables"][esc] = filas
# (g) transaccion
out["g_transaccion"] = {}
for esc in ("tx0", "tx1", "tx2", "tx3", "tx4"):
    pasos = [l for l in por_esc[esc] if not l["paso"].startswith("ctx:") and l["paso"] != "shared"]
    out["g_transaccion"][esc] = [{k: v for k, v in l.items() if k in ("secuencia", "paso", "resultado", "hex", "ErrorCode", "InnerFault", "id_devuelto_igual_al_enviado", "el_plugin_termina_sin_lanzar", "mensaje")} for l in sorted(pasos, key=lambda x: x["secuencia"])]
# (16) tipos
out["tipos_dentro_del_sandbox"] = {}
for t in sorted([x for x in tr if x["lineas"] and x["lineas"][0]["escenario"] == "tipos"], key=lambda x: x["creado"]):
    ctx = next(p for p in t["lineas"] if p["paso"].startswith("ctx:"))
    clave = ctx["paso"][4:] + " #" + t["correlacion"]
    attrs = defaultdict(dict)
    for l in t["lineas"]:
        if l["paso"].endswith(".attr"):
            attrs[l["paso"][:-5]][l["col"]] = l["valor"]
    out["tipos_dentro_del_sandbox"][clave] = {"depth": ctx["depth"], "UserId_igual_a_InitiatingUserId": ctx["UserId_igual_a_InitiatingUserId"], **{k: v for k, v in attrs.items()}}
# rev
out["rev"] = [{k: v for k, v in l.items() if k in ("paso", "nombre_presente")} for l in por_esc["rev"] if l["paso"] == "rev_lanza"]
with open(os.path.join(SALIDA, "41-sonda-resumen.json"), "w", encoding="utf-8") as f:
    json.dump({"cabecera": {"fuente": "salida/31-sonda-trazas.json y 32-ids-webapi.json (trazas de plugintracelogs)", "nota": "booleanos y tipos .NET; ningun GUID de usuario ni texto de columnas"}, "contenido": out}, f, indent=1, ensure_ascii=False)
print("resumen de la sonda escrito")
