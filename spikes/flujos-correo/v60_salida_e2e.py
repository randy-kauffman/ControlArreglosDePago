"""V-4 SALIDA INTEGRAL (H-5 con flujo). NO ejecutar hasta que admin@ haya ACTIVADO el flujo SND, tenga Full Access + Send As sobre el buzón 1
y el usuario haya dado la ventana. AUTORIZADO por el usuario: apagar TEMPORALMENTE la salida SSS del buzón 1 (outgoingemaildeliverymethod 2 -> 0)
y volver a encenderla. Nada más del buzón 1 ni de la cola del área se modifica; las variables de entorno del área no se tocan.

Orden (la ventana cubre SOLO la fase 3):
 0) línea base del mailbox 1 (se guarda ANTES de cambiar nada; si una ventana anterior quedó sin restaurar, se restaura primero);
 1) con la salida SSS aún encendida: el buzón 2 escribe al buzón 1 (SSS de ENTRADA intacto) y S-34 crea la conversación;
 2) El Oficial (User 8) toma la conversación y crea la respuesta (S-31 fija la cola del área como remitente);
 3) VENTANA: salida SSS del mailbox 1 a None -> SendEmail como User 8 -> espera a que el flujo SND lo pase a Sent (o a Failed) -> restaurar y verificar;
 4) fuera de la ventana: el buzón 2 recibió la respuesta (remitente = buzón 1, internetMessageId = el grabado) y contesta; la contestación entra por SSS
    y debe correlacionar sola (ruta A) con el messageid que grabó el flujo.
Si algo falla en la ventana, el bloque finally restaura SIEMPRE el valor original y verifica. Si el correo quedó en Pending Send/Sending sin que el flujo
lo tomara, se marca Failed (8) ANTES de restaurar, para que SSS no lo envíe de verdad al volver a encenderse."""
from flib import *

dv = adm()
R = Res("v60_salida_e2e.json")
c = Correo()
e = est()
CALL8 = {"MSCRMCallerID": OFICIAL}
COLS_MBX = ("incomingemaildeliverymethod,outgoingemaildeliverymethod,enabledforincomingemail,enabledforoutgoingemail,incomingemailstatus,"
            "outgoingemailstatus,statuscode,emailrouteraccessapproval,testemailconfigurationscheduled,processanddeleteemails,_emailserverprofile_value")


def mbx():
    x = get(dv, f"mailboxes({AREA_MAILBOX})?$select={COLS_MBX}")
    return {k: v for k, v in x.items() if not k.startswith("@")}


def restaurar(original):
    st, b, _ = dv.call("PATCH", f"mailboxes({AREA_MAILBOX})", {"outgoingemaildeliverymethod": original["outgoingemaildeliverymethod"]})
    time.sleep(5)
    ahora = mbx()
    iguales = {k: (ahora[k] == original[k]) for k in original}
    return st, ahora, iguales


# 0) línea base y restauración de una ventana anterior que haya quedado abierta
W = e.get("ventana_salida", {})
if W.get("abierta"):
    print("Hay una ventana anterior sin cerrar: se restaura primero")
    st, ahora, iguales = restaurar(W["original"])
    W["abierta"] = False
    W["restaurada_antes"] = {"http": st, "iguales": iguales}
    poner("ventana_salida", W)
if "original" not in W or not W.get("original"):
    W["original"] = mbx()
    W["abierta"] = False
    poner("ventana_salida", W)
original = W["original"]
volcar("v60_mailbox1_original.json", original)
R.v("E2E", "línea base del mailbox 1 (SSS entrada y salida)", original["incomingemaildeliverymethod"] == 2 and original["outgoingemaildeliverymethod"] == 2, "2/2", {k: original[k] for k in ("incomingemaildeliverymethod", "outgoingemaildeliverymethod", "outgoingemailstatus", "statuscode")})
assert original["outgoingemaildeliverymethod"] == 2
fl = {f["name"]: f["statecode"] for f in flujos_spk(dv)}
snd = fl.get("Cloud Flow - CARP - SPK - SND Salida de respuestas (r2)")
R.v("E2E", "flujo SND encendido", snd == 1, "statecode 1", fl)
assert snd == 1, "el flujo SND no está encendido: no se abre la ventana"

