"""Caso 8. 8a: borra el Email del caso 6A (PDF + PNG, 2 Queue Items) y relee adjuntos, attachment y Queue Items.
8b (extra): asigna el Email del caso 6B a una spk_conversacion descartable y borra la conversacion; relee todo."""
from lib import *
dv = Dataverse(); est = cargar(); tag = est["tag"]

def foto(eid):
    s, qi, _ = dv.call("GET", f"queueitems?$filter=_objectid_value eq {eid}&$select=queueitemid")
    s, am, _ = dv.call("GET", f"activitymimeattachments?$filter=_objectid_value eq {eid}&$select=activitymimeattachmentid,filename,_attachmentid_value")
    return {"queueitems": [q["queueitemid"] for q in qi["value"]],
            "activitymimeattachments": [(a["activitymimeattachmentid"], a["filename"]) for a in am["value"]],
            "attachment_ids": [a["_attachmentid_value"] for a in am["value"] if a["_attachmentid_value"]]}

def existe(entidad, idv, clave):
    if entidad == "attachments":   # Retrieve por clave no esta soportado en 'attachment': se consulta con filtro
        s, b, _ = dv.call("GET", f"attachments?$select=attachmentid&$filter=attachmentid eq {idv}")
        if s != 200:
            raise SystemExit(f"consulta attachments HTTP {s}: {b}")
        return len(b["value"]) > 0
    s, b, _ = dv.call("GET", f"{entidad}({idv})?$select={clave}")
    if s not in (200, 404):
        raise SystemExit(f"lectura inesperada {entidad}({idv}) HTTP {s}: {b}")   # un 2xx/4xx distinto no cuenta como "existe"
    return s == 200

def releer(eid, f):
    return {"email_existe": existe("emails", eid, "activityid"),
            "queueitems_existentes": [q for q in f["queueitems"] if existe("queueitems", q, "queueitemid")],
            "activitymimeattachments_existentes": [a for a, _ in f["activitymimeattachments"] if existe("activitymimeattachments", a, "activitymimeattachmentid")],
            "attachments_existentes": [a for a in f["attachment_ids"] if existe("attachments", a, "attachmentid")]}

res = {}
# 8a
e6a = est["caso6a"]["dv_email_id"]; f = foto(e6a); print("8a ANTES", f, releer(e6a, f))
s, r, _ = dv.call("DELETE", f"emails({e6a})"); print("DELETE email 6A", s, "" if s < 300 else r)
for _ in range(6):
    d = releer(e6a, f)
    if not d["email_existe"]: break
    time.sleep(5)
print("8a DESPUES", d); res["8a"] = {"antes": f, "delete_http": s, "despues": d}
# 8b
e6b = est["caso6b"]["dv_email_id"]
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": f"[SPIKE3] conversacion descartable {tag}"}, solucion="SpikesArreglosPago"); c2 = Dataverse.id_creado(h)
s, r, _ = dv.call("PATCH", f"emails({e6b})", {"regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({c2})"}); print("PATCH regarding 6B", s)
s, em, _ = dv.call("GET", f"emails({e6b})?$select=_regardingobjectid_value"); print("regarding releido", em.get("_regardingobjectid_value") == c2)
f = foto(e6b); print("8b ANTES", f, releer(e6b, f))
s, r, _ = dv.call("DELETE", f"spk_conversacions({c2})"); print("DELETE conversacion", s, "" if s < 300 else r)
for _ in range(6):
    d = releer(e6b, f)
    if not d["email_existe"]: break
    time.sleep(5)
print("8b DESPUES", d, "conversacion existe:", existe("spk_conversacions", c2, "spk_conversacionid"))
res["8b"] = {"conv_descartable": c2, "antes": f, "delete_http": s, "despues": d}
volcar("caso8_cascada.json", res)
