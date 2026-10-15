"""Dobles, punto (a): falla de "sin privilegio". Autorizacion del usuario (2026-10-15, opcion B): habilitar por un rato al usuario de aplicacion NO administrador,
darle un rol minimo SOLO si hace falta, medir y dejarlo como estaba. Nada mas.
Flujo: (1) linea base de solo lectura -> evidencia/40_privilegios_linea_base.json y el estado ORIGINAL en estado.json ANTES de cambiar nada;
(2) habilitar (isdisabled=false) y verificar releyendo; (3) medir con la consola (modo priv, CallerId); (4) en el finally: quitar EXACTAMENTE los roles agregados,
volver isdisabled al valor original y verificar releyendo que isdisabled, accessmode, businessunitid, la lista de roles (y la pertenencia a equipos) son IGUALES a la linea base
-> evidencia/41_privilegios_restaurado.json (restaurado_OK). No crea, copia ni modifica roles; no toca otros usuarios, equipos ni la organizacion. Nunca imprime secretos."""
import sys
from dlib import *

ROLES_A_AGREGAR = []   # (nombre, roleid) de roles EXISTENTES que se asignen; vacio = no hizo falta ninguno (se decide mirando la medicion, ver MODO)
MODO = sys.argv[1] if len(sys.argv) > 1 else "completo"   # completo | solo_restaurar

dv = adm()
st = leer_estado()
if "linea_base" not in st:
    sys.exit("DETENIDO: falta la linea base del spike (d00).")
sec = leer_secretos(requeridas=("PP_MAIL_CLIENT_ID",))
us = get(dv, f"systemusers?$select=systemuserid&$filter=applicationid eq {sec['PP_MAIL_CLIENT_ID']}")["value"]
assert len(us) == 1, "el usuario de aplicacion no administrador no esta (o esta duplicado)"
UID = us[0]["systemuserid"]
assert UID == st["linea_base"]["usuario_no_admin_id"], "el usuario no coincide con la linea base del spike"


def foto():
    """Estado observable del usuario, normalizado para compararlo. Solo lee."""
    u = get(dv, f"systemusers({UID})?$select=isdisabled,accessmode,_businessunitid_value")
    roles_rm = paginar(dv, f"systemuserrolescollection?$select=roleid&$filter=systemuserid eq {UID}")   # RetrieveMultiple de systemuserroles (no admite Retrieve)
    roles_asoc = get(dv, f"systemusers({UID})/systemuserroles_association?$select=roleid,name")["value"]
    equipos = get(dv, f"systemusers({UID})/teammembership_association?$select=teamid,name,teamtype")["value"]
    por_equipo = {}
    for t in equipos:
        por_equipo[t["teamid"]] = sorted(r["roleid"] for r in get(dv, f"teams({t['teamid']})/teamroles_association?$select=roleid")["value"])
    return {
        "isdisabled": u["isdisabled"], "accessmode": u["accessmode"], "businessunitid": u["_businessunitid_value"],
        "roles_directos_systemuserroles": sorted(r["roleid"] for r in roles_rm),
        "roles_directos_por_asociacion": sorted(r["roleid"] for r in roles_asoc),
        "roles_directos_nombres": sorted(r["name"] for r in roles_asoc),
        "equipos": sorted([[t["teamid"], t["teamtype"], t["name"]] for t in equipos]),
        "roles_de_cada_equipo": {k: v for k, v in sorted(por_equipo.items())},
    }


COMPARAR = ("isdisabled", "accessmode", "businessunitid", "roles_directos_systemuserroles", "roles_directos_por_asociacion", "equipos", "roles_de_cada_equipo")


