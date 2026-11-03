"""V-4, parte 1 (requiere el flujo V4 (r8) ACTIVADO por admin@): entrada por flujo sobre el buzón 4 con S-34 real.
Casos: A) correo nuevo sin adjuntos (ruta C, dueño/BU/cola/categoría); D) correo con adjuntos (archivo, imagen normal, imagen en línea, .eml)
enviado al buzón 4 (flujo) y al buzón 1 (SSS, para comparar); E) mide qué hace S-34 en cada uno. Cada caso: fila SPK-DIAG + Email + adjuntos."""
import base64
from flib import *

dv = adm()
R = Res("v21_v4_entrada.json")
c = Correo()
e = est()
V = e.get("v4", {})
nombre_flujo = "Cloud Flow - CARP - SPK - V4 Entrada completa (r8)"
fl = {f["name"]: f["statecode"] for f in flujos_spk(dv)}
R.v("V-4", "flujo V4 (r8) encendido", fl.get(nombre_flujo) == 1, "statecode 1", fl)
desde = reloj(dv)
V.setdefault("desde", iso(desde))
poner("v4", V)

PNG = base64.b64encode(bytes.fromhex("89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360f8cfc0f01f0005000101a5f645400000000049454e44ae426082")).decode()
EML = base64.b64encode(b"From: a@spike.invalid\r\nTo: b@spike.invalid\r\nSubject: eml adjunto ficticio\r\nMessage-ID: <spk-eml@spike.invalid>\r\n\r\nCuerpo ficticio.\r\n").decode()
ADJ = [
    {"@odata.type": "#microsoft.graph.fileAttachment", "name": "nota.txt", "contentType": "text/plain", "contentBytes": base64.b64encode(b"Adjunto ficticio SPK-FC.").decode()},
    {"@odata.type": "#microsoft.graph.fileAttachment", "name": "pixel.png", "contentType": "image/png", "contentBytes": PNG},
    {"@odata.type": "#microsoft.graph.fileAttachment", "name": "logo-en-linea.png", "contentType": "image/png", "contentBytes": PNG, "isInline": True, "contentId": "logo-spk-fc"},
    {"@odata.type": "#microsoft.graph.fileAttachment", "name": "reenviado.eml", "contentType": "message/rfc822", "contentBytes": EML},
]
HTML = '<p>Correo ficticio SPK-FC con imagen en l&iacute;nea:</p><p><img src="cid:logo-spk-fc" width="20" height="20"></p><p>Fin.</p>'


def columnas(x):
    return {k: x.get(k) for k in ("statuscode", "statecode", "directioncode", "messageid", "correlationmethod", "inreplyto", "actualend", "_regardingobjectid_value",
                                  "_ownerid_value", "_owningbusinessunit_value", "_receivingmailboxid_value")}


def adjuntos_de(eid):
    r = get(dv, f"activitymimeattachments?$select=filename,mimetype,attachmentcontentid,filesize,subject&$filter=_objectid_value eq {eid}&$orderby=filename asc")["value"]
    return [{k: a.get(k) for k in ("filename", "mimetype", "attachmentcontentid", "filesize")} for a in r]


def por_messageid(imid, desde_iso):
    r = get(dv, f"emails?$select={COLS_EMAIL}&$filter=createdon ge {desde_iso} and directioncode eq false&$orderby=createdon asc")["value"]
    return [x for x in r if (x.get("messageid") or "").strip("<>").lower() == (imid or "").strip("<>").lower()]


# ------------------------------------------------------------------ A) correo nuevo sin adjuntos, buzón 3 -> buzón 4
if "A" not in V:
    V["A"] = c.enviar(3, [4], PREFIJO + f"V4 nuevo {int(time.time())}", "Correo ficticio del spike V-4, caso A (sin adjuntos).")
    poner("v4", V)
A = V["A"]
dA, sA = esperar_diag(dv, "V4", A["asunto"], c=c)
R.v("V-4", "A: el flujo corre y deja su diagnóstico", dA is not None, "fila SPK-DIAG", sA)
assert dA
volcar("v21_A_diag.json", json.loads(sanear(json.dumps(dA, ensure_ascii=False), c)))
R.v("V-4", "A: estado de las acciones y opcionales (informativo)", True, "informativo", {"modo": dA.get("modo"), "acciones": dA.get("acciones"), "opcionales": dA.get("opcionales")})
filas = por_messageid(A["internetMessageId"], V["desde"])
R.v("V-4", "A: existe UN email en Dataverse con el messageid real del correo", len(filas) == 1, 1, len(filas))
assert filas
xa = filas[0]
V["A_email"] = xa["activityid"]
poner("v4", V)


