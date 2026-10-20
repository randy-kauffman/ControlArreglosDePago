"""H-19 (b) Oficial no admin paga la ultima cuota; H-8 gesto de jefatura sin y con SPIKE4 en Jefatura (temporal); H-12 (b) subida y reemplazo del archivo por un Jefe no admin.
SPIKE4 se agrega a Jefatura SOLO dentro del try; el finally lo saca (y t99_revertir.py lo verifica y deshabilita)."""
import urllib.error
import urllib.parse
import urllib.request
from t2lib import *
from hmap import datos_plantilla  # noqa: E402

dv = adm()
u = noadm()
R = Res("40_h19_h8_h12.json")
base = leer_estado()["linea_base"]
UID = base["usuario"]["systemuserid"]
JEF = base["jefatura"]["teamid"]
AR, CU, VC = conjuntos(dv)["arreglo"], conjuntos(dv)["cuota"], conjuntos(dv)["version"]
a_h19, a_h8 = fila("arreglo-t2-h19"), fila("arreglo-t2-h8")
v94, v91 = fila1("version-v94"), fila1("version-v91")
t0 = reloj(dv) - timedelta(seconds=3)


def subir(cli, ruta, nombre, datos):
    """PATCH de bytes a una columna de archivo con el token de `cli` (sin imprimirlo)."""
    h = {"Authorization": "Bearer " + cli._token, "Accept": "application/json", "OData-MaxVersion": "4.0", "OData-Version": "4.0",
         "Content-Type": "application/octet-stream", "x-ms-file-name": nombre}
    req = urllib.request.Request(cli.api + ruta, method="PATCH", headers=h, data=datos)
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            return r.status, {}
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try:
            return e.code, {"error": json.loads(txt)["error"]["message"]}
        except Exception:  # noqa: BLE001
            return e.code, {"error": txt[:600]}


def cuotas(arr):
    return get(dv, f"{CU}?$select=sanic_carp_tbl_cuotaid,sanic_numero&$filter=_sanic_arregloid_value eq {arr}&$orderby=sanic_numero asc")["value"]