def restaurar(original, agregados):
    r = {"fecha": time.strftime("%Y-%m-%d %H:%M:%S"), "roles_quitados": [], "errores": []}
    for nombre, rid in reversed(agregados):
        est, b, _ = dv.call("DELETE", f"systemusers({UID})/systemuserroles_association({rid})/$ref")
        r["roles_quitados"].append({"rol": nombre, "http": est})
        if est not in (200, 204):
            r["errores"].append(f"quitar rol {nombre}: {est} {str(b)[:200]}")
    est, b, _ = dv.call("PATCH", f"systemusers({UID})", {"isdisabled": original["isdisabled"]})
    r["patch_isdisabled_http"] = est
    if est not in (200, 204):
        r["errores"].append(f"restaurar isdisabled: {est} {str(b)[:200]}")
    time.sleep(3)
    ahora = foto()
    r["foto_ahora"] = ahora
    r["comparacion"] = {k: {"antes": original[k], "ahora": ahora[k], "igual": original[k] == ahora[k]} for k in COMPARAR}
    r["restaurado_OK"] = all(v["igual"] for v in r["comparacion"].values()) and not r["errores"]
    volcar("41_privilegios_restaurado.json", r)
    return r


# ---------------------------------------------------------------- 1) linea base (solo lectura)
original = foto()
res = residuales_zzf(dv)
sond = residuales_sonda(dv)
base = {
    "fecha": time.strftime("%Y-%m-%d %H:%M:%S"), "usuario": original,
    "residuales_zzf": res, "residuales_sonda": sond,
    "filas_en_tablas_del_proyecto": {t: len(paginar(dv, f"{TABLAS[t][1]}?$select={TABLAS[t][2]}")) for t in PROPIAS},
    "roles_del_proyecto_existentes": [r["name"] for r in get(dv, f"roles?$select=name&$filter=_businessunitid_value eq {BU_ID} and startswith(name,'SR - CARP')")["value"]],
}
volcar("40_privilegios_linea_base.json", base)
print("linea base:", {k: original[k] for k in ("isdisabled", "accessmode")}, "| roles directos:", len(original["roles_directos_systemuserroles"]),
      "| equipos:", [(e[2], e[1]) for e in original["equipos"]], "| residuales ZZF:", res or "ninguno", "| Sonda:", sond or "ninguno")
if res or sond:
    sys.exit("DETENIDO: hay residuales; no se toca nada.")
if original["roles_directos_systemuserroles"] != original["roles_directos_por_asociacion"]:
    sys.exit("DETENIDO: la lista de roles por RetrieveMultiple y por asociacion no coincide.")
if "privilegios_original" not in st:   # el ORIGINAL se anota una sola vez y ANTES de cambiar nada
    st["privilegios_original"] = {"anotado": base["fecha"], **original}
    guardar_estado(st)
else:
    original = {k: v for k, v in st["privilegios_original"].items() if k != "anotado"}
    print("ya habia un estado original anotado en estado.json: se compara contra ese")

agregados = []
try:
    if MODO == "completo":
        # ---------------------------------------------------------------- 2) habilitar
        est, b, _ = dv.call("PATCH", f"systemusers({UID})", {"isdisabled": False})
        print("habilitar: HTTP", est, str(b)[:200] if est not in (200, 204) else "")
        h = get(dv, f"systemusers({UID})?$select=isdisabled")
        print("releido isdisabled:", h["isdisabled"])
        if h["isdisabled"]:
            print("DETENIDO: no quedo habilitado")
        else:
            time.sleep(30)   # la cache de seguridad del principal no se refresca al instante (spikes/roles)
            # ---------------------------------------------------------------- 3) medir sin rol directo
            rc, out, err = correr_hijo("priv", extra={"TZ": "America/Managua", "ZZF_ESCENARIO": "sin_rol_directo",
                                                        "ZZF_ESCENARIO_DESC": "usuario habilitado, 0 roles directos; privilegios efectivos = rol del equipo por defecto de su unidad de negocio"})
            print(out)
            if err.strip():
                print("STDERR:", err[:2000])
            with open(os.path.join(EVID, "42_privilegios.log"), "w", encoding="utf-8") as f:
                f.write(out + ("\n--- STDERR ---\n" + err if err.strip() else ""))
            print("codigo de salida del hijo:", rc)
finally:
    r = restaurar(original, agregados)
    print(json.dumps({"restaurado_OK": r["restaurado_OK"], "errores": r["errores"],
                      "iguales": {k: v["igual"] for k, v in r["comparacion"].items()}}, indent=1))
    if not r["restaurado_OK"]:
        print("DETENIDO: LA RESTAURACION NO QUEDO IGUAL A LA LINEA BASE. GUID del usuario (no va al repo):", UID)
        sys.exit(3)
