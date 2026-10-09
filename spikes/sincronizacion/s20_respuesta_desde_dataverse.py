"""Caso 2 (y base de 3/4/7): asigna el Email del caso 1 a una spk_conversacion nueva (como haria el plugin), crea respuestas
en Dataverse con la Queue 'Spike' (buzon 1) como remitente y las envia con la accion SendEmail.
V1: parentactivityid = correo original (lo que hace Responder). V2: solo Regarding, sin parent. Mide demoras y verifica por Graph."""
import sys
from lib import *
dv = Dataverse(); g = Graph(); est = cargar()
Q_SPIKE = "4411c78a-4ec3-f111-a05a-000d3a35abca"
orig = est["caso1"]["dv_email_id"]; tag = est["tag"]
b1, b2 = g.buzon[1], g.buzon[2]

# 1) conversacion nueva y Regarding del Email recibido (releido despues)
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": f"[SPIKE3] conversacion {tag}"}, solucion="SpikesArreglosPago")
conv = Dataverse.id_creado(h); print("POST spk_conversacion", s, conv)
s, r, _ = dv.call("PATCH", f"emails({orig})", {"regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})"})
print("PATCH Regarding sobre Email recibido (Completed):", s, "" if s in (200, 204) else r)
e = email_completo(dv, orig)
print("releido Regarding:", e.get("_regardingobjectid_value"), e.get("_regardingobjectid_value@Microsoft.Dynamics.CRM.lookuplogicalname"))
est["conversacion_id"] = conv; est["conv_email_orig_regarding"] = e.get("_regardingobjectid_value")
orig_row = email_por_messageid(dv, est["caso1"]["internetMessageId"], utc_iso(est["caso1"]["t_envio_epoch"] - 120))[0]
subj = orig_row["subject"]

def crear_respuesta(etq, asunto, con_parent):
    cuerpo = {"subject": asunto, "description": f"<p>Respuesta {etq} desde Dataverse (prueba spike 3).</p>", "directioncode": True,
              "regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})",
              "email_activity_parties": [
                  {"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({Q_SPIKE})"},
                  {"participationtypemask": 2, "addressused": b2}]}
    if con_parent:
        cuerpo["parentactivityid@odata.bind"] = f"/emails({orig})"
    t_crea = time.time()
    s, r, h = dv.call("POST", "emails", cuerpo, solucion="SpikesArreglosPago")
    eid = Dataverse.id_creado(h)
    print(etq, "POST email", s, eid, "" if s in (200, 201, 204) else r)
    return {"email_id": eid, "post_http": s, "asunto": asunto, "t_crea_utc": utc_iso(t_crea)}

def enviar(c):
    t = time.time()
    s, r, _ = dv.call("POST", f"emails({c['email_id']})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""})
    c["sendemail_http"] = s; c["t_sendemail_epoch"] = t; c["t_sendemail_utc"] = utc_iso(t)
    c["sendemail_resp"] = r if s >= 300 else {k: str(v)[:200] for k, v in r.items() if not k.startswith("@")}
    print("SendEmail", s, c["sendemail_resp"])

v1 = crear_respuesta("V1", "RE: " + subj, True)
v2 = crear_respuesta("V2", "RE: " + subj + " [V2 solo Regarding]", False)
for c in (v1, v2):
    # releer antes de enviar
    e = email_completo(dv, c["email_id"])
    c["pre_envio"] = {k: e.get(k) for k in ("statuscode@OData.Community.Display.V1.FormattedValue", "sender", "torecipients", "_regardingobjectid_value", "_parentactivityid_value", "_emailsender_value@OData.Community.Display.V1.FormattedValue", "_ownerid_value@OData.Community.Display.V1.FormattedValue")}
    print(c["asunto"][-30:], c["pre_envio"])
    enviar(c)
est["resp_v1"] = v1; est["resp_v2"] = v2
guardar(est)
