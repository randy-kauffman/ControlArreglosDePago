"""Utilidades de la tanda 3 de humo (2026-10-20): correo real por la cola `Arreglos de Pago` (buzón de prueba 1).
Nunca imprime secretos, tokens ni direcciones de correo (la evidencia nombra los buzones por su variable, `PP_MAIL_BUZON_n`).

Autorización del usuario (2026-10-20): hasta ~8 correos con asunto `HUMO-T3-...`, SOLO desde los buzones 2 a 4 hacia el
buzón 1, más las respuestas que salen desde la cola (buzón 1) hacia esos buzones. `Correo` lo hace cumplir antes de
llamar a Graph: remitente en 2..4, destinatario exactamente el buzón 1, prefijo del asunto y un tope de 8 envíos que
se cuenta en estado.json (sobrevive entre scripts). Dataverse: reutiliza hlib de la tanda 1 (administrador) y llama
como User 8 (Oficial) o User 9 (Jefe) con MSCRMCallerID."""
import json
import os
import sys
import time

AQUI3 = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI3, "..", "tanda1"))
sys.path.insert(0, os.path.join(AQUI3, "..", "..", "herramientas"))
import hlib as _h  # noqa: E402
from hlib import *  # noqa: E402,F401,F403
from graph_api import Graph  # noqa: E402

_h.ESTADO = os.path.join(AQUI3, "estado.json")
_h.EVID = os.path.join(AQUI3, "evidencia")
os.makedirs(_h.EVID, exist_ok=True)
ESTADO = _h.ESTADO
EVID = _h.EVID

COLA = "88b4a845-b1cc-f111-a05b-00224808ffcd"
MAILBOX = "8ab4a845-b1cc-f111-a05b-00224808ffcd"
BU_AREA = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"
OFICIAL = "fab4b6ed-6116-f111-8342-000d3a316609"  # # User 8
JEFE = "7d93e3f3-6116-f111-8342-000d3a316609"     # # User 9
TOPE_CORREOS = 8
PREFIJO = "HUMO-T3-"
CONV_ABIERTA, CONV_FINALIZADA = 1, 2
RES_CON_ARREGLO = 159460001
EST_EMAIL = {1: "Draft", 2: "Completed", 3: "Sent", 4: "Received", 5: "Canceled", 6: "Pending Send", 7: "Sending", 8: "Failed"}
COLS_EMAIL = ("subject,directioncode,statuscode,statecode,messageid,inreplyto,actualend,senton,createdon,modifiedon,"
              "_regardingobjectid_value,_ownerid_value,_owningbusinessunit_value,_parentactivityid_value,_createdby_value,"
              "_sanic_respondidoporid_value,sanic_tiemporespuesta,correlationmethod,_receivingmailboxid_value,trackingtoken")
COLS_CONV = ("sanic_nombre,statecode,statuscode,sanic_fechaprimercorreo,sanic_sinrespuestadesde,sanic_cantidadrespuestas,"
             "sanic_tiempoprimerarespuesta,sanic_tiempototal,sanic_correonuevoenfinalizada,_sanic_oficialasignadoid_value,_sanic_finalizadoporid_value,_sanic_unidaaid_value,_sanic_clienteid_value,"
             "_ownerid_value,_owningbusinessunit_value,sanic_fechafinalizacion,sanic_resultado")


def est3():
    return leer_estado()


def poner(k, v):
    e = leer_estado()
    e[k] = v
    guardar_estado(e)


