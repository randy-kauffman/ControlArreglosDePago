"""Polling (30 s, max 25 min) de una respuesta enviada desde Dataverse: estado/senton/messageid en Dataverse y llegada a un
buzon destino por Graph (headers). Uso: s21_vigilar_salida.py <clave_estado> <buzon_destino 2|3|4> [clave_estado ...]"""
import sys
from lib import *
args = sys.argv[1:]
claves = args[0::2]; dest = {k: int(d) for k, d in zip(args[0::2], args[1::2])}
dv = Dataverse(); g = Graph()
pend = set(claves); t_ini = time.time()
EST = {1: "Draft", 2: "Completed", 3: "Sent", 4: "Received", 5: "Canceled", 6: "Pending Send", 7: "Sending", 8: "Failed"}
while pend and time.time() - t_ini < 25 * 60 + 60:
    est = cargar()
    for k in sorted(pend):
        c = est[k]; obs = c.setdefault("obs", {})
        s, e, _ = dv.call("GET", f"emails({c['email_id']})?$select=statuscode,senton,messageid,actualend,modifiedon,subject,sender,_emailsender_value,inreplyto,conversationindex,trackingtoken,attachmentcount")
        if s == 200:
            sc = EST.get(e["statuscode"], e["statuscode"])
            if sc != obs.get("ultimo_estado"):
                obs.setdefault("transiciones", []).append([utc_iso(), sc, e.get("modifiedon"), e.get("senton")])
                obs["ultimo_estado"] = sc
            for col in ("senton", "messageid", "actualend", "inreplyto", "conversationindex", "trackingtoken"):
                if e.get(col): obs[col] = e[col]
            obs["asunto_final_dv"] = e.get("subject")
        if "graph_recibido" not in obs:
            est_g, v = g.buscar(g.buzon[dest[k]], top=30)
            for m in v if est_g == 200 else []:
                if m["receivedDateTime"] >= utc_iso(c["t_sendemail_epoch"] - 5) and "[SPIKE3]" in (m.get("subject") or "") and (m.get("internetMessageId") or "").lower() == (obs.get("messageid") or "x").lower():
                    obs["graph_recibido"] = m["receivedDateTime"]; obs["graph_msg_id"] = m["id"]; obs["graph_asunto"] = m["subject"]
                    obs["graph_from"] = m.get("from"); obs["graph_internetMessageId"] = m["internetMessageId"]
                    est_h, mm = g.call("GET", f"/users/{g.buzon[dest[k]]}/messages/{m['id']}?$select=internetMessageHeaders,from,sender,replyTo,conversationId,internetMessageId,subject,hasAttachments,receivedDateTime,sentDateTime")
                    if est_h == 200:
                        obs["graph_headers"] = {h["name"]: h["value"] for h in mm.get("internetMessageHeaders", []) if h["name"].lower() in
                            ("from", "sender", "reply-to", "in-reply-to", "references", "message-id", "thread-index", "thread-topic", "x-ms-exchange-organization-authas", "return-path", "x-ms-exchange-crosstenant-id")}
                        obs["graph_from_prop"] = mm.get("from"); obs["graph_sender_prop"] = mm.get("sender"); obs["graph_conversationId"] = mm.get("conversationId")
                        obs["graph_hasAttachments"] = mm.get("hasAttachments"); obs["graph_sentDateTime"] = mm.get("sentDateTime")
        if obs.get("senton"): obs["demora_sendemail_a_senton_s"] = delta(c["t_sendemail_utc"], obs["senton"])
        if obs.get("graph_recibido"):
            obs["demora_sendemail_a_llegada_s"] = delta(c["t_sendemail_utc"], obs["graph_recibido"])
            if obs.get("graph_sentDateTime"): obs["demora_sendemail_a_sentDateTime_s"] = delta(c["t_sendemail_utc"], obs["graph_sentDateTime"])
        if obs.get("graph_recibido") and obs.get("ultimo_estado") == "Sent":
            pend.discard(k); print(k, "LLEGO a buzon", dest[k], obs["graph_recibido"], "demora", obs["demora_sendemail_a_llegada_s"], "s", flush=True)
        est[k] = c
    cur = cargar(); cur.update({k: est[k] for k in claves}); guardar(cur)
    if pend: time.sleep(30)
print("pendientes:", pend)
