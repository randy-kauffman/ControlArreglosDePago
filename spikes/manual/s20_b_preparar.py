"""Tarea B (preparacion): correo entrante [SPIKE4] (buzon 2 -> buzon 1), espera a que aparezca en Dataverse, crea la
conversacion '[SPIKE4] Tarea B' (como admin: las crea el sistema) y fija el Regarding del Email entrante."""
from lib import *
dv = DvAdmin(); g = Graph4(); est = cargar()
tag = time.strftime("%H%M%S"); b1, b2 = g.buzon[1], g.buzon[2]
r = g.enviar_borrador(b2, [b1], f"[SPIKE4] Tarea B correo entrante {tag}", "<p>Hola, consulto por mi arreglo de pago (prueba tarea B, cedula ficticia 0-000-0000).</p>")
print("enviado:", {k: r[k] for k in ("enviado_http", "internetMessageId", "t_envio_utc")}); assert r.get("enviado_http") == 202
desde = utc_iso(r["t_envio_epoch"] - 120)
enc = None; t0 = time.time()
while time.time() - t0 < 600 and not enc:
    s, b, _ = dv.call("GET", f"emails?$select=activityid,subject,messageid,createdon,_regardingobjectid_value,_ownerid_value&$filter=createdon ge {desde}&$orderby=createdon asc")
    hits = [e for e in b.get("value", []) if (e.get("messageid") or "").lower() == r["internetMessageId"].lower()]
    if hits: enc = hits[0]; break
    time.sleep(10)
assert enc, "el correo no aparecio en Dataverse en 10 minutos"
print("Email entrante en Dataverse:", enc["activityid"], "demora desde envio (s):", int(time.time() - r["t_envio_epoch"]))
s, _, h = dv.call("POST", "spk_conversacions", {"spk_nombre": "[SPIKE4] Tarea B conversacion " + tag}, solucion=SOLUCION)
conv = Dataverse.id_creado(h)
s, rr, _ = dv.call("PATCH", f"emails({enc['activityid']})", {"regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({conv})"})
print("PATCH Regarding:", s, "" if s in (200, 204) else rr)
s, e, _ = dv.call("GET", f"emails({enc['activityid']})?$select=_regardingobjectid_value,_ownerid_value,statuscode,directioncode,subject"); print("RELEER:", {k: v for k, v in e.items() if not k.startswith("@")})
assert e["_regardingobjectid_value"] == conv
est["B"] = {"entrante": {**r, "dv_email_id": enc["activityid"], "asunto": enc["subject"]}, "conversacion": conv, "tag": tag}
guardar(est)
