"""V-1 (paso 3, extra para el flujo SND): desde Pending Send, ¿se puede pasar a 7 Sending, luego a 3 Sent con un messageid propio?
¿y de 6 a 8 Failed? Se escribe como el service principal (el flujo real lo hará con la cuenta de servicio). Sin envío real."""
from flib import *

dv = adm()
R = Res("v12_transiciones.json")
c = Correo()
e = est()
qid = e["cola_spk"]
idA = e["v1_issuesend_true"]["id"]
MSG = "<SPK-FC-V12-prueba@spike.invalid>"


def leer(i):
    x = limpio(email(dv, i))
    return {k: x.get(k) for k in ("statuscode", "statecode", "senton", "actualend", "messageid")}


a = {"inicial": leer(idA)}
st, b, _ = dv.call("PATCH", f"emails({idA})", {"statuscode": 7})
a["a7"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(idA)}
st, b, _ = dv.call("PATCH", f"emails({idA})", {"statuscode": 3, "messageid": MSG})
a["a3"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(idA)}
st, b, _ = dv.call("PATCH", f"emails({idA})", {"statecode": 1, "statuscode": 3})
a["a3_con_estado"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(idA)}
R.v("V-1", "6 -> 7 Sending", a["a7"]["http"] == 204 and a["a7"]["despues"]["statuscode"] == 7, "204 y 7", a["a7"])
R.v("V-1", "7 -> 3 Sent con messageid propio", a["a3"]["http"] == 204 and a["a3"]["despues"]["statuscode"] == 3, "204 y 3", a["a3"])
R.v("V-1", "el messageid propio queda grabado", a["a3"]["despues"]["messageid"] == MSG, MSG, a["a3"]["despues"]["messageid"])

# 6 -> 8
cuerpo = {"subject": PREFIJO + "V1 transicion a Failed", "description": "<p>Ficticio, no sale.</p>", "directioncode": True,
          "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues/({qid})".replace("/queues/(", "/queues(")},
                                     {"participationtypemask": 2, "addressused": c.b[2]}]}
if "v1_failed" not in e:
    st, b, h = dv.call("POST", "emails", cuerpo)
    assert st in (201, 204), (st, err(b))
    fid = Dataverse.id_creado(h)
    poner("v1_failed", {"id": fid})
    st, b, _ = dv.call("POST", f"emails({fid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""})
    assert st == 200, (st, err(b))
fid = est()["v1_failed"]["id"]
f = {"inicial": leer(fid)}
st, b, _ = dv.call("PATCH", f"emails({fid})", {"statuscode": 8})
f["a8"] = {"http": st, "error": err(b) if st >= 300 else None, "despues": leer(fid)}
R.v("V-1", "6 -> 8 Failed", f["a8"]["http"] == 204 and f["a8"]["despues"]["statuscode"] == 8, "204 y 8", f["a8"])
volcar("v12_detalle.json", {"A": a, "F": f})
