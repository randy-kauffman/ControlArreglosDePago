"""Previo a V-4: ensaya por Web API (como service principal, dueño = el propio SP, o sea FUERA de la BU del área, así S-34 no registra nada)
la misma forma de `Create` que hará el flujo de entrada: partes from/to, actualend, messageid, sender/torecipients; el cierre a Received;
un activitymimeattachment con y sin attachmentcontentid; AddToQueue. Sirve para no descubrir en el flujo un campo inválido."""
import base64
from flib import *

dv = adm()
R = Res("v40_simular_entrada.json")
c = Correo()
e = est()
qid = e["cola_spk"]
S = e.get("v40", {})
PNG = base64.b64encode(bytes.fromhex("89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360f8cfc0f01f0005000101a5f645400000000049454e44ae426082")).decode()

if "email" not in S:
    cuerpo = {"subject": PREFIJO + "V4 simulacion de entrada (no es del area)", "description": '<p>Hola</p><p><img src="cid:logo1"></p>',
              "directioncode": False, "messageid": "<spk-fc-v40-sim@spike.invalid>", "sender": c.b[2], "torecipients": c.b[4],
              "actualend": iso(reloj(dv)),
              "email_activity_parties": [{"participationtypemask": 1, "addressused": c.b[2]},
                                         {"participationtypemask": 2, "partyid_queue@odata.bind": f"/queues({qid})"}]}
    st, b, h = dv.call("POST", "emails", cuerpo)
    S["create"] = {"http": st, "error": err(b) if st >= 300 else None}
    if st in (201, 204):
        S["email"] = Dataverse.id_creado(h)
    poner("v40", {**e.get("v40", {}), **S})
    R.v("V-4", "Create de un entrante con partes from/to, actualend, messageid, sender, torecipients", st in (201, 204), "204", S["create"])
    assert "email" in S
eid = S["email"]
e = est()
e["v40"] = S
guardar_estado(e)
if "cierre" not in S:
    st, b, _ = dv.call("PATCH", f"emails({eid})", {"statecode": 1, "statuscode": 4})
    S["cierre"] = {"http": st, "error": err(b) if st >= 300 else None}
    R.v("V-4", "Update a Received (statecode 1, statuscode 4)", st == 204, 204, S["cierre"])
if "adjuntos" not in S:
    adj = []
    for nombre, cid in (("logo.png", "logo1"), ("normal.png", None)):
        cuerpo = {"filename": nombre, "mimetype": "image/png", "body": PNG, "subject": nombre,
                  "objectid_email@odata.bind": f"/emails({eid})", "objecttypecode": "email"}
        if cid:
            cuerpo["attachmentcontentid"] = cid
        st, b, h = dv.call("POST", "activitymimeattachments", cuerpo)
        adj.append({"nombre": nombre, "cid": cid, "http": st, "error": err(b) if st >= 300 else None})
    S["adjuntos"] = adj
    R.v("V-4", "activitymimeattachment normal y en línea (attachmentcontentid)", all(a["http"] in (201, 204) for a in adj), "204", adj)
if "queue" not in S:
    st, b, _ = dv.call("POST", f"queues({qid})/Microsoft.Dynamics.CRM.AddToQueue", {"Target": {"@odata.type": "Microsoft.Dynamics.CRM.email", "activityid": eid}})
    S["queue"] = {"http": st, "error": err(b) if st >= 300 else None}
    R.v("V-4", "AddToQueue del entrante a la cola de prueba", st == 200, 200, S["queue"])
poner("v40", S)
x = limpio(email(dv, eid))
at = get(dv, f"activitymimeattachments?$select=filename,mimetype,attachmentcontentid,filesize&$filter=_objectid_value eq {eid}")["value"]
R.v("V-4", "estado final del entrante simulado", True, "informativo", {"email": {k: x.get(k) for k in ("statuscode", "statecode", "actualend", "messageid", "directioncode", "correlationmethod")},
                                                                  "adjuntos": [{k: a.get(k) for k in ("filename", "mimetype", "attachmentcontentid", "filesize")} for a in at]})
volcar("v40_detalle.json", S)
