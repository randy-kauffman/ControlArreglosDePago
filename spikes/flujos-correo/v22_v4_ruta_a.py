"""V-4, parte 2: correlación por In-Reply-To en el flujo de entrada (ruta A de S-34). Requiere haber corrido v21 (caso A: email del buzón 4 con conversación).
Simula lo que dejará el flujo de salida: 1) el buzón 4 contesta por Graph (createReply+send) y se lee el internetMessageId real R;
2) en Dataverse se crea un saliente Sent con Regarding a la conversación y messageid = R (como hará SND);
3) el buzón 3 contesta ese correo (createReply, buzón 3 -> buzón 4) y el flujo de entrada debe poner el Regarding sin intervención (modo ruta_A)."""
from flib import *

dv = adm()
R = Res("v22_v4_ruta_a.json")
c = Correo()
e = est()
V = e["v4"]
conv0 = V["A_conv"]
A = V["A"]
RA = V.get("RA", {})
# 1) respuesta del buzón 4 al correo A (como saldría de SND)
if "salida" not in RA:
    ms = [m for m in c.bandeja(4, A["asunto"]) if not m["subject"].lower().startswith("re:")]
    assert ms, "no está el correo A en la bandeja del buzón 4"
    RA["salida"] = c.responder(4, ms[0]["id"], "Respuesta ficticia desde el area (simula la salida por flujo).", esperado_para=3)
    poner_v = dict(V, RA=RA)
    poner("v4", poner_v)
sal = RA["salida"]
R.v("V-4", "A->R: el buzón 4 responde por Graph (202)", sal["http"] == 202, 202, sal["http"])
# 2) saliente en Dataverse con el messageid real, Regarding a la conversación
if "saliente_id" not in RA:
    cuerpo = {"subject": sal["asunto"], "description": "<p>Respuesta ficticia (simula SND).</p>", "directioncode": True,
              "regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind": f"/sanic_carp_tbl_conversacions({conv0})",
              "parentactivityid@odata.bind": f"/emails({V['A_email']})",
              "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({AREA_COLA})"},
                                         {"participationtypemask": 2, "addressused": c.b[3]}]}
    st, b, h = dv.call("POST", "emails", cuerpo)
    RA["crear_saliente"] = {"http": st, "error": err(b) if st >= 300 else None}
    if st in (201, 204):
        RA["saliente_id"] = Dataverse.id_creado(h)
        st2, b2, _ = dv.call("PATCH", f"emails({RA['saliente_id']})", {"statuscode": 7})
        st3, b3, _ = dv.call("PATCH", f"emails({RA['saliente_id']})", {"statecode": 1, "statuscode": 3, "messageid": sal["internetMessageId"]})
        RA["a_sent"] = {"a7": st2, "a3": st3, "error": err(b3) if st3 >= 300 else None}
    poner("v4", dict(V, RA=RA))
R.v("V-4", "A->R: saliente Sent en Dataverse con el messageid real", RA.get("saliente_id") is not None and RA["a_sent"]["a3"] == 204, 204, RA.get("crear_saliente") if "saliente_id" not in RA else RA["a_sent"])
sx = limpio(email(dv, RA["saliente_id"]))
R.v("V-4", "A->R: queda Sent con ese messageid y el Regarding de la conversación", sx["statuscode"] == 3 and (sx["messageid"] or "").lower() == (sal["internetMessageId"] or "").lower() and sx["_regardingobjectid_value"] == conv0, "3 + messageid + conversación",
    {k: sx.get(k) for k in ("statuscode", "_regardingobjectid_value", "actualend", "senton")})
# 3) el buzón 3 contesta la respuesta
def respuesta_en_3():
    m = [x for x in c.bandeja(3, A["asunto"]) if x["subject"].lower().startswith("re:")]
    return m[0] if m else None
if "cliente" not in RA:
    rm, s = esperar(respuesta_en_3, 240, 15)
    R.v("V-4", "A->R: la respuesta llega al buzón 3", rm is not None, "mensaje", s)
    assert rm
    R.v("V-4", "A->R: el internetMessageId recibido es el que se grabó en Dataverse", (rm["internetMessageId"] or "").lower() == (sal["internetMessageId"] or "").lower(), "iguales", [rm["internetMessageId"], sal["internetMessageId"]])
    RA["cliente"] = c.responder(3, rm["id"], "Contestacion ficticia del cliente, spike V-4 ruta A.", esperado_para=4)
    poner("v4", dict(V, RA=RA))
cli = RA["cliente"]
dR, sR = esperar_diag(dv, "V4", cli["asunto"], c=c)
R.v("V-4", "ruta A: el flujo corre con la contestación del cliente", dR is not None, "fila SPK-DIAG", sR)
assert dR
volcar("v22_diag.json", json.loads(sanear(json.dumps(dR, ensure_ascii=False), c)))
R.v("V-4", "ruta A: el flujo leyó el In-Reply-To (Graph) igual al messageid grabado", (dR.get("in_reply_to") or "").strip("<>").lower() == (sal["internetMessageId"] or "").strip("<>").lower(), "iguales", dR.get("in_reply_to"))
R.v("V-4", "ruta A: el flujo encontró la conversación y creó el email con Regarding (modo ruta_A)", dR.get("conversacion_encontrada") == conv0 and "ruta_A" in (dR.get("modo") or ""), conv0, {"conversacion": dR.get("conversacion_encontrada"), "modo": dR.get("modo")})
fR = [x for x in get(dv, f"emails?$select={COLS_EMAIL}&$filter=createdon ge {V['desde']} and directioncode eq false")["value"] if (x.get("messageid") or "").strip("<>").lower() == (cli["internetMessageId"] or "").strip("<>").lower()]
R.v("V-4", "ruta A: email entrante creado con Regarding = la misma conversación (sin tocar a nadie)", len(fR) == 1 and fR[0]["_regardingobjectid_value"] == conv0, conv0, [x["_regardingobjectid_value"] for x in fR])
time.sleep(30)
tr = resumen_trazas(trazas(dv, datetime.fromtimestamp(cli["t_envio_epoch"] - 60, timezone.utc), esperar=30))
mias = [t for t in tr if fR and any(fR[0]["activityid"] in l for l in t["lineas"])]
volcar("v22_trazas.json", mias)
R.v("V-4", "ruta A: S-34 entra por la ruta A (Unida/YaTenia), sin conversación nueva", any(("Unida" in l or "YaTenia" in l) for t in mias for l in t["lineas"]), "Unida (A)", [l for t in mias for l in t["lineas"] if "ruta" in l or "resultado" in l])
cn = get(dv, f"sanic_carp_tbl_conversacions?$select=sanic_nombre&$filter=_sanic_clienteid_value eq null and createdon ge {V['desde']}")["value"]
R.v("V-4", "ruta A: no se abrió conversación nueva por la contestación (informativo)", True, "informativo", {"conversaciones_desde_el_inicio_del_spike": len(cn)})
