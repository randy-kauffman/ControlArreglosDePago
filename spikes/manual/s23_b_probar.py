"""Tarea B: un intento completo como el usuario de aplicacion (NO administrador): leer conversacion, correo entrante y queue;
crear Email (from = queue Spike, to = buzon 2, Regarding = conversacion, parentactivityid = entrante); SendEmail.
Se detiene en el primer fallo de escritura y guarda el mensaje exacto en evidencia/b_intentos.jsonl.
Uso: python3 s23_b_probar.py "<etiqueta del intento>" """
import sys
from lib import *
etq = sys.argv[1] if len(sys.argv) > 1 else "intento"
adm = DvAdmin(); est = cargar(); B = est["B"]; uid = est["usuario_app_id"]
Q_SPIKE = "4411c78a-4ec3-f111-a05a-000d3a35abca"
privs = json.load(open(os.path.join(EVID, "rol_oficial_privilegios.json")))
reg = {"etiqueta": etq, "t": utc_iso(), "privilegios_del_rol": {k: v for k, v in sorted(privs.items())}, "pasos": []}
def paso(nombre, s, detalle=""):
    ok = s in (200, 201, 204)
    reg["pasos"].append({"paso": nombre, "http": s, "ok": ok, "detalle": detalle if not ok else ""})
    print(f"  [{'OK ' if ok else 'ERR'}] {nombre}: HTTP {s} {'' if ok else detalle}")
    return ok
print(f"=== {etq} ===")
dv = DvCorreo()
s, w, _ = dv.call("GET", "WhoAmI"); paso("WhoAmI", s, w); assert w.get("UserId") == uid
print("  roles (releidos por admin):", roles_de_usuario(adm, uid))
s, b, _ = dv.call("GET", f"spk_conversacions({B['conversacion']})?$select=spk_nombre"); paso("leer spk_conversacion", s, b.get("error"))
s, b, _ = dv.call("GET", f"emails({B['entrante']['dv_email_id']})?$select=subject,messageid"); paso("leer Email entrante", s, b.get("error"))
s, b, _ = dv.call("GET", f"queues({Q_SPIKE})?$select=name,emailaddress"); paso("leer Queue Spike", s, b.get("error"))
s, b, _ = dv.call("GET", f"queueitems?$select=queueitemid&$filter=_objectid_value eq {B['entrante']['dv_email_id']}"); paso("leer Queue Items del entrante", s, b.get("error"))
asunto = "RE: " + B["entrante"]["asunto"]
cuerpo = {"subject": asunto, "description": f"<p>Respuesta de prueba ({etq}) enviada con el usuario de aplicacion, sin rol de administrador.</p>", "directioncode": True,
          "regardingobjectid_spk_conversacion_email@odata.bind": f"/spk_conversacions({B['conversacion']})",
          "parentactivityid@odata.bind": f"/emails({B['entrante']['dv_email_id']})",
          "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({Q_SPIKE})"},
                                      {"participationtypemask": 2, "addressused": adm_b2 if (adm_b2 := Graph4().buzon[2]) else ""}]}
s, b, h = dv.call("POST", "emails", cuerpo, solucion=None)
eid = Dataverse.id_creado(h) if s in (200, 201, 204) else None
ok_creado = paso("crear Email (from queue, Regarding, parentactivityid)", s, b.get("error"))
ok_envio = False
if ok_creado:
    est.setdefault("B_emails", []).append(eid)
    s, e, _ = adm.call("GET", f"emails({eid})?$select=statuscode,_ownerid_value,_parentactivityid_value,_regardingobjectid_value,sender,torecipients"); 
    reg["email_releido_tras_crear"] = {k: v for k, v in e.items() if not k.startswith("@")}
    s, b, _ = dv.call("POST", f"emails({eid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""})
    ok_envio = paso("SendEmail", s, b.get("error")); reg["t_sendemail"] = time.time()
    if ok_envio:
        if not os.environ.get("SPK_NO_GUARDAR_ENVIO"):
            est["B"]["email_enviado"] = eid; est["B"]["t_sendemail_utc"] = utc_iso(reg["t_sendemail"]); est["B"]["t_sendemail_epoch"] = reg["t_sendemail"]
    else:
        print("  (se borra el borrador del intento fallido, como admin)"); adm.call("DELETE", f"emails({eid})")
reg["resultado"] = "ENVIADO" if ok_envio else "FALLO"
with open(os.path.join(EVID, "b_intentos.jsonl"), "a") as f: f.write(json.dumps(reg, ensure_ascii=False) + "\n")
guardar(est)
print("RESULTADO:", reg["resultado"])
