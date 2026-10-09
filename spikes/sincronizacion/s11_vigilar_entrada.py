"""Polling cada 60 s (max 25 min por caso) hasta que cada correo enviado aparezca como Email en Dataverse.
Uso: python3 s11_vigilar_entrada.py caso1 caso6a caso6b   (claves de estado.json con 'internetMessageId')
Guarda en estado.json[clave]['llegada'] los tiempos; vuelca la fila completa del Email a evidencia/."""
import sys
from lib import *
claves = sys.argv[1:]
dv = Dataverse(); g = Graph()
pend = set(claves)
t_ini = time.time()
while pend and time.time() - t_ini < 25 * 60 + 90:
    est = cargar()
    for k in sorted(pend):
        c = est[k]
        desde = utc_iso(c["t_envio_epoch"] - 120)
        # Graph: cuando llego al buzon 1
        if "graph_recibido" not in c:
            est_g, v = g.buscar(g.buzon[1], top=30)
            for m in v if est_g == 200 else []:
                if (m.get("internetMessageId") or "").lower() == c["internetMessageId"].lower():
                    c["graph_recibido"] = m["receivedDateTime"]; c["graph_sent"] = m["sentDateTime"]
        hits = email_por_messageid(dv, c["internetMessageId"], desde)
        if hits:
            e = hits[0]
            c["dv_email_id"] = e["activityid"]; c["dv_createdon"] = e["createdon"]
            c["n_emails_dv"] = len(hits)
            c["demora_envio_a_dv_s"] = delta(c["t_envio_utc"], e["createdon"])
            c["demora_graph_recibido_a_dv_s"] = delta(c.get("graph_recibido"), e["createdon"])
            c["t_observado_utc"] = utc_iso()
            volcar(f"{k}_email_entrada.json", email_completo(dv, e["activityid"]))
            pend.discard(k)
            print(k, "VISTO en Dataverse", e["createdon"], "demora desde envio", c["demora_envio_a_dv_s"], "s", flush=True)
        est[k] = c
    guardar({**cargar(), **{k: est[k] for k in claves}})
    if pend:
        time.sleep(60)
print("pendientes:", pend)
