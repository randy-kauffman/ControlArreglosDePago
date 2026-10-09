"""Utilidades del Spike 4 (sin secretos): clientes de Dataverse (administrador y usuario de aplicacion de correo) y de Graph.

- DvAdmin: la cuenta de servicio del estudio (System Administrator), la del helper del spike 2.
- DvCorreo: el service principal de correo (PP_MAIL_*) autenticado CONTRA Dataverse. Es el principal NO administrador
  de las tareas A y B (usuario de aplicacion con roles de prueba).
- Graph4: Graph con la misma restriccion del spike 3 (solo buzones 1 a 4) pero con el prefijo [SPIKE4].
Nunca imprime secretos ni tokens.
"""
import json
import os
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "plataforma"))
sys.path.insert(0, os.path.join(AQUI, "..", "sincronizacion"))
import dv as _dv  # noqa: E402  (plataforma/dv.py)
from dv import Dataverse, ENTORNO_AUTORIZADO, ARCHIVO_ENV, SOLUCION  # noqa: E402
import graph as _graph  # noqa: E402  (sincronizacion/graph.py)
from graph import Graph, encabezado  # noqa: E402

PREFIJO = "[SPIKE4]"
ESTADO = os.path.join(AQUI, "estado.json")
EVID = os.path.join(AQUI, "evidencia")
os.makedirs(EVID, exist_ok=True)
PREFER = {"Prefer": 'odata.include-annotations="OData.Community.Display.V1.FormattedValue,Microsoft.Dynamics.CRM.lookuplogicalname"'}


def cfg():
    return _graph._cfg()


class DvAdmin(Dataverse):
    pass


class DvCorreo(Dataverse):
    """Dataverse autenticado como el service principal de correo (usuario de aplicacion NO administrador)."""

    def __init__(self, version="v9.2"):
        c = cfg()
        self.org = c["DATAVERSE_URL"].rstrip("/")
        if self.org != ENTORNO_AUTORIZADO:
            raise SystemExit(f"Entorno no autorizado para el spike: {self.org}")
        self.api = f"{self.org}/api/data/{version}/"
        datos = urllib.parse.urlencode({
            "grant_type": "client_credentials", "client_id": c["PP_MAIL_CLIENT_ID"],
            "client_secret": c["PP_MAIL_CLIENT_SECRET"], "scope": self.org + "/.default",
        }).encode()
        url = f"https://login.microsoftonline.com/{c['PP_MAIL_TENANT_ID']}/oauth2/v2.0/token"
        with urllib.request.urlopen(urllib.request.Request(url, data=datos), timeout=30) as r:
            self._token = json.load(r)["access_token"]


def id_mail_client():
    return cfg()["PP_MAIL_CLIENT_ID"]


class Graph4(Graph):
    """Mismo cliente del spike 3, con el prefijo [SPIKE4] y la restriccion de buzones 1 a 4."""

    def enviar_borrador(self, remitente, para, asunto, html, adjuntos=None):
        self._validar(remitente, para)
        assert asunto.startswith(PREFIJO), f"El asunto debe llevar el prefijo {PREFIJO}"
        msg = {"subject": asunto, "body": {"contentType": "HTML", "content": html},
               "toRecipients": [{"emailAddress": {"address": a}} for a in para]}
        if adjuntos:
            msg["attachments"] = adjuntos
        est, d = self.call("POST", f"/users/{remitente}/messages", msg)
        if est not in (200, 201):
            return {"error": (est, d)}
        t0 = time.time()
        est2, _ = self.call("POST", f"/users/{remitente}/messages/{d['id']}/send")
        return {"gid": d["id"], "internetMessageId": d.get("internetMessageId"), "enviado_http": est2,
                "t_envio_epoch": t0, "t_envio_utc": utc_iso(t0)}

    def mensajes(self, buzon, top=40, carpeta="inbox"):
        """Mensajes de una carpeta (mas recientes primero) con encabezados de internet."""
        q = ("?$top=%d&$select=id,subject,from,sender,toRecipients,receivedDateTime,sentDateTime,internetMessageId,"
             "conversationId,conversationIndex,hasAttachments,internetMessageHeaders&$orderby=receivedDateTime desc" % top)
        est, b = self.call("GET", f"/users/{buzon}/mailFolders/{carpeta}/messages" + urllib.parse.quote(q, safe="?&=$,'"))
        return (est, b.get("value", []) if est == 200 else b)


def utc_iso(t=None):
    return datetime.fromtimestamp(t if t is not None else time.time(), timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def cargar():
    try:
        return json.load(open(ESTADO))
    except Exception:
        return {}


def guardar(est):
    json.dump(est, open(ESTADO, "w"), indent=1, ensure_ascii=False)


def volcar(nombre, obj):
    json.dump(obj, open(os.path.join(EVID, nombre), "w"), indent=1, ensure_ascii=False)


def raiz_bu(dv):
    est, b, _ = dv.call("GET", "businessunits?$select=businessunitid,name&$filter=_parentbusinessunitid_value eq null")
    assert est == 200 and len(b["value"]) == 1, (est, b)
    return b["value"][0]["businessunitid"]


def rol_por_nombre(dv, nombre, bu):
    est, b, _ = dv.call("GET", f"roles?$select=roleid,name,ismanaged&$filter=name eq '{nombre}' and _businessunitid_value eq {bu}")
    assert est == 200, (est, b)
    return [r for r in b["value"] if r["name"] == nombre]   # nombre exacto (el filtro ignora tildes y mayusculas)


def roles_de_usuario(dv, uid):
    est, b, _ = dv.call("GET", f"systemusers({uid})?$select=fullname&$expand=systemuserroles_association($select=name,roleid)")
    assert est == 200, (est, b)
    return sorted(r["name"] for r in b.get("systemuserroles_association", []))


def usuario_app(dv):
    est, b, _ = dv.call("GET", f"systemusers?$select=systemuserid,fullname,applicationid,isdisabled,accessmode,_businessunitid_value&$filter=applicationid eq {id_mail_client()}")
    assert est == 200, (est, b)
    return b["value"][0] if b["value"] else None


def privilegio(dv, nombre):
    est, b, _ = dv.call("GET", f"privileges?$select=privilegeid,name,canbebasic,canbelocal,canbedeep,canbeglobal&$filter=name eq '{nombre}'")
    assert est == 200, (est, b)
    return [p for p in b["value"] if p["name"] == nombre]
