"""Fixtures: EXPORTAR. Lanza el exportador compilado (spikes/fixtures/src/Zzf.Exportador) con los secretos por variables de entorno del PROCESO.
El hijo crea filas ZZF-, las registra en estado.json al nacer, escribe salida/*.json y las borra en su finally.
Este lanzador, ademas, ejecuta SIEMPRE f90 (red de seguridad) si quedara algo en estado.json."""
import sys
from flib import *

st = leer_estado()
if "linea_base" not in st:
    sys.exit("DETENIDO: falta la linea base (f00_linea_base.py).")
if st.get("filas"):
    sys.exit("DETENIDO: estado.json tiene filas pendientes; correr f90_limpieza.py y f91_verificar_limpieza.py.")
if not os.path.exists(DLL):
    sys.exit("DETENIDO: falta compilar src/Zzf.Exportador")

rc, out, err = correr_hijo("exportar")
print(out)
if err.strip():
    print("STDERR:", err[:2000])
with open(os.path.join(EVID, "10_exportar.log"), "w", encoding="utf-8") as f:
    f.write(out + ("\n--- STDERR ---\n" + err if err.strip() else ""))
print("codigo de salida del hijo:", rc)
sys.exit(rc)
