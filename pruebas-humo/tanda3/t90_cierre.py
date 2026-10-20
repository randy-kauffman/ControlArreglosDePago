"""Tanda 3, cierre (solo lecturas): trabajos asíncronos de S-34/S-35 desde el inicio de la tanda (ninguno fallido), datos del
gráfico 12.15 (respuestas con tiempo y oficial), steps habilitados y conteo exacto de correos de la tanda."""
from t3lib import *

dv = adm()
R = Res("90_cierre.json")
e = est3()
desde = "2026-10-20T19:30:00Z"
jobs = get(dv, f"asyncoperations?$select=name,statuscode,createdon,message&$filter=createdon ge {desde} and operationtype eq 1&$top=500")["value"]
carp = [j for j in jobs if "Sanic.Carp" in (j["name"] or "") or "CARP" in (j["name"] or "")]
fallidos = [{"name": j["name"], "statuscode": j["statuscode"], "msg": (j.get("message") or "")[:200]} for j in carp if j["statuscode"] != 30]
R.v("jobs", "trabajos asíncronos de los plugins CARP desde las 19:30: todos Succeeded", not fallidos, 0, {"total": len(carp), "no_exitosos": fallidos})
resp = get(dv, f"emails?$select=sanic_tiemporespuesta,_sanic_respondidoporid_value&$filter=_sanic_respondidoporid_value ne null")["value"]
R.v("12.15", "el gráfico Promedio por respuesta ya tiene datos (respuestas con oficial)", len(resp) >= 2, ">= 2",
    [{"oficial": x["_sanic_respondidoporid_value"], "minutos": x["sanic_tiemporespuesta"]} for x in resp])
st = get(dv, "sdkmessageprocessingsteps?$select=statecode&$filter=startswith(name,'STEP - CARP')")["value"]
R.v("base", "los 37 steps CARP siguen habilitados", len(st) == 37 and all(s["statecode"] == 0 for s in st), 37, sum(s["statecode"] == 0 for s in st))
env, sal = e.get("correos_enviados", []), e.get("correos_salientes", [])
R.v("correo", "correos enviados por Graph (solo buzones 2-4 hacia el buzón 1)", len(env) <= TOPE_CORREOS and all(x["para"] == "PP_MAIL_BUZON_1" and x["desde"] in ("PP_MAIL_BUZON_2", "PP_MAIL_BUZON_3", "PP_MAIL_BUZON_4") for x in env),
    f"<= {TOPE_CORREOS}", [(x["desde"], x["para"], x["asunto"]) for x in env])
R.v("correo", "respuestas que salieron desde la cola (buzón 1)", all(x["para"] in ("PP_MAIL_BUZON_2", "PP_MAIL_BUZON_3") for x in sal), "solo a buzones 2 y 3",
    [(x["desde"], x["para"], x["asunto"]) for x in sal])
