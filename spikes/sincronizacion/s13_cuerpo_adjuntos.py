"""Caso 6: compara byte a byte (sha256) el cuerpo del activitymimeattachment en Dataverse con lo enviado por Graph."""
import hashlib
from lib import *
dv = Dataverse(); est = cargar()
esperado = {"comprobante.pdf": pdf_chico("Spike 3 comprobante"), "foto.png": png_chico(), "logo.png": png_chico(16, 16, (30, 30, 200))}
for k in ("caso6a", "caso6b"):
    s, b, _ = dv.call("GET", f"activitymimeattachments?$filter=_objectid_value eq {est[k]['dv_email_id']}&$select=filename,body,filesize")
    for a in b["value"]:
        raw = base64.b64decode(a["body"])
        ok = hashlib.sha256(raw).hexdigest() == hashlib.sha256(esperado[a["filename"]]).hexdigest()
        print(k, a["filename"], "filesize", a["filesize"], "bytes leidos", len(raw), "sha256 igual al enviado:", ok)
