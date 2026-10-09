"""Q2c: busqueda por la cedula (columna segura) con el filtro de quick find, como System Administrator; y quien podria probar sin ese rol."""
import json, urllib.parse
from dv import Dataverse
dv = Dataverse()
for nom, ced, tel in [("Q2-ana", "001-000000-0001A", "8888-0001"), ("Q2-beto", "001-000000-0002B", "8888-0002")]:
    est, b, _ = dv.call("GET", f"spk_clientes?$select=spk_nombre&$filter=spk_nombre eq '{nom}'")
    if not b["value"]:
        est, b, h = dv.call("POST", "spk_clientes", {"spk_nombre": nom, "spk_cedula": ced, "spk_telefono": tel}); print("alta", nom, est)
# lectura de la fila con la cedula (como sysadmin)
est, b, _ = dv.call("GET", "spk_clientes?$select=spk_nombre,spk_cedula&$filter=startswith(spk_nombre,'Q2-')&$orderby=spk_nombre")
print("RELEER (sysadmin ve la columna segura):", [(r["spk_nombre"], r.get("spk_cedula")) for r in b["value"]])
def qf(term):
    fx = ("<fetch><entity name='spk_cliente'><attribute name='spk_nombre'/><filter type='and'><condition attribute='statecode' operator='eq' value='0'/></filter>"
          f"<filter type='or' isquickfindfields='1'><condition attribute='spk_nombre' operator='like' value='{term}'/><condition attribute='spk_cedula' operator='like' value='{term}'/></filter></entity></fetch>")
    est, b, _ = dv.call("GET", "spk_clientes?fetchXml=" + urllib.parse.quote(fx, safe=""))
    return est, [r["spk_nombre"] for r in b["value"]] if est == 200 else str(b)[:300]
print("filtro de quick find, termino '001-000000-0002%' :", qf("001-000000-0002%"))
print("filtro de quick find, termino '%0001A' :", qf("%0001A"))
# Quien tiene Read sobre spk_cliente? (roles con el privilegio prvReadspk_cliente)
est, b, _ = dv.call("GET", "privileges?$select=privilegeid,name&$filter=name eq 'prvReadspk_cliente'")
pid = b["value"][0]["privilegeid"]
est, b, _ = dv.call("GET", f"roleprivilegescollection?$select=roleid,privilegedepthmask&$filter=privilegeid eq {pid}")
roles = []
for rp in b["value"]:
    est, r, _ = dv.call("GET", f"roles({rp['roleid']})?$select=name,ismanaged"); roles.append((r.get("name"), rp["privilegedepthmask"]))
print("roles con Read sobre spk_cliente:", roles)