X = e.get("e2e", {})
# 1) correo del cliente (buzón 2 -> buzón 1) por SSS de entrada
if "cliente" not in X:
    X["cliente"] = c.enviar(2, [1], PREFIJO + f"E2E consulta {int(time.time())}", "Consulta ficticia del spike (salida integral).")
    poner("e2e", X)
cl = X["cliente"]
fila, seg = esperar_email(dv, cl["internetMessageId"], cl["t_envio_epoch"]) if "email_id" not in X else (email(dv, X["email_id"]), 0)
R.v("E2E", "la consulta entra por SSS", fila is not None, "email", seg)
assert fila
X["email_id"] = fila["activityid"]
poner("e2e", X)
x, s = esperar(lambda: (lambda y: y if y.get("_regardingobjectid_value") else None)(email(dv, X["email_id"])), 600, 10)
R.v("E2E", "S-34 crea la conversación", x is not None, "Regarding", s)
assert x
X["conv"] = x["_regardingobjectid_value"]
poner("e2e", X)

# 2) el Oficial toma la conversación y crea la respuesta (sin enviar todavía)
cv = get(dv, f"sanic_carp_tbl_conversacions({X['conv']})?$select=_sanic_oficialasignadoid_value")
if cv["_sanic_oficialasignadoid_value"] != OFICIAL:
    st, b, t = llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": X["conv"]}, caller=OFICIAL)
    R.v("E2E", "el Oficial toma la conversación", st == 200, 200, st)
