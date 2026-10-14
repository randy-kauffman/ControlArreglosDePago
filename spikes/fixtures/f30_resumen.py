"""Fixtures: resumen de los TIPOS observados (lee salida/*.json, no toca Dev). Guarda evidencia/30_tipos_observados.json y lo imprime."""
import json
from flib import *


def cargar(n):
    return json.load(open(os.path.join(SALIDA, n), encoding="utf-8"))["contenido"]


def corto(a):
    return a["tipo"].replace("Microsoft.Xrm.Sdk.", "").replace("System.", "") if a and a.get("tipo") else None


def det(a):
    if not a or not a.get("tipo"):
        return ""
    v = a["valor"]
    if a["tipo"] == "System.DateTime":
        return f"Kind={a['kind']} {v}"
    if isinstance(v, dict) and "logicalName" in v:
        return f"-> {v['logicalName']} (Name {'null' if v['name'] is None else 'presente'})"
    if a["tipo"] == "System.Decimal":
        return f"'{v}'"
    if isinstance(v, (str, int, bool)) and a["tipo"] != "System.Guid":
        return str(v) if len(str(v)) < 40 else str(v)[:37] + "..."
    return ""


filas = []


def fila(fx, ent, attr, a):
    if a is not None:
        filas.append({"fixture": fx, "entidad": ent, "atributo": attr, "tipo": corto(a), "detalle": det(a)})
    else:
        filas.append({"fixture": fx, "entidad": ent, "atributo": attr, "tipo": "(AUSENTE: la columna no viene en Attributes)", "detalle": ""})


# 01 email
e = cargar("01-email.json")
for dir_ in ("entrante", "saliente"):
    m = e[f"retrieve_columnset_true_{dir_}"][0]["atributos"]
    for k in ("statuscode", "statecode", "directioncode", "messageid", "inreplyto", "actualend", "senton", "createdby", "regardingobjectid", "ownerid", "from", "to"):
        fila("01", f"email real {dir_}", k, m.get(k))
s = e["sinteticos_con_regardingobjectid_a_la_conversacion"]
for k in ("entrante", "saliente"):
    fila("01", f"email sintetico {k}", "regardingobjectid", s[k]["atributos"].get("regardingobjectid"))
# 02 conversacion
c = cargar("02-conversacion.json")
for est in ("abierta", "finalizada", "unida"):
    a = c[est]["atributos"]
    for k in ("statecode", "statuscode", "sanic_clienteid", "sanic_unidaaid", "sanic_finalizadoporid", "sanic_resultado", "sanic_fechaprimercorreo", "sanic_fechafinalizacion", "sanic_cantidadrespuestas", "sanic_correonuevoenfinalizada", "ownerid", "createdon"):
        if est == "abierta" or k in ("statecode", "statuscode", "sanic_unidaaid", "sanic_finalizadoporid", "sanic_resultado", "sanic_fechafinalizacion"):
            fila("02", f"conversacion {est}", k, a.get(k))
    filas.append({"fixture": "02", "entidad": f"conversacion {est}", "atributo": "Entity.RowVersion", "tipo": "String", "detalle": c[est]["RowVersion"]})
# 03 arreglo
a3 = cargar("03-arreglo.json")
for est, clave in (("borrador congelado", "borrador_congelado"), ("vigente", "vigente")):
    a = a3[clave]["atributos"]
    for k in ("statecode", "statuscode", "sanic_producto", "sanic_saldoinicial", "sanic_cuotamensual", "sanic_plazo", "sanic_fechainicio", "sanic_fechaformalizacion", "sanic_fechacongelamiento", "sanic_fechaformalizado", "sanic_numeroarreglo", "sanic_oficialid", "sanic_clienteid", "sanic_conversacionid", "sanic_versionplantillaid"):
        if k in a or est == "vigente" or k in ("sanic_fechacongelamiento", "sanic_numeroarreglo"):
            fila("03", f"arreglo {est}", k, a.get(k))
# 04
t = cargar("04-teammembership-team-queue.json")
for k in ("teammembership_de_usuario_conectado", "teammembership_de_usuario_de_aplicacion_no_administrador"):
    for attr, a in t[k]["Entities"][0]["atributos"].items():
        fila("04", "teammembership", attr, a)
    break
ta = t["team_isdefault_de_la_BU_Arreglos_de_Pago"]["Entities"][0]["atributos"]
for k in ("isdefault", "teamtype", "businessunitid", "name"):
    fila("04", "team", k, ta.get(k))
for q in t["queue_con_emailaddress"][:1]:
    fila("04", "queue", "emailaddress", q["atributos"].get("emailaddress"))
# 05
v = cargar("05-environmentvariable.json")
for k in ("schemaname", "type", "defaultvalue"):
    fila("05", "environmentvariabledefinition", k, v["definicion_descartable"]["atributos"].get(k))
for k in ("value", "environmentvariabledefinitionid", "schemaname"):
    fila("05", "environmentvariablevalue", k, v["valor_descartable"]["atributos"].get(k))
