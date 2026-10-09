"""Q5: operadores de fecha relativa sobre una columna DateOnly (spk_cuota.spk_fechavencimiento). Siembra y consulta."""
import json, datetime as dt, urllib.parse
from dv import Dataverse
dv = Dataverse()
hoy_utc = dt.datetime.now(dt.timezone.utc).date()
hoy_local = dt.date.today()
print("fecha UTC del cliente:", hoy_utc, "| fecha local de la maquina:", hoy_local)
# zona horaria del principal que consulta
est, b, _ = dv.call("GET", "usersettingscollection?$select=timezonecode,localeid&$filter=systemuserid eq ffc778df-d287-f111-8075-70a8a5af5a32")
print("usersettings del principal:", b.get("value"))
HOY = hoy_utc
D = lambda n: (HOY + dt.timedelta(days=n)).isoformat()
SEMBRAR = [  # (padre, num, [(nombre cuota, offset dias, pagada)])
    ("Q5-PA", "Q5-001", [("Q5-ayer", -1, False), ("Q5-hace10", -10, False)]),
    ("Q5-PB", "Q5-002", [("Q5-hoy", 0, False)]),
    ("Q5-PC", "Q5-003", [("Q5-manana", 1, False)]),
    ("Q5-PD", "Q5-004", [("Q5-ayer-pagada", -1, True)]),
    ("Q5-PE", "Q5-005", []),
]
for pn, num, cuotas in SEMBRAR:
    est, b, _ = dv.call("GET", f"spk_arreglos?$select=spk_arregloid&$filter=spk_nombre eq '{pn}'")
    if b["value"]:
        pid = b["value"][0]["spk_arregloid"]
    else:
        est, b, h = dv.call("POST", "spk_arreglos", {"spk_nombre": pn, "spk_producto": 928380001, "spk_numeroproducto": num})
        pid = dv.id_creado(h)
        print("alta padre", pn, est)
    for cn, off, pag in cuotas:
        est, b, _ = dv.call("GET", f"spk_cuotas?$select=spk_nombre&$filter=spk_nombre eq '{cn}'")
        if b["value"]:
            continue
        cu = {"spk_nombre": cn, "spk_fechavencimiento": D(off), "spk_ArregloId@odata.bind": f"/spk_arreglos({pid})"}
        if pag:
            cu["spk_fechapago"] = D(off)
        est, b, h = dv.call("POST", "spk_cuotas", cu)
        print("alta cuota", cn, D(off), est, "" if est == 204 else b)
# relectura de lo sembrado
est, b, _ = dv.call("GET", "spk_cuotas?$select=spk_nombre,spk_fechavencimiento,spk_fechapago&$filter=startswith(spk_nombre,'Q5-')&$orderby=spk_fechavencimiento")
print("\nSEMBRADO (releido):")
for r in b["value"]:
    print("  ", r["spk_nombre"].ljust(16), r["spk_fechavencimiento"], r["spk_fechapago"])

def fetch_cuotas(cond_xml, extra_filter=""):
    fx = ("<fetch><entity name='spk_cuota'><attribute name='spk_nombre'/><attribute name='spk_fechavencimiento'/>"
          "<filter type='and'><condition attribute='spk_nombre' operator='like' value='Q5-%'/>" + cond_xml + extra_filter + "</filter>"
          "<order attribute='spk_fechavencimiento'/></entity></fetch>")
    est, b, _ = dv.call("GET", "spk_cuotas?fetchXml=" + urllib.parse.quote(fx, safe=""))
    if est != 200:
        return est, str(b)[:300]
    return est, [r["spk_nombre"] for r in b["value"]]

PRUEBAS = [
    ("olderthan-x-days 0", "<condition attribute='spk_fechavencimiento' operator='olderthan-x-days' value='0'/>"),
    ("olderthan-x-days 1", "<condition attribute='spk_fechavencimiento' operator='olderthan-x-days' value='1'/>"),
    ("olderthan-x-days 2", "<condition attribute='spk_fechavencimiento' operator='olderthan-x-days' value='2'/>"),
    (f"lt '{D(0)}' (hoy literal)", f"<condition attribute='spk_fechavencimiento' operator='lt' value='{D(0)}'/>"),
    (f"le '{D(-1)}' (ayer literal)", f"<condition attribute='spk_fechavencimiento' operator='le' value='{D(-1)}'/>"),
    ("last-x-days 1", "<condition attribute='spk_fechavencimiento' operator='last-x-days' value='1'/>"),
    ("last-x-days 2", "<condition attribute='spk_fechavencimiento' operator='last-x-days' value='2'/>"),
    ("last-x-days 11", "<condition attribute='spk_fechavencimiento' operator='last-x-days' value='11'/>"),
    ("last-seven-days", "<condition attribute='spk_fechavencimiento' operator='last-seven-days'/>"),
    ("yesterday", "<condition attribute='spk_fechavencimiento' operator='yesterday'/>"),
    ("today", "<condition attribute='spk_fechavencimiento' operator='today'/>"),
    ("tomorrow", "<condition attribute='spk_fechavencimiento' operator='tomorrow'/>"),
    ("on-or-before (hoy literal)", f"<condition attribute='spk_fechavencimiento' operator='on-or-before' value='{D(0)}'/>"),
    ("next-x-days 1", "<condition attribute='spk_fechavencimiento' operator='next-x-days' value='1'/>"),
]
print("\nOPERADORES sobre spk_fechavencimiento (DateOnly). Filas: ayer=%s hoy=%s manana=%s hace10=%s" % (D(-1), D(0), D(1), D(-10)))
for etiqueta, cond in PRUEBAS:
    est, r = fetch_cuotas(cond)
    print(f"  {etiqueta.ljust(34)} HTTP {est}  {r}")
print("\nCOMBINADO con 'sin pago' (fechapago null):")
sinpago = "<condition attribute='spk_fechapago' operator='null'/>"
for etiqueta, cond in [PRUEBAS[0], PRUEBAS[1], PRUEBAS[3]]:
    est, r = fetch_cuotas(cond, sinpago)
    print(f"  {etiqueta.ljust(34)} + sin pago -> HTTP {est}  {r}")
