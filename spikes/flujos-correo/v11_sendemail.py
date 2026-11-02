"""V-1 (paso 2): SendEmail desde la cola de prueba (buzón 4, entrega None) hacia el buzón 2.
Caso A: IssueSend=true. Caso B: IssueSend=false (conocimiento de respaldo). Mide statuscode/senton/actualend y comprueba que NADA salió."""
from flib import *

dv = adm()
R = Res("v11_sendemail.json")
c = Correo()
e = est()
qid = e["cola_spk"]
org0 = get(dv, "organizations?$select=requireapprovalforqueueemail")["value"][0]["requireapprovalforqueueemail"]
R.v("V-1", "requireapprovalforqueueemail del entorno (informativo)", True, "informativo", org0)


def crear_saliente(asunto):
    cuerpo = {"subject": asunto, "description": "<p>Correo ficticio del spike F0 (V-1). No debe salir.</p>", "directioncode": True,
              "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({qid})"},
                                         {"participationtypemask": 2, "addressused": c.b[2]}]}
    st, b, h = dv.call("POST", "emails", cuerpo)
    assert st in (201, 204), (st, err(b))
    return Dataverse.id_creado(h)


def caso(clave, asunto, issue_send):
    a = e.get(clave) or {}
    if "id" not in a:
        a["id"] = crear_saliente(asunto)
        poner(clave, a)
    x0 = limpio(email(dv, a["id"]))
    a["antes"] = {k: x0.get(k) for k in ("statuscode", "statecode", "senton", "actualend", "messageid")}
    if "sendemail" not in a:
        t0 = time.time()
        st, b, _ = dv.call("POST", f"emails({a['id']})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": issue_send, "TrackingToken": ""})
        a["sendemail"] = {"http": st, "error": err(b) if st >= 300 else None, "codigo": codigo(b) if st >= 300 else None,
                          "respuesta_keys": sorted(b.keys()) if isinstance(b, dict) else None, "t": round(time.time() - t0, 2)}
        poner(clave, a)
    lect = []
    for espera in (0, 20, 60):
        time.sleep(espera if espera == 0 else 20 if espera == 20 else 40)
        x = limpio(email(dv, a["id"]))
        lect.append({"+s": espera, **{k: x.get(k) for k in ("statuscode", "statecode", "senton", "actualend", "messageid", "correlationmethod")}})
    a["lecturas"] = lect
    poner(clave, a)
    return a


A = caso("v1_issuesend_true", PREFIJO + "V1 IssueSend true", True)
R.v("V-1", "SendEmail con IssueSend=true lo acepta (200/204)", A["sendemail"]["http"] in (200, 204), "200/204", A["sendemail"])
fin = A["lecturas"][-1]
R.v("V-1", "queda en 6 Pending Send", fin["statuscode"] == 6, 6, [(l["+s"], l["statuscode"]) for l in A["lecturas"]])
B = caso("v1_issuesend_false", PREFIJO + "V1 IssueSend false", False)
R.v("V-1", "SendEmail con IssueSend=false (informativo)", True, "informativo", {"sendemail": B["sendemail"], "antes": B["antes"], "lecturas": B["lecturas"]})

# nada salió: ni en Enviados del buzón 4 ni en la bandeja del buzón 2
env4 = c.bandeja(4, PREFIJO + "V1", carpeta="SentItems")
ent2 = c.bandeja(2, PREFIJO + "V1")
R.v("V-1", "nada salió: 0 en Enviados del buzón 4 y 0 en la bandeja del buzón 2", not env4 and not ent2, "0 y 0", [len(env4), len(ent2)])
volcar("v11_resumen.json", {"requireapprovalforqueueemail": org0, "issuesend_true": A, "issuesend_false": B})
