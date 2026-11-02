"""V-3: `DeliverIncomingEmail` (Web API, colección emails): ¿correlaciona una respuesta como SSS (Regarding/correlationmethod)?
La acción NO tiene parámetro de cabeceras (ni In-Reply-To). Casos:
  C1 asunto "RE: <asunto del original>" + remitente/destinatario del hilo, sin token;
  C2 asunto con el tracking token del original (token del entorno);
  C3 intento con ExtraProperties (documentada 'solo uso interno') llevando inreplyto.
El 'original' es un saliente de la cola de prueba con Regarding a una cuenta ficticia (no a una conversación, para no activar S-31/S-37)."""
from flib import *

dv = adm()
R = Res("v30_deliver.json")
c = Correo()
e = est()
qid = e["cola_spk"]
V3 = e.get("v3", {})

if "cuenta" not in V3:
    st, b, h = dv.call("POST", "accounts", {"name": "SPK-FC Cuenta ficticia (V-3)"}, cabeceras=PREFER_REP)
    assert st == 201, (st, err(b))
    V3["cuenta"] = b["accountid"]
    registrar("cuenta_spk", "accounts", b["accountid"], "V-3")
    poner("v3", V3)
cuenta = V3["cuenta"]


def saliente(clave, asunto, token):
    if clave in V3:
        return V3[clave]
    cuerpo = {"subject": asunto, "description": "<p>Original ficticio de V-3.</p>", "directioncode": True,
              "regardingobjectid_account_email@odata.bind": f"/accounts({cuenta})",
              "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({qid})"},
                                         {"participationtypemask": 2, "addressused": c.b[2]}]}
    st, b, h = dv.call("POST", "emails", cuerpo)
    assert st in (201, 204), (st, err(b))
    eid = Dataverse.id_creado(h)
    st, b, _ = dv.call("POST", f"emails({eid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": token})
    V3[clave] = {"id": eid, "sendemail_http": st, "error": err(b) if st >= 300 else None}
    poner("v3", V3)
    return V3[clave]


o1 = saliente("orig1", PREFIJO + "V3 original uno", "")
o2 = saliente("orig2", PREFIJO + "V3 original dos", "CRM:0001001")
for k in ("orig1", "orig2"):
    x = limpio(email(dv, V3[k]["id"], COLS_EMAIL + ",trackingtoken"))
    V3[k]["leido"] = {kk: x.get(kk) for kk in ("subject", "statuscode", "messageid", "trackingtoken", "_regardingobjectid_value")}
    poner("v3", V3)
R.v("V-3", "originales: Pending Send con Regarding a la cuenta ficticia", all(V3[k]["leido"]["statuscode"] in (3, 6) and V3[k]["leido"]["_regardingobjectid_value"] == cuenta for k in ("orig1", "orig2")), "6 (o 3 si ya se pasó a Sent) y cuenta",
    {k: V3[k]["leido"] for k in ("orig1", "orig2")})
# para que el 'original' sea un correo ya enviado (como lo dejará el flujo), se pasa a Sent con un messageid
for k, mid in (("orig1", "<spk-fc-v3-orig1@spike.invalid>"), ("orig2", "<spk-fc-v3-orig2@spike.invalid>")):
    if V3[k]["leido"]["statuscode"] == 6:
        dv.call("PATCH", f"emails({V3[k]['id']})", {"statuscode": 7})
        st, b, _ = dv.call("PATCH", f"emails({V3[k]['id']})", {"statuscode": 3, "messageid": mid})
        V3[k]["a_sent"] = st
        poner("v3", V3)


def entregar(clave, asunto, msgid, extra=None):
    cuerpo = {"MessageId": msgid, "Subject": asunto, "From": c.b[2], "To": c.b[4], "Cc": "", "Bcc": "",
              "ReceivedOn": iso(reloj(dv)), "SubmittedBy": c.b[4], "Importance": "normal", "Attachments": [],
              "Body": "<p>Respuesta ficticia de V-3 (datos ficticios).</p>"}
    if extra:
        cuerpo["ExtraProperties"] = extra
    st, b, _ = dv.call("POST", "emails/Microsoft.Dynamics.CRM.DeliverIncomingEmail", cuerpo)
    r = {"http": st, "error": err(b) if st >= 300 else None, "codigo": codigo(b) if st >= 300 else None}
    if st < 300:
        r["id"] = b.get("activityid")
    return r


casos = {}
if not all(r.get("http") in (200, 204) for r in V3.get("casos", {}).values()) or "casos" not in V3:
    casos["C1"] = entregar("C1", "RE: " + V3["orig1"]["leido"]["subject"], "<spk-fc-v3-c1@spike.invalid>")
    casos["C2"] = entregar("C2", V3["orig2"]["leido"]["subject"].replace(PREFIJO, "RE: " + PREFIJO, 1), "<spk-fc-v3-c2@spike.invalid>")
    casos["C3"] = entregar("C3", "RE: " + V3["orig1"]["leido"]["subject"] + " (c3)", "<spk-fc-v3-c3@spike.invalid>",
                           {"@odata.type": "Microsoft.Dynamics.CRM.email", "inreplyto": "<spk-fc-v3-orig1@spike.invalid>"})
    V3["casos"] = casos
    poner("v3", V3)
casos = V3["casos"]
time.sleep(20)
for k, r in casos.items():
    if r.get("id"):
        time.sleep(0)
        x = limpio(email(dv, r["id"]))
        r["leido"] = {kk: x.get(kk) for kk in ("subject", "statuscode", "directioncode", "messageid", "inreplyto", "correlationmethod",
                                              "_regardingobjectid_value", "_parentactivityid_value", "_correlatedactivityid_value", "_ownerid_value",
                                              "_owningbusinessunit_value", "_receivingmailboxid_value", "trackingtoken", "conversationindex")}
        r["correlaciono"] = r["leido"]["_regardingobjectid_value"] == cuenta
    R.v("V-3", f"{k}: respuesta entregada con DeliverIncomingEmail", r["http"] in (200, 204), "200/204", {kk: r.get(kk) for kk in ("http", "error", "codigo")})
    if r.get("leido"):
        R.v("V-3", f"{k}: ¿correlacionó (Regarding = cuenta del original)?", True, "informativo",
            {"correlaciono": r["correlaciono"], "correlationmethod": r["leido"]["correlationmethod"], "regarding": r["leido"]["_regardingobjectid_value"], "inreplyto": bool(r["leido"]["inreplyto"])})
poner("v3", V3)
volcar("v30_detalle.json", V3)