# 06
u = cargar("06-whoami-systemuser.json")
for k, a in u["WhoAmIResponse_del_usuario_conectado"]["Results"].items():
    fila("06", u["WhoAmIResponse_del_usuario_conectado"]["tipo"].split(".")[-1] + ".Results", k, a)
for etq, k in (("SYSTEM", "systemuser_SYSTEM"), ("ordinario (app conectada)", "systemuser_ordinario_usuario_de_aplicacion_conectado")):
    for attr in ("fullname", "isdisabled", "businessunitid", "accessmode", "systemuserid"):
        fila("06", f"systemuser {etq}", attr, u[k]["atributos"].get(attr))
# 08
x = cargar("08-aliased-y-agregados.json")
e0 = x["querybyexpression_con_joins_alias_cli_ver_conv"]["Entities"][0]["atributos"]
for k in ("cli.sanic_nombre", "cli.statecode", "cli.createdby", "cli.createdon", "ver.sanic_estado", "ver.sanic_capacidad", "ver.sanic_aprobadoporid", "conv.statuscode", "conv.sanic_fechaprimercorreo"):
    a = e0.get(k)
    if a:
        inner = a["valor"]["valor"]
        filas.append({"fixture": "08", "entidad": "arreglo + joins", "atributo": k, "tipo": "AliasedValue -> " + corto(inner), "detalle": det(inner)})
for k in ("total", "ultima", "suma"):
    a = x["fetchxml_agregado_count_max_sum"]["Entities"][0]["atributos"][k]
    filas.append({"fixture": "08", "entidad": "fetchxml agregado", "atributo": k, "tipo": "AliasedValue -> " + corto(a["valor"]["valor"]), "detalle": det(a["valor"]["valor"])})
a = x["fetchxml_agregado_groupby_statuscode"]["Entities"][0]["atributos"]["estado"]
filas.append({"fixture": "08", "entidad": "fetchxml groupby", "atributo": "estado", "tipo": "AliasedValue -> " + corto(a["valor"]["valor"]), "detalle": det(a["valor"]["valor"])})

# 07
f7 = cargar("07-fallas.json")
fallas = []
for k, etq in (("k01_violacion_de_clave_alterna", "K-01 (clave alterna)"), ("version_de_fila_no_coincide", "version de fila no coincide"), ("retrieve_de_fila_inexistente", "Retrieve de fila inexistente")):
    f = f7[k]["falla"]
    ft = f["fault"]
    fallas.append({"caso": etq, "tipo_exterior": f["exterior"].split("`")[0] + "<OrganizationServiceFault>", "cadena_de_tipos_largo": len(f["cadena_de_tipos"]),
                   "exterior_es_FaultException_OSF": f["el_exterior_es_FaultException_OrganizationServiceFault"], "ErrorCode_dec": ft["ErrorCode_decimal"], "ErrorCode_hex": ft["ErrorCode_hex"],
                   "claves_ErrorDetails": {a: b["tipo_del_valor"].replace("System.", "") for a, b in ft["ErrorDetails"].items()}, "tiene_InnerFault": ft["InnerFault"] is not None,
                   "Timestamp_Kind": ft["Timestamp"]["kind"]})
pag = f7["paginacion"]
paginacion = {k: {"cantidad": pag[k]["cantidad"], "MoreRecords": pag[k]["MoreRecords"], "TotalRecordCount": pag[k]["TotalRecordCount"],
                  "TotalRecordCountLimitExceeded": pag[k]["TotalRecordCountLimitExceeded"], "PagingCookie_forma": (pag[k]["PagingCookie"] or "")[:30], "MinActiveRowVersion": pag[k]["MinActiveRowVersion"]}
              for k in ("pagina_1", "pagina_2")}
paginacion["pagina_1_sin_ReturnTotalRecordCount"] = pag["pagina_1_sin_ReturnTotalRecordCount"]

# EntityReference.Name nulo vs presente en todos los fixtures (en Retrieve/RetrieveMultiple)
nulos, presentes = {}, {}


def recorre(n):
    if isinstance(n, dict):
        if n.get("tipo") == "Microsoft.Xrm.Sdk.EntityReference":
            k = n["valor"]["logicalName"]
            (nulos if n["valor"]["name"] is None else presentes)[k] = (nulos if n["valor"]["name"] is None else presentes).get(k, 0) + 1
        for v in n.values():
            recorre(v)
    elif isinstance(n, list):
        for v in n:
            recorre(v)


for nombre in sorted(os.listdir(SALIDA)):
    if nombre.endswith(".json"):
        recorre(json.load(open(os.path.join(SALIDA, nombre), encoding="utf-8")))

res = {"atributos_clave": filas, "fallas": fallas, "paginacion": paginacion, "EntityReference_Name_nulo_por_tabla_destino": nulos, "EntityReference_Name_presente_por_tabla_destino": presentes}
volcar("30_tipos_observados.json", res)
for r in filas:
    print(f"{r['fixture']} | {r['entidad']:<34} | {r['atributo']:<34} | {r['tipo']:<42} | {r['detalle']}")
print(json.dumps({k: v for k, v in res.items() if k != "atributos_clave"}, indent=1))
