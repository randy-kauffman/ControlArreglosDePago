"""Confirma el hallazgo de FLS: el gesto Desbloquear de un Oficial no admin (S-21 escribe null en sanic_instantaneacedula, que el perfil solo deja LEER)."""
from t2lib import *

dv = adm()
u = noadm()
R = Res("41_desbloquear_fls.json")
AR = conjuntos(dv)["arreglo"]
a_gen = fila("arreglo-t2-gen")
t0 = reloj(dv)
antes = leer(dv, "arreglo", a_gen, "sanic_fechacongelamiento,sanic_numeroprovisional")
est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_fechacongelamiento": None})
despues = leer(dv, "arreglo", a_gen, "sanic_fechacongelamiento,sanic_numeroprovisional")
R.v("FLS", "Oficial no admin: Desbloquear un Borrador congelado (S-21 vacia las 4 instantaneas y el provisional)", est == 204, "204 (hallazgo si es 403)",
    (est, codigo(b), err(b)[:300], "congelado antes/despues:", antes["sanic_fechacongelamiento"] is not None, despues["sanic_fechacongelamiento"] is not None))
tr = resumen_trazas(trazas(dv, t0, esperar=90, estable=3))
volcar("41_trazas.json", tr)
mostrar(tr)
fp = get(dv, f"fieldpermissions?$select=attributelogicalname,canread,cancreate,canupdate&$filter=attributelogicalname eq 'sanic_instantaneacedula'")["value"]
print([{k: v for k, v in x.items() if not k.startswith("@")} for x in fp])
