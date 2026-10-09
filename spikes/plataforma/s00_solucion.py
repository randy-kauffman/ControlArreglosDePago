"""Paso 0: publisher propio (spk) y solucion no administrada SpikesArreglosPago. Idempotente."""
from dv import Dataverse, mostrar, SOLUCION

dv = Dataverse()
est, b, _ = dv.call("GET", "publishers?$select=publisherid,uniquename,customizationprefix,customizationoptionvalueprefix&$filter=uniquename eq 'spikesarreglospago'")
if not b.get("value"):
    # No se manda customizationoptionvalueprefix: lo asigna el sistema.
    est, b, h = dv.call("POST", "publishers", {
        "uniquename": "spikesarreglospago", "friendlyname": "Spikes Arreglos de Pago",
        "customizationprefix": "spk",
        "description": "Publisher descartable del Spike 2 del proyecto 2026-003"})
    mostrar("POST publishers", est, b)
est, b, _ = dv.call("GET", "publishers?$select=publisherid,uniquename,customizationprefix,customizationoptionvalueprefix&$filter=uniquename eq 'spikesarreglospago'")
mostrar("RELEER publisher", est, b)
pub = b["value"][0]["publisherid"]

est, b, _ = dv.call("GET", f"solutions?$select=solutionid,uniquename,ismanaged&$filter=uniquename eq '{SOLUCION}'")
if not b.get("value"):
    est, b, h = dv.call("POST", "solutions", {
        "uniquename": SOLUCION, "friendlyname": "Spikes Arreglos de Pago", "version": "1.0.0.0",
        "description": "Spike 2 del proyecto 2026-003. Descartable.",
        "publisherid@odata.bind": f"/publishers({pub})"})
    mostrar("POST solutions", est, b)
est, b, _ = dv.call("GET", f"solutions?$select=solutionid,uniquename,ismanaged,version&$expand=publisherid($select=uniquename,customizationprefix,customizationoptionvalueprefix)&$filter=uniquename eq '{SOLUCION}'")
mostrar("RELEER solucion", est, b)