try:
    # ------------------------------------------------------------ H-19 (b)
    cu = cuotas(a_h19)
    c1, c2 = cu[0]["sanic_carp_tbl_cuotaid"], cu[1]["sanic_carp_tbl_cuotaid"]
    est, b, _ = u.call("PATCH", f"{CU}({c1})", {"sanic_fechapago": "2026-11-05", "sanic_montopagado": 500})
    r1 = leer(dv, "arreglo", a_h19, "statecode,statuscode")
    R.v("H-19", "(b) Oficial NO admin paga la cuota 1 de 2: pasa y el arreglo sigue Vigente", est == 204 and r1["statecode"] == 0, "204 y Vigente", (est, codigo(b), err(b)[:200] if est >= 300 else "", r1["statecode"]))
    t = time.time()
    est, b, _ = u.call("PATCH", f"{CU}({c2})", {"sanic_fechapago": "2026-12-05", "sanic_montopagado": 500})
    seg = round(time.time() - t, 2)
    r2 = leer(dv, "arreglo", a_h19, "statecode,statuscode,sanic_fechatermino")
    R.v("H-19", "(b) Oficial NO admin paga la ULTIMA cuota: S-24 escribe el arreglo con SYSTEM, S-21 lo deja pasar (DC-03), queda Terminado sin CARP-ARR-010",
        est == 204 and r2["statecode"] == 1 and r2["statuscode"] == 2 and r2["sanic_fechatermino"], "204 y Terminado (1/2) con fechatermino",
        (est, codigo(b), err(b)[:200] if est >= 300 else "", {k: v for k, v in r2.items() if not k.startswith("@")}, seg), segundos=seg)

    # ------------------------------------------------------------ H-8 / H-12 (b) SIN Jefatura (solo Oficial)
    est, b, _ = u.call("PATCH", f"{AR}({a_h8})", {"sanic_motivoreapertura": "HUMO motivo T2"})
    R.v("H-8", "SIN Jefatura: el Oficial intenta reabrir un Vigente congelado: CARP-ARR-016", est == 400 and codigo(b) == "CARP-ARR-016", "400 ARR-016", (est, codigo(b), err(b)[:200]))
    est, b, _ = u.call("PATCH", f"{AR}({a_h19})", {"statecode": 0, "statuscode": 159460001, "sanic_motivoreapertura": "HUMO motivo T2"})
    R.v("H-8", "SIN Jefatura: el Oficial intenta reabrir un Terminado: CARP-ARR-016", est == 400 and codigo(b) == "CARP-ARR-016", "400 ARR-016", (est, codigo(b), err(b)[:200]))
    est, b = subir(u, f"{VC}({v94})/sanic_archivo", "HUMO-T2-oficial.xlsx", datos_plantilla())
    R.v("H-12", "(b) Oficial (sin Jefatura) intenta subir el archivo de un Borrador: la plataforma lo niega (sin prvWrite versionplantilla)", est == 403, "403", (est, codigo(b), err(b)[:200]))

    # ------------------------------------------------------------ SPIKE4 a Jefatura (temporal)
    est, b, _ = dv.call("POST", f"teams({JEF})/Microsoft.Dynamics.CRM.AddMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": UID}]})
    R.v("H-8", "agregar a SPIKE4 a Jefatura (temporal)", est in (200, 204), "204", (est, err(b)[:200] if est >= 300 else ""))
    guardar_clave("en_jefatura_ahora", True)
    pasa = None
    for i in range(10):
        est, b, _ = u.call("PATCH", f"{AR}({a_h8})", {"sanic_motivoreapertura": "HUMO motivo T2"})
        pasa = (est, codigo(b), err(b)[:200] if est >= 300 else "", i + 1)
        print("  intento", i + 1, pasa)
        if not (est == 400 and codigo(b) == "CARP-ARR-016"):
            break
        time.sleep(15)
    rel = leer(dv, "arreglo", a_h8, "statecode,statuscode,sanic_fechacongelamiento,sanic_cantidadreaperturas,_sanic_reabiertoporid_value,sanic_fechareapertura")
    R.v("H-8", "CON Jefatura: el mismo gesto (reabrir Vigente congelado) pasa; descongela, cuenta la reapertura, reabiertoporid = SPIKE4",
        pasa[0] == 204 and rel["sanic_fechacongelamiento"] is None and rel["sanic_cantidadreaperturas"] == 1 and rel["_sanic_reabiertoporid_value"] == UID, "204, sin congelar, reaperturas 1, reabiertoporid=SPIKE4",
        (pasa, {k: v for k, v in rel.items() if not k.startswith("@")}))
    est, b, _ = u.call("PATCH", f"{AR}({a_h19})", {"statecode": 0, "statuscode": 159460001, "sanic_motivoreapertura": "HUMO motivo T2"})
    rel = leer(dv, "arreglo", a_h19, "statecode,statuscode,sanic_fechatermino,sanic_cantidadreaperturas")
    R.v("H-8", "CON Jefatura: reabrir un Terminado (gesto ReabrirTerminado) pasa y queda Vigente", est == 204 and rel["statecode"] == 0 and rel["statuscode"] == 159460001 and rel["sanic_cantidadreaperturas"] == 1, "204, Vigente",
        (est, codigo(b), err(b)[:200] if est >= 300 else "", {k: v for k, v in rel.items() if not k.startswith("@")}))

    # ------------------------------------------------------------ H-12 (b) CON rol Jefe
    antes = leer(dv, "version", v94, "sanic_archivo_name")
    est1, b1 = subir(u, f"{VC}({v94})/sanic_archivo", "HUMO-T2-jefe-1.xlsx", datos_plantilla())
    r1 = leer(dv, "version", v94, "sanic_archivo_name,sanic_archivo")
    est2, b2 = subir(u, f"{VC}({v94})/sanic_archivo", "HUMO-T2-jefe-2.xlsx", datos_plantilla())
    r2 = leer(dv, "version", v94, "sanic_archivo_name,sanic_archivo")
    R.v("H-12", "(b) Jefe NO admin: subida y reemplazo del archivo sobre un Borrador pasan (las dos pasadas del reemplazo)",
        est1 in (200, 204) and est2 in (200, 204) and r2["sanic_archivo_name"] == "HUMO-T2-jefe-2.xlsx" and bool(r2.get("sanic_archivo")), "2xx y archivo nuevo",
        (antes.get("sanic_archivo_name"), (est1, codigo(b1), err(b1)[:160]), r1["sanic_archivo_name"], (est2, codigo(b2), err(b2)[:160]), r2["sanic_archivo_name"]))
    antes = leer(dv, "version", v91, "sanic_archivo_name")
    est3, b3 = subir(u, f"{VC}({v91})/sanic_archivo", "HUMO-T2-jefe-vigente.xlsx", datos_plantilla())
    r3 = leer(dv, "version", v91, "sanic_archivo_name,sanic_archivo")
    R.v("H-12", "(b) Jefe NO admin: reemplazo del archivo de la Vigente v91: CARP-VER-003 y el archivo queda como estaba", est3 >= 400 and codigo(b3) == "CARP-VER-003" and r3["sanic_archivo_name"] == antes["sanic_archivo_name"] and bool(r3.get("sanic_archivo")),
        "4xx VER-003 y archivo igual", (est3, codigo(b3), err(b3)[:160], antes["sanic_archivo_name"], r3["sanic_archivo_name"]))
    est4, b4, _ = u.call("PATCH", f"{VC}({v94})", {"sanic_capacidad": 59})
    R.v("H-12", "(b) Jefe NO admin: editar la capacidad de un Borrador pasa (control)", est4 == 204, 204, (est4, codigo(b4), err(b4)[:160] if est4 >= 300 else ""))
finally:
    est, b, _ = dv.call("POST", f"teams({JEF})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": UID}]})
    print("quitar de Jefatura:", est, err(b)[:200] if est >= 300 else "")
    miembros = miembros_team(dv, JEF)
    print("Jefatura miembros ahora:", miembros)
    guardar_clave("en_jefatura_ahora", UID in miembros)
tr = resumen_trazas(trazas(dv, t0, esperar=120, estable=4))
volcar("40_trazas.json", tr)
cnt = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["ent"].replace("sanic_carp_tbl_", ""), x["depth"])
    cnt[k] = cnt.get(k, 0) + 1
for k, n in sorted(cnt.items()):
    print("  traza", k, "x", n)
