"""Tanda 3, caso C (correos 3 y 4 de la tanda; sale R2 por la cola): correo nuevo en una conversación Finalizada y gesto 5.
1. El buzón 3 escribe al buzón 1 (sin Cliente con ese correo: DC-14, la conversación queda sin cliente).
2. User 8 toma y responde R2 por la cola (llega al buzón 3).
3. User 8 finaliza la conversación (gesto 2, resultado "Sin arreglo de pago").
4. El buzón 3 contesta R2: entra a la Finalizada, que sigue Finalizada con el indicador y el pendiente (PA-ENT-07, RF-15).
5. User 8 ejecuta Iniciar conversación nueva (gesto 5, el camino feliz que el lote B no pudo probar): conversación nueva Abierta
   sin oficial, con el correo movido; la anterior queda Finalizada, sin indicador y sin pendiente. Una segunda llamada da CARP-INI-003."""
from t3lib import *

dv = adm()
R = Res("40_finalizada_e_iniciar_nueva.json")
c = Correo()
CALL8 = {"MSCRMCallerID": OFICIAL}
e = est3()
k = e.get("caso_c", {})

# 1) correo nuevo del buzón 3
if "c1" not in k:
    k["c1"] = c.enviar(3, "HUMO-T3-C Pago de cuota atrasada", "Prueba de humo T3, caso C. Datos ficticios.")
    poner("caso_c", k)
if "email_id" not in k["c1"]:
    fila, seg = esperar_email(dv, k["c1"]["internetMessageId"], k["c1"]["t_envio_epoch"])
    assert fila, "el correo C1 no entró"
    k["c1"]["email_id"] = fila["activityid"]
    k["c1"]["segundos_a_dataverse"] = seg
    x, _ = esperar(lambda: (lambda y: y if y.get("_regardingobjectid_value") else None)(email(dv, fila["activityid"])), 600, 10)
    k["conversacion_id"] = x["_regardingobjectid_value"]
    poner("caso_c", k)
cid = k["conversacion_id"]
e1 = k["c1"]["email_id"]
cv = conv(dv, cid)
R.v("C", "conversación nueva para el buzón 3, Abierta", cv["statuscode"] == CONV_ABIERTA, "Abierta", {"nombre": cv["sanic_nombre"], "segundos_a_dataverse": k["c1"]["segundos_a_dataverse"]})
R.v("DC-14", "sin Cliente con el correo del buzón 3: la conversación queda sin cliente", cv["_sanic_clienteid_value"] is None, None, cv["_sanic_clienteid_value"])

# 2) tomar y responder R2
if cv["_sanic_oficialasignadoid_value"] != OFICIAL:
    st, b, t = llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": cid}, caller=OFICIAL)
    R.v("TOM", "User 8 toma la conversación C", st == 200, 200, (st, err(b) if st != 200 else t))
