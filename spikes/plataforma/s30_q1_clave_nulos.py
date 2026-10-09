"""Q1: clave alterna (producto, numero de producto, numero de arreglo) con numero de arreglo vacio. Cada afirmacion se hace releyendo."""
import json
from dv import Dataverse, mostrar
dv = Dataverse()
PREST, TARJ = 928380000, 928380001
Q = "spk_arreglos"

def alta(nombre, prod, num, arr):
    cuerpo = {"spk_nombre": nombre, "spk_producto": prod, "spk_numeroproducto": num}
    if arr is not None:
        cuerpo["spk_numeroarreglo"] = arr
    est, b, h = dv.call("POST", Q, cuerpo)
    return est, b, dv.id_creado(h)

def leer_todo():
    est, b, _ = dv.call("GET", f"{Q}?$select=spk_nombre,spk_producto,spk_numeroproducto,spk_numeroarreglo&$filter=startswith(spk_nombre,'Q1-')&$orderby=spk_nombre")
    return b["value"]

# 0) estado de la clave (releido)
est, k, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_arreglo')/Keys(LogicalName='spk_clave_arreglo')?$select=EntityKeyIndexStatus,KeyAttributes")
print("clave:", k["EntityKeyIndexStatus"], k["KeyAttributes"])
assert k["EntityKeyIndexStatus"] == "Active"

pasos = [
    ("A1 borrador (Prestamo, P-001, vacio)", ("Q1-A1", PREST, "P-001", None)),
    ("A2 OTRO borrador igual (Prestamo, P-001, vacio)", ("Q1-A2", PREST, "P-001", None)),
    ("A3 OTRO borrador igual (tercero)", ("Q1-A3", PREST, "P-001", None)),
    ("B1 formalizado nro 1 (Prestamo, P-001, 1)", ("Q1-B1", PREST, "P-001", 1)),
    ("B2 DUPLICADO con numero informado (Prestamo, P-001, 1)", ("Q1-B2dup", PREST, "P-001", 1)),
    ("C1 otro producto, mismo numero, vacio (Tarjeta, P-001, vacio)", ("Q1-C1", TARJ, "P-001", None)),
    ("C2 otro producto, mismo numero, 1 (Tarjeta, P-001, 1)", ("Q1-C2", TARJ, "P-001", 1)),
]
ids = {}
for desc, args in pasos:
    est, b, i = alta(*args)
    ids[args[0]] = i
    print(f"[{desc}] HTTP {est}", ("id=" + i) if est == 204 else json.dumps(b)[:700])

print("\nRELECTURA de las filas Q1-*:")
for r in leer_todo():
    print("  ", r["spk_nombre"].ljust(10), r.get("spk_producto"), r.get("spk_numeroproducto"), r.get("spk_numeroarreglo"))

# formalizar un borrador: PATCH numero de arreglo vacio -> 2 (flujo real), y -> 1 (choca con B1)
print("\nFormalizar A1 con numero 2 (libre):")
est, b, _ = dv.call("PATCH", f"{Q}({ids['Q1-A1']})", {"spk_numeroarreglo": 2}); print("  HTTP", est, json.dumps(b)[:300])
print("Formalizar A2 con numero 1 (ya usado por B1):")
est, b, _ = dv.call("PATCH", f"{Q}({ids['Q1-A2']})", {"spk_numeroarreglo": 1}); print("  HTTP", est, json.dumps(b)[:700])
print("Formalizar A2 con numero 3 (libre):")
est, b, _ = dv.call("PATCH", f"{Q}({ids['Q1-A2']})", {"spk_numeroarreglo": 3}); print("  HTTP", est)

# acceso por clave alterna (con el numero informado)
ruta = f"{Q}(spk_producto={PREST},spk_numeroproducto='P-001',spk_numeroarreglo=1)?$select=spk_nombre"
est, b, _ = dv.call("GET", ruta); print("\nGET por clave alterna (Prestamo,P-001,1):", est, b.get("spk_nombre"))
print("\nRELECTURA final:")
for r in leer_todo():
    print("  ", r["spk_nombre"].ljust(10), r.get("spk_producto"), r.get("spk_numeroproducto"), r.get("spk_numeroarreglo"))
json.dump(ids, open("/tmp/claude-1000/-home-gmaker-projects-ControlArreglosDePago/2a1c9540-94de-4279-b899-1e8a4eb38d2a/scratchpad/q1_ids.json", "w"))
