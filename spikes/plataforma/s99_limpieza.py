"""LIMPIEZA del Spike 2 (NO se ejecuta sola). Borra SOLO lo que creo el spike, localizado por nombre exacto del spike
y comprobando antes que pertenece a la solucion SpikesArreglosPago. Uso:  python3 s99_limpieza.py --si-borrar
Orden: tablas (hija primero; se llevan sus columnas, claves, relacion, vistas y filas) -> perfil de columna -> rol -> solucion -> publisher.
Cada tabla tarda del orden de 40 s en borrarse; el script espera y relee."""
import sys, time
from dv import Dataverse, SOLUCION
if "--si-borrar" not in sys.argv:
    sys.exit("Dry run. Agregar --si-borrar para borrar de verdad.")
dv = Dataverse()
est, b, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")
sid = b["value"][0]["solutionid"]
est, b, _ = dv.call("GET", f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {sid}")
en_solucion = {c["objectid"] for c in b["value"]}
def borrar_tabla(logica):
    est, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{logica}')?$select=MetadataId")
    if est == 404:
        print(logica, "ya no existe"); return
    if e["MetadataId"] not in en_solucion:
        sys.exit(f"{logica} NO esta en la solucion: no se borra")
    est, r, _ = dv.call("DELETE", f"EntityDefinitions(LogicalName='{logica}')", timeout=600)
    print("DELETE tabla", logica, est, "" if est in (200, 204) else r)
    for _ in range(30):
        est, _, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{logica}')?$select=MetadataId")
        if est == 404:
            print("  releido: ya no existe"); return
        time.sleep(10)
for t in ("spk_cuota", "spk_arreglo", "spk_cliente", "spk_conversacion"):
    borrar_tabla(t)
for entidad, nombre_col, nombre in [("fieldsecurityprofiles", "fieldsecurityprofileid", "SPK Perfil Cedula Lectura"), ("roles", "roleid", "SPK Lector Cliente")]:
    est, b, _ = dv.call("GET", f"{entidad}?$select={nombre_col}&$filter=name eq '{nombre}'")
    for x in b.get("value", []):
        if x[nombre_col] in en_solucion:
            print("DELETE", entidad, nombre, dv.call("DELETE", f"{entidad}({x[nombre_col]})")[0])
est, _, _ = dv.call("DELETE", f"solutions({sid})"); print("DELETE solucion", est)
est, b, _ = dv.call("GET", "publishers?$select=publisherid&$filter=uniquename eq 'spikesarreglospago'")
for p in b.get("value", []):
    print("DELETE publisher", dv.call("DELETE", f"publishers({p['publisherid']})")[0])