if "r2" not in k:
    cuerpo = {"subject": "RE: " + k["c1"]["asunto"], "description": "<p>Respuesta HUMO-T3 caso C desde la cola (datos ficticios).</p>", "directioncode": True,
              "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({cid})",
              "parentactivityid@odata.bind": f"/emails({e1})",
              "email_activity_parties": [{"participationtypemask": 1, "partyid_systemuser@odata.bind": f"/systemusers({OFICIAL})"},
                                         {"participationtypemask": 2, "addressused": c.b[3]}]}
    st, b, h = dv.call("POST", "emails", cuerpo, cabeceras=CALL8)
    assert st == 204, err(b)
    rid = Dataverse.id_creado(h)
    t0 = time.time()
    st, b, _ = dv.call("POST", f"emails({rid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""}, cabeceras=CALL8)
    k["r2"] = {"id": rid, "sendemail_http": st, "t_send_epoch": t0}
    poner("caso_c", k)
    e2 = leer_estado()
    e2.setdefault("correos_salientes", []).append({"desde": "PP_MAIL_BUZON_1 (cola)", "para": "PP_MAIL_BUZON_3", "asunto": cuerpo["subject"],
                                                   "t_utc": iso(datetime.fromtimestamp(t0, timezone.utc)), "email_id": rid})
    guardar_estado(e2)
rid = k["r2"]["id"]
y, s = esperar(lambda: (lambda z: z if z.get("_sanic_respondidoporid_value") else None)(email(dv, rid)), 900, 15)
R.v("H-6", "R2 enviada y procesada por S-35 (respondidoporid = User 8)", bool(y) and y["statuscode"] == 3 and y["_sanic_respondidoporid_value"] == OFICIAL, "Sent/User 8",
    {"estado": EST_EMAIL.get((y or email(dv, rid))["statuscode"]), "tiempo": (y or {}).get("sanic_tiemporespuesta"), "segundos": s})

def llego_b3():
    m = [m for m in c.bandeja(3, "HUMO-T3-C") if (m.get("subject") or "").startswith("RE:")]
    return m[0] if m else None
m, s = esperar(llego_b3, 900, 20)
R.v("H-5", "R2 llega al buzón 3 desde el buzón 1", bool(m) and c.nombre(m["from"]["emailAddress"]["address"]) == "PP_MAIL_BUZON_1", "PP_MAIL_BUZON_1", {"segundos": s})
k["r2"]["graph_msg_id_b3"] = m["id"]
poner("caso_c", k)

# 3) finalizar como User 8
cv = conv(dv, cid)
if cv["statuscode"] != CONV_FINALIZADA:
    st, b = actualizar(dv, "conversacion", cid, {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460002}, cabeceras=CALL8)
    R.v("FIN", "User 8 finaliza la conversación C (Sin arreglo de pago)", st == 204, 204, st if st == 204 else (codigo(b), err(b)))
cv = conv(dv, cid)
R.v("FIN", "Finalizada con quién y cuándo", cv["statuscode"] == CONV_FINALIZADA and cv["_sanic_finalizadoporid_value"] == OFICIAL and cv["sanic_fechafinalizacion"],
    "Finalizada/User 8", {"estado": cv["statuscode"], "finalizadopor": cv["_sanic_finalizadoporid_value"], "fecha": cv["sanic_fechafinalizacion"], "tiempototal": cv["sanic_tiempototal"]})
st, b, _ = llamar(dv, "sanic_carp_capi_iniciarconversacionnueva", {"conversacionid": cid}, caller=OFICIAL)
R.v("INI", "antes del correo nuevo, Iniciar nueva da CARP-INI-003", codigo(b) == "CARP-INI-003", "CARP-INI-003", (st, codigo(b)))

# 4) el buzón 3 contesta R2 (correo 4 de la tanda)
if "c2" not in k:
    k["c2"] = c.responder(3, k["r2"]["graph_msg_id_b3"], "El cliente HUMO-T3 vuelve a escribir después de la finalización (datos ficticios).")
    poner("caso_c", k)
if "email_id" not in k["c2"]:
    fila, seg = esperar_email(dv, k["c2"]["internetMessageId"], k["c2"]["t_envio_epoch"])
    assert fila, "el correo C2 no entró"
    k["c2"]["email_id"] = fila["activityid"]
    k["c2"]["segundos_a_dataverse"] = seg
    poner("caso_c", k)
e2id = k["c2"]["email_id"]
def marcada():
    v = conv(dv, cid)
    return v if v["sanic_correonuevoenfinalizada"] else None
cv, s = esperar(marcada, 600, 10)
cv = cv or conv(dv, cid)
x = email(dv, e2id)
R.v("PA-ENT-07", "el correo nuevo entra a la Finalizada, que sigue Finalizada con el indicador y el pendiente = su llegada",
    x["_regardingobjectid_value"] == cid and cv["statuscode"] == CONV_FINALIZADA and cv["sanic_correonuevoenfinalizada"] and cv["sanic_sinrespuestadesde"] == x["actualend"],
    {"regarding": cid, "estado": 2, "indicador": True, "pendiente": x["actualend"]},
    {"regarding": x["_regardingobjectid_value"], "estado": cv["statuscode"], "indicador": cv["sanic_correonuevoenfinalizada"], "pendiente": cv["sanic_sinrespuestadesde"], "segundos": s})

# 5) Iniciar conversación nueva como User 8
if "nueva" not in k:
    st, b, t = llamar(dv, "sanic_carp_capi_iniciarconversacionnueva", {"conversacionid": cid}, caller=OFICIAL)
    k["nueva"] = {"http": st, "segundos": t, "salida": {kk: v for kk, v in b.items() if not kk.startswith("@")} if st == 200 else None, "codigo": codigo(b)}
    poner("caso_c", k)
nv = k["nueva"]
R.v("INI", "Iniciar conversación nueva (User 8): 200 y 1 correo movido", nv["http"] == 200 and (nv["salida"] or {}).get("correosmovidos") == 1, "200/1", nv)
nid = (nv["salida"] or {}).get("nuevaconversacionid")
if nid:
    n = conv(dv, nid)
    R.v("INI", "la nueva: Abierta, sin oficial, del team, pendiente = llegada del correo, sin cliente (la anterior no tenía)",
        n["statuscode"] == CONV_ABIERTA and n["_sanic_oficialasignadoid_value"] is None and n["_ownerid_value"] == TEAM_DEF
        and n["sanic_sinrespuestadesde"] == x["actualend"] and n["sanic_fechaprimercorreo"] == x["actualend"] and n["_sanic_clienteid_value"] is None,
        "Abierta/sin oficial/team", limpio(n))
    R.v("INI", "el correo nuevo apunta a la nueva", email(dv, e2id)["_regardingobjectid_value"] == nid, nid, email(dv, e2id)["_regardingobjectid_value"])
    R.v("INI", "C1 y R2 siguen en la anterior", email(dv, e1)["_regardingobjectid_value"] == cid and email(dv, rid)["_regardingobjectid_value"] == cid, cid,
        (email(dv, e1)["_regardingobjectid_value"], email(dv, rid)["_regardingobjectid_value"]))
    cv = conv(dv, cid)
    R.v("INI", "la anterior: Finalizada, sin indicador y sin pendiente", cv["statuscode"] == CONV_FINALIZADA and not cv["sanic_correonuevoenfinalizada"] and cv["sanic_sinrespuestadesde"] is None,
        "Finalizada/no/None", (cv["statuscode"], cv["sanic_correonuevoenfinalizada"], cv["sanic_sinrespuestadesde"]))
    st, b, _ = llamar(dv, "sanic_carp_capi_iniciarconversacionnueva", {"conversacionid": cid}, caller=OFICIAL)
    R.v("PA-INI-11", "segunda llamada: CARP-INI-003", codigo(b) == "CARP-INI-003", "CARP-INI-003", (st, codigo(b)))
    volcar("40_nueva.json", limpio(n))
volcar("40_anterior.json", limpio(conv(dv, cid)))
