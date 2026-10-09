#!/usr/bin/env python3
"""Cliente mínimo de Microsoft Graph para mandar (y, si el permiso está, leer)
correo de prueba. Sin dependencias. Mismo patrón que `dataverse_api.py`.

Lee las credenciales del archivo de secretos del puesto (ver `credenciales.py` y `proyecto.json`) y obtiene el token por
credenciales de cliente, con scope `https://graph.microsoft.com/.default`. El service
principal es GENÉRICO (se reusa entre proyectos): tiene permiso de aplicación `Mail.Send`
y una Application Access Policy que lo limita a los buzones de prueba configurados acá
como `PP_MAIL_BUZON_1` … `PP_MAIL_BUZON_4`. **Nunca imprime el secreto ni el token.**

Si además tiene `Mail.Read`, `enviados()` funciona; si no, devuelve `disponible=False`
en cuanto Graph contesta 403 — nunca se asume el permiso de antemano.

Como librería:
    from graph_api import Graph
    g = Graph()
    estado, cuerpo = g.mandar_correo(g.buzon(1), g.buzon(1), "ZZP- prueba", "cuerpo")

Como comando:
    python3 herramientas/graph_api.py enviar 1 destino@correo "ZZP- asunto" "cuerpo" [ruta_adjunto]
    python3 herramientas/graph_api.py GET "users/<buzon>/mailFolders/SentItems/messages?\\$top=5"
"""
import base64
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from credenciales import leer_secretos  # noqa: E402

CLAVES_REQUERIDAS = ("PP_MAIL_TENANT_ID", "PP_MAIL_CLIENT_ID", "PP_MAIL_CLIENT_SECRET")


class Graph:
    def __init__(self, archivo_env=None):
        cfg = leer_secretos(archivo_env, requeridas=CLAVES_REQUERIDAS)
        self._cfg = cfg
        self.api = "https://graph.microsoft.com/v1.0/"
        self.buzones = {k: v for k, v in cfg.items() if k.startswith("PP_MAIL_BUZON_")}
        if not self.buzones:
            raise SystemExit("el archivo de secretos no tiene ningún PP_MAIL_BUZON_N configurado")
        datos = urllib.parse.urlencode({
            "grant_type": "client_credentials",
            "client_id": cfg["PP_MAIL_CLIENT_ID"],
            "client_secret": cfg["PP_MAIL_CLIENT_SECRET"],
            "scope": "https://graph.microsoft.com/.default",
        }).encode()
        url = f"https://login.microsoftonline.com/{cfg['PP_MAIL_TENANT_ID']}/oauth2/v2.0/token"
        with urllib.request.urlopen(urllib.request.Request(url, data=datos), timeout=30) as r:
            self._token = json.load(r)["access_token"]
        # Se detecta en tiempo de ejecución, la primera vez que se llama a `enviados()`.
        # Nunca se asume ni se exige: el aprobador puede no haber otorgado Mail.Read.
        self._mail_read_disponible = None

    def buzon(self, n):
        """Devuelve la dirección configurada como `PP_MAIL_BUZON_<n>` (1..4)."""
        clave = f"PP_MAIL_BUZON_{n}"
        if clave not in self._cfg:
            raise KeyError(f"no está configurado {clave} en el archivo de secretos")
        return self._cfg[clave]

    def call(self, metodo, ruta, cuerpo=None, cabeceras=None, timeout=60):
        """Devuelve (estado_http, cuerpo_json). Un error HTTP no lanza excepción:
        vuelve como (código, {"error": mensaje})."""
        h = {
            "Authorization": "Bearer " + self._token,
            "Accept": "application/json",
            "Content-Type": "application/json; charset=utf-8",
        }
        h.update(cabeceras or {})
        ruta = urllib.parse.quote(ruta, safe="/?&=$(),'@._-:")
        req = urllib.request.Request(
            self.api + ruta, method=metodo, headers=h,
            data=json.dumps(cuerpo).encode() if cuerpo is not None else None)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                txt = r.read().decode() or "{}"
                return r.status, (json.loads(txt) if txt.lstrip().startswith(("{", "[")) else {})
        except urllib.error.HTTPError as e:
            txt = e.read().decode()
            try:
                msg = json.loads(txt)["error"]["message"]
            except Exception:
                msg = txt[:1000]
            return e.code, {"error": msg}

    def mandar_correo(self, buzon, para, asunto, cuerpo_texto, adjunto=None, es_html=False):
        """POST /users/{buzon}/sendMail. `para` es una dirección o una lista de
        direcciones. `adjunto`, si se pasa, es `(nombre_archivo, bytes)` — los casos
        de prueba mandan un `.xlsx` — y va en base64 como `fileAttachment`.

        Devuelve (estado_http, cuerpo). Graph contesta 202 sin cuerpo cuando lo aceptó:
        eso NO confirma la entrega, solo que Graph encoló el envío."""
        destinatarios = para if isinstance(para, list) else [para]
        mensaje = {
            "subject": asunto,
            "body": {"contentType": "HTML" if es_html else "Text", "content": cuerpo_texto},
            "toRecipients": [{"emailAddress": {"address": d}} for d in destinatarios],
        }
        if adjunto:
            nombre_archivo, contenido = adjunto
            mensaje["attachments"] = [{
                "@odata.type": "#microsoft.graph.fileAttachment",
                "name": nombre_archivo,
                "contentBytes": base64.b64encode(contenido).decode("ascii"),
            }]
        return self.call("POST", f"users/{buzon}/sendMail",
                         {"message": mensaje, "saveToSentItems": True})

    def enviados(self, buzon, top=10):
        """Lee los últimos mensajes de la carpeta Enviados de `buzon`. Devuelve
        (disponible, estado_http, cuerpo). `disponible=False` cuando Graph contesta
        403: significa que el service principal no tiene `Mail.Read` (o la Application
        Access Policy no lo alcanza para ese buzón) — se detecta acá, nunca se exige
        de antemano."""
        estado, cuerpo = self.call(
            "GET",
            f"users/{buzon}/mailFolders/SentItems/messages"
            "?$top=" + str(top) + "&$select=subject,sentDateTime,toRecipients")
        disponible = estado != 403
        self._mail_read_disponible = disponible
        return disponible, estado, cuerpo


def main():
    if len(sys.argv) < 2:
        sys.exit("uso: graph_api.py enviar <buzon_n> <para> <asunto> <cuerpo> [adjunto]\n"
                 "  o: graph_api.py <GET|POST|PATCH|DELETE> <ruta> [json]")
    g = Graph()
    if sys.argv[1] == "enviar":
        if len(sys.argv) < 6:
            sys.exit("uso: graph_api.py enviar <buzon_n> <para> <asunto> <cuerpo> [adjunto]")
        _, _, n, para, asunto, cuerpo = sys.argv[:6]
        adjunto = None
        if len(sys.argv) > 6:
            ruta_adjunto = sys.argv[6]
            with open(ruta_adjunto, "rb") as f:
                adjunto = (os.path.basename(ruta_adjunto), f.read())
        estado, resp = g.mandar_correo(g.buzon(n), para, asunto, cuerpo, adjunto=adjunto)
        print(estado)
        print(json.dumps(resp, ensure_ascii=False, indent=1))
        return 0 if estado in (200, 202, 204) else 1

    cuerpo = json.loads(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[3] else None
    estado, datos = g.call(sys.argv[1].upper(), sys.argv[2], cuerpo)
    print(estado)
    print(json.dumps(datos, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
