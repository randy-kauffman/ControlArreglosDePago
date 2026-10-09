"""Utilidades comunes del Spike 3 (sin secretos)."""
import base64, json, os, struct, time, zlib
from datetime import datetime, timezone
from dv import Dataverse
from graph import Graph, encabezado

AQUI = os.path.dirname(os.path.abspath(__file__))
EVID = os.path.join(AQUI, "evidencia")
ESTADO = os.path.join(AQUI, "estado.json")
os.makedirs(EVID, exist_ok=True)
COLS_EMAIL = ("activityid,subject,messageid,inreplyto,conversationindex,actualend,senton,createdon,modifiedon,"
              "scheduledend,sortdate,_ownerid_value,_owningbusinessunit_value,_regardingobjectid_value,"
              "statuscode,statecode,sender,torecipients,_emailsender_value,directioncode,correlationmethod,"
              "_parentactivityid_value,_correlatedactivityid_value,correlatedsubjectchanged,_receivingmailboxid_value,"
              "attachmentcount,trackingtoken,delayedemailsendtime,conversationtrackingid,replycount,"
              "_createdby_value,_modifiedby_value,_createdonbehalfby_value,isworkflowcreated,submittedby")
PREFER = {"Prefer": 'odata.include-annotations="OData.Community.Display.V1.FormattedValue,Microsoft.Dynamics.CRM.lookuplogicalname"'}


def cargar():
    try:
        return json.load(open(ESTADO))
    except Exception:
        return {}


def guardar(est):
    json.dump(est, open(ESTADO, "w"), indent=1, ensure_ascii=False)


def utc_iso(t=None):
    return datetime.fromtimestamp(t if t is not None else time.time(), timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse(s):
    return datetime.strptime(s.replace("Z", "+0000")[:19] + "+0000", "%Y-%m-%dT%H:%M:%S%z") if s else None


def delta(a, b):
    """segundos entre dos ISO (b - a)"""
    return int((parse(b) - parse(a)).total_seconds()) if a and b else None


def png_chico(ancho=16, alto=16, rgb=(200, 30, 30)):
    def trozo(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    fila = b"\x00" + bytes(rgb) * ancho
    return (b"\x89PNG\r\n\x1a\n" + trozo(b"IHDR", struct.pack(">IIBBBBB", ancho, alto, 8, 2, 0, 0, 0)) +
            trozo(b"IDAT", zlib.compress(fila * alto)) + trozo(b"IEND", b""))


def pdf_chico(texto="Spike 3 prueba"):
    obj = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
           "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
           None, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
    flujo = f"BT /F1 12 Tf 10 50 Td ({texto}) Tj ET"
    obj[3] = f"<< /Length {len(flujo)} >>\nstream\n{flujo}\nendstream"
    out = b"%PDF-1.4\n"; offs = []
    for i, o in enumerate(obj, 1):
        offs.append(len(out)); out += f"{i} 0 obj\n{o}\nendobj\n".encode()
    x = len(out)
    out += f"xref\n0 {len(obj)+1}\n0000000000 65535 f \n".encode()
    for o in offs:
        out += f"{o:010d} 00000 n \n".encode()
    out += f"trailer\n<< /Size {len(obj)+1} /Root 1 0 R >>\nstartxref\n{x}\n%%EOF".encode()
    return out


def adj_graph(nombre, datos, tipo, inline=False, cid=None):
    a = {"@odata.type": "#microsoft.graph.fileAttachment", "name": nombre, "contentType": tipo,
         "contentBytes": base64.b64encode(datos).decode()}
    if inline:
        a["isInline"] = True; a["contentId"] = cid
    return a


def email_por_messageid(dv, imid, desde_iso, extra=""):
    """Busca en Dataverse los Email creados desde `desde_iso` cuyo messageid coincide (sin distinguir mayusculas)."""
    est, b, _ = dv.call("GET", f"emails?$select={COLS_EMAIL}&$filter=createdon ge {desde_iso}&$orderby=createdon asc{extra}", cabeceras=PREFER)
    if est != 200:
        # reintento con columnas basicas por si alguna columna no existe
        est, b, _ = dv.call("GET", f"emails?$filter=createdon ge {desde_iso}&$orderby=createdon asc")
    v = b.get("value", [])
    return [e for e in v if (e.get("messageid") or "").lower() == (imid or "").lower()]


def volcar(nombre, obj):
    json.dump(obj, open(os.path.join(EVID, nombre), "w"), indent=1, ensure_ascii=False)


def email_completo(dv, eid):
    """Fila completa del Email releida de Dataverse, sin nulos, con anotaciones de lookup."""
    est, b, _ = dv.call("GET", f"emails({eid})", cabeceras=PREFER)
    return {k: v for k, v in b.items() if v is not None and not k.startswith("@odata.") or "@" in k and v is not None}
