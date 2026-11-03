"""F4 (plan B, modo flujos): punta a punta con los flujos de PRODUCCIÓN ENT y SND encendidos y el buzón 1 SIN SSS (autorizado por el usuario,
2026-11-03). Correo solo entre los buzones de prueba 1 y 2, datos ficticios. Tope de envíos de flib (TOPE_CORREOS) incluido.

 0) precondiciones: mailbox 1 en 0/0, sanic_carp_ev_modocorreo = flujos, ENT y SND encendidos;
 1) el cliente (buzón 2) escribe al buzón 1 con un adjunto normal y uno con comillas en el nombre -> ENT crea el email (dueño team, Received,
    cola del área, adjuntos) y S-34 crea la conversación;
 2) el Oficial toma la conversación y responde con un adjunto con comillas en el nombre y una imagen pegada (cid) -> SND la envía (Sent,
    messageid) y S-35 registra al Oficial;
 3) el buzón 2 recibe la respuesta del buzón 1 con los dos adjuntos (la imagen en línea) y contesta -> ENT la liga a la MISMA conversación;
 4) correo SIN original: el Oficial redacta un correo nuevo (sin parentactivityid) en la conversación -> SND manda un mensaje nuevo (rama sin padre)."""
import base64
from flib import *

dv = adm()
R = Res("f4_modo_flujos.json")
c = Correo()
e = est()
X = e.get("f4", {})
CALL8 = {"MSCRMCallerID": OFICIAL}
ENT = "Cloud Flow - CARP - ENT - Capturar correo entrante"
SND = "Cloud Flow - CARP - SND - Enviar respuestas"
PNG = base64.b64encode(bytes.fromhex("89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360f8cfc0f01f0005000101a5f645400000000049454e44ae426082")).decode()
COMILLAS = 'cotizacion "final".txt'


def b64(t):
    return base64.b64encode(t.encode()).decode()


def adjuntos_de(eid):
    r = get(dv, f"activitymimeattachments?$select=filename,mimetype,attachmentcontentid,filesize&$filter=_objectid_value eq {eid}&$orderby=filename asc")["value"]
    return [{k: a.get(k) for k in ("filename", "mimetype", "attachmentcontentid", "filesize")} for a in r]


def adjuntos_graph(n, msg_id):
    st, b = c.g.call("GET", f"users/{c.b[n]}/messages/{msg_id}/attachments?$select=name,contentType,isInline,size")
    assert st == 200, st
    return sorted([{"name": a["name"], "isInline": a.get("isInline")} for a in b.get("value", [])], key=lambda a: a["name"])


def por_imid(imid, t0):
    r = get(dv, f"emails?$select=activityid,messageid&$filter=createdon ge {iso_de(t0 - 120)} and directioncode eq false")["value"]
    m = [x for x in r if (x.get("messageid") or "").strip("<>").lower() == (imid or "").strip("<>").lower()]
    return m[0] if m else None


def iso_de(epoch):
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(epoch))


