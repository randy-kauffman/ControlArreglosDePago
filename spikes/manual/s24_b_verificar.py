"""Tarea B: verifica el ultimo envio (est['B']['email_enviado']) releyendo Dataverse y el buzon 2 por Graph:
estado Sent, propietario, parentactivityid, Regarding, y en Graph: From, Sender, In-Reply-To, Thread-Index frente al original."""
from lib import *
dv = DvAdmin(); g = Graph4(); est = cargar(); B = est["B"]; b1, b2 = g.buzon[1], g.buzon[2]
eid = B["email_enviado"]
s, e, _ = dv.call("GET", f"emails({eid})?$select=subject,statuscode,senton,actualend,sender,torecipients,messageid,inreplyto,_ownerid_value,_createdby_value,_parentactivityid_value,_regardingobjectid_value,_emailsender_value,directioncode,trackingtoken", cabeceras=PREFER)
fila = {k: v for k, v in e.items() if not k.startswith("@odata") and v is not None}
print("Dataverse (releido):", json.dumps(fila, ensure_ascii=False, indent=1))
esperado_parent = B["entrante"]["dv_email_id"]
print("parentactivityid = entrante:", fila.get("_parentactivityid_value") == esperado_parent, "| Regarding = conversacion:", fila.get("_regardingobjectid_value") == B["conversacion"])
asunto = fila["subject"]; encontrado = None; t0 = time.time()
while time.time() - t0 < 300 and not encontrado:
    st, v = g.mensajes(b2, top=20)
    for m in v if st == 200 else []:
        if (m["internetMessageId"] or "").lower() == (fila.get("messageid") or "").lower():
            encontrado = m; break
    if not encontrado: time.sleep(10)
assert encontrado, "no llego al buzon 2 en 5 minutos"
st, orig = g.mensajes(b1, top=20, carpeta="sentitems") if False else (None, None)
# el original enviado por buzon 2: se lee de su carpeta de enviados
st, ench = g.call("GET", urllib.parse.quote(f"/users/{b2}/mailFolders/sentitems/messages?$top=15&$select=subject,internetMessageId,conversationIndex,internetMessageHeaders&$orderby=sentDateTime desc", safe="/?&=$,@.:"))
orig = next((m for m in ench.get("value", []) if m["internetMessageId"].lower() == B["entrante"]["internetMessageId"].lower()), None)
res = {"from": encontrado["from"]["emailAddress"], "sender": encontrado["sender"]["emailAddress"],
       "in_reply_to": encabezado(encontrado, "In-Reply-To"), "references": encabezado(encontrado, "References"),
       "thread_index": encabezado(encontrado, "Thread-Index"), "message_id": encontrado["internetMessageId"],
       "llegada_utc": encontrado["receivedDateTime"],
       "demora_sendemail_a_llegada_s": int((datetime.fromisoformat(encontrado["receivedDateTime"].replace("Z", "+00:00")).timestamp() - B["t_sendemail_epoch"]))}
if orig:
    res["original_message_id"] = orig["internetMessageId"]; res["original_thread_index"] = orig.get("conversationIndex")
    res["in_reply_to_coincide_con_original"] = (res["in_reply_to"] or "").lower() == orig["internetMessageId"].lower()
    import base64
    _d = lambda x: base64.b64decode((x or "") + "=" * (-len(x or "") % 4))
    res["misma_raiz_thread_index"] = _d(res["thread_index"])[:22] == _d(res["original_thread_index"])[:22] and len(_d(res["original_thread_index"])) >= 22
s2, e2, _ = dv.call("GET", f"emails({eid})?$select=statuscode,senton", cabeceras=PREFER); res["dataverse_estado_final"] = [e2.get("statuscode@OData.Community.Display.V1.FormattedValue"), e2.get("senton")]
print("Graph (buzon 2):", json.dumps(res, ensure_ascii=False, indent=1))
volcar("b_envio_verificado.json", {"dataverse": fila, "graph": res})
