"""Tanda 2b: tras dar 'actualizar' sobre sanic_instantaneacedula al perfil, repite con el NO admin (SPIKE4, habilitado TEMPORALMENTE):
(c) cambio manual de sanic_instantaneacedula rechazado, (a) Oficial desbloquea un Borrador congelado, (b) con SPIKE4 en Jefatura (temporal) reabre
un Terminado y un Vigente congelado, M7 (View Audit History del Oficial). SPIKE4 y Jefatura se revierten en el finally; t99_revertir.py lo verifica."""
import urllib.parse
from t2blib import *

dv = adm()
u = noadm_ = None
R = Res("50_desbloquear_reabrir.json")
base = leer_estado()["linea_base"]
UID = base["usuario"]["systemuserid"]
JEF = base["jefatura"]["teamid"]
AR = conjuntos(dv)["arreglo"]
a_gen, a_h8, a_h19 = fila2("arreglo-t2-gen"), fila2("arreglo-t2-h8"), fila2("arreglo-t2-h19")
COLS = "statecode,statuscode,sanic_fechacongelamiento,sanic_numeroprovisional,sanic_instantaneanombre,sanic_instantaneacedula,sanic_instantaneacorreo,sanic_instantaneacelular,sanic_cantidadreaperturas,sanic_fechatermino"


def estado(a):
    r = leer(dv, "arreglo", a, COLS + ",_sanic_reabiertoporid_value")
    return {k: ("presente" if k.startswith("sanic_instantanea") and v else v) for k, v in r.items() if not k.startswith("@")}


def llano(est, b):
    return (est, codigo(b), err(b)[:260] if est >= 300 else "")


def auditoria(cli, a, rotulo):
    """M7: el historial de auditoria de un arreglo, medido con el cliente `cli`."""
    out = {}
    tgt = urllib.parse.quote(json.dumps({"@odata.id": f"{AR}({a})"}))
    est, b, _ = cli.call("GET", f"RetrieveRecordChangeHistory(Target=@t)?@t={tgt}")
    out["RetrieveRecordChangeHistory"] = (est, codigo(b), err(b)[:260] if est >= 300 else {"cambios": len(((b.get("AuditDetailCollection") or {}).get("AuditDetails")) or [])})
    est, b, _ = cli.call("GET", f"audits?$select=auditid,operation,createdon&$filter=_objectid_value eq {a}&$top=20")
    out["audits"] = (est, codigo(b), err(b)[:260] if est >= 300 else {"filas": len(b.get("value", []))})
    return out


