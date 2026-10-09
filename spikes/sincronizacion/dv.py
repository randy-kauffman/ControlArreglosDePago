#!/usr/bin/env python3
"""Cliente minimo del Web API de Dataverse para el Spike 2 (sin dependencias).

Reutiliza el mecanismo del estudio (credenciales de cliente leidas de
~/projects/pp_secrets.env, variable SPIKE_ENV para otra ruta). Nunca imprime el
secreto ni el token. Comprueba que DATAVERSE_URL sea el entorno autorizado.
"""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

ENTORNO_AUTORIZADO = "https://org36e60d9d.crm.dynamics.com"
ARCHIVO_ENV = os.environ.get("SPIKE_ENV", os.path.expanduser("~/projects/pp_secrets.env"))
SOLUCION = "SpikesArreglosPago"
SAFE = "/?&=$(),'@._-:*"


class Dataverse:
    def __init__(self, version="v9.2"):
        cfg = {}
        with open(ARCHIVO_ENV, encoding="utf-8") as f:
            for linea in f:
                linea = linea.strip()
                if linea and not linea.startswith("#") and "=" in linea:
                    k, v = linea.split("=", 1)
                    cfg[k.strip()] = v.strip().strip('"').strip("'")
        self.org = cfg["DATAVERSE_URL"].rstrip("/")
        if self.org != ENTORNO_AUTORIZADO:
            raise SystemExit(f"Entorno no autorizado para el spike: {self.org}")
        self.api = f"{self.org}/api/data/{version}/"
        datos = urllib.parse.urlencode({
            "grant_type": "client_credentials",
            "client_id": cfg["PP_CLIENT_ID"],
            "client_secret": cfg["PP_CLIENT_SECRET"],
            "scope": self.org + "/.default",
        }).encode()
        url = f"https://login.microsoftonline.com/{cfg['PP_TENANT_ID']}/oauth2/v2.0/token"
        with urllib.request.urlopen(urllib.request.Request(url, data=datos), timeout=30) as r:
            self._token = json.load(r)["access_token"]

    def call(self, metodo, ruta, cuerpo=None, solucion=None, cabeceras=None, timeout=180):
        """Devuelve (estado, cuerpo_json_o_dict, cabeceras_respuesta). Un HTTP de error no lanza:
        vuelve (codigo, {"error": mensaje}, cabeceras)."""
        h = {
            "Authorization": "Bearer " + self._token,
            "Accept": "application/json",
            "OData-MaxVersion": "4.0",
            "OData-Version": "4.0",
            "Content-Type": "application/json; charset=utf-8",
        }
        if solucion:
            h["MSCRM.SolutionUniqueName"] = solucion
        h.update(cabeceras or {})
        ruta = urllib.parse.quote(ruta, safe=SAFE)
        req = urllib.request.Request(
            self.api + ruta, method=metodo, headers=h,
            data=json.dumps(cuerpo).encode() if cuerpo is not None else None)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                txt = r.read().decode() or "{}"
                return r.status, (json.loads(txt) if txt.lstrip().startswith(("{", "[")) else {"_texto": txt}), dict(r.headers)
        except urllib.error.HTTPError as e:
            txt = e.read().decode()
            try:
                msg = json.loads(txt)["error"]["message"]
            except Exception:
                msg = txt[:1500]
            return e.code, {"error": msg}, dict(e.headers)

    @staticmethod
    def id_creado(cabeceras):
        return cabeceras.get("OData-EntityId", "").split("(")[-1].rstrip(")")


def etiqueta(texto, lcid=1033):
    return {"@odata.type": "Microsoft.Dynamics.CRM.Label",
            "LocalizedLabels": [{"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel",
                                 "Label": texto, "LanguageCode": lcid}]}


def mostrar(titulo, estado, cuerpo, limite=1500):
    txt = json.dumps(cuerpo, ensure_ascii=False)
    print(f"[{titulo}] HTTP {estado} {txt[:limite]}")


if __name__ == "__main__":
    dv = Dataverse()
    est, cuerpo, _ = dv.call(sys.argv[1].upper(), sys.argv[2],
                             json.loads(sys.argv[3]) if len(sys.argv) > 3 else None)
    print(est)
    print(json.dumps(cuerpo, ensure_ascii=False, indent=1))
