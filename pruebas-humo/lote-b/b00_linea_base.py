"""Linea base de solo lectura: versiones de plantilla, Vigente, arreglos y conversaciones HUMO de las tandas 1 y 2."""
from blib import *

dv = adm()
VC, AR, CV = conjuntos(dv)["version"], conjuntos(dv)["arreglo"], conjuntos(dv)["conversacion"]
out = {}
vs = get(dv, f"{VC}?$select=sanic_nombre,sanic_estado,statecode,sanic_archivo_name&$orderby=createdon asc&$top=50")["value"]
out["versiones"] = [{k: v for k, v in x.items() if not k.startswith("@")} for x in vs]
print(json.dumps(out["versiones"], indent=0, ensure_ascii=False))
for clave in ("conv-01", "conv-aux-unida", "conv-04-poblada", "conv-777777"):
    print(clave, snap(dv, "conversacion", fila_de("tanda1", clave), "statecode,statuscode,_sanic_oficialasignadoid_value,sanic_correonuevoenfinalizada,sanic_resultado,_sanic_unidaaid_value"))
for clave in ("arreglo-t2-gen", "arreglo-t2-h8", "arreglo-t2-h19"):
    print(clave, snap(dv, "arreglo", fila_de("tanda2", clave), "statecode,statuscode,sanic_fechacongelamiento,sanic_cantidadreaperturas,sanic_numeroarreglo"))
print("yo", get(dv, "WhoAmI")["UserId"])
volcar("00_linea_base.json", out)