def enviar_como_oficial(cuerpo, adj):
    st, b, h = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    assert st == 204, err(b)
    rid = Dataverse.id_creado(h)
    for a in adj:
        st, b, _ = dv.call("POST", "activitymimeattachments", dict(a, **{"objectid_email@odata.bind": f"/emails({rid})", "objecttypecode": "email"}), cabeceras=CALL8)
        assert st == 204, err(b)
    st, b, _ = dv.call("POST", f"emails({rid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""}, cabeceras=CALL8)
    R.v("F4", "SendEmail del Oficial aceptado (buzón sin SSS)", st in (200, 204), "200/204", st if st < 300 else err(b))
    return rid


def esperar_envio(rid, cuanto=420):
    lect = []
    def listo():
        y = limpio(email(dv, rid))
        lect.append(y["statuscode"])
        return y if y["statuscode"] in (3, 8) else None
    y, seg = esperar(listo, cuanto, 10)
    return y, seg, lect


# 0) precondiciones
m = get(dv, f"mailboxes({AREA_MAILBOX})?$select=incomingemaildeliverymethod,outgoingemaildeliverymethod")
R.v("F4", "mailbox 1 sin SSS (entrada 0, salida 0)", (m["incomingemaildeliverymethod"], m["outgoingemaildeliverymethod"]) == (0, 0), "(0, 0)", (m["incomingemaildeliverymethod"], m["outgoingemaildeliverymethod"]))
modo = get(dv, "environmentvariablevalues?$select=value&$filter=EnvironmentVariableDefinitionId/schemaname eq 'sanic_carp_ev_modocorreo'")["value"][0]["value"]
R.v("F4", "sanic_carp_ev_modocorreo = flujos", modo == "flujos", "flujos", modo)
fl = {w["name"]: w["statecode"] for w in get(dv, "workflows?$select=name,statecode&$filter=category eq 5 and (name eq '" + ENT + "' or name eq '" + SND + "')")["value"]}
R.v("F4", "ENT y SND encendidos", fl.get(ENT) == 1 and fl.get(SND) == 1, "1 y 1", fl)
assert modo == "flujos" and fl.get(ENT) == 1 and fl.get(SND) == 1

# 1) entrada del cliente con adjuntos
if "cliente" not in X:
    adj = [{"@odata.type": "#microsoft.graph.fileAttachment", "name": "estado-de-cuenta.txt", "contentType": "text/plain", "contentBytes": b64("Estado de cuenta ficticio.")},
           {"@odata.type": "#microsoft.graph.fileAttachment", "name": COMILLAS, "contentType": "text/plain", "contentBytes": b64("Cotizacion ficticia con comillas en el nombre.")}]
    X["cliente"] = c.enviar(2, [1], PREFIJO + f"F4 consulta {int(time.time())}", "Consulta ficticia del cliente (F4, modo flujos).", adjuntos=adj)
    poner("f4", X)
cl = X["cliente"]
fila, seg = esperar(lambda: por_imid(cl["internetMessageId"], cl["t_envio_epoch"]), 600, 15)
R.v("F4", "ENT crea el email del cliente (sin SSS)", fila is not None, "email", {"segundos": seg})
assert fila
X["email_id"] = fila["activityid"]
poner("f4", X)
x = email(dv, X["email_id"], "statuscode,statecode,_ownerid_value,_owningbusinessunit_value,_regardingobjectid_value")
R.v("F4", "ENT: dueño = team del área y BU del área", x["_owningbusinessunit_value"] == BU_AREA, BU_AREA, x["_owningbusinessunit_value"])
R.v("F4", "ENT: cerrado como Received (1/4)", (x["statecode"], x["statuscode"]) == (1, 4), "(1, 4)", (x["statecode"], x["statuscode"]))
qi = get(dv, f"queueitems?$select=queueitemid&$filter=_objectid_value eq {X['email_id']} and _queueid_value eq {AREA_COLA}")["value"]
R.v("F4", "ENT: el email está en la cola del área", len(qi) == 1, 1, len(qi))
adj_in = adjuntos_de(X["email_id"])
R.v("F4", "ENT: los dos adjuntos, incluido el de comillas", sorted(a["filename"] for a in adj_in) == sorted(["estado-de-cuenta.txt", COMILLAS]), [COMILLAS, "estado-de-cuenta.txt"], [a["filename"] for a in adj_in])
xs, s = esperar(lambda: (lambda y: y if y.get("_regardingobjectid_value") else None)(email(dv, X["email_id"])), 600, 10)
R.v("F4", "S-34 crea la conversación", xs is not None, "Regarding", s)
assert xs
X["conv"] = xs["_regardingobjectid_value"]
poner("f4", X)

# 2) el Oficial responde con adjunto con comillas e imagen pegada
cv = get(dv, f"sanic_carp_tbl_conversacions({X['conv']})?$select=_sanic_oficialasignadoid_value")
if cv["_sanic_oficialasignadoid_value"] != OFICIAL:
    st, b, t = llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": X["conv"]}, caller=OFICIAL)
    R.v("F4", "el Oficial toma la conversación", st == 200, 200, st)
if "resp_id" not in X:
    cuerpo = {"subject": "RE: " + cl["asunto"], "directioncode": True,
              "description": '<p>Respuesta ficticia del Oficial (F4) con imagen pegada:</p><p><img src="cid:firma-f4" width="20" height="20"></p>',
              "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({X['conv']})",
              "parentactivityid@odata.bind": f"/emails({X['email_id']})",
              "email_activity_parties": [{"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
                                         {"participationtypemask": 2, "addressused": c.b[2]}]}
    X["resp_id"] = enviar_como_oficial(cuerpo, [
        {"filename": 'propuesta "arreglo".txt', "mimetype": "text/plain", "body": b64("Propuesta ficticia de arreglo.")},
        {"filename": "firma.png", "mimetype": "image/png", "body": PNG, "attachmentcontentid": "firma-f4"}])
    X["t_resp"] = time.time()
    poner("f4", X)
y, seg, lect = esperar_envio(X["resp_id"])
R.v("F4", "SND pasa la respuesta a Sent (3)", bool(y) and y["statuscode"] == 3, 3, {"final": y and y["statuscode"], "segundos": seg, "estados": lect[:40]})
assert y and y["statuscode"] == 3
y2 = email(dv, X["resp_id"], "messageid,_sanic_respondidoporid_value")
R.v("F4", "S-35 registra al Oficial como quien respondió", y2["_sanic_respondidoporid_value"] == OFICIAL, OFICIAL, y2["_sanic_respondidoporid_value"])

# 3) el cliente recibe y contesta
def en_2():
    r = [z for z in c.bandeja(2, cl["asunto"]) if z["subject"].lower().startswith("re:")]
    return r[0] if r else None
rm, s3 = esperar(en_2, 300, 15)
R.v("F4", "la respuesta llega al buzón 2", rm is not None, "mensaje", s3)
assert rm
R.v("F4", "remitente = buzón 1 (Send As)", c.nombre(rm["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_1", "PP_MAIL_BUZON_1", c.nombre(rm["from"]["emailAddress"]["address"]))
R.v("F4", "internetMessageId recibido = messageid grabado por SND", (rm["internetMessageId"] or "").lower() == (y2["messageid"] or "").lower(), "iguales", None)
ag = adjuntos_graph(2, rm["id"])
R.v("F4", "el cliente recibe el adjunto con comillas y la imagen como en línea",
    ag == sorted([{"name": "firma.png", "isInline": True}, {"name": 'propuesta "arreglo".txt', "isInline": False}], key=lambda a: a["name"]),
    "firma.png en línea + propuesta con comillas", ag)
if "contestacion" not in X:
    X["contestacion"] = c.responder(2, rm["id"], "Contestacion ficticia del cliente (F4).", esperado_para=1)
    poner("f4", X)
ct = X["contestacion"]
fc, s5 = esperar(lambda: por_imid(ct["internetMessageId"], ct["t_envio_epoch"]), 600, 15)
R.v("F4", "ENT crea la contestación del cliente", fc is not None, "email", s5)
assert fc
xc, s6 = esperar(lambda: (lambda z: z if z.get("_regardingobjectid_value") else None)(email(dv, fc["activityid"])), 300, 10)
R.v("F4", "la contestación queda en la MISMA conversación (In-Reply-To = messageid de SND)", bool(xc) and xc["_regardingobjectid_value"] == X["conv"], X["conv"], xc and xc["_regardingobjectid_value"])

# 4) correo sin original (rama sin padre de SND)
if "nuevo_id" not in X:
    X["asunto_nuevo"] = PREFIJO + f"F4 aviso del Oficial {int(time.time())}"
    X["nuevo_id"] = enviar_como_oficial({"subject": X["asunto_nuevo"], "directioncode": True, "description": "<p>Correo nuevo ficticio del Oficial, sin original (F4).</p>",
        "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({X['conv']})",
        "email_activity_parties": [{"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
                                   {"participationtypemask": 2, "addressused": c.b[2]}]}, [])
    poner("f4", X)
y, seg, lect = esperar_envio(X["nuevo_id"])
R.v("F4", "SND sin original: el correo nuevo queda Sent (3)", bool(y) and y["statuscode"] == 3, 3, {"final": y and y["statuscode"], "segundos": seg, "estados": lect[:40]})
rn, s7 = esperar(lambda: (c.bandeja(2, X["asunto_nuevo"]) or [None])[0], 300, 15)
R.v("F4", "SND sin original: el correo nuevo llega al buzón 2 desde el buzón 1", bool(rn) and c.nombre(rn["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_1", "PP_MAIL_BUZON_1", rn and c.nombre(rn["from"]["emailAddress"]["address"]))
