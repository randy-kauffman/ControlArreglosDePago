"""Tarea D.1: correo nuevo '[SPIKE4] Prueba manual' (buzon 2 -> buzon 1), espera a que aparezca como Email en Dataverse, crea la conversacion
'Prueba manual SPIKE4' y fija el Regarding del Email entrante. Guarda los ids en estado.json['D'] y arma los links."""
import sys
from lib import *
dv = DvAdmin(); g = Graph4(); est = cargar()
if est.get("D", {}).get("conversacion"):
    sys.exit("Ya existe una preparacion D en estado.json; no se repite (borrarla antes con s99 si hace falta)")
b1, b2 = g.buzon[1], g.buzon[2]
asunto = "[SPIKE4] Prueba manual"
r = g.enviar_borrador(b2, [b1], asunto, "<p>Hola, esta es la consulta de la prueba manual del Spike 4. Cedula ficticia 0-000-0000. Por favor respondan a este correo.</p>")
assert r.get("enviado_http") == 202, r
desde = utc_iso(r["t_envio_epoch"] - 120); enc = None; t0 = time.time()
while time.time() - t0 < 600 and not enc:
    s, b, _ = dv.call("GET", f"emails?$select=activityid,subject,messageid,createdon,_ownerid_value&$filter=createdon ge {desde}&$orderby=createdon asc")
    hits = [e for e in b.get("value", []) if (e.get("messageid") or "").lower() == r["internetMessageId"].lower()]
    if hits: enc = hits[0]
    else: time.sleep(10)
assert enc, "no aparecio en Dataverse en 10 minutos"
print("Email entrante:", enc["activityid"], "owner", enc["_ownerid_value"], "| demora (s):", int(time.time() - r["t_envio_epoch"]))
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": "Prueba manual SPIKE4"}, solucion=SOLUCION); conv = Dataverse.id_creado(h)
s, rr, _ = dv.call("PATCH", f"emails({enc['activityid']})", {"regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})"}); print("PATCH Regarding:", s)
s, e, _ = dv.call("GET", f"emails({enc['activityid']})?$select=_regardingobjectid_value,subject,statuscode"); assert e["_regardingobjectid_value"] == conv, e
s, c, _ = dv.call("GET", f"spk_conversacions({conv})?$select=spk_nombre"); assert c["spk_nombre"] == "Prueba manual SPIKE4"
app = est["C"]["appid"]; base = f"{dv.org}/main.aspx?appid={app}"
D = {"asunto": asunto, "email_id": enc["activityid"], "conversacion": conv, "internetMessageId": r["internetMessageId"], "gid_buzon2": r["gid"],
     "t_envio_utc": r["t_envio_utc"], "url_app": base,
     "url_conversacion": f"{base}&pagetype=entityrecord&etn=spk_conversacion&id={conv}",
     "url_correo": f"{base}&pagetype=entityrecord&etn=email&id={enc['activityid']}"}
est["D"] = D; guardar(est); print(json.dumps(D, indent=1))
