"""Tanda 3, caso A (correo 1 de la tanda): el buzón 2 escribe al buzón 1 un correo nuevo. Mide la entrada por la sincronización
del servidor y S-34: H-26 (BU y dueño del Email entrante), H-25 (¿el Create trae el Regarding?), ruta C (conversación nueva),
DC-14 (cliente enlazado por el correo del remitente), H-27 (a, b) (SYSTEM escribe el Regarding y asigna un Email ya cerrado).
Uso: t10_entrada_nueva.py [--sin-enviar]  (con --sin-enviar solo vuelve a medir el caso ya enviado)."""
from t3lib import *

dv = adm()
R = Res("10_entrada_nueva.json")
e = est3()
if "caso_a" not in e and "--sin-enviar" not in sys.argv:
    c = Correo()
    env = c.enviar(2, "HUMO-T3-A Consulta de arreglo de pago", "Prueba de humo T3, caso A. Datos ficticios.")
    R.v("A", "Graph acepta el envío (buzón 2 a buzón 1)", env["http"] == 202, 202, env["http"])
    poner("caso_a", env)
    e = est3()
a = e["caso_a"]
fila, seg = esperar_email(dv, a["internetMessageId"], a["t_envio_epoch"])
R.v("A", "el correo entra a Dataverse por la sincronización", fila is not None, "Email entrante", {"segundos_desde_el_envio": seg})
assert fila
eid = fila["activityid"]
a["email_id"] = eid
poner("caso_a", a)
R.v("A", "lo recibe el buzón de la cola", fila["_receivingmailboxid_value"] == MAILBOX, MAILBOX, fila["_receivingmailboxid_value"])

# S-34 es asíncrono: esperar a que el Regarding apunte a una conversación
def con_conv():
    x = email(dv, eid)
    return x if x.get("_regardingobjectid_value") else None
x, s2 = esperar(con_conv, 600, 10)
R.v("A", "S-34 apunta el correo a una conversación", x is not None, "Regarding = conversación", {"segundos_tras_aparecer": s2})
x = x or email(dv, eid)
R.v("H-26", "el Email entrante queda en la BU del área", x["_owningbusinessunit_value"] == BU_AREA, BU_AREA, x["_owningbusinessunit_value"])
R.v("H-26", "el Email entrante es del team del área", x["_ownerid_value"] == TEAM_DEF, TEAM_DEF, x["_ownerid_value"])
R.v("H-27", "el Email sigue cerrado (Received) después de que SYSTEM lo apunta", x["statecode"] == 1 and x["statuscode"] == 4, "1/4", (x["statecode"], x["statuscode"]))
cid = x["_regardingobjectid_value"]
a["conversacion_id"] = cid
poner("caso_a", a)
cv = conv(dv, cid)
R.v("A", "conversación nueva Abierta, del team, en la BU", cv["statuscode"] == CONV_ABIERTA and cv["_ownerid_value"] == TEAM_DEF and cv["_owningbusinessunit_value"] == BU_AREA,
    "Abierta/team/BU", limpio(cv))
R.v("A", "fecha del primer correo y pendiente = llegada (actualend)", cv["sanic_fechaprimercorreo"] == x["actualend"] and cv["sanic_sinrespuestadesde"] == x["actualend"],
    x["actualend"], (cv["sanic_fechaprimercorreo"], cv["sanic_sinrespuestadesde"]))
R.v("DC-14", "la conversación se enlaza al cliente con el correo del remitente", cv["_sanic_clienteid_value"] == e["cliente_t3"], e["cliente_t3"], cv["_sanic_clienteid_value"])
R.v("A", "nombre autonumérico CONV-nnnnnn", (cv["sanic_nombre"] or "").startswith("CONV-") and len(cv["sanic_nombre"]) == 11, "CONV-nnnnnn", cv["sanic_nombre"])

tr = resumen_trazas(trazas(dv, datetime.fromtimestamp(a["t_envio_epoch"] - 60, timezone.utc), esperar=30))
mias = [t for t in tr if any(eid in l for l in t["lineas"])]
volcar("10_trazas.json", mias)
ruta = [l for t in mias for l in t["lineas"] if "ruta" in l]
R.v("H-25", "por qué ruta entró (A = Exchange ya correlacionó en el Create; C = nueva por huella)", any("ruta C" in l for l in ruta), "ruta C (correo nuevo)", ruta)

# H-25 y H-27: quién escribió el Regarding y el dueño (auditoría del Email, si está activa)
hist = cambios_de(dv, "emails", eid)
volcar("10_auditoria_email.json", hist)
R.v("H-25/H-27", "historial del Email (Regarding y dueño: en el Create o en un Update posterior, y quién)", True, "informativo", hist if isinstance(hist, dict) else hist[:6])
volcar("10_email.json", limpio(x))
volcar("10_conversacion.json", limpio(cv))
