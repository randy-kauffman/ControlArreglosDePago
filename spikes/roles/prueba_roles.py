"""Prueba de los roles SR - CARP con un usuario de aplicacion NO administrador (dudas D-02, D-03 y D-07 de
diseno/04-matriz-privilegios.md). Usa el service principal de correo (PP_MAIL_*), el del spike 4, en el entorno de desarrollo.
Fase A: con SOLO un rol descartable (Create y Read de Cliente, sin Assign), crear un Cliente con el team del area como dueno.
Fase B: con la configuracion del diseno (Oficial en el team del sistema de la BU, Jefe en el team Jefatura, el usuario en Jefatura):
lookups a usuario, adjunto real, alta de version de plantilla y de mapeo con el team como dueno, y que no pueda borrar.
Al final borra los datos de prueba, quita al usuario de Jefatura y lo deja deshabilitado. La asignacion de roles a los teams queda
(es la configuracion del diseno, inventario 13). Nunca imprime secretos. Uso: python3 prueba_roles.py"""
import base64
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "herramientas"))
sys.path.insert(0, os.path.join(AQUI, "..", "manual"))
from dataverse_api import Dataverse  # noqa: E402
from lib import DvCorreo, id_mail_client  # noqa: E402

BU_NOMBRE, JEFATURA = "Arreglos de Pago", "Arreglos de Pago - Jefatura"
ROL_ZZ = "ZZ CARP Prueba sin Assign"
res, creados = {}, []  # creados: (entityset, id) para borrar al final
adm = Dataverse()


def uno(ruta):
    s, b, _ = adm.call("GET", ruta)
    assert s == 200 and len(b["value"]) == 1, (ruta, s, b)
    return b["value"][0]


def anotar(clave, est, cuerpo, esperado):
    ok = (est in (200, 201, 204)) if esperado == "ok" else (est == 403 or est == 400 and "privilege" in json.dumps(cuerpo).lower())
    res[clave] = {"http": est, "esperado": esperado, "resultado": "CONFIRMADO" if ok else "REFUTADO", "detalle": cuerpo.get("error", "") if isinstance(cuerpo, dict) else ""}
    print(f"[{res[clave]['resultado']}] {clave}: HTTP {est} {res[clave]['detalle'][:220]}")


bu = uno(f"businessunits?$select=businessunitid&$filter=name eq '{BU_NOMBRE}'")["businessunitid"]
team = uno(f"teams?$select=teamid&$filter=_businessunitid_value eq {bu} and isdefault eq true")["teamid"]
jef = uno(f"teams?$select=teamid&$filter=_businessunitid_value eq {bu} and name eq '{JEFATURA}'")["teamid"]
usr = uno(f"systemusers?$select=systemuserid,isdisabled,_businessunitid_value&$filter=applicationid eq {id_mail_client()}")
uid = usr["systemuserid"]


def rol_en_bu(nombre):
    raiz = uno(f"roles?$select=roleid&$filter=name eq '{nombre}' and _parentroleid_value eq null")["roleid"]
    return uno(f"roles?$select=roleid&$filter=_parentrootroleid_value eq {raiz} and _businessunitid_value eq {bu}")["roleid"]


def ref(ruta_nav, entityset, rid):
    return adm.call("POST", f"{ruta_nav}/$ref", {"@odata.id": f"{adm.api}{entityset}({rid})"})


def es(tabla):
    return adm.call("GET", f"EntityDefinitions(LogicalName='{tabla}')?$select=EntitySetName")[1]["EntitySetName"]