if "resp_id" not in X:
    cuerpo = {"subject": "RE: " + cl["asunto"], "description": "<p>Respuesta ficticia del Oficial por el flujo SND (spike).</p>", "directioncode": True,
              "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({X['conv']})",
              "parentactivityid@odata.bind": f"/emails({X['email_id']})",
              "email_activity_parties": [{"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
                                         {"participationtypemask": 2, "addressused": c.b[2]}]}
    st, b, h = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    R.v("E2E", "el Oficial crea la respuesta (S-31 fija la cola del área)", st == 204, 204, st if st == 204 else err(b))
    assert st == 204
    X["resp_id"] = Dataverse.id_creado(h)
    poner("e2e", X)
rid = X["resp_id"]

# 3) VENTANA
resultado = {}
try:
    W["abierta"] = True
    poner("ventana_salida", W)
    st, b, _ = dv.call("PATCH", f"mailboxes({AREA_MAILBOX})", {"outgoingemaildeliverymethod": 0})
    R.v("E2E", "VENTANA ABIERTA: salida SSS del mailbox 1 a None", st == 204, 204, st if st == 204 else err(b))
    m = mbx()
    R.v("E2E", "mailbox 1 en entrada SSS (2) y salida None (0)", m["incomingemaildeliverymethod"] == 2 and m["outgoingemaildeliverymethod"] == 0, "2/0", [m["incomingemaildeliverymethod"], m["outgoingemaildeliverymethod"]])
    t0 = time.time()
    st, b, _ = dv.call("POST", f"emails({rid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""}, cabeceras=CALL8)
    R.v("E2E", "SendEmail del Oficial aceptado", st in (200, 204), "200/204", st if st < 300 else err(b))
    lect = []
    def listo():
        y = limpio(email(dv, rid))
        lect.append({"+s": round(time.time() - t0), "statuscode": y["statuscode"]})
        return y if y["statuscode"] in (3, 8) else None
    y, seg = esperar(listo, 420, 5)
    resultado = {"final": y["statuscode"] if y else None, "segundos": seg, "lecturas": lect[:60]}
    R.v("E2E", "el flujo SND pasa el correo a Sent (3) sin que SSS de salida intervenga", bool(y) and y["statuscode"] == 3, 3, resultado)
    if not y:
        # no lo tomó nadie: se marca Failed para que SSS no lo envíe de verdad al volver a encenderse
        dv.call("PATCH", f"emails({rid})", {"statecode": 0, "statuscode": 8})
        resultado["marcado_failed_por_el_script"] = True
finally:
    st, ahora, iguales = restaurar(original)
    W["abierta"] = not all(iguales.get(k) for k in ("incomingemaildeliverymethod", "outgoingemaildeliverymethod"))
    W["cierre"] = {"http": st, "iguales": iguales}
    poner("ventana_salida", W)
    volcar("v60_mailbox1_restaurado.json", ahora)
    R.v("E2E", "VENTANA CERRADA: salida SSS del mailbox 1 restaurada", st == 204 and iguales.get("outgoingemaildeliverymethod"), "salida = 2", {"http": st, "iguales_a_la_linea_base": iguales})
    R.v("E2E", "mailbox 1 idéntico a la línea base (métodos, habilitados, estados, aprobación)", all(iguales.values()), "todos iguales", {k: v for k, v in iguales.items() if not v} or "todos iguales")

X["ventana"] = resultado
poner("e2e", X)
y = limpio(email(dv, rid))
R.v("E2E", "estado final del saliente (informativo)", True, "informativo", {k: y.get(k) for k in ("statuscode", "statecode", "messageid", "actualend", "senton", "_createdby_value")})
d, sd = esperar_diag(dv, "SND", str(rid), maximo=60, c=c)
volcar("v60_diag_snd.json", json.loads(sanear(json.dumps(d, ensure_ascii=False), c)) if d else {"sin_diag": True})
R.v("E2E", "diagnóstico del flujo SND (informativo)", True, "informativo", d and {k: d.get(k) for k in ("modo", "es_conversacion", "remitente_coincide", "sigue_pendiente", "imid_grabado")})
assert y["statuscode"] == 3, "el correo no quedó Sent: se detiene antes de la fase 4"

# S-35: respondido por y tiempo
y2 = email(dv, rid, "statuscode,messageid,_sanic_respondidoporid_value,sanic_tiemporespuesta,actualend,senton")
R.v("E2E", "S-35 (filtro statuscode) se dispara con el cambio a Sent hecho por el flujo: respondidopor = el Oficial", y2["_sanic_respondidoporid_value"] == OFICIAL, OFICIAL, y2["_sanic_respondidoporid_value"])
R.v("E2E", "S-35 calcula el tiempo de respuesta (informativo)", True, "informativo", {"tiemporespuesta": y2["sanic_tiemporespuesta"], "senton": y2["senton"], "actualend": y2["actualend"]})

# 4) fuera de la ventana: el cliente recibe y contesta
def en_2():
    m = [z for z in c.bandeja(2, cl["asunto"]) if z["subject"].lower().startswith("re:")]
    return m[0] if m else None
rm, s4 = esperar(en_2, 300, 15)
R.v("E2E", "la respuesta llega al buzón 2", rm is not None, "mensaje", s4)
assert rm
R.v("E2E", "remitente de la respuesta = buzón 1 (la cola del área; Send As)", c.nombre(rm["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_1", "PP_MAIL_BUZON_1", c.nombre(rm["from"]["emailAddress"]["address"]))
R.v("E2E", "el internetMessageId recibido es el messageid grabado por el flujo", (rm["internetMessageId"] or "").lower() == (y2["messageid"] or "").lower(), "iguales", [rm["internetMessageId"], y2["messageid"]])
if "contestacion" not in X:
    X["contestacion"] = c.responder(2, rm["id"], "Contestacion ficticia del cliente (salida integral).", esperado_para=1)
    poner("e2e", X)
ct = X["contestacion"]
fc, s5 = esperar_email(dv, ct["internetMessageId"], ct["t_envio_epoch"])
R.v("E2E", "la contestación del cliente entra por SSS", fc is not None, "email", s5)
assert fc
xc, s6 = esperar(lambda: (lambda y_: y_ if y_.get("_regardingobjectid_value") else None)(email(dv, fc["activityid"])), 300, 10)
xc = xc or email(dv, fc["activityid"])
R.v("E2E", "la contestación correlaciona con la MISMA conversación (el messageid que grabó el flujo sirve de In-Reply-To)", xc["_regardingobjectid_value"] == X["conv"], X["conv"],
    {"regarding": xc["_regardingobjectid_value"], "correlationmethod": xc.get("correlationmethod"), "inreplyto_informado": bool(xc.get("inreplyto"))})
