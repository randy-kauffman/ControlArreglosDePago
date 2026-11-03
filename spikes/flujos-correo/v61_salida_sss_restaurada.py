"""Después de la ventana de v60: outgoingemailstatus del mailbox 1 queda en 0 (Not Run) al cambiar el método de salida. Es un estado, no
configuración. Esta prueba manda UN correo ficticio por SSS desde la cola del área (buzón 1) al buzón 2 y comprueba que se envía de verdad y
que outgoingemailstatus vuelve a 1 (Success). Correo solo entre buzones de prueba."""
from flib import *

dv = adm()
R = Res("v61_salida_sss_restaurada.json")
c = Correo()
cuerpo = {"subject": PREFIJO + f"V61 salida SSS restaurada {int(time.time())}", "description": "<p>Correo ficticio: comprobación de la salida SSS tras la ventana.</p>",
          "directioncode": True,
          "email_activity_parties": [{"participationtypemask": 1, "partyid_queue@odata.bind": f"/queues({AREA_COLA})"},
                                     {"participationtypemask": 2, "addressused": c.b[2]}]}
st, b, h = dv.call("POST", "emails", cuerpo)
assert st == 204, err(b)
rid = Dataverse.id_creado(h)
st, b, _ = dv.call("POST", f"emails({rid})/Microsoft.Dynamics.CRM.SendEmail", {"IssueSend": True, "TrackingToken": ""})
R.v("V-4", "SendEmail por SSS aceptado", st in (200, 204), "200/204", st)
y, seg = esperar(lambda: (lambda z: z if z["statuscode"] in (3, 8) else None)(limpio(email(dv, rid))), 900, 20)
R.v("V-4", "SSS de salida del buzón 1 vuelve a enviar (Sent)", bool(y) and y["statuscode"] == 3, 3, {"final": y and y["statuscode"], "segundos": seg})
m, s2 = esperar(lambda: (lambda x: x if x["outgoingemailstatus"] == 1 else None)(get(dv, f"mailboxes({AREA_MAILBOX})?$select=outgoingemailstatus")), 300, 20)
R.v("V-4", "outgoingemailstatus del mailbox 1 vuelve a Success (1)", m is not None, 1, s2)