ES = {t: es(f"sanic_carp_tbl_{t}") for t in ("cliente", "conversacion", "arreglo", "versionplantilla", "mapeocelda")}
OWNER = {"ownerid@odata.bind": f"/teams({team})"}
try:
    # ---- preparacion
    print("reactivar:", adm.call("PATCH", f"systemusers({uid})", {"isdisabled": False})[0])
    if usr["_businessunitid_value"] != bu:
        print("mover a la BU:", adm.call("PATCH", f"systemusers({uid})", {"businessunitid@odata.bind": f"/businessunits({bu})"})[0:2])
    # ---- fase A: rol descartable sin Assign, asignado directo al usuario (el team del sistema todavia no tiene el Oficial)
    s, b, h = adm.call("POST", "roles", {"name": ROL_ZZ, "businessunitid@odata.bind": f"/businessunits({uno('businessunits?$select=businessunitid&$filter=_parentbusinessunitid_value eq null')['businessunitid']})"},
                       cabeceras={"Prefer": "return=representation"})
    rzz = b["roleid"]
    privs = adm.call("GET", "privileges?$select=privilegeid,name&$filter=name eq 'prvCreatesanic_carp_tbl_cliente' or name eq 'prvReadsanic_carp_tbl_cliente'")[1]["value"]
    print("privilegios zz:", adm.call("POST", f"roles({rzz})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
                                      {"Privileges": [{"PrivilegeId": p["privilegeid"], "Depth": "Local"} for p in privs]})[0])
    time.sleep(3)
    rzz_bu = uno(f"roles?$select=roleid&$filter=_parentrootroleid_value eq {rzz} and _businessunitid_value eq {bu}")["roleid"]
    print("rol zz al usuario:", ref(f"systemusers({uid})/systemuserroles_association", "roles", rzz_bu)[0])
    time.sleep(5)
    yo = DvCorreo()
    s, b, _ = yo.call("POST", ES["cliente"], {"sanic_nombre": "ZZ prueba A propio"}, cabeceras={"Prefer": "return=representation"})
    anotar("A1 crear Cliente propio sin Assign", s, b, "ok")
    if s in (200, 201): creados.append((ES["cliente"], b["sanic_carp_tbl_clienteid"]))
    s, b, _ = yo.call("POST", ES["cliente"], {"sanic_nombre": "ZZ prueba A team", **OWNER}, cabeceras={"Prefer": "return=representation"})
    anotar("A2 crear Cliente con el team como dueno SIN Assign (D-02: se espera rechazo)", s, b, "rechazo")
    if s in (200, 201): creados.append((ES["cliente"], b["sanic_carp_tbl_clienteid"]))
    print("quitar rol zz:", adm.call("DELETE", f"systemusers({uid})/systemuserroles_association({rzz_bu})/$ref")[0], "| borrar rol zz:", adm.call("DELETE", f"roles({rzz})")[0])
    # ---- fase B: configuracion del diseno
    print("Oficial al team del sistema:", ref(f"teams({team})/teamroles_association", "roles", rol_en_bu("SR - CARP - Oficial"))[0])
    print("Jefe al team Jefatura:", ref(f"teams({jef})/teamroles_association", "roles", rol_en_bu("SR - CARP - Jefe"))[0])
    print("usuario a Jefatura:", adm.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.AddMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]})[0])
    time.sleep(10)
    yo = DvCorreo()
    s, b, _ = yo.call("POST", ES["cliente"], {"sanic_nombre": "ZZ prueba B cliente", **OWNER}, cabeceras={"Prefer": "return=representation"})
    anotar("B1 Oficial crea Cliente con el team como dueno (con Assign)", s, b, "ok")
    cli = b.get("sanic_carp_tbl_clienteid")
    if cli: creados.append((ES["cliente"], cli))
    s, b, _ = adm.call("POST", ES["conversacion"], {"sanic_nombre": "ZZ prueba conversacion", **OWNER}, cabeceras={"Prefer": "return=representation"})
    conv = b.get("sanic_carp_tbl_conversacionid"); creados.append((ES["conversacion"], conv))
    s, b, _ = yo.call("GET", f"{ES['conversacion']}({conv})?$select=sanic_nombre")
    anotar("B2 Oficial lee una Conversacion del team", s, b, "ok")
    nav = {r["ReferencingAttribute"]: r["ReferencingEntityNavigationPropertyName"] for r in adm.call("GET", "EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')/ManyToOneRelationships?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName")[1]["value"]}
    s, b, _ = yo.call("POST", ES["arreglo"], {**OWNER, f"{nav['sanic_oficialid']}@odata.bind": f"/systemusers({uid})", f"{nav['sanic_clienteid']}@odata.bind": f"/{ES['cliente']}({cli})",
                                              f"{nav['sanic_conversacionid']}@odata.bind": f"/{ES['conversacion']}({conv})"}, cabeceras={"Prefer": "return=representation"})
    anotar("B3 Oficial crea Arreglo con lookup a usuario, cliente y conversacion (D-07)", s, b, "ok")
    arr = b.get("sanic_carp_tbl_arregloid")
    if arr: creados.insert(0, (ES["arreglo"], arr))
    nav_e = [r["ReferencingEntityNavigationPropertyName"] for r in adm.call("GET", "EntityDefinitions(LogicalName='email')/ManyToOneRelationships?$select=ReferencedEntity,ReferencingEntityNavigationPropertyName,ReferencingAttribute")[1]["value"]
             if r["ReferencedEntity"] == "sanic_carp_tbl_conversacion" and r["ReferencingAttribute"] == "regardingobjectid"][0]
    s, b, _ = yo.call("POST", "emails", {"subject": "ZZ prueba de roles (borrador, no se envia)", "description": "<p>prueba</p>", f"{nav_e}@odata.bind": f"/{ES['conversacion']}({conv})"},
                      cabeceras={"Prefer": "return=representation"})
    anotar("B4 Oficial crea un borrador de Email con Regarding a la conversacion", s, b, "ok")
    em = b.get("activityid")
    if em:
        creados.insert(0, ("emails", em))
        s, b, _ = yo.call("POST", "activitymimeattachments", {"objectid_activitypointer@odata.bind": f"/activitypointers({em})", "objecttypecode": "email", "filename": "zz.txt",
                                                              "mimetype": "text/plain", "body": base64.b64encode(b"prueba de adjunto").decode()}, cabeceras={"Prefer": "return=representation"})
        anotar("B5 Oficial adjunta un archivo al borrador (D-03)", s, b, "ok")
        s, b, _ = yo.call("GET", f"activitymimeattachments?$select=filename&$filter=_objectid_value eq {em}")
        anotar("B6 Oficial lee el adjunto", s, b, "ok")
    s, b, _ = yo.call("POST", ES["versionplantilla"], {"sanic_nombre": "v99", **OWNER}, cabeceras={"Prefer": "return=representation"})
    anotar("B7 Jefe crea Version de plantilla con el team como dueno", s, b, "ok")
    ver = b.get("sanic_carp_tbl_versionplantillaid")
    if ver:
        creados.append((ES["versionplantilla"], ver))
        navm = {r["ReferencingAttribute"]: r["ReferencingEntityNavigationPropertyName"] for r in adm.call("GET", "EntityDefinitions(LogicalName='sanic_carp_tbl_mapeocelda')/ManyToOneRelationships?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName")[1]["value"]}
        s, b, _ = yo.call("POST", ES["mapeocelda"], {"sanic_nombre": "x!A1", "sanic_hoja": "x", "sanic_celda": "A1", **OWNER, f"{navm['sanic_versionplantillaid']}@odata.bind": f"/{ES['versionplantilla']}({ver})"},
                          cabeceras={"Prefer": "return=representation"})
        anotar("B8 Jefe crea Mapeo con lookup a la version y el team como dueno", s, b, "ok")
        if b.get("sanic_carp_tbl_mapeoceldaid"):
            mid = b["sanic_carp_tbl_mapeoceldaid"]
            s, b, _ = yo.call("GET", f"{ES['mapeocelda']}({mid})?$select=sanic_celda")
            anotar("B9 Jefe lee el Mapeo (D-01)", s, b, "ok")
            creados.insert(0, (ES["mapeocelda"], mid))
    s, b, _ = yo.call("DELETE", f"{ES['cliente']}({cli})")
    anotar("B10 Oficial NO puede borrar un Cliente", s, b, "rechazo")
finally:
    print("\nlimpieza:")
    for ent, rid in creados:
        print(" ", ent, rid, adm.call("DELETE", f"{ent}({rid})")[0])
    s, b, _ = adm.call("GET", f"roles?$select=roleid&$filter=name eq '{ROL_ZZ}'")
    for r in b.get("value", []):
        print("  rol zz", adm.call("DELETE", f"roles({r['roleid']})")[0])
    print("  quitar de Jefatura:", adm.call("POST", f"teams({jef})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": uid}]})[0])
    print("  deshabilitar:", adm.call("PATCH", f"systemusers({uid})", {"isdisabled": True})[0])
    with open(os.path.join(AQUI, "resultado.json"), "w", encoding="utf-8") as f:
        json.dump(res, f, ensure_ascii=False, indent=2)
