"""Tarea A.2 (pregunta 2c): busqueda por cedula con quick find, con el principal de correo (usuario de aplicacion, NO administrador).
Escenario B: solo rol 'SPK Lector Cliente', fuera del perfil de columna. Escenario A: ademas miembro de 'SPK Perfil Cedula Lectura'.
Autentica como el propio principal (sin suplantacion). Cada resultado sale de releer. Uso: python3 s10_q2c.py [B|A|ambos]"""
import sys, time, urllib.parse
from lib import *
adm = DvAdmin(); est = cargar()
uid = est["usuario_app_id"]
est_p, b, _ = adm.call("GET", "fieldsecurityprofiles?$select=fieldsecurityprofileid,name&$filter=name eq 'SPK Perfil Cedula Lectura'")
pid = [x for x in b["value"] if x["name"] == "SPK Perfil Cedula Lectura"][0]["fieldsecurityprofileid"]
est["perfil_cedula"] = pid

def miembros():
    s, b, _ = adm.call("GET", f"fieldsecurityprofiles({pid})/systemuserprofiles_association?$select=systemuserid")
    assert s == 200, (s, b)
    return [x["systemuserid"] for x in b["value"]]

def en_perfil(valor):
    actual = uid in miembros()
    if valor and not actual:
        s, r, _ = adm.call("POST", f"fieldsecurityprofiles({pid})/systemuserprofiles_association/$ref", {"@odata.id": f"{adm.api}systemusers({uid})"})
        print("  asociar al perfil:", s, "" if s in (200, 204) else r)
    if not valor and actual:
        s, r, _ = adm.call("DELETE", f"fieldsecurityprofiles({pid})/systemuserprofiles_association/$ref?$id={adm.api}systemusers({uid})")
        print("  quitar del perfil:", s, "" if s in (200, 204) else r)
    ahora = uid in miembros()
    assert ahora == valor, f"el perfil no quedo como se pidio (miembro={ahora})"
    print("  RELEER miembro del perfil:", ahora)

def correr(esc):
    evid = {"escenario": esc}
    print(f"=== Escenario {esc} ===")
    en_perfil(esc == "A")
    time.sleep(45)  # cache de seguridad
    dv = DvCorreo()
    s, w, _ = dv.call("GET", "WhoAmI"); print("WhoAmI (principal de correo):", s, w.get("UserId") == uid)
    evid["whoami_ok"] = (w.get("UserId") == uid)
    roles = roles_de_usuario(adm, uid); print("roles (releidos por el administrador):", roles); evid["roles"] = roles
    s, b, _ = dv.call("GET", "spk_clientes?$select=spk_nombre,spk_cedula&$filter=startswith(spk_nombre,'Q2-')&$orderby=spk_nombre")
    filas = [(r["spk_nombre"], r.get("spk_cedula")) for r in b.get("value", [])] if s == 200 else b
    print("1) lectura de filas y columna:", s, filas); evid["lectura"] = [s, filas]
    # cedulas reales leidas como admin, para armar las busquedas
    s2, ba, _ = adm.call("GET", "spk_clientes?$select=spk_nombre,spk_cedula&$filter=startswith(spk_nombre,'Q2-')&$orderby=spk_nombre")
    ced = {r["spk_nombre"]: r["spk_cedula"] for r in ba["value"]}; evid["cedulas_vistas_por_admin"] = ced
    objetivo = ced.get("Q2-beto") or next(iter(ced.values()))
    termino = objetivo[:-1] + "%"
    def qf(term, cols):
        conds = "".join(f"<condition attribute='{c}' operator='like' value='{term}'/>" for c in cols)
        fx = ("<fetch><entity name='spk_cliente'><attribute name='spk_nombre'/><filter type='and'><condition attribute='statecode' operator='eq' value='0'/></filter>"
              f"<filter type='or' isquickfindfields='1'>{conds}</filter></entity></fetch>")
        s, b, _ = dv.call("GET", "spk_clientes?fetchXml=" + urllib.parse.quote(fx, safe=""))
        return s, ([r["spk_nombre"] for r in b["value"]] if s == 200 else b["error"][:300])
    r2 = qf(termino, ["spk_nombre", "spk_cedula"]); print(f"2) quick find (nombre OR cedula) por cedula '{termino}':", r2); evid["quickfind_cedula"] = r2
    r2b = qf(termino, ["spk_cedula"]); print("2b) like solo sobre spk_cedula:", r2b); evid["like_solo_cedula"] = r2b
    r3 = qf("Q2-be%", ["spk_nombre", "spk_cedula"]); print("3) quick find por nombre 'Q2-be%' (control):", r3); evid["quickfind_nombre"] = r3
    s, b, _ = dv.call("GET", f"spk_clientes?$select=spk_nombre&$filter=spk_cedula eq '{objetivo}'")
    r4 = (s, [r["spk_nombre"] for r in b["value"]] if s == 200 else b["error"][:300]); print("4) $filter spk_cedula eq <valor exacto>:", r4); evid["filter_eq_cedula"] = r4
    s, b, _ = dv.call("GET", f"spk_clientes?$select=spk_nombre&$filter=spk_cedula ne '{objetivo}' and startswith(spk_nombre,'Q2-')")
    r5 = (s, [r["spk_nombre"] for r in b["value"]] if s == 200 else b["error"][:300]); print("5) $filter spk_cedula ne <valor> (canal de deduccion):", r5); evid["filter_ne_cedula"] = r5
    return evid

modo = sys.argv[1] if len(sys.argv) > 1 else "ambos"
res = {}
for esc in (["B", "A"] if modo == "ambos" else [modo]):
    res[esc] = correr(esc)
volcar("q2c_resultado.json", {**(json.load(open(os.path.join(EVID, "q2c_resultado.json"))) if os.path.exists(os.path.join(EVID, "q2c_resultado.json")) else {}), **res})
guardar(est)
