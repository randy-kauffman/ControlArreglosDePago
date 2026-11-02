"""Prepara lo que los flujos del spike necesitan, SIN tocar la solución principal:
1) solución sin gestionar `sanic_carp_sol_spikecorreo` con el mismo publisher (si no existe);
2) tres variables de entorno (definición con la herramienta variable_entorno.py y playbooks de esta carpeta, `valor: null`);
3) sus valores (buzón 4, team del área, cola de prueba) cargados desde memoria: las direcciones NO se escriben en el repositorio.
Idempotente. Nada se borra."""
import subprocess
from flib import *

dv = adm()
R = Res("v50_solucion_y_variables.json")
c = Correo()
e = est()
SOL = "sanic_carp_sol_spikecorreo"
RAIZ = os.path.abspath(os.path.join(AQUI_F, "..", ".."))

pub = get(dv, "solutions?$select=_publisherid_value&$filter=uniquename eq 'sanic_carp_sol_controlarreglospago'")["value"][0]["_publisherid_value"]
s = get(dv, f"solutions?$select=solutionid,uniquename,ismanaged&$filter=uniquename eq '{SOL}'")["value"]
if not s:
    st, b, h = dv.call("POST", "solutions", {"uniquename": SOL, "friendlyname": "SOL - CARP - SPK Correo por flujos (spike)",
                                              "description": "Spike F0 de diseno/08: flujos de prueba de correo. Temporal; fuera de la solucion principal.",
                                              "version": "1.0.0.0", "publisherid@odata.bind": f"/publishers({pub})"})
    R.v("F0", "crear la solución del spike", st in (201, 204), "204", st if st in (201, 204) else err(b))
    s = get(dv, f"solutions?$select=solutionid,uniquename,ismanaged&$filter=uniquename eq '{SOL}'")["value"]
R.v("F0", "la solución del spike existe, sin gestionar, aparte de la principal", len(s) == 1 and not s[0]["ismanaged"], "1 fila, ismanaged false", s[0]["solutionid"] if s else None)
poner("solucion_spk", s[0]["solutionid"])

for pb in ("sanic_carp_ev_spkbuzon", "sanic_carp_ev_spkteamarea", "sanic_carp_ev_spkcola", "sanic_carp_ev_spksalida"):
    r = subprocess.run([sys.executable, os.path.join(RAIZ, "herramientas", "construir", "variable_entorno.py"),
                        os.path.join(AQUI_F, "playbooks", pb + ".md")], capture_output=True, text=True, cwd=RAIZ)
    ult = r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-300:]
    R.v("F0", f"variable {pb} (definición)", '"estado": "error"' not in ult and "bloqueado" not in ult, "creado/ya_existia/actualizado", ult[:300])

# valores
team = get(dv, f"teams?$select=name,teamid,_businessunitid_value,teamtype&$filter=teamid eq {TEAM_DEF}")["value"][0]
R.v("F0", "team del área (informativo)", True, "informativo", {"nombre": team["name"], "tipo": team["teamtype"]})
valores = {"sanic_carp_ev_spkbuzon": c.b[4], "sanic_carp_ev_spkteamarea": TEAM_DEF, "sanic_carp_ev_spkcola": e["cola_spk"], "sanic_carp_ev_spksalida": c.b[1]}
for schema, valor in valores.items():
    d = get(dv, f"environmentvariabledefinitions?$select=environmentvariabledefinitionid&$filter=schemaname eq '{schema}'")["value"]
    assert d, schema
    did = d[0]["environmentvariabledefinitionid"]
    v = get(dv, f"environmentvariablevalues?$select=environmentvariablevalueid,value&$filter=_environmentvariabledefinitionid_value eq {did}")["value"]
    if not v:
        st, b, _ = dv.call("POST", "environmentvariablevalues", {"value": valor, "EnvironmentVariableDefinitionId@odata.bind": f"/environmentvariabledefinitions({did})"},
                           solucion=None)
        R.v("F0", f"valor de {schema}", st in (201, 204), "204", st if st in (201, 204) else err(b))
    elif v[0]["value"] != valor:
        st, b, _ = dv.call("PATCH", f"environmentvariablevalues({v[0]['environmentvariablevalueid']})", {"value": valor})
        R.v("F0", f"valor de {schema} corregido", st == 204, "204", st)
    else:
        R.v("F0", f"valor de {schema}", True, "ya cargado", "ya_existia")
