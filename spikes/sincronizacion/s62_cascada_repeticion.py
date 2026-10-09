"""Caso 8 (repeticion controlada con borradores propios, sin enviar nada): dos Emails borrador con adjunto.
 R1: se borra el Email directamente. R2: se borra la spk_conversacion de la que cuelga (Regarding).
Se relee, con filtro, la fila 'attachment' (blob) y el activitymimeattachment."""
from lib import *
dv = Dataverse(); est = cargar(); tag = est["tag"]
def borrador(conv):
    cuerpo = {"subject": f"[SPIKE3] Caso 8 borrador {tag}", "description": "<p>borrador</p>", "directioncode": True,
              "regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})"}
    s, r, h = dv.call("POST", "emails", cuerpo, solucion="SpikesArreglosPago"); eid = Dataverse.id_creado(h)
    s2, r, h2 = dv.call("POST", "activitymimeattachments", {"objectid_email@odata.bind": f"/emails({eid})", "objecttypecode": "email", "filename": "x.pdf",
                        "mimetype": "application/pdf", "body": base64.b64encode(pdf_chico("cascada")).decode(), "subject": "x.pdf", "attachmentnumber": 1})
    s3, am, _ = dv.call("GET", f"activitymimeattachments?$filter=_objectid_value eq {eid}&$select=activitymimeattachmentid,_attachmentid_value")
    return eid, am["value"][0]["activitymimeattachmentid"], am["value"][0]["_attachmentid_value"]
def hay(entidad, clave, i):
    s, b, _ = dv.call("GET", f"{entidad}?$select={clave}&$filter={clave} eq {i}"); assert s == 200, (s, b); return len(b["value"]) > 0
res = {}
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": f"[SPIKE3] caso8 R1 {tag}"}, solucion="SpikesArreglosPago"); c1 = Dataverse.id_creado(h)
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": f"[SPIKE3] caso8 R2 {tag}"}, solucion="SpikesArreglosPago"); c2 = Dataverse.id_creado(h)
e1, a1, b1 = borrador(c1); e2, a2, b2 = borrador(c2)
print("antes  R1", hay("emails", "activityid", e1), hay("activitymimeattachments", "activitymimeattachmentid", a1), hay("attachments", "attachmentid", b1))
print("antes  R2", hay("emails", "activityid", e2), hay("activitymimeattachments", "activitymimeattachmentid", a2), hay("attachments", "attachmentid", b2))
print("DELETE email R1", dv.call("DELETE", f"emails({e1})")[0]); print("DELETE conversacion R2", dv.call("DELETE", f"spk_conversacions({c2})")[0])
dv.call("DELETE", f"spk_conversacions({c1})")
for seg in (0, 30, 90):
    time.sleep(seg)
    r1 = (hay("emails", "activityid", e1), hay("activitymimeattachments", "activitymimeattachmentid", a1), hay("attachments", "attachmentid", b1))
    r2 = (hay("emails", "activityid", e2), hay("activitymimeattachments", "activitymimeattachmentid", a2), hay("attachments", "attachmentid", b2))
    print(utc_iso(), "despues R1 (email, activitymimeattachment, attachment):", r1, "| R2:", r2)
res = {"R1_borrado_directo": {"email": e1, "ama": a1, "attachment": b1, "final": r1}, "R2_borrado_via_conversacion": {"email": e2, "ama": a2, "attachment": b2, "final": r2}}
volcar("caso8_repeticion.json", res)
