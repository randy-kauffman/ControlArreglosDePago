"""Utilidades de los lanzadores del spike de fixtures (2026-10-14). Nunca imprime secretos, tokens ni datos reales.
Reutiliza herramientas/dataverse_api.Dataverse (administrador, PP_CLIENT_*) y herramientas/credenciales.py.
El estado (estado.json) guarda solo GUIDs, conteos y banderas, nunca secretos ni textos reales."""
import json
import os
import subprocess
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "herramientas"))
from dataverse_api import Dataverse  # noqa: E402
from credenciales import leer_secretos  # noqa: E402

ENTORNO = "https://org36e60d9d.crm.dynamics.com"
SOLUCION = "sanic_carp_sol_controlarreglospago"
BU_ID = "2dbc18e7-f9c3-f111-a05a-7ced8d3c456b"        # BU "Arreglos de Pago" (vlib.py)
TEAM_DEF = "2ebc18e7-f9c3-f111-a05a-7ced8d3c456b"     # team por defecto de la BU (vlib.py)
DOTNET = "/home/gmaker/.dotnet/dotnet"
PROYECTO = os.path.join(AQUI, "src", "Zzf.Exportador")
DLL = os.path.join(PROYECTO, "bin", "Release", "net8.0", "Zzf.Exportador.dll")
EVID = os.path.join(AQUI, "evidencia")
SALIDA = os.path.join(AQUI, "salida")
ESTADO = os.path.join(AQUI, "estado.json")
os.makedirs(EVID, exist_ok=True)
os.makedirs(SALIDA, exist_ok=True)

TABLAS = {  # nombre corto -> (logicalname, entity set, primary id)
    "conversacion": ("sanic_carp_tbl_conversacion", "sanic_carp_tbl_conversacions", "sanic_carp_tbl_conversacionid"),
    "arreglo": ("sanic_carp_tbl_arreglo", "sanic_carp_tbl_arreglos", "sanic_carp_tbl_arregloid"),
    "cliente": ("sanic_carp_tbl_cliente", "sanic_carp_tbl_clientes", "sanic_carp_tbl_clienteid"),
    "versionplantilla": ("sanic_carp_tbl_versionplantilla", "sanic_carp_tbl_versionplantillas", "sanic_carp_tbl_versionplantillaid"),
    "cuota": ("sanic_carp_tbl_cuota", "sanic_carp_tbl_cuotas", "sanic_carp_tbl_cuotaid"),
    "generacionformato": ("sanic_carp_tbl_generacionformato", "sanic_carp_tbl_generacionformatos", "sanic_carp_tbl_generacionformatoid"),
    "mapeocelda": ("sanic_carp_tbl_mapeocelda", "sanic_carp_tbl_mapeoceldas", "sanic_carp_tbl_mapeoceldaid"),
    "email": ("email", "emails", "activityid"),
    "environmentvariabledefinition": ("environmentvariabledefinition", "environmentvariabledefinitions", "environmentvariabledefinitionid"),
    "environmentvariablevalue": ("environmentvariablevalue", "environmentvariablevalues", "environmentvariablevalueid"),
}


def adm():
    dv = Dataverse()
    assert dv.org == ENTORNO, "entorno no autorizado"
    return dv


def get(dv, ruta, cabeceras=None):
    est, b, _ = dv.call("GET", ruta, cabeceras=cabeceras)
    assert est == 200, (ruta.split("?")[0], est, str(b)[:200])
    return b


def _get_url(dv, url):
    """GET de una URL absoluta (@odata.nextLink) SIN volver a codificarla."""
    import urllib.request
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + dv._token, "Accept": "application/json", "OData-MaxVersion": "4.0", "OData-Version": "4.0"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return json.loads(r.read().decode())


def paginar(dv, ruta):
    """Todas las filas de una consulta (sigue @odata.nextLink tal cual viene)."""
    est, b, _ = dv.call("GET", ruta)
    assert est == 200, (ruta.split("?")[0], est, str(b)[:200])
    out = list(b["value"])
    while b.get("@odata.nextLink"):
        b = _get_url(dv, b["@odata.nextLink"])
        out += b["value"]
    return out


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


def componentes_solucion(dv):
    sid = get(dv, f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")["value"][0]["solutionid"]
    out = paginar(dv, f"solutioncomponents?$select=solutioncomponentid,componenttype,objectid,rootcomponentbehavior&$filter=_solutionid_value eq {sid}")
    return sid, sorted(out, key=lambda c: (c["componenttype"], c["objectid"]))


def residuales_zzf(dv):
    """Filas ZZF- que existan ahora mismo en cualquiera de las tablas que toca el spike. Devuelve {tabla: [guid]} (solo no vacias)."""
    filtros = {
        "conversacion": "startswith(sanic_huellamessageid,'ZZF-')",
        "arreglo": "startswith(sanic_numeroproducto,'ZZF-')",
        "cliente": "startswith(sanic_nombre,'ZZF-')",
        "versionplantilla": "startswith(sanic_nombre,'ZZF-')",
        "email": "startswith(subject,'ZZF-')",
        "environmentvariabledefinition": "startswith(schemaname,'sanic_zzf')",
        "environmentvariablevalue": "startswith(schemaname,'sanic_zzf')",
    }
    out = {}
    for t, filtro in filtros.items():
        _, conj, pk = TABLAS[t]
        filas = paginar(dv, f"{conj}?$select={pk}&$filter={filtro}")
        if filas:
            out[t] = [f[pk] for f in filas]
    return out


def entorno_hijo():
    """Entorno del proceso hijo dotnet: los secretos viajan SOLO por variables de entorno del proceso (nunca por linea de comandos ni archivo)."""
    c = leer_secretos(requeridas=("DATAVERSE_URL", "PP_CLIENT_ID", "PP_CLIENT_SECRET"))
    env = dict(os.environ)
    env.update({"ZZF_URL": c["DATAVERSE_URL"].rstrip("/"), "ZZF_CLIENT_ID": c["PP_CLIENT_ID"], "ZZF_CLIENT_SECRET": c["PP_CLIENT_SECRET"],
                "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"})
    return env


def correr_hijo(modo):
    """Ejecuta el exportador compilado. Su salida estandar solo trae conteos y nombres logicos (el programa lo garantiza)."""
    env = entorno_hijo()
    p = subprocess.run([DOTNET, DLL, modo], env=env, cwd=AQUI, capture_output=True, text=True, timeout=1800)
    return p.returncode, _redactar(p.stdout, env), _redactar(p.stderr, env)


def _redactar(txt, env):
    """Defensa en profundidad: si por error el hijo imprimiera un secreto, no llega a la consola."""
    for k in ("ZZF_CLIENT_SECRET", "ZZF_CLIENT_ID", "ZZF_URL"):
        if env.get(k):
            txt = txt.replace(env[k], "<redactado>")
    return txt
