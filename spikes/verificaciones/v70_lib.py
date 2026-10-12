"""Utilidades de V-8 (2026-10-11). Reutiliza vlib.py (spike V-1 a V-7) pero guarda su propio estado en estado_v70.json
(GUIDs y valores iniciales, sin secretos) para no tocar estado.json del spike anterior. Nunca imprime secretos ni tokens."""
from vlib import *  # noqa: F401,F403
from vlib import AQUI, ES, OWNER, PREFER_REP, LN, BU_ID, crear, navs, get, volcar  # noqa: F401

ESTADO70 = os.path.join(AQUI, "estado_v70.json")
AR = ES["arreglo"]


def leer70():
    try:
        with open(ESTADO70, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return {}


def guardar70(d):
    with open(ESTADO70, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=1, ensure_ascii=False)


def reg70(clave, conj, rid):
    e = leer70(); e.setdefault("filas", []).append([clave, conj, rid]); guardar70(e)


def cliente70(dv, sufijo):
    b = crear(dv, ES["cliente"], {"sanic_nombre": f"ZZV-cli-{sufijo}", "sanic_nombrecompleto": f"ZZV-cliente {sufijo}", "sanic_cedula": "ZZV-ced-cli", **OWNER}, "cliente")
    rid = b["sanic_carp_tbl_clienteid"]; reg70(f"cliente-{sufijo}", ES["cliente"], rid); return rid


def version70(dv, sufijo):
    b = crear(dv, ES["versionplantilla"], {"sanic_nombre": f"ZZV-{sufijo}"[:10], "sanic_capacidad": 60, "sanic_estado": 159460001, **OWNER}, "version")
    rid = b["sanic_carp_tbl_versionplantillaid"]; reg70(f"version-{sufijo}", ES["versionplantilla"], rid); return rid


def cuerpo_arreglo(dv, cli, ver, oficial, sufijo, nombre=None):
    """Cuerpo de Create de Arreglo. nombre=None => NO se envia sanic_nombre."""
    nv = navs(dv, LN["arreglo"])
    c = {"sanic_producto": 159460001, "sanic_numeroproducto": f"ZZV-{sufijo}", "sanic_tipo": 159460001, "sanic_moneda": 159460001,
         "sanic_canalorigen": 159460001, "sanic_saldoinicial": 1000, "sanic_montoabonado": 100, "sanic_saldoremanente": 900, "sanic_plazo": 6, "sanic_cuotamensual": 150,
         "sanic_totalpagar": 900, "sanic_fechainicio": "2026-11-01", "sanic_fechafin": "2027-04-01", "sanic_cuentadebitar": "ZZV-cta-0001", "sanic_fechaformalizacion": "2026-10-10",
         "sanic_cantidadreaperturas": 0, "sanic_instantaneacedula": "ZZV-ced-0001", **OWNER,
         f"{nv['sanic_clienteid']}@odata.bind": f"/{ES['cliente']}({cli})", f"{nv['sanic_versionplantillaid']}@odata.bind": f"/{ES['versionplantilla']}({ver})",
         f"{nv['sanic_oficialid']}@odata.bind": f"/systemusers({oficial})"}
    if nombre is not None:
        c["sanic_nombre"] = nombre
    return c


def crear_arreglo_raw(svc, cuerpo, sufijo):
    """POST de Create; devuelve (http, cuerpo_respuesta). Si crea, registra la fila para la limpieza."""
    est, b, h = svc.call("POST", AR, cuerpo, cabeceras=PREFER_REP)
    if est == 201:
        reg70(f"arreglo-{sufijo}", AR, b["sanic_carp_tbl_arregloid"])
    return est, b
