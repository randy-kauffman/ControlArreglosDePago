"""Lote 0 (2026-10-20), inventario 13.7: mueve a `# User 8` (Oficial) y `# User 9` (Jefe) a la BU `Arreglos de Pago`.
Decisión del usuario del 2026-10-20 ("User 8 y User 9 podemos ocuparlos"). Idempotente. Nunca imprime secretos ni tokens.

Sigue la matriz 04 §1.2: los roles llegan por los teams, no se asignan directo al usuario.
- El rol Oficial llega por el team del sistema de la BU; la plataforma agrega al usuario al cambiar de BU.
- El rol Jefe llega por el team `Arreglos de Pago - Jefatura`; acá se agrega a User 9 con AddMembersTeam.
- El perfil `CSP - CARP - Datos sensibles` llega por los dos teams (13.8).
Cambiar de BU le quita al usuario los roles de la BU anterior (DoNotRemoveRolesOnChangeBusinessUnit = false por defecto).
Por eso la línea base se guarda una sola vez, antes del primer cambio, en evidencia/04_usuarios_linea_base.json.
No modifica roles, no borra nada.
Uso: l03_usuarios.py [--solo-verificar]"""
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse  # noqa: E402

EVID = os.path.join(os.path.dirname(AQUI), "evidencia")
BASE = os.path.join(EVID, "04_usuarios_linea_base.json")
SALIDA = os.path.join(EVID, "05_usuarios_verificacion.json")
USUARIOS = {"oficial": "User8@55xljh.onmicrosoft.com", "jefe": "User9@55xljh.onmicrosoft.com"}
SOLO_VERIFICAR = "--solo-verificar" in sys.argv
ARREGLO_HUMO = "0d68be71-aecc-f111-a05b-00224808ffcd"  # arreglo-l0-v07b (estado-plantilla.json)

dv = Dataverse()
assert dv.org == "https://org36e60d9d.crm.dynamics.com", "entorno no autorizado"


def get(ruta, cab=None):
    est, b, _ = dv.call("GET", ruta, cabeceras=cab)
    assert est == 200, (ruta, est, b)
    return b


bu = get("businessunits?$select=businessunitid&$filter=name eq 'Arreglos de Pago'")["value"][0]["businessunitid"]
team_bu = get(f"teams?$select=teamid&$filter=isdefault eq true and _businessunitid_value eq {bu}")["value"][0]["teamid"]
jef = get(f"teams?$select=teamid&$filter=name eq 'Arreglos de Pago - Jefatura' and _businessunitid_value eq {bu}")["value"][0]["teamid"]
perfil = get("fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter=name eq 'CSP - CARP - Datos sensibles'")["value"][0]["fieldsecurityprofileid"]


def foto(uid):
    u = get(f"systemusers({uid})?$select=fullname,domainname,isdisabled,islicensed,accessmode,_businessunitid_value"
            "&$expand=systemuserroles_association($select=name,_businessunitid_value),teammembership_association($select=name,isdefault)")
    return {"systemuserid": uid, "fullname": u["fullname"], "domainname": u["domainname"], "isdisabled": u["isdisabled"],
            "islicensed": u["islicensed"], "accessmode": u["accessmode"], "businessunitid": u["_businessunitid_value"],
            "roles_directos": sorted((r["name"], r["_businessunitid_value"]) for r in u["systemuserroles_association"]),
            "teams": sorted(t["teamid"] for t in u["teammembership_association"]),
            "teams_nombres": sorted(t["name"] for t in u["teammembership_association"])}


ids = {}
for papel, upn in USUARIOS.items():
    r = get(f"systemusers?$select=systemuserid&$filter=domainname eq '{upn}'")["value"]
    assert len(r) == 1, (papel, len(r))
    ids[papel] = r[0]["systemuserid"]

if not os.path.exists(BASE):
    with open(BASE, "w", encoding="utf-8") as f:
        json.dump({"fecha": "2026-10-20", "nota": "estado previo a mover los usuarios a la BU Arreglos de Pago",
                   "usuarios": {p: foto(i) for p, i in ids.items()}}, f, ensure_ascii=False, indent=1)
    print("línea base guardada")

acciones = []
if not SOLO_VERIFICAR:
    for papel, uid in ids.items():
        if foto(uid)["businessunitid"] != bu:
            est, b, _ = dv.call("PATCH", f"systemusers({uid})", {"businessunitid@odata.bind": f"/businessunits({bu})"})
            acciones.append({"usuario": papel, "accion": "mover a la BU", "http": est, "error": b.get("error") if est >= 300 else None})
            print(papel, "mover a la BU:", est, "" if est < 300 else b)
    if jef not in foto(ids["jefe"])["teams"]:
        est, b, _ = dv.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.AddMembersTeam",
                            {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": ids["jefe"]}]})
        acciones.append({"usuario": "jefe", "accion": "agregar a Jefatura", "http": est, "error": b.get("error") if est >= 300 else None})
        print("jefe agregar a Jefatura:", est, "" if est < 300 else b)

# Verificación
pf = get(f"fieldsecurityprofiles({perfil})?$expand=teamprofiles_association($select=teamid)")
teams_perfil = {t["teamid"] for t in pf["teamprofiles_association"]}
ver = {"bu": bu, "team_del_sistema": team_bu, "jefatura": jef, "perfil_en_teams": {"team_del_sistema": team_bu in teams_perfil, "jefatura": jef in teams_perfil},
       "acciones": acciones, "usuarios": {}}
ok = ver["perfil_en_teams"]["team_del_sistema"] and ver["perfil_en_teams"]["jefatura"]
for papel, uid in ids.items():
    f = foto(uid)
    who = dv.call("GET", "WhoAmI", cabeceras={"MSCRMCallerID": uid})
    # lectura con el perfil de columnas: la cuenta a debitar (columna protegida) del arreglo HUMO del lote 0
    est_c, b_c, _ = dv.call("GET", f"sanic_carp_tbl_arreglos({ARREGLO_HUMO})?$select=sanic_cuentadebitar", cabeceras={"MSCRMCallerID": uid})
    chk = {"en_bu": f["businessunitid"] == bu, "habilitado": not f["isdisabled"], "licenciado": f["islicensed"],
           "en_team_del_sistema": team_bu in f["teams"], "en_jefatura": jef in f["teams"],
           "sin_roles_directos": not f["roles_directos"], "whoami_impersonado": who[0], "leer_arreglo_impersonado": est_c,
           "ve_columna_protegida": est_c == 200 and b_c.get("sanic_cuentadebitar") is not None}
    esperado_jef = papel == "jefe"
    bien = (chk["en_bu"] and chk["habilitado"] and chk["licenciado"] and chk["en_team_del_sistema"] and chk["en_jefatura"] == esperado_jef
            and chk["sin_roles_directos"] and who[0] == 200 and chk["ve_columna_protegida"])
    ok = ok and bien
    ver["usuarios"][papel] = {"systemuserid": uid, "fullname": f["fullname"], "chequeos": chk, "ok": bien,
                              "roles_directos": f["roles_directos"], "teams_nombres": f["teams_nombres"]}
    print(papel, "OK" if bien else "DIFIERE", json.dumps(chk, ensure_ascii=False))
ver["veredicto"] = "ya_existia" if ok and not acciones else ("hecho" if ok else "difiere")
with open(SALIDA, "w", encoding="utf-8") as fo:
    json.dump(ver, fo, ensure_ascii=False, indent=1)
print("veredicto:", ver["veredicto"])
sys.exit(0 if ok else 1)
