"""V-2 (requiere el flujo V2 ACTIVADO por admin@). Envía de buzón 2 a buzón 4 un correo SPK-FC-V2; el flujo lee encabezados por Graph,
responde con createReply+send y deja una fila SPK-DIAG. Después el buzón 2 contesta la respuesta del flujo (createReply, buzón 2 -> buzón 4)
para ver el In-Reply-To REAL leído en el segundo disparo. Mide: ¿el id del disparador sirve para Graph?, ¿llegan internetMessageHeaders?,
¿el internetMessageId del borrador es el que sale?, latencias."""
from flib import *

dv = adm()
R = Res("v20_v2.json")
c = Correo()
e = est()
fl = {f["name"]: f["statecode"] for f in flujos_spk(dv)}
R.v("V-2", "flujo V2 encendido", fl.get("Cloud Flow - CARP - SPK - V2 Encabezados y respuesta") == 1, "statecode 1", fl)
V = e.get("v2", {})
if "m1" not in V:
    V["m1"] = c.enviar(2, [4], PREFIJO + f"V2 uno {int(time.time())}", "Correo ficticio del spike V-2 (primer mensaje).")
    poner("v2", V)
m1 = V["m1"]
R.v("V-2", "Graph acepta el envío buzón 2 -> buzón 4", m1["http"] == 202, 202, m1["http"])
d1, s1 = esperar_diag(dv, "V2", m1["asunto"], c=c)
R.v("V-2", "el flujo corre y deja su diagnóstico", d1 is not None, "fila SPK-DIAG", {"segundos_hasta_el_diagnostico": round(datetime.fromisoformat(d1["_createdon"].replace("Z", "+00:00")).timestamp() - m1["t_envio_epoch"]) if d1 else None})
assert d1
V["diag1"] = d1
poner("v2", V)
volcar("v20_diag1.json", json.loads(sanear(json.dumps(d1, ensure_ascii=False), c)))
R.v("V-2", "Graph lee el mensaje con el id del disparador (internetMessageId igual al del disparador)",
    d1.get("graph_internetMessageId") and d1["graph_internetMessageId"] == d1.get("trigger_internetMessageId"), "iguales", [d1.get("trigger_internetMessageId"), d1.get("graph_internetMessageId")])
R.v("V-2", "internetMessageId coincide con el que registró Graph al crear el borrador en el envío", (d1.get("graph_internetMessageId") or "").lower() == (m1["internetMessageId"] or "").lower(), "iguales", [m1["internetMessageId"], d1.get("graph_internetMessageId")])
R.v("V-2", "llegan internetMessageHeaders", (d1.get("encabezados_cantidad") or 0) > 0, "> 0", {"cantidad": d1.get("encabezados_cantidad"), "nombres": d1.get("encabezados_nombres")})
R.v("V-2", "createReply + GET del borrador devuelven internetMessageId", bool(d1.get("borrador_internetMessageId_get")), "presente", [d1.get("borrador_internetMessageId_createReply"), d1.get("borrador_internetMessageId_get")])
R.v("V-2", "send aceptado (202)", d1.get("enviar_status") == 202, 202, d1.get("enviar_status"))
R.v("V-2", "en Enviados el internetMessageId es el del borrador", d1.get("enviados_coincide_con_borrador") is True, True, d1.get("enviados_internetMessageId"))
R.v("V-2", "estado de las acciones (informativo)", True, "informativo", d1.get("acciones"))

# la respuesta del flujo llega al buzón 2: su internetMessageId real
def respuesta_en_2():
    m = [x for x in c.bandeja(2, V["m1"]["asunto"]) if x["subject"].lower().startswith("re:")]
    return m[0] if m else None
rm, s2 = esperar(respuesta_en_2, 240, 15)
R.v("V-2", "la respuesta del flujo llega al buzón 2", rm is not None, "mensaje RE:", {"segundos": s2})
assert rm
R.v("V-2", "remitente de la respuesta = buzón 4", c.nombre(rm["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_4", "PP_MAIL_BUZON_4", c.nombre(rm["from"]["emailAddress"]["address"]))
R.v("V-2", "internetMessageId recibido = el leído por el flujo en el borrador", (rm["internetMessageId"] or "").lower() == (d1.get("borrador_internetMessageId_get") or "").lower(), "iguales", [rm["internetMessageId"], d1.get("borrador_internetMessageId_get")])
V["resp_flujo"] = {"id": rm["id"], "internetMessageId": rm["internetMessageId"]}
poner("v2", V)

# segundo mensaje: el buzón 2 contesta la respuesta del flujo
if "m2" not in V:
    V["m2"] = c.responder(2, rm["id"], "Contestacion ficticia del spike V-2.", esperado_para=4)
    poner("v2", V)
m2 = V["m2"]
d2, s3 = esperar_diag(dv, "V2", m2["asunto"], c=c)
R.v("V-2", "el flujo corre con la contestación y deja su diagnóstico", d2 is not None, "fila SPK-DIAG", s3)
if d2:
    V["diag2"] = d2
    poner("v2", V)
    volcar("v20_diag2.json", json.loads(sanear(json.dumps(d2, ensure_ascii=False), c)))
    R.v("V-2", "In-Reply-To leído = internetMessageId de la respuesta que mandó el flujo", (d2.get("in_reply_to") or "").lower() == (d1.get("borrador_internetMessageId_get") or "").lower(), "iguales", [d2.get("in_reply_to"), d1.get("borrador_internetMessageId_get")])
    R.v("V-2", "References leído (informativo)", True, "informativo", d2.get("references"))
