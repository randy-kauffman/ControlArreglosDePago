"""V-4, idempotencia: después de que el usuario REENVIÓ (Resubmit) en el historial del flujo V4 (r3) la corrida del correo A, comprueba que NO se duplicó el email."""
from flib import *

dv = adm()
R = Res("v23_idempotencia.json")
e = est()
V = e["v4"]
A = V["A"]
time.sleep(5)
filas = [x for x in get(dv, f"emails?$select=activityid,messageid,createdon&$filter=createdon ge {V['desde']} and directioncode eq false")["value"] if (x.get("messageid") or "").strip("<>").lower() == (A["internetMessageId"] or "").strip("<>").lower()]
R.v("V-4", "idempotencia: tras reenviar la corrida sigue habiendo UN solo email con ese messageid", len(filas) == 1, 1, len(filas))
d, s = esperar_diag(dv, "V4", A["asunto"], maximo=5, c=Correo())
diags = get(dv, "emails?$select=activityid,subject,description,createdon&$orderby=createdon desc&$top=30&$filter=startswith(subject,'SPK-DIAG V4 ')")["value"]
mios = [x for x in diags if A["asunto"] in x["subject"]]
modos = []
for x in mios:
    try:
        modos.append(json.loads(x["description"]).get("modo"))
    except Exception:
        pass
R.v("V-4", "idempotencia: la corrida reenviada quedó como duplicado_no_se_crea", any((m or "").startswith("duplicado") for m in modos), "duplicado_no_se_crea", modos)
