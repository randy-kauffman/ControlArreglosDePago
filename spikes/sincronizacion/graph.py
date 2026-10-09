#!/usr/bin/env python3
"""Cliente minimo de Microsoft Graph para el Spike 3 (sin dependencias).

Lee PP_MAIL_* de ~/projects/pp_secrets.env (variable SPIKE_ENV para otra ruta). Nunca imprime secretos ni tokens.
Restriccion dura: solo permite enviar correo si TODOS los destinatarios (to/cc/bcc) estan en los buzones 1 a 4
y el remitente (el buzon dueño de la llamada) tambien. Cualquier otra direccion aborta antes de llamar a Graph.
"""
import json
import os
import time
import urllib.error
import urllib.parse
import urllib.request

ARCHIVO_ENV = os.environ.get("SPIKE_ENV", os.path.expanduser("~/projects/pp_secrets.env"))
GRAPH = "https://graph.microsoft.com/v1.0"


def _cfg():
    cfg = {}
    with open(ARCHIVO_ENV, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if linea and not linea.startswith("#") and "=" in linea:
                k, v = linea.split("=", 1)
                cfg[k.strip()] = v.strip().strip('"').strip("'")
    return cfg


class Graph:
    def __init__(self):
        cfg = _cfg()
        self.buzon = {i: cfg[f"PP_MAIL_BUZON_{i}"].lower() for i in (1, 2, 3, 4)}
        self.permitidos = set(self.buzon.values())
        datos = urllib.parse.urlencode({
            "grant_type": "client_credentials", "client_id": cfg["PP_MAIL_CLIENT_ID"],
            "client_secret": cfg["PP_MAIL_CLIENT_SECRET"], "scope": "https://graph.microsoft.com/.default",
        }).encode()
        url = f"https://login.microsoftonline.com/{cfg['PP_MAIL_TENANT_ID']}/oauth2/v2.0/token"
        with urllib.request.urlopen(urllib.request.Request(url, data=datos), timeout=30) as r:
            self._token = json.load(r)["access_token"]

    def call(self, metodo, ruta, cuerpo=None, cabeceras=None):
        """Devuelve (estado, json_o_dict). HTTP de error no lanza: (codigo, {"error": ...})."""
        h = {"Authorization": "Bearer " + self._token, "Content-Type": "application/json"}
        h.update(cabeceras or {})
        url = ruta if ruta.startswith("http") else GRAPH + ruta
        req = urllib.request.Request(url, method=metodo, headers=h,
                                     data=json.dumps(cuerpo).encode() if cuerpo is not None else None)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                txt = r.read().decode()
                return r.status, (json.loads(txt) if txt.strip().startswith(("{", "[")) else {"_texto": txt})
        except urllib.error.HTTPError as e:
            txt = e.read().decode()
            return e.code, {"error": txt[:1500]}

    def _validar(self, remitente, *listas):
        for d in [remitente] + [x for l in listas for x in l]:
            if d.lower() not in self.permitidos:
                raise SystemExit(f"DIRECCION NO PERMITIDA (solo buzones 1 a 4): {d}")

    def enviar(self, remitente, para, asunto, html, adjuntos=None, cc=None):
        """sendMail desde `remitente` (un buzon 1..4). Asunto debe empezar con [SPIKE3]."""
        cc = cc or []
        self._validar(remitente, para, cc)
        assert asunto.startswith("[SPIKE3]"), "El asunto debe llevar el prefijo [SPIKE3]"
        msg = {"subject": asunto, "body": {"contentType": "HTML", "content": html},
               "toRecipients": [{"emailAddress": {"address": a}} for a in para],
               "ccRecipients": [{"emailAddress": {"address": a}} for a in cc]}
        if adjuntos:
            msg["attachments"] = adjuntos
        return self.call("POST", f"/users/{remitente}/sendMail", {"message": msg, "saveToSentItems": True})

    def enviar_borrador(self, remitente, para, asunto, html, adjuntos=None):
        """Crea un borrador (para conocer el internetMessageId antes de enviar) y lo envia. Devuelve dict."""
        self._validar(remitente, para)
        assert asunto.startswith("[SPIKE3]"), "El asunto debe llevar el prefijo [SPIKE3]"
        msg = {"subject": asunto, "body": {"contentType": "HTML", "content": html},
               "toRecipients": [{"emailAddress": {"address": a}} for a in para]}
        if adjuntos:
            msg["attachments"] = adjuntos
        est, d = self.call("POST", f"/users/{remitente}/messages", msg)
        if est not in (200, 201):
            return {"error": (est, d)}
        t0 = time.time()
        est2, r = self.call("POST", f"/users/{remitente}/messages/{d['id']}/send")
        return {"gid": d["id"], "internetMessageId": d.get("internetMessageId"), "enviado_http": est2,
                "t_envio_epoch": t0, "t_envio_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0))}

    def responder(self, buzon, msg_id, html, asunto=None):
        """createReply -> (opcional cambia asunto) -> send. Devuelve dict como enviar_borrador. El borrador conserva el hilo."""
        self._validar(buzon, [])
        est, d = self.call("POST", f"/users/{buzon}/messages/{msg_id}/createReply")
        if est not in (200, 201):
            return {"error": (est, d)}
        patch = {"body": {"contentType": "HTML", "content": html + (d.get("body", {}).get("content") or "")}}
        if asunto:
            assert asunto.startswith("[SPIKE3]") or asunto.startswith("RE:") or asunto.startswith("Re:"), asunto
            patch["subject"] = asunto
        est, d2 = self.call("PATCH", f"/users/{buzon}/messages/{d['id']}", patch)
        if est != 200:
            return {"error": (est, d2)}
        to = [x["emailAddress"]["address"] for x in d2.get("toRecipients", [])]
        self._validar(buzon, to)
        t0 = time.time()
        est2, _ = self.call("POST", f"/users/{buzon}/messages/{d['id']}/send")
        return {"gid": d["id"], "internetMessageId": d2.get("internetMessageId"), "asunto": d2.get("subject"), "para": to,
                "enviado_http": est2, "t_envio_epoch": t0, "t_envio_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0))}

    def buscar(self, buzon, asunto_contiene=None, desde=None, top=25, carpeta="inbox"):
        """Mensajes de una carpeta, ordenados por recepcion desc, con encabezados de internet."""
        q = ("?$top=%d&$select=id,subject,from,toRecipients,receivedDateTime,sentDateTime,internetMessageId,"
             "conversationId,conversationIndex,hasAttachments,internetMessageHeaders" % top)
        q += "&$orderby=receivedDateTime desc"
        est, b = self.call("GET", f"/users/{buzon}/mailFolders/{carpeta}/messages" + urllib.parse.quote(q, safe="?&=$,'"))
        if est != 200:
            return est, b
        v = b.get("value", [])
        if asunto_contiene:
            v = [m for m in v if asunto_contiene in (m.get("subject") or "")]
        return 200, v

    def esperar_mensaje(self, buzon, asunto_contiene, desde_iso, maximo=1500, paso=30, carpeta="inbox"):
        t0 = time.time()
        while time.time() - t0 < maximo:
            est, v = self.buscar(buzon, asunto_contiene, carpeta=carpeta)
            if est == 200:
                for m in v:
                    if m["receivedDateTime"] >= desde_iso:
                        return m
            time.sleep(paso)
        return None


def encabezado(msg, nombre):
    for h in msg.get("internetMessageHeaders") or []:
        if h["name"].lower() == nombre.lower():
            return h["value"]
    return None
