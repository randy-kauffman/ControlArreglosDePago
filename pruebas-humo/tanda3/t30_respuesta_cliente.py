"""Tanda 3, caso A (cierre; correo 2 de la tanda): el buzón 2 contesta (createReply) la respuesta R1 que le llegó desde la cola.
H-5 (final): la respuesta del cliente entra a la MISMA conversación (no se crea otra). H-25: ¿Exchange ya trae el Regarding en el
Create (ruta A), lo encuentra S-34 por In-Reply-To (ruta B) o queda por huella (C, defecto)? Y la conversación vuelve a pendiente."""
from t3lib import *

dv = adm()
R = Res("30_respuesta_cliente.json")
e = est3()
a = e["caso_a"]
cid = a["conversacion_id"]
if "respuesta_cliente" not in a:
    c = Correo()
    env = c.responder(2, a["r1"]["graph_msg_id_b2"], "Respuesta del cliente HUMO-T3 caso A (datos ficticios).")
    R.v("A2", "Graph acepta la respuesta del buzón 2", env["http"] == 202, 202, env["http"])
    a["respuesta_cliente"] = env
    poner("caso_a", a)
rc = a["respuesta_cliente"]
fila, seg = esperar_email(dv, rc["internetMessageId"], rc["t_envio_epoch"])
R.v("A2", "la respuesta del cliente entra a Dataverse", fila is not None, "Email entrante", {"segundos_desde_el_envio": seg})
assert fila
xid = fila["activityid"]
rc["email_id"] = xid
poner("caso_a", a)
R.v("H-25", "¿el Email ya nace con Regarding a la conversación? (correlación de Exchange en el Create)", True, "informativo",
    {"regarding_en_la_primera_lectura": fila["_regardingobjectid_value"], "correlationmethod": fila.get("correlationmethod"),
     "inreplyto_informado": bool(fila.get("inreplyto"))})

def apuntado():
    x = email(dv, xid)
    return x if x.get("_regardingobjectid_value") else None
x, s = esperar(apuntado, 600, 10)
x = x or email(dv, xid)
R.v("H-5", "la respuesta del cliente queda en la MISMA conversación", x["_regardingobjectid_value"] == cid, cid, {"regarding": x["_regardingobjectid_value"], "segundos": s})
time.sleep(20)
tr = resumen_trazas(trazas(dv, datetime.fromtimestamp(rc["t_envio_epoch"] - 60, timezone.utc), esperar=30))
mias = [t for t in tr if any(xid in l for l in t["lineas"])]
volcar("30_trazas.json", mias)
R.v("H-25", "ruta de S-34 para la respuesta del cliente", any("Unida" in l or "YaTenia" in l for t in mias for l in t["lineas"]), "Unida (A o B), sin conversación nueva",
    [l for t in mias for l in t["lineas"] if "ruta" in l or "resultado" in l])
hist = cambios_de(dv, "emails", xid)
volcar("30_auditoria_email.json", hist)
R.v("H-25", "historial: quién escribió el Regarding (el Create de la sincronización o SYSTEM después)", True, "informativo",
    [{"cuando": h["cuando"], "operacion": h["operacion"], "usuario": h["usuario"], "regarding": h["cols"].get("_regardingobjectid_value")} for h in hist] if isinstance(hist, list) else hist)
nuevas = get(dv, f"sanic_carp_tbl_conversacions?$select=sanic_nombre&$filter=createdon ge {iso(datetime.fromtimestamp(rc['t_envio_epoch'] - 60, timezone.utc))}")["value"]
R.v("H-5", "no se creó ninguna conversación nueva por la respuesta", len(nuevas) == 0, 0, [n["sanic_nombre"] for n in nuevas])
cv = conv(dv, cid)
R.v("A2", "la conversación vuelve a pendiente desde la llegada de la respuesta, sigue Abierta y con 1 respuesta",
    cv["sanic_sinrespuestadesde"] == x["actualend"] and cv["statuscode"] == CONV_ABIERTA and cv["sanic_cantidadrespuestas"] == 1,
    x["actualend"], {"pendiente": cv["sanic_sinrespuestadesde"], "estado": cv["statuscode"], "cantidad": cv["sanic_cantidadrespuestas"]})
R.v("H-26", "la respuesta también queda en la BU y del team del área", x["_owningbusinessunit_value"] == BU_AREA and x["_ownerid_value"] == TEAM_DEF, "BU/team", (x["_owningbusinessunit_value"], x["_ownerid_value"]))
volcar("30_email.json", limpio(x))
volcar("30_conversacion.json", limpio(cv))
