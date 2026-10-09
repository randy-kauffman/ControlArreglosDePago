"""Utilidades del spike 1b (sandbox). Reutiliza el cliente de ../plataforma/dv.py. Nunca imprime secretos ni tokens."""
import os, sys, json, time
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "plataforma"))
from dv import Dataverse, etiqueta, mostrar, SOLUCION  # noqa: E402
from spk_lib import escribir, col_texto, REQ_NONE, REQ_APP, AUD_OFF, T  # noqa: E402

TABLA = "spk_versionplantilla"
CONJUNTO = "spk_versionplantillas"
PAQUETE_UNICO = "spk_ArreglosPagoSandbox"
API = "spk_GenerarFormatoSpike"
PLANTILLA = os.path.join(AQUI, "..", "plantilla", "F-NIC-0000052_v07_borrador.xlsx")

import hashlib, urllib.request, urllib.parse, urllib.error
ESTADO = os.path.join(AQUI, "estado_sandbox.json")  # solo GUIDs y huellas, sin secretos


def leer_estado():
    try:
        with open(ESTADO, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return {}


def guardar_estado(d):
    with open(ESTADO, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=1, ensure_ascii=False)


def sha256(b):
    return hashlib.sha256(b).hexdigest()  # minuscula, igual que GeneradorFormato.CalcularHuella


def raw(dv, metodo, ruta, datos=None, cabeceras=None, timeout=300):
    """Llamada con cuerpo binario o respuesta binaria. Devuelve (estado, bytes, cabeceras)."""
    h = {"Authorization": "Bearer " + dv._token, "OData-MaxVersion": "4.0", "OData-Version": "4.0", "Accept": "application/json"}
    h.update(cabeceras or {})
    req = urllib.request.Request(dv.api + urllib.parse.quote(ruta, safe="/?&=$(),'@._-:*"), method=metodo, headers=h, data=datos)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)
