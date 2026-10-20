"""Tanda 3, caso A (sigue): User 8 (Oficial, rol por el team) toma la conversación y responde desde Dataverse por la cola.
- Tomar con un usuario real (MSCRMCallerID).
- S-31 (DC-07): el remitente por defecto (el propio usuario) se reemplaza por la queue; otro usuario como remitente da CARP-EML-001.
- H-28 (a, b): un borrador saliente sin Regarding con el usuario como remitente; un Update que le pone Regarding a la conversación
  dispara S-37, que REEMPLAZA el remitente por la queue (no lo acumula). Ese borrador no se envía.
- H-5: la respuesta R1 sale por la queue con `parentactivityid` = el correo del cliente y llega al buzón 2 desde el buzón 1.
- H-6 / S-35: al pasar a Sent se llenan `sanic_respondidoporid` (= User 8) y `sanic_tiemporespuesta`, y se recalcula la conversación.
- H-28 (c): el Update interno del envío no rompe el remitente (sigue la queue) ni lo rechaza S-37.
Sale UN correo (R1, desde el buzón 1 hacia el buzón 2)."""
from t3lib import *

dv = adm()
R = Res("20_tomar_y_responder.json")
e = est3()
a = e["caso_a"]
eid, cid = a["email_id"], a["conversacion_id"]
c = Correo()
CALL8 = {"MSCRMCallerID": OFICIAL}


def partes_from(xid):
    r = get(dv, f"emails({xid})/email_activity_parties?$select=participationtypemask,_partyid_value,addressused&$filter=participationtypemask eq 1")
    return [{"partyid": p["_partyid_value"], "tipo": p.get("_partyid_value@Microsoft.Dynamics.CRM.lookuplogicalname")} for p in r["value"]]


def partes_from_tipadas(xid):
    st, r, _ = dv.call("GET", f"emails({xid})/email_activity_parties?$select=participationtypemask,_partyid_value&$filter=participationtypemask eq 1",
                       cabeceras={"Prefer": 'odata.include-annotations="Microsoft.Dynamics.CRM.lookuplogicalname"'})
    return [(p["_partyid_value"], p.get("_partyid_value@Microsoft.Dynamics.CRM.lookuplogicalname")) for p in r.get("value", [])]


# 1) Tomar como User 8
cv = conv(dv, cid)
if cv["_sanic_oficialasignadoid_value"] != OFICIAL:
    st, b, t = llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": cid}, caller=OFICIAL)
    R.v("TOM", "User 8 toma la conversación (rol Oficial por el team)", st == 200, 200, (st, t, b if st != 200 else {k: v for k, v in b.items() if not k.startswith("@")}))
cv = conv(dv, cid)
R.v("TOM", "la conversación queda asignada a User 8", cv["_sanic_oficialasignadoid_value"] == OFICIAL, OFICIAL, cv["_sanic_oficialasignadoid_value"])

base_resp = {"description": "<p>Respuesta HUMO-T3 caso A desde la cola (datos ficticios).</p>", "directioncode": True,
             "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({cid})",
             "parentactivityid@odata.bind": f"/emails({eid})"}
asunto_r1 = "RE: " + a["asunto"]

# 2) S-31 rechaza otro usuario como remitente (no se crea nada)
if "s31_rechazo" not in a:
    cuerpo = dict(base_resp, subject=asunto_r1, email_activity_parties=[
        {"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({JEFE})"},
        {"participationtypemask": 2, "addressused": c.b[2]}])
    st, b, _ = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    a["s31_rechazo"] = {"http": st, "codigo": codigo(b)}
    poner("caso_a", a)
R.v("S-31", "otro usuario como remitente: CARP-EML-001 y sin fila", a["s31_rechazo"]["codigo"] == "CARP-EML-001", "CARP-EML-001", a["s31_rechazo"])