antes = {"gen": estado(a_gen), "h8": estado(a_h8), "h19": estado(a_h19)}
volcar("50_antes.json", antes)
print(json.dumps(antes, indent=1, default=str))
M7 = {}
try:
    print("habilitar SPIKE4:", dv.call("PATCH", f"systemusers({UID})", {"isdisabled": False})[:2])
    guardar_clave("habilitado", True)
    time.sleep(30)
    u = noadm()
    est, b, _ = u.call("GET", "WhoAmI")
    R.v("preparacion", "SPIKE4 habilitado: WhoAmI", est == 200 and b.get("UserId") == UID, "200 y SPIKE4", est)

    # El Borrador generado debe estar congelado; si no, se genera con el mismo usuario (Oficial).
    if antes["gen"]["sanic_fechacongelamiento"] is None:
        est, b, seg = llamar(u, "sanic_carp_capi_generarformato", {"arregloid": a_gen})
        R.v("preparacion", "generarformato como Oficial para dejar el Borrador congelado", est == 200, 200, (est, codigo(b), err(b)[:200] if est != 200 else "ok"))
    print("gen antes:", estado(a_gen))

    # ------------------------------------------------------------ M7 como Oficial (antes de tocar Jefatura)
    M7["oficial_gen"] = auditoria(u, a_gen, "oficial")
    M7["admin_gen"] = auditoria(dv, a_gen, "admin")

    # ------------------------------------------------------------ (c) cambio manual con el Oficial, arreglo congelado
    for etiqueta, cuerpo in (("valor nuevo", {"sanic_instantaneacedula": "HUMO-MANUAL-T2B"}), ("null", {"sanic_instantaneacedula": None})):
        est, b, _ = u.call("PATCH", f"{AR}({a_gen})", cuerpo)
        R.v("(c)", f"Oficial, Borrador congelado: cambio manual de sanic_instantaneacedula ({etiqueta}) rechazado con codigo del catalogo",
            est == 400 and codigo(b) is not None, "400 CARP-ARR-009", llano(est, b))
    ced = estado(a_gen)["sanic_instantaneacedula"]
    R.v("(c)", "la cedula copiada sigue intacta tras los intentos", ced == antes["gen"]["sanic_instantaneacedula"], antes["gen"]["sanic_instantaneacedula"], ced)

    # ------------------------------------------------------------ (a) Desbloquear como Oficial
    t = time.time()
    est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_fechacongelamiento": None})
    seg = round(time.time() - t, 2)
    d = estado(a_gen)
    R.v("(a)", "Oficial NO admin: Desbloquear un Borrador congelado pasa (antes 403) y vacia las 4 instantaneas y el provisional",
        est == 204 and d["sanic_fechacongelamiento"] is None and d["sanic_numeroprovisional"] is None and all(d[k] is None for k in d if k.startswith("sanic_instantanea")),
        "204, sin congelar, instantaneas y provisional nulos", (llano(est, b), d, seg), segundos=seg)
    # (c) tambien sin congelar
    est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_instantaneacedula": "HUMO-MANUAL-T2B"})
    R.v("(c)", "Oficial, Borrador sin congelar: cambio manual de sanic_instantaneacedula rechazado con codigo del catalogo", est == 400 and codigo(b) is not None, "400 CARP-ARR-009", llano(est, b))
    est, b, _ = u.call("PATCH", f"{AR}({a_gen})", {"sanic_comentarios": "HUMO-T2B-control"})
    R.v("(c)", "control: una edicion normal del Borrador sigue pasando", est == 204, 204, llano(est, b))

    # ------------------------------------------------------------ (b) Reabrir con SPIKE4 en Jefatura (temporal)
    est, b, _ = dv.call("POST", f"teams({JEF})/Microsoft.Dynamics.CRM.AddMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": UID}]})
    R.v("(b)", "agregar a SPIKE4 a Jefatura (temporal)", est in (200, 204), "204", (est, err(b)[:200] if est >= 300 else ""))
    guardar_clave("en_jefatura_ahora", True)

    def reabrir(a, cuerpo):
        res = None
        for i in range(10):
            est, b, _ = u.call("PATCH", f"{AR}({a})", cuerpo)
            res = (llano(est, b), i + 1)
            print("  intento", i + 1, res)
            if not (est == 400 and codigo(b) == "CARP-ARR-016"):
                break
            time.sleep(15)
        return res

    res = reabrir(a_h8, {"sanic_motivoreapertura": "HUMO motivo T2B"})
    d = estado(a_h8)
    R.v("(b)", "Jefatura: Reabrir un Vigente congelado pasa (antes 403), descongela, vacia instantaneas, cuenta la reapertura, reabiertoporid = SPIKE4",
        res[0][0] == 204 and d["sanic_fechacongelamiento"] is None and d["sanic_cantidadreaperturas"] == 1 and d["_sanic_reabiertoporid_value"] == UID and d["sanic_instantaneacedula"] is None,
        "204, sin congelar, reaperturas 1, reabiertoporid=SPIKE4", (res, d))
    res = reabrir(a_h19, {"statecode": 0, "statuscode": 159460001, "sanic_motivoreapertura": "HUMO motivo T2B"})
    d = estado(a_h19)
    R.v("(b)", "Jefatura: Reabrir un Terminado pasa (antes 403) y queda Vigente sin fechatermino",
        res[0][0] == 204 and d["statecode"] == 0 and d["statuscode"] == 159460001 and d["sanic_fechatermino"] is None and d["sanic_cantidadreaperturas"] == 1, "204, Vigente", (res, d))
    # (c) con el rol Jefe
    est, b, _ = u.call("PATCH", f"{AR}({a_h8})", {"sanic_instantaneacedula": "HUMO-MANUAL-T2B"})
    R.v("(c)", "Jefe NO admin: cambio manual de sanic_instantaneacedula tambien rechazado", est == 400 and codigo(b) is not None, "400 con codigo", llano(est, b))
    M7["jefe_gen"] = auditoria(u, a_gen, "jefe")
finally:
    est, b, _ = dv.call("POST", f"teams({JEF})/Microsoft.Dynamics.CRM.RemoveMembersTeam", {"Members": [{"@odata.type": "Microsoft.Dynamics.CRM.systemuser", "systemuserid": UID}]})
    print("quitar de Jefatura:", est, err(b)[:200] if est >= 300 else "")
    miembros = miembros_team(dv, JEF)
    guardar_clave("en_jefatura_ahora", UID in miembros)
    print("Jefatura miembros ahora:", miembros)
    volcar("51_m7_auditoria.json", M7)
    print(json.dumps(M7, indent=1, default=str))
