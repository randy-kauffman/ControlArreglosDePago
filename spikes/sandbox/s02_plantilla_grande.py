"""Fila extra para el limite de tamano: la v07 mas una parte NO referenciada de 4,5 MB incompresibles (xl/media/relleno.bin).
Sirve para ver que pasa con salidas Base64 de varios MB (la columna File del spike admite hasta 5 MB). Idempotente."""
import zipfile, io, random
from sbx_lib import *
dv = Dataverse(); e = leer_estado()
os.makedirs(os.path.join(AQUI, "salida"), exist_ok=True)
origen = zipfile.ZipFile(PLANTILLA)
buf = io.BytesIO()
with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
    for i in origen.infolist():
        datos = origen.read(i.filename)
        if i.filename == "[Content_Types].xml":
            txt = datos.decode("utf-8")
            if 'Extension="bin"' not in txt:
                txt = txt.replace("<Default ", '<Default Extension="bin" ContentType="application/octet-stream"/><Default ', 1)
            datos = txt.encode("utf-8")
        z.writestr(i, datos)
    z.writestr("xl/media/relleno.bin", random.Random(42).randbytes(4_500_000))
grande = buf.getvalue()
open(os.path.join(AQUI, "salida", "plantilla-grande.xlsx"), "wb").write(grande)
nombre = "plantilla grande (4,5 MB de relleno)"
est, b, _ = dv.call("GET", f"{CONJUNTO}?$select=spk_versionplantillaid&$filter=spk_nombre eq '{nombre}'")
if b["value"]:
    rid = b["value"][0]["spk_versionplantillaid"]; dv.call("PATCH", f"{CONJUNTO}({rid})", {"spk_huella": sha256(grande)})
else:
    est, b, h = dv.call("POST", CONJUNTO, {"spk_nombre": nombre, "spk_huella": sha256(grande)}, solucion=SOLUCION)
    assert est in (201, 204); rid = dv.id_creado(h)
est, _, _ = raw(dv, "PATCH", f"{CONJUNTO}({rid})/spk_archivo", grande, {"Content-Type": "application/octet-stream", "x-ms-file-name": "plantilla_grande.xlsx"})
assert est in (200, 204), est
est, cont, _ = raw(dv, "GET", f"{CONJUNTO}({rid})/spk_archivo/$value")
assert est == 200 and cont == grande
e["filas"][nombre] = rid; guardar_estado(e)
print("OK", nombre, rid, len(grande), "bytes; releido igual; sha256", sha256(grande)[:16])
