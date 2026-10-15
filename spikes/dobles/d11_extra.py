"""Dobles: MEDIR complementos desde afuera (modo extra). Lanza la consola compilada (src/Dbl.Medidor) con los secretos por variables de entorno del PROCESO.
El hijo crea filas ZZF-, las registra en estado.json antes/al nacer, escribe salida/*.json y las borra en su finally.
Este lanzador, ademas, corre SIEMPRE la red de seguridad (d90) si quedara algo vivo. El hijo corre con TZ=America/Managua (UTC-6) para que Kind=Local sea distinto de UTC."""
import sys
from dlib import *

st = leer_estado()
if "linea_base" not in st:
    sys.exit("DETENIDO: falta la linea base (d00_linea_base.py).")
if not os.path.exists(DLL):
    sys.exit("DETENIDO: falta compilar src/Dbl.Medidor")

rc, out, err = correr_hijo("extra", extra={"TZ": "America/Managua"})
print(out)
if err.strip():
    print("STDERR:", err[:2000])
with open(os.path.join(EVID, "11_extra.log"), "w", encoding="utf-8") as f:
    f.write(out + ("\n--- STDERR ---\n" + err if err.strip() else ""))
print("codigo de salida del hijo:", rc)
sys.exit(rc)
