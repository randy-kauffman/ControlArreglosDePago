"""Utilidades del spike de verificaciones V-1 a V-7 (2026-10-10). Nunca imprime secretos ni tokens.
Reutiliza: herramientas/dataverse_api.Dataverse (administrador, PP_CLIENT_*) y spikes/manual/lib.DvCorreo (usuario de aplicacion NO admin, PP_MAIL_*)."""
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "herramientas"))
sys.path.insert(0, os.path.join(AQUI, "..", "manual"))
from dataverse_api import Dataverse  # noqa: E402
from lib import DvCorreo, id_mail_client, usuario_app  # noqa: E402

ENTORNO = "https://org36e60d9d.crm.dynamics.com"
SOLUCION = "sanic_carp_sol_controlarreglospago"
EVID = os.path.join(AQUI, "evidencia")
os.makedirs(EVID, exist_ok=True)
ESTADO = os.path.join(AQUI, "estado.json")  # solo GUIDs y valores iniciales, sin secretos


def adm():
    dv = Dataverse()
    assert dv.org == ENTORNO, "entorno no autorizado"
    return dv


def correo():
    return DvCorreo()


def volcar(nombre, obj):
    with open(os.path.join(EVID, nombre), "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=1, ensure_ascii=False, default=str)


def leer_estado():
    try:
        with open(ESTADO, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return {}


def guardar_estado(d):
    with open(ESTADO, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=1, ensure_ascii=False)


def get(dv, ruta):
    est, b, _ = dv.call("GET", ruta)
    assert est == 200, (ruta, est, b)
    return b


def componentes_solucion(dv):
    sid = get(dv, f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")["value"][0]["solutionid"]
    out, ruta = [], f"solutioncomponents?$select=solutioncomponentid,componenttype,objectid,rootcomponentbehavior&$filter=_solutionid_value eq {sid}"
    while ruta:
        est, b, _ = dv.call("GET", ruta)
        assert est == 200, (est, b)
        out += b["value"]
        nxt = b.get("@odata.nextLink")
        ruta = nxt.split("/api/data/v9.2/")[1] if nxt else None
    return sid, sorted(out, key=lambda c: (c["componenttype"], c["objectid"]))


# ---------------------------------------------------------------- helpers de pruebas
from datetime import datetime, timedelta, timezone
from email.utils import parsedate_to_datetime

BU_ID = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"        # BU "Arreglos de Pago"
TEAM_DEF = "2ebc18e7-f9c3-f111-a05a-7ced8d3c456b"     # team por defecto de la BU (rol Oficial)
OWNER = {"ownerid@odata.bind": f"/teams({TEAM_DEF})"}
ES = {"cliente": "sanic_carp_tbl_clientes", "conversacion": "sanic_carp_tbl_conversacions", "arreglo": "sanic_carp_tbl_arreglos",
      "versionplantilla": "sanic_carp_tbl_versionplantillas"}
LN = {k: "sanic_carp_tbl_" + k for k in ES}
PREFER_REP = {"Prefer": "return=representation"}


def reloj(dv):
    """Hora UTC del servidor (cabecera Date), para acotar las trazas."""
    _, _, h = dv.call("GET", "WhoAmI")
    return parsedate_to_datetime(h.get("Date") or h.get("date")).astimezone(timezone.utc)


def iso(t):
    return t.strftime("%Y-%m-%dT%H:%M:%SZ")


def trazas(dv, desde, esperar=25, minimo=0, estable=2):
    """Trazas del spike (tipos Zzv.Probe.*) creadas desde `desde`. Espera a que lleguen (y a que dejen de crecer)."""
    t0, ult, rep = time.time(), -1, 0
    filas = []
    while time.time() - t0 < esperar:
        b = get(dv, "plugintracelogs?$select=typename,messagename,primaryentity,depth,mode,operationtype,correlationid,requestid,messageblock,createdon,exceptiondetails,performanceexecutionduration"
                    f"&$filter=startswith(typename,'Zzv.Probe') and createdon ge {iso(desde)}&$orderby=createdon asc&$top=200")
        filas = b["value"]
        if len(filas) == ult and len(filas) >= minimo:
            rep += 1
            if rep >= estable:
                break
        else:
            rep = 0
        ult = len(filas)
        time.sleep(3)
    return filas


def resumen_trazas(filas):
    """Lista compacta: una entrada por traza con las lineas ZZV del messageblock."""
    out = []
    for f in filas:
        lineas = [l for l in (f["messageblock"] or "").splitlines() if l.startswith("ZZV")]
        out.append({"createdon": f["createdon"], "typename": f["typename"].split(".")[-1], "messagename": f["messagename"], "primaryentity": f["primaryentity"],
                    "depth": f["depth"], "mode": f["mode"], "correlationid": f["correlationid"], "lineas": lineas, "excepcion": (f.get("exceptiondetails") or "")[:600]})
    return out


def mostrar_resumen(res):
    for r in res:
        print(f"  [{r['createdon'][11:19]}] {r['typename']} msg={r['messagename']} ent={r['primaryentity']} depth={r['depth']} corr={r['correlationid'][:8]}")
        for l in r["lineas"]:
            print("      ", l[:400])
        if r["excepcion"]:
            print("      EXC:", r["excepcion"][:300])


def crear(dv, conj, cuerpo, tag=""):
    est, b, h = dv.call("POST", conj, cuerpo, cabeceras=PREFER_REP)
    assert est == 201, (tag, est, b)
    return b


def navs(dv, tabla):
    r = get(dv, f"EntityDefinitions(LogicalName='{tabla}')/ManyToOneRelationships?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName")
    return {x["ReferencingAttribute"]: x["ReferencingEntityNavigationPropertyName"] for x in r["value"]}


def pk(fila, tabla):
    return fila[f"{tabla}id"]


def registrar_creado(clave, conj, rid):
    e = leer_estado(); e.setdefault("filas", []).append([clave, conj, rid]); guardar_estado(e)


def mi_id(dv):
    return get(dv, "WhoAmI")["UserId"]


def crear_cliente(dv, sufijo, cedula="ZZV-ced-cli"):
    b = crear(dv, ES["cliente"], {"sanic_nombre": f"ZZV-cli-{sufijo}", "sanic_nombrecompleto": f"ZZV-cliente {sufijo}", "sanic_cedula": cedula, **OWNER}, "cliente")
    rid = b["sanic_carp_tbl_clienteid"]; registrar_creado(f"cliente-{sufijo}", ES["cliente"], rid); return rid


def crear_version(dv, sufijo):
    b = crear(dv, ES["versionplantilla"], {"sanic_nombre": f"ZZV-{sufijo}"[:10], "sanic_capacidad": 60, "sanic_estado": 159460001, **OWNER}, "version")
    rid = b["sanic_carp_tbl_versionplantillaid"]; registrar_creado(f"version-{sufijo}", ES["versionplantilla"], rid); return rid


def crear_conversacion(dv, sufijo):
    b = crear(dv, ES["conversacion"], {"sanic_nombre": f"ZZV-cv-{sufijo}"[:20], "sanic_huellamessageid": f"ZZV-h-{sufijo}", "sanic_fechaprimercorreo": "2026-10-10T10:00:00Z",
                                       "sanic_correonuevoenfinalizada": False, "sanic_cantidadrespuestas": 0, **OWNER}, "conversacion")
    rid = b["sanic_carp_tbl_conversacionid"]; registrar_creado(f"conversacion-{sufijo}", ES["conversacion"], rid); return rid


def crear_arreglo(dv, sufijo, cli, ver, oficial, conv=None, cuenta="ZZV-cta-0001", cedula="ZZV-ced-0001"):
    nv = navs(dv, LN["arreglo"])
    cuerpo = {"sanic_nombre": f"ZZV-a-{sufijo}"[:20], "sanic_producto": 159460001, "sanic_numeroproducto": f"ZZV-{sufijo}", "sanic_tipo": 159460001, "sanic_moneda": 159460001,
              "sanic_canalorigen": 159460001, "sanic_saldoinicial": 1000, "sanic_montoabonado": 100, "sanic_saldoremanente": 900, "sanic_plazo": 6, "sanic_cuotamensual": 150,
              "sanic_totalpagar": 900, "sanic_fechainicio": "2026-11-01", "sanic_fechafin": "2027-04-01", "sanic_cuentadebitar": cuenta, "sanic_fechaformalizacion": "2026-10-10",
              "sanic_cantidadreaperturas": 0, "sanic_instantaneacedula": cedula, **OWNER,
              f"{nv['sanic_clienteid']}@odata.bind": f"/{ES['cliente']}({cli})", f"{nv['sanic_versionplantillaid']}@odata.bind": f"/{ES['versionplantilla']}({ver})",
              f"{nv['sanic_oficialid']}@odata.bind": f"/systemusers({oficial})"}
    if conv:
        cuerpo[f"{nv['sanic_conversacionid']}@odata.bind"] = f"/{ES['conversacion']}({conv})"
    b = crear(dv, ES["arreglo"], cuerpo, "arreglo")
    rid = b["sanic_carp_tbl_arregloid"]; registrar_creado(f"arreglo-{sufijo}", ES["arreglo"], rid); return rid


def estado_fila(dv, conj, rid, extra=""):
    return get(dv, f"{conj}({rid})?$select=statecode,statuscode{extra}")


def cmd(dv, cli, tag, *partes):
    """Dispara una orden para ZzvComando: Update de Cliente.sanic_nombrecompleto con la orden. Devuelve (HTTP, cuerpo)."""
    texto = "|".join(["ZZV-CMD", tag, *partes])
    assert len(texto) <= 200, len(texto)
    est, b, _ = dv.call("PATCH", f"{ES['cliente']}({cli})", {"sanic_nombrecompleto": texto})
    return est, b


class Corrida:
    """Ejecuta pasos secuenciales y atribuye cada traza a su paso por performanceexecutionstarttime (hora exacta del servidor en ms).
    createdon de la traza NO sirve: la plataforma las escribe por lotes con hasta ~30 s de atraso."""

    def __init__(self, dv, archivo):
        self.dv, self.archivo, self.pasos = dv, archivo, []
        self.t_global = reloj(dv) - timedelta(seconds=2)

    def paso(self, nombre, accion, relectura=None):
        dv = self.dv
        t_ini = reloj(dv)
        est, b = accion()
        t_fin = reloj(dv)
        rel = relectura() if relectura else None
        err = b.get("error") if isinstance(b, dict) else None
        self.pasos.append({"paso": nombre, "http": est, "error": err, "relectura": rel, "t_ini": t_ini, "t_fin": t_fin, "trazas": []})
        print(f"### {nombre}\n  HTTP {est} {('ERROR: ' + str(err)[:400]) if err else ''} | relectura: {self._corta(rel)}", flush=True)
        time.sleep(1.6)
        return est, b

    @staticmethod
    def _corta(rel):
        if isinstance(rel, dict):
            return {k: v for k, v in rel.items() if not k.startswith("@odata")}
        return rel

    def cerrar(self, espera_min=40):
        dv = self.dv
        fin = time.time()
        ult, estable, filas = -1, 0, []
        while True:
            filas = get(dv, "plugintracelogs?$select=typename,messagename,primaryentity,depth,mode,correlationid,messageblock,createdon,performanceexecutionstarttime,exceptiondetails"
                            f"&$filter=startswith(typename,'Zzv.Probe') and createdon ge {iso(self.t_global)}&$orderby=performanceexecutionstarttime asc&$top=500")["value"]
            if len(filas) == ult:
                estable += 1
            else:
                estable = 0
            ult = len(filas)
            if estable >= 2 and time.time() - fin >= espera_min:
                break
            if time.time() - fin > 240:
                break
            time.sleep(10)
        sin_paso = []
        for f in filas:
            ini = datetime.fromisoformat(f["performanceexecutionstarttime"].replace("Z", "+00:00"))
            if ini.tzinfo is None:
                ini = ini.replace(tzinfo=timezone.utc)
            # pasos secuenciales y sincronos: la traza es del ULTIMO paso cuyo t_ini no supera su inicio (la cabecera Date del servidor y la hora del plugin difieren ~1 s)
            cand = [p for p in self.pasos if p["t_ini"] <= ini][-1:]
            r = resumen_trazas([f])[0]
            r["inicio"] = f["performanceexecutionstarttime"]
            (cand[0]["trazas"] if cand else sin_paso).append(r)
        for p in self.pasos:
            print(f"\n### {p['paso']}  (HTTP {p['http']})")
            mostrar_resumen(p["trazas"])
        if sin_paso:
            print("\n### TRAZAS SIN PASO:"); mostrar_resumen(sin_paso)
        salida = [dict(p, t_ini=iso(p["t_ini"]), t_fin=iso(p["t_fin"])) for p in self.pasos]
        volcar(self.archivo, {"pasos": salida, "trazas_sin_paso": sin_paso})
        return self.pasos
