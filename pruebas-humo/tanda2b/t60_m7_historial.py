"""M7 (diseno/07 §12 punto 16, DA-21): el Oficial NO admin (SPIKE4 aun habilitado, fuera de Jefatura) pide el historial de auditoria de un arreglo HUMO.
Solo mide; no cambia roles. El primer intento de t50 uso un parametro mal codificado (400 de sintaxis); este lo repite bien."""
from t2blib import *

dv = adm()
u = noadm()
AR = conjuntos(dv)["arreglo"]
a = fila2("arreglo-t2-gen")
UID = leer_estado()["linea_base"]["usuario"]["systemuserid"]
assert UID not in miembros_team(dv, leer_estado()["linea_base"]["jefatura"]["teamid"]), "SPIKE4 no deberia estar en Jefatura"
out = {}
ruta = 'RetrieveRecordChangeHistory(Target=@t)?@t={"@odata.id":"' + f"{AR}({a})" + '"}'
for quien, cli in (("oficial", u), ("admin", dv)):
    est, b, _ = cli.call("GET", ruta)
    det = ((b.get("AuditDetailCollection") or {}).get("AuditDetails")) if est == 200 else None
    out[quien] = {"RetrieveRecordChangeHistory": (est, err(b)[:200] if est != 200 else {"cambios": len(det or [])})}
    est, b, _ = cli.call("GET", f"audits?$select=auditid,operation&$filter=_objectid_value eq {a}&$top=20")
    out[quien]["audits"] = (est, err(b)[:140] if est != 200 else {"filas": len(b.get("value", []))})
    est, b, _ = cli.call("GET", f"{AR}({a})?$select=sanic_nombre")
    out[quien]["lee_el_arreglo"] = est
print(json.dumps(out, indent=1, default=str))
volcar("60_m7_historial.json", out)
