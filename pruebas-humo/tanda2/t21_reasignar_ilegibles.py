"""H-32: vuelve ILEGIBLES para SPIKE4 (rol Oficial, lectura Local BU) las filas cliente-t2-adm y arreglo-t2-h32arr reasignandolas a un usuario de la BU raiz
(Frederick Alguera DEV, sin roles, nunca se toca el usuario). Solo datos HUMO. Verifica con una lectura directa de SPIKE4."""
from t2lib import *

dv = adm()
u = noadm()
R = Res("21_reasignar_ilegibles.json")
FRED = "42c56114-b3f3-f011-8407-000d3a316609"
for clave, t in (("cliente-t2-adm", "cliente"), ("arreglo-t2-h32arr", "arreglo")):
    conj = conjuntos(dv)[t]
    rid = fila(clave)
    est, b, _ = dv.call("PATCH", f"{conj}({rid})", {"ownerid@odata.bind": f"/systemusers({FRED})"})
    R.v("H-32", f"reasignar {clave} a un usuario de la BU raiz", est == 204, 204, (est, codigo(b), err(b)[:240]))
    r = get(dv, f"{conj}({rid})?$select=_ownerid_value,_owningbusinessunit_value")
    print("  dueno ahora:", r.get("_ownerid_value"), "BU", r.get("_owningbusinessunit_value"))
    est, b, _ = u.call("GET", f"{conj}({rid})?$select=statecode")
    R.v("H-32", f"SPIKE4 lee {clave} directamente", True, "medicion (se busca 4xx)", (est, err(b)[:300]))
