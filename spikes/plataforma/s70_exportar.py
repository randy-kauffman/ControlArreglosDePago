"""Verificacion independiente: ExportSolution (unmanaged) y lectura del zip. Guarda el zip en el directorio de trabajo temporal (no en el proyecto)."""
import base64, sys, os
from dv import Dataverse, SOLUCION
dv = Dataverse()
est, b, _ = dv.call("POST", "ExportSolution", {"SolutionName": SOLUCION, "Managed": False}, timeout=300)
print("ExportSolution HTTP", est)
if est != 200:
    print(str(b)[:500]); sys.exit(1)
destino = sys.argv[1]
os.makedirs(destino, exist_ok=True)
open(os.path.join(destino, "SpikesArreglosPago.zip"), "wb").write(base64.b64decode(b["ExportSolutionFile"]))
print("guardado en", destino)
