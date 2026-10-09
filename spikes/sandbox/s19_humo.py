"""Llamada de humo: una sola llamada, imprime todo salvo el Base64 (guarda el archivo para inspeccion)."""
import sys, time, base64
from sbx_lib import *
dv = Dataverse(); e = leer_estado()
plazo = int(sys.argv[1]) if len(sys.argv) > 1 else 60
fila = sys.argv[2] if len(sys.argv) > 2 else "v07 borrador (huella correcta)"
t0 = time.perf_counter()
est, b, _ = dv.call("POST", API, {"VersionId": e["filas"][fila], "Plazo": plazo})
ms = (time.perf_counter() - t0) * 1000
print("HTTP", est, f"{ms:.0f} ms totales")
if est == 200:
    print({k: (v if k != "ArchivoBase64" else f"<{len(v)} chars>") for k, v in b.items() if not k.startswith("@")})
    os.makedirs(os.path.join(AQUI, "salida"), exist_ok=True)
    open(os.path.join(AQUI, "salida", f"humo-{plazo}.xlsx"), "wb").write(base64.b64decode(b["ArchivoBase64"]))
else:
    print(b)
