"""Caso 7: respuesta desde Dataverse (Queue 'Spike' como remitente) con un activitymimeattachment PDF, y una imagen PNG.
Se verifica por Graph que llegan al buzon 2 con los mismos bytes."""
from lib import *
dv = Dataverse(); g = Graph(); est = cargar(); tag = est["tag"]
Q_SPIKE = "4411c78a-4ec3-f111-a05a-000d3a35abca"
conv = est["conversacion_id"]; orig = est["caso1"]["dv_email_id"]
cuerpo = {"subject": f"[SPIKE3] Caso 7 respuesta con adjunto {tag}", "description": "<p>Respuesta con PDF y PNG adjuntos desde Dataverse.</p>",
          "directioncode": True, "regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})",
          "parentactivityid@odata.bind": f"/emails({orig})",
          "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({Q_SPIKE})"},
                                     {"participationtypemask": 2, "addressused": g.buzon[2]}]}
s, r, h = dv.call("POST", "emails", cuerpo, solucion="SpikesArreglosPago"); eid = Dataverse.id_creado(h); print("POST email", s, eid, "" if s < 300 else r)
pdf = pdf_chico("Spike 3 adjunto de salida"); png = png_chico(16, 16, (20, 160, 20))
for n, (nombre, datos, mime) in enumerate((("salida.pdf", pdf, "application/pdf"), ("salida.png", png, "image/png")), 1):
    s, r, h = dv.call("POST", "activitymimeattachments", {"objectid_email@odata.bind": f"/emails({eid})", "objecttypecode": "email", "filename": nombre,
        "mimetype": mime, "body": base64.b64encode(datos).decode(), "subject": nombre, "attachmentnumber": n})
    print("POST adjunto", nombre, s, "" if s < 300 else r)
s, e, _ = dv.call("GET", f"emails({eid})?$select=attachmentcount,statuscode")
s2, at, _ = dv.call("GET", f"activitymimeattachments?$filter=_objectid_value eq {eid}&$select=filename,mimetype,filesize")
print("pre-envio: attachmentcount", e.get("attachmentcount"), [(a["filename"], a["filesize"]) for a in at["value"]])
t = time.time()
s, r, _ = dv.call("POST", f"emails({eid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""}); print("SendEmail", s, r if s >= 300 else "")
est["caso7"] = {"email_id": eid, "t_sendemail_epoch": t, "t_sendemail_utc": utc_iso(t), "sendemail_http": s,
                "bytes_enviados": {"salida.pdf": len(pdf), "salida.png": len(png)}}
guardar(est)
