"""V-1 (paso 1): crea la cola de PRUEBA sobre el buzón 4 (no la del área, sin tocar variables) y deja su mailbox con entrega None/None.
Idempotente: si la cola ya está en estado.json, solo la verifica. Nada se borra."""
from flib import *

dv = adm()
R = Res("v10_cola_prueba.json")
c = Correo()
e = est()
if "cola_spk" not in e:
    # la cola del área y el buzón 1 no se tocan; esta cola es nueva
    st, b, h = dv.call("POST", "queues", {"name": "SPK - Correo por flujos (prueba, buzon 4)", "emailaddress": c.b[4], "queueviewtype": 0},
                       cabeceras=PREFER_REP)
    R.v("V-1", "crear cola de prueba con el buzón 4", st == 201, 201, st if st == 201 else err(b))
    assert st == 201
    qid = b["queueid"]
    registrar("cola_spk", "queues", qid, "cola de prueba V-1 (buzón 4)")
    e = est()
    e["cola_spk"] = qid
    guardar_estado(e)
qid = est()["cola_spk"]
q = get(dv, f"queues({qid})?$select=name,queueid,_defaultmailbox_value,emailaddress,_ownerid_value,incomingemaildeliverymethod,outgoingemaildeliverymethod,emailrouteraccessapproval")
mid = q["_defaultmailbox_value"]
R.v("V-1", "la cola quedó con buzón por defecto", bool(mid), "mailbox", mid)
R.v("V-1", "la dirección de la cola es el buzón 4", c.nombre(q["emailaddress"]) == "PP_MAIL_BUZON_4", "PP_MAIL_BUZON_4", c.nombre(q["emailaddress"]))
poner("mailbox_spk", mid)
cols = "name,statuscode,incomingemaildeliverymethod,outgoingemaildeliverymethod,actdeliverymethod,emailrouteraccessapproval,enabledforoutgoingemail,enabledforincomingemail,_emailserverprofile_value,_regardingobjectid_value"
m0 = get(dv, f"mailboxes({mid})?$select={cols}")
volcar("v10_mailbox_inicial.json", {k: v for k, v in m0.items() if not k.startswith("@")})
R.v("V-1", "mailbox inicial (informativo)", True, "informativo", {k: m0.get(k) for k in ("incomingemaildeliverymethod", "outgoingemaildeliverymethod", "emailrouteraccessapproval", "statuscode")})
if m0["incomingemaildeliverymethod"] != 0 or m0["outgoingemaildeliverymethod"] != 0:
    st, b, _ = dv.call("PATCH", f"mailboxes({mid})", {"incomingemaildeliverymethod": 0, "outgoingemaildeliverymethod": 0})
    R.v("V-1", "mailbox de prueba a None/None", st == 204, 204, st if st == 204 else err(b))
m1 = get(dv, f"mailboxes({mid})?$select={cols}")
R.v("V-1", "mailbox de prueba en None/None", m1["incomingemaildeliverymethod"] == 0 and m1["outgoingemaildeliverymethod"] == 0, "0/0",
    [m1["incomingemaildeliverymethod"], m1["outgoingemaildeliverymethod"]])
# el mailbox del área no cambió
ma = get(dv, f"mailboxes({AREA_MAILBOX})?$select=incomingemaildeliverymethod,outgoingemaildeliverymethod,statuscode")
R.v("V-1", "mailbox del área intacto (SSS entrada y salida)", ma["incomingemaildeliverymethod"] == 2 and ma["outgoingemaildeliverymethod"] == 2, "2/2",
    [ma["incomingemaildeliverymethod"], ma["outgoingemaildeliverymethod"]])
volcar("v10_mailbox_final.json", {k: v for k, v in m1.items() if not k.startswith("@")})