def con_conv(i):
    def f():
        x = email(dv, i)
        return x if x.get("_regardingobjectid_value") else None
    return f
x, sx = esperar(con_conv(xa["activityid"]), 300, 10)
x = x or email(dv, xa["activityid"])
R.v("V-4", "A: dueño = team del área (S-34 lo exige)", x["_ownerid_value"] == TEAM_DEF, TEAM_DEF, x["_ownerid_value"])
R.v("V-4", "A: BU = la del área", x["_owningbusinessunit_value"] == BU_AREA, BU_AREA, x["_owningbusinessunit_value"])
R.v("V-4", "A: cerrado como Received (1/4)", x["statecode"] == 1 and x["statuscode"] == 4, "1/4", (x["statecode"], x["statuscode"]))
R.v("V-4", "A: S-34 procesa el entrante y lo apunta a una conversación nueva (ruta C)", bool(x.get("_regardingobjectid_value")), "Regarding = conversación", {"segundos": sx, "regarding": x.get("_regardingobjectid_value")})
V["A_conv"] = x.get("_regardingobjectid_value")
poner("v4", V)
tr = resumen_trazas(trazas(dv, datetime.fromtimestamp(A["t_envio_epoch"] - 60, timezone.utc), esperar=20))
volcar("v21_A_trazas.json", [t for t in tr if any(xa["activityid"] in l for l in t["lineas"])])
R.v("V-4", "A: cola — existe el queueitem en la cola de prueba", bool(get(dv, f"queueitems?$select=queueitemid&$filter=_objectid_value eq {xa['activityid']} and _queueid_value eq {e['cola_spk']}")["value"]), "1 queueitem", None)
g = c.g
st, m = g.call("GET", f"users/{c.b[4]}/mailFolders/inbox/messages?$top=30&$select=subject,categories&$orderby=receivedDateTime desc")
cat = [mm.get("categories") for mm in m.get("value", []) if A["asunto"] in (mm.get("subject") or "")]
R.v("V-4", "A: el mensaje queda con la categoría CARP-registrado en Outlook", bool(cat) and "CARP-registrado" in cat[0], "CARP-registrado", cat)

# ------------------------------------------------------------------ D) adjuntos: buzón 3 -> buzón 4 (flujo) y buzón 3 -> buzón 1 (SSS)
if "D" not in V:
    V["D"] = c.enviar(3, [4], PREFIJO + f"V4 adjuntos {int(time.time())}", "Cuerpo texto", html=HTML, adjuntos=ADJ)
    V["D_sss"] = c.enviar(3, [1], PREFIJO + f"V4 adjuntos SSS {int(time.time())}", "Cuerpo texto", html=HTML, adjuntos=ADJ)
    poner("v4", V)
D, DS = V["D"], V["D_sss"]
dD, sD = esperar_diag(dv, "V4", D["asunto"], c=c)
R.v("V-4", "D: el flujo corre con adjuntos y deja su diagnóstico", dD is not None, "fila SPK-DIAG", sD)
if dD:
    volcar("v21_D_diag.json", json.loads(sanear(json.dumps(dD, ensure_ascii=False), c)))
    R.v("V-4", "D: adjuntos que ve el disparador vs Graph (informativo)", True, "informativo", {"disparador": dD.get("trigger_adjuntos_cantidad"), "graph": dD.get("graph_adjuntos"), "acciones": dD.get("acciones"), "opcionales": dD.get("opcionales")})
fD = por_messageid(D["internetMessageId"], V["desde"])
R.v("V-4", "D: email creado por el flujo", len(fD) == 1, 1, len(fD))
adj_flujo = adjuntos_de(fD[0]["activityid"]) if fD else []
volcar("v21_D_adjuntos_flujo.json", adj_flujo)
fS, sS = esperar(lambda: (por_messageid(DS["internetMessageId"], V["desde"]) or None), 1200, 30)
adj_sss = adjuntos_de(fS[0]["activityid"]) if fS else []
volcar("v21_D_adjuntos_sss.json", adj_sss)
R.v("V-4", "D: el mismo correo por SSS (buzón 1) entró a Dataverse", bool(fS), "email", sS)
R.v("V-4", "D: adjuntos del flujo vs SSS (informativo; el flujo r7 NO crea adjuntos, F1)", True, "informativo", {"flujo": adj_flujo, "sss": adj_sss})
if fD:
    hd = get(dv, f"emails({fD[0]['activityid']})?$select=description")["description"] or ""
    R.v("V-4", "D: el HTML conserva la referencia cid (informativo)", True, "informativo", None) if False else R.v("V-4", "D: el HTML conserva la referencia cid", "cid:logo-spk-fc" in hd, "cid:logo-spk-fc", "cid presente" if "cid:logo-spk-fc" in hd else hd[:200])
