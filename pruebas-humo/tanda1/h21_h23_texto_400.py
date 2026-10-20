"""H-23: texto del 400 cuando el Create trae statecode=1 y statuscode=2 (version y mapeo). No deja filas si falla."""
from hlib import *

dv = adm()
VC = conjuntos(dv)["version"]
est, b, rid = crear(dv, "version-sonda-v95", "version", {"sanic_nombre": "v95", "sanic_capacidad": 60, "sanic_estado": VER_BORRADOR, "statecode": 1, "statuscode": 2, **OWNER}, "H-23 texto")
print("version:", est, err(b)[:500], rid)
host = fila("version-sonda-v92")
est, b, rid = crear(dv, "mapeo-sonda-c", "mapeo", {"sanic_hoja": "HUMO-hoja", "sanic_celda": "Z97", "sanic_tipo": 159460001, "sanic_campo": 159460019, "sanic_nombre": "x", "statecode": 1, "statuscode": 2,
                                                  "sanic_versionplantillaid@odata.bind": f"/{VC}({host})", **OWNER}, "H-23 texto")
print("mapeo:", est, err(b)[:500], rid)
est, b, rid = crear(dv, "mapeo-sonda-d", "mapeo", {"sanic_hoja": "HUMO-hoja", "sanic_celda": "Z96", "sanic_tipo": 159460001, "sanic_campo": 159460019, "sanic_nombre": "x", "statuscode": 2,
                                                  "sanic_versionplantillaid@odata.bind": f"/{VC}({host})", **OWNER}, "H-23 solo statuscode=2")
print("mapeo solo statuscode=2:", est, err(b)[:300], rid, (b or {}).get("statecode") if est == 201 else "")