# 3) H-28: borrador sin Regarding con remitente = User 8, luego Update con Regarding (no se envía)
if "h28_borrador" not in a:
    cuerpo = {"subject": "HUMO-T3-H28 borrador (no se envía)", "description": "<p>Borrador HUMO-T3 para S-37.</p>", "directioncode": True,
              "email_activity_parties": [{"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
                                         {"participationtypemask": 2, "addressused": c.b[2]}]}
    st, b, h = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    R.v("H-28", "borrador saliente sin Regarding creado por User 8", st == 204, 204, st if st == 204 else err(b))
    bid = Dataverse.id_creado(h)
    a["h28_borrador"] = {"id": bid, "from_antes": partes_from_tipadas(bid)}
    st, b, _ = dv.call("PATCH", f"emails({bid})", {"regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({cid})"}, cabeceras=CALL8)
    a["h28_borrador"]["patch_regarding"] = {"http": st, "codigo": codigo(b) if st >= 300 else None, "error": err(b) if st >= 300 else None}
    a["h28_borrador"]["from_despues"] = partes_from_tipadas(bid)
    poner("caso_a", a)
hb = a["h28_borrador"]
R.v("H-28", "(a) el Update con Regarding a la conversación pasa", hb["patch_regarding"]["http"] == 204, 204, hb["patch_regarding"])
R.v("H-28", "(b) S-37 deja como remitente SOLO la queue (reemplaza, no acumula)",
    hb["from_despues"] == [[COLA, "queue"]] or hb["from_despues"] == [(COLA, "queue")], [COLA, "queue"], {"antes": hb["from_antes"], "despues": hb["from_despues"]})

# 4) R1: respuesta con remitente por defecto (User 8) -> S-31 pone la queue; se envía como User 8
if "r1" not in a:
    cuerpo = dict(base_resp, subject=asunto_r1, email_activity_parties=[
        {"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
        {"participationtypemask": 2, "addressused": c.b[2]}])
    st, b, h = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    R.v("H-5", "User 8 crea la respuesta en la conversación", st == 204, 204, st if st == 204 else err(b))
    rid = Dataverse.id_creado(h)
    a["r1"] = {"id": rid, "from_al_crear": partes_from_tipadas(rid)}
    poner("caso_a", a)
    t0 = time.time()
    st, b, _ = dv.call("POST", f"emails({rid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""}, cabeceras=CALL8)
    a["r1"].update({"sendemail_http": st, "sendemail_error": err(b) if st >= 300 else None, "t_send_epoch": t0})
    poner("caso_a", a)
    if st < 300:
        e2 = leer_estado()
        e2.setdefault("correos_salientes", []).append({"desde": "PP_MAIL_BUZON_1 (cola)", "para": "PP_MAIL_BUZON_2", "asunto": asunto_r1,
                                                       "t_utc": iso(datetime.fromtimestamp(t0, timezone.utc)), "email_id": rid})
        guardar_estado(e2)
r1 = a["r1"]
rid = r1["id"]
R.v("S-31", "el remitente por defecto (User 8) se reemplaza por la queue al crear", r1["from_al_crear"] in ([[COLA, "queue"]], [(COLA, "queue")]), [COLA, "queue"], r1["from_al_crear"])
R.v("H-5", "SendEmail como User 8 (Write sobre el Email, sin privilegio Send)", r1["sendemail_http"] in (200, 204), "200/204", (r1["sendemail_http"], r1["sendemail_error"]))

def enviado():
    x = email(dv, rid)
    return x if x["statuscode"] in (3, 8) else None
x, s = esperar(enviado, 900, 15)
x = x or email(dv, rid)
R.v("H-5", "la respuesta pasa a Sent", x["statuscode"] == 3, "Sent", {"estado": EST_EMAIL.get(x["statuscode"]), "segundos": s, "senton": x["senton"]})
R.v("H-5", "parentactivityid = el correo del cliente", x["_parentactivityid_value"] == eid, eid, x["_parentactivityid_value"])
R.v("H-28", "(c) tras el envío el remitente sigue siendo solo la queue", partes_from_tipadas(rid) in ([[COLA, "queue"]], [(COLA, "queue")]), [COLA, "queue"], partes_from_tipadas(rid))

# S-35 (asíncrono)
def procesado():
    y = email(dv, rid)
    return y if y.get("_sanic_respondidoporid_value") else None
y, s = esperar(procesado, 600, 10)
y = y or email(dv, rid)
R.v("H-6", "S-35: respondidoporid = User 8 (createdby)", y.get("_sanic_respondidoporid_value") == OFICIAL, OFICIAL, {"valor": y.get("_sanic_respondidoporid_value"), "segundos": s, "createdby": y["_createdby_value"]})
llegada = email(dv, eid)["actualend"]
minutos = int((datetime.fromisoformat(y["senton"].replace("Z", "+00:00")) - datetime.fromisoformat(llegada.replace("Z", "+00:00"))).total_seconds() // 60) if y.get("senton") else None
R.v("H-6", "S-35: tiemporespuesta = minutos entre la llegada y senton (truncados)", y.get("sanic_tiemporespuesta") == minutos, minutos, y.get("sanic_tiemporespuesta"))
cv = conv(dv, cid)
R.v("H-6", "conversación: 1 respuesta, sin pendiente, primera respuesta = tiempo del correo",
    cv["sanic_cantidadrespuestas"] == 1 and cv["sanic_sinrespuestadesde"] is None and cv["sanic_tiempoprimerarespuesta"] == y.get("sanic_tiemporespuesta"),
    {"cantidad": 1, "pendiente": None, "primera": y.get("sanic_tiemporespuesta")},
    {"cantidad": cv["sanic_cantidadrespuestas"], "pendiente": cv["sanic_sinrespuestadesde"], "primera": cv["sanic_tiempoprimerarespuesta"]})

# Llegada al buzón 2 (Graph): desde el buzón 1
def llego():
    m = [m for m in c.bandeja(2, "HUMO-T3-A") if (m.get("subject") or "").startswith("RE:")]
    return m[0] if m else None
m, s = esperar(llego, 900, 20)
if m:
    a["r1"]["graph_msg_id_b2"] = m["id"]
    a["r1"]["graph_recibido"] = m["receivedDateTime"]
    poner("caso_a", a)
R.v("H-5", "la respuesta llega al buzón 2 con remitente = buzón 1 (la cola)", bool(m) and c.nombre(m["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_1",
    "PP_MAIL_BUZON_1", {"remitente": c.nombre(m["from"]["emailAddress"]["address"]) if m else None, "segundos": s})
volcar("20_r1.json", limpio(y))
volcar("20_conversacion.json", limpio(cv))
tr = resumen_trazas(trazas(dv, datetime.fromtimestamp(r1["t_send_epoch"] - 120, timezone.utc), esperar=30))
volcar("20_trazas.json", [t for t in tr if any(rid in l or cid in l for l in t["lineas"])])
