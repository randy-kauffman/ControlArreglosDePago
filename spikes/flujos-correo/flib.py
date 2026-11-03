"""Utilidades del spike F0 de `diseno/08` (correo por flujos de Power Automate). Reutiliza hlib (tanda 1) como t3lib.
Nunca imprime secretos, tokens ni direcciones: los buzones se nombran por su variable `PP_MAIL_BUZON_n`.

Reglas del spike: correo SOLO entre los buzones de prueba 1 a 4, asuntos con el prefijo `SPK-FC-`, datos ficticios,
nada se borra en el entorno. Tope de envíos por Graph guardado en estado.json (sobrevive entre scripts)."""
import json
import os
import sys
import time

AQUI_F = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI_F, "..", "..", "pruebas-humo", "tanda1"))
sys.path.insert(0, os.path.join(AQUI_F, "..", "..", "herramientas"))
import hlib as _h  # noqa: E402
from hlib import *  # noqa: E402,F401,F403
from graph_api import Graph  # noqa: E402

_h.ESTADO = os.path.join(AQUI_F, "estado.json")
_h.EVID = os.path.join(AQUI_F, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
ESTADO = _h.ESTADO
EVID = _h.EVID

PREFIJO = "SPK-FC-"
TOPE_CORREOS = 20
AREA_COLA = "88b4a845-b1cc-f111-a05b-00224808ffcd"      # cola del área (buzón 1): NO se toca
AREA_MAILBOX = "8ab4a845-b1cc-f111-a05b-00224808ffcd"
EST_EMAIL = {1: "Draft", 2: "Completed", 3: "Sent", 4: "Received", 5: "Canceled", 6: "Pending Send", 7: "Sending", 8: "Failed"}
COLS_EMAIL = ("subject,directioncode,statuscode,statecode,messageid,inreplyto,actualend,senton,createdon,modifiedon,"
              "_regardingobjectid_value,_ownerid_value,_owningbusinessunit_value,_parentactivityid_value,_createdby_value,"
              "correlationmethod,_receivingmailboxid_value,trackingtoken,sender,torecipients,conversationindex,"
              "correlatedsubjectchanged,_correlatedactivityid_value")


def est():
    return leer_estado()


def poner(k, v):
    e = leer_estado()
    e[k] = v
    guardar_estado(e)


def email(dv, eid, cols=COLS_EMAIL):
    return get(dv, f"emails({eid})?$select={cols}")


def limpio(d):
    """Quita anotaciones y columnas con direcciones o texto libre."""
    return {k: v for k, v in d.items() if not k.startswith("@") and k not in ("sender", "torecipients")}


def esperar(fn, maximo=600, paso=15):
    t0 = time.time()
    while time.time() - t0 < maximo:
        r = fn()
        if r:
            return r, round(time.time() - t0)
        time.sleep(paso)
    return None, round(time.time() - t0)


class Correo:
    """Graph restringido a los 4 buzones de prueba. Las direcciones solo viven en memoria."""

    def __init__(self):
        self.g = Graph()
        self.b = {n: self.g.buzon(n).lower() for n in (1, 2, 3, 4)}

    def nombre(self, direccion):
        for n, d in self.b.items():
            if (direccion or "").lower() == d:
                return f"PP_MAIL_BUZON_{n}"
        return "otra"

    def _contar(self):
        e = leer_estado()
        env = e.setdefault("correos_enviados", [])
        if len(env) >= TOPE_CORREOS:
            raise SystemExit(f"TOPE de {TOPE_CORREOS} correos alcanzado: no se envía")
        return e, env

    def _anotar(self, e, env, desde, para, asunto, http, t0, imid):
        env.append({"desde": f"PP_MAIL_BUZON_{desde}", "para": [f"PP_MAIL_BUZON_{p}" for p in para], "asunto": asunto, "http": http,
                    "t_envio_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0)), "internetMessageId": imid})
        guardar_estado(e)

    def enviar(self, desde, para, asunto, texto, html=None, adjuntos=None):
        """Correo nuevo `desde` -> `para` (lista de números 1..4). Crea el borrador para conocer su internetMessageId."""
        assert desde in (1, 2, 3, 4) and all(p in (1, 2, 3, 4) for p in para)
        assert asunto.startswith(PREFIJO), "el asunto debe empezar con " + PREFIJO
        e, env = self._contar()
        msg = {"subject": asunto, "body": {"contentType": "HTML" if html else "Text", "content": html or texto},
               "toRecipients": [{"emailAddress": {"address": self.b[p]}} for p in para]}
        if adjuntos:
            msg["attachments"] = adjuntos
        st, d = self.g.call("POST", f"users/{self.b[desde]}/messages", msg)
        assert st in (200, 201), (st, d.get("error"))
        t0 = time.time()
        st2, _ = self.g.call("POST", f"users/{self.b[desde]}/messages/{d['id']}/send")
        self._anotar(e, env, desde, para, asunto, st2, t0, d.get("internetMessageId"))
        return {"http": st2, "internetMessageId": d.get("internetMessageId"), "t_envio_epoch": t0, "asunto": asunto}

    def responder(self, desde, msg_id, texto, esperado_para):
        """createReply desde `desde`; el destinatario tiene que ser el buzón `esperado_para`."""
        st, d = self.g.call("POST", f"users/{self.b[desde]}/messages/{msg_id}/createReply")
        assert st in (200, 201), (st, d.get("error"))
        para = [x["emailAddress"]["address"].lower() for x in d.get("toRecipients", [])]
        if para != [self.b[esperado_para]] or d.get("ccRecipients") or PREFIJO not in (d.get("subject") or ""):
            raise SystemExit("respuesta fuera de lo autorizado: no se envía")
        e, env = self._contar()
        st, d2 = self.g.call("PATCH", f"users/{self.b[desde]}/messages/{d['id']}",
                             {"body": {"contentType": "HTML", "content": f"<p>{texto}</p>" + (d.get("body", {}).get("content") or "")}})
        assert st == 200, (st, d2.get("error"))
        t0 = time.time()
        st2, _ = self.g.call("POST", f"users/{self.b[desde]}/messages/{d['id']}/send")
        self._anotar(e, env, desde, [esperado_para], d["subject"], st2, t0, d2.get("internetMessageId"))
        return {"http": st2, "internetMessageId": d2.get("internetMessageId"), "t_envio_epoch": t0, "asunto": d["subject"]}

    def bandeja(self, n, contiene, top=30, carpeta="inbox"):
        st, b = self.g.call("GET", f"users/{self.b[n]}/mailFolders/{carpeta}/messages?$top={top}&$orderby=receivedDateTime desc"
                                   "&$select=id,subject,from,receivedDateTime,internetMessageId,conversationId,hasAttachments")
        assert st == 200, (st, b.get("error"))
        return [m for m in b.get("value", []) if contiene in (m.get("subject") or "")]


# ---------------------------------------------------------------------------------------------------- diagnóstico de los flujos
import re as _re
OFICIAL = "fab4b6ed-6116-f111-8342-000d3a316609"  # # User 8 (Oficial), como en la tanda 3
BU_AREA = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"


def sanear(texto, c=None):
    """Reemplaza direcciones de correo por el nombre de su buzón de prueba (o <correo>): nada de direcciones en la evidencia."""
    def rep(m):
        return c.nombre(m.group(0)) if c else "<correo>"
    texto = _re.sub(r"%40", "@", texto, flags=_re.I)  # Graph devuelve la dirección codificada en las URL de @odata.context
    return _re.sub(r"[\w.+-]+@[\w-]+(\.[\w-]+)+", lambda m: (c.nombre(m.group(0)) if c and c.nombre(m.group(0)) != "otra" else "<correo>") if "invalid" not in m.group(0) else m.group(0), texto)


def esperar_diag(dv, quien, contiene, maximo=600, paso=20, c=None):
    """Espera la fila `SPK-DIAG <quien> ...<contiene>` que dejan los flujos y devuelve (dict, segundos) o (None, s)."""
    t0 = time.time()
    while time.time() - t0 < maximo:
        r = get(dv, "emails?$select=activityid,subject,description,createdon&$orderby=createdon desc&$top=30&$filter=startswith(subject,'SPK-DIAG " + quien + " ')")
        for x in r["value"]:
            if contiene in (x.get("subject") or ""):
                try:
                    d = json.loads(x["description"])
                except Exception:
                    d = {"_crudo": (x.get("description") or "")[:3000]}
                d["_activityid"] = x["activityid"]
                d["_createdon"] = x["createdon"]
                return d, round(time.time() - t0)
        time.sleep(paso)
    return None, round(time.time() - t0)


def flujos_spk(dv):
    r = get(dv, "workflows?$select=name,statecode,statuscode,workflowid&$filter=startswith(name,'Cloud Flow - CARP - SPK')&$orderby=name asc")
    return r["value"]


def email_por_imid(dv, imid, desde_iso):
    r = get(dv, f"emails?$select={COLS_EMAIL}&$filter=createdon ge {desde_iso} and directioncode eq false&$orderby=createdon asc")
    return [x for x in r["value"] if (x.get("messageid") or "").strip("<>").lower() == (imid or "").strip("<>").lower()]


def esperar_email(dv, imid, t_envio_epoch, maximo=1500, paso=30):
    """Sondea hasta que el correo enviado aparezca como Email entrante en Dataverse. Devuelve (fila, segundos) o (None, s)."""
    desde = iso(datetime.fromtimestamp(t_envio_epoch - 300, timezone.utc))
    t0 = time.time()
    while time.time() - t0 < maximo:
        h = email_por_imid(dv, imid, desde)
        if h:
            return h[0], round(time.time() - t0)
        time.sleep(paso)
    return None, round(time.time() - t0)


_volcar_original = _h.volcar


def _volcar_saneado(nombre, obj):
    """Todo lo que se vuelca a evidencia pasa por sanear (Res incluido): el repo es público."""
    _volcar_original(nombre, json.loads(sanear(json.dumps(obj, ensure_ascii=False, default=str))))


_h.volcar = _volcar_saneado
volcar = _volcar_saneado
