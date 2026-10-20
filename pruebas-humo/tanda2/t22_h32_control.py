"""H-32, evidencia complementaria (solo lecturas y llamadas que rechazan):
 (1) el codigo de falla de plataforma de un Oficial que lee una tabla sin privilegio (email: el rol no trae prvReadEmail) => 0x80040220 por Web API.
 (2) GenerarFormato con un arregloid inexistente: el Retrieve falla (0x80040217) dentro del plugin, LeerOMenos lo atrapa y el cliente ve CARP-GEN-002 (no GEN-900)."""
import uuid
from t2lib import *

dv = adm()
u = noadm()
R = Res("22_h32_control.json")
API = "sanic_carp_capi_generarformato"
em = get(dv, "emails?$select=activityid&$top=1")["value"]
if em:
    est, b, _ = u.call("GET", f"emails({em[0]['activityid']})?$select=subject")
    R.v("H-32", "(1) Oficial lee un email por id (sin prvReadEmail): codigo de la falla de plataforma", est >= 400, "4xx con 0x80040220", (est, err(b)[:400]))
    est, b, _ = u.call("GET", "emails?$select=activityid&$top=1")
    R.v("H-32", "(1b) Oficial lista emails (RetrieveMultiple): cuantos ve", True, "medicion", (est, err(b)[:200] if est != 200 else len(b.get("value", []))))
t1 = reloj(dv)
est, b, seg = llamar(u, API, {"arregloid": str(uuid.uuid4())})
R.v("H-32", "(2) GenerarFormato con un arregloid inexistente: el Retrieve falla en el plugin y se atrapa => CARP-GEN-002", est >= 400 and codigo(b) == "CARP-GEN-002", "4xx CARP-GEN-002", (est, codigo(b), err(b)[:200], seg))
tr = resumen_trazas(trazas(dv, t1, esperar=90, estable=3))
volcar("22_trazas.json", tr)
mostrar(tr)