class Correo:
    """Graph con las restricciones de la tanda. Las direcciones solo viven en memoria."""

    def __init__(self):
        self.g = Graph()
        self.b = {n: self.g.buzon(n).lower() for n in (1, 2, 3, 4)}

    def nombre(self, direccion):
        """`PP_MAIL_BUZON_n` de una dirección (o `otra` si no es de los cuatro)."""
        for n, d in self.b.items():
            if (direccion or "").lower() == d:
                return f"PP_MAIL_BUZON_{n}"
        return "otra"

    def _contar(self, desde, asunto):
        e = leer_estado()
        enviados = e.setdefault("correos_enviados", [])
        if len(enviados) >= TOPE_CORREOS:
            raise SystemExit(f"TOPE de {TOPE_CORREOS} correos alcanzado: no se envía")
        return e, enviados

    def _registrar(self, e, enviados, desde, asunto, http, t0, imid):
        enviados.append({"desde": f"PP_MAIL_BUZON_{desde}", "para": "PP_MAIL_BUZON_1", "asunto": asunto, "http": http,
                         "t_envio_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0)), "internetMessageId": imid})
        guardar_estado(e)

    def enviar(self, desde, asunto, texto):
        """Correo nuevo desde el buzón `desde` (2..4) al buzón 1. Crea el borrador para conocer su internetMessageId."""
        assert desde in (2, 3, 4), "solo se envía desde los buzones 2 a 4"
        assert asunto.startswith(PREFIJO), "el asunto debe empezar con " + PREFIJO
        e, enviados = self._contar(desde, asunto)
        msg = {"subject": asunto, "body": {"contentType": "Text", "content": texto},
               "toRecipients": [{"emailAddress": {"address": self.b[1]}}]}
        st, d = self.g.call("POST", f"users/{self.b[desde]}/messages", msg)
        assert st in (200, 201), (st, d.get("error"))
        t0 = time.time()
        st2, _ = self.g.call("POST", f"users/{self.b[desde]}/messages/{d['id']}/send")
        self._registrar(e, enviados, desde, asunto, st2, t0, d.get("internetMessageId"))
        return {"http": st2, "internetMessageId": d.get("internetMessageId"), "t_envio_epoch": t0, "asunto": asunto}

    def responder(self, desde, msg_id, texto):
        """createReply desde el buzón `desde` (2..4) a un mensaje de su bandeja; el destinatario tiene que ser el buzón 1."""
        assert desde in (2, 3, 4)
        st, d = self.g.call("POST", f"users/{self.b[desde]}/messages/{msg_id}/createReply")
        assert st in (200, 201), (st, d.get("error"))
        para = [x["emailAddress"]["address"].lower() for x in d.get("toRecipients", [])]
        cc = d.get("ccRecipients") or []
        if para != [self.b[1]] or cc or not (d.get("subject") or "").replace("RE: ", "").replace("Re: ", "").startswith(PREFIJO):
            raise SystemExit("respuesta fuera de lo autorizado (destinatario o asunto): no se envía; el borrador queda en Borradores del buzón de prueba")
        e, enviados = self._contar(desde, d["subject"])
        cuerpo = {"body": {"contentType": "HTML", "content": f"<p>{texto}</p>" + (d.get("body", {}).get("content") or "")}}
        st, d2 = self.g.call("PATCH", f"users/{self.b[desde]}/messages/{d['id']}", cuerpo)
        assert st == 200, (st, d2.get("error"))
        t0 = time.time()
        st2, _ = self.g.call("POST", f"users/{self.b[desde]}/messages/{d['id']}/send")
        self._registrar(e, enviados, desde, d["subject"], st2, t0, d2.get("internetMessageId"))
        return {"http": st2, "internetMessageId": d2.get("internetMessageId"), "t_envio_epoch": t0, "asunto": d["subject"]}

    def bandeja(self, n, contiene, top=30):
        """Mensajes de la bandeja de entrada del buzón n cuyo asunto contiene `contiene` (sin direcciones en la salida)."""
        st, b = self.g.call("GET", f"users/{self.b[n]}/mailFolders/inbox/messages?$top={top}&$orderby=receivedDateTime desc"
                                   "&$select=id,subject,from,receivedDateTime,internetMessageId,conversationId")
        assert st == 200, (st, b.get("error"))
        return [m for m in b.get("value", []) if contiene in (m.get("subject") or "")]


def email_por_imid(dv, imid, desde_iso):
    r = get(dv, f"emails?$select={COLS_EMAIL}&$filter=createdon ge {desde_iso} and directioncode eq false&$orderby=createdon asc")
    return [x for x in r["value"] if (x.get("messageid") or "").strip("<>").lower() == (imid or "").strip("<>").lower()]


def esperar_email(dv, imid, t_envio_epoch, maximo=1500, paso=30):
    """Sondea hasta que el correo enviado aparezca como Email en Dataverse. Devuelve (fila, segundos) o (None, s)."""
    desde = iso(datetime.fromtimestamp(t_envio_epoch - 300, timezone.utc))
    t0 = time.time()
    while time.time() - t0 < maximo:
        h = email_por_imid(dv, imid, desde)
        if h:
            return h[0], round(time.time() - t_envio_epoch)
        time.sleep(paso)
    return None, round(time.time() - t_envio_epoch)


def esperar(fn, maximo=600, paso=15):
    """Repite fn() hasta que devuelva algo verdadero."""
    t0 = time.time()
    while time.time() - t0 < maximo:
        r = fn()
        if r:
            return r, round(time.time() - t0)
        time.sleep(paso)
    return None, round(time.time() - t0)


def email(dv, eid):
    return get(dv, f"emails({eid})?$select={COLS_EMAIL}")


def conv(dv, cid):
    return get(dv, f"sanic_carp_tbl_conversacions({cid})?$select={COLS_CONV}")


def limpio(d):
    return {k: v for k, v in d.items() if not k.startswith("@") and k not in ("subject", "sender", "torecipients")}


def cambios_de(dv, tabla, rid):
    """Historial de auditoría (RetrieveRecordChangeHistory) resumido: columna, valor viejo y nuevo, quién y cuándo."""
    st, b, _ = dv.call("GET", f"RetrieveRecordChangeHistory(Target=@t)?@t={{'@odata.id':'{tabla}({rid})'}}")
    if st != 200:
        return {"http": st, "error": err(b)}
    out = []
    for d in b.get("AuditDetailCollection", {}).get("AuditDetails", []):
        a = d.get("AuditRecord", {})
        viejo, nuevo = d.get("OldValue") or {}, d.get("NewValue") or {}
        cols = sorted(set(k for k in list(viejo) + list(nuevo) if not k.startswith("@") and k not in ("activityid",)))
        out.append({"cuando": a.get("createdon"), "accion": a.get("action"), "operacion": a.get("operation"), "usuario": a.get("_userid_value"),
                    "cols": {c: [str(viejo.get(c))[:60], str(nuevo.get(c))[:60]] for c in cols if c not in ("subject", "description", "sender", "torecipients", "submittedby", "emailsender", "_emailsender_value")}})
    return out
