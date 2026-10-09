"""Casos 3 y 4: el buzon 2 (cliente) responde por Graph (createReply) a la respuesta enviada desde Dataverse (V1), que tiene
la misma Regarding. Caso 3: asunto intacto. Caso 4: asunto cambiado antes de enviar."""
from lib import *
g = Graph(); est = cargar(); tag = est["tag"]
b2 = g.buzon[2]
target = est["resp_v1"]["obs"]["graph_msg_id"]
est["t_clientes"] = utc_iso()
est["caso3"] = g.responder(b2, target, f"<p>Caso 3: respuesta del cliente en el mismo hilo ({tag}).</p>")
est["caso4"] = g.responder(b2, target, f"<p>Caso 4: respuesta con asunto cambiado ({tag}).</p>", asunto=f"[SPIKE3] Caso 4 asunto distinto {tag}")
guardar(est)
print(json.dumps({k: {x: y for x, y in est[k].items() if x != 'gid'} for k in ("caso3", "caso4")}, indent=1))
