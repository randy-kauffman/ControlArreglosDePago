"""Q3: concurrencia optimista con If-Match y un ETag viejo. Se afirma releyendo la fila, nunca por el codigo."""
import json
from dv import Dataverse
dv = Dataverse()
Q = "spk_arreglos"
def leer(i):
    est, b, h = dv.call("GET", f"{Q}({i})?$select=spk_nombre,spk_numeroproducto,versionnumber,modifiedon")
    return est, b
est, b, h = dv.call("POST", Q, {"spk_nombre": "Q3-base", "spk_producto": 928380000, "spk_numeroproducto": "Q3-001"})
i = dv.id_creado(h); print("alta", est, i)
est, r0 = leer(i); etag0 = r0["@odata.etag"]; print("lectura 0: etag", etag0, "| nombre", r0["spk_nombre"], "| versionnumber", r0["versionnumber"])

# Oficial A actualiza sin condicion: el ETag cambia
est, b, _ = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-oficialA"}); print("PATCH A (sin If-Match):", est)
est, r1 = leer(i); etag1 = r1["@odata.etag"]; print("lectura 1: etag", etag1, "| nombre", r1["spk_nombre"])
assert etag1 != etag0

# Oficial B intenta con el ETag viejo (lo que leyo antes que A)
est, b, h = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-oficialB-ETAG-VIEJO"}, cabeceras={"If-Match": etag0})
print("PATCH B con If-Match ETag VIEJO:", est, json.dumps(b)[:400])
est, r2 = leer(i); print("lectura 2 (fila NO cambio):", r2["spk_nombre"], r2["@odata.etag"] == etag1, "versionnumber", r2["versionnumber"])

# Control positivo: con el ETag vigente si pasa
est, b, _ = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-oficialB-ETAG-VIGENTE"}, cabeceras={"If-Match": etag1})
print("PATCH con If-Match VIGENTE:", est)
est, r3 = leer(i); print("lectura 3:", r3["spk_nombre"], "| etag cambio:", r3["@odata.etag"] != etag1)
# Dos 'Tomar' simultaneos con el mismo ETag: el segundo debe fallar
etag3 = r3["@odata.etag"]
e1, _, _ = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-tomar-1"}, cabeceras={"If-Match": etag3})
e2, b2, _ = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-tomar-2"}, cabeceras={"If-Match": etag3})
print("Tomar #1:", e1, "| Tomar #2 con el mismo ETag:", e2, json.dumps(b2)[:200])
est, r4 = leer(i); print("lectura 4:", r4["spk_nombre"])
# If-Match: * no protege (solo existe)
e3, _, _ = dv.call("PATCH", f"{Q}({i})", {"spk_nombre": "Q3-comodin"}, cabeceras={"If-Match": "*"}); print("PATCH If-Match * :", e3)
