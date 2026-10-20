"""Inventario 13.8 (definitivo, NO se revierte): asocia el perfil 'CSP - CARP - Datos sensibles' al team del sistema (default) de la BU y al team Jefatura. Verifica releyendo
y espera a que SPIKE4 (Oficial por el team default, sin perfil propio ni de usuario) lea la cuenta."""
from t2lib import *

dv = adm()
u = noadm()
R = Res("31_asociar_perfil.json")
base = leer_estado()["linea_base"]
pid = base["perfil"]["id"]
jef = base["jefatura"]["teamid"]
a_gen = fila("arreglo-t2-gen")
AR = conjuntos(dv)["arreglo"]
antes = miembros_perfil(dv, pid)
R.v("13.8", "perfil sin miembros antes", antes == {"usuarios": [], "teams": []}, "vacio", antes)
for nombre, tid in (("team default", TEAM_DEF), ("team Jefatura", jef)):
    est, b, _ = dv.call("POST", f"fieldsecurityprofiles({pid})/teamprofiles_association/$ref", {"@odata.id": f"{dv.api}teams({tid})"})
    R.v("13.8", f"asociar el perfil al {nombre}", est in (200, 204), "204", (est, err(b)[:200] if est >= 300 else ""))
despues = miembros_perfil(dv, pid)
R.v("13.8", "releido: el perfil tiene los dos teams y ningun usuario directo", despues["usuarios"] == [] and sorted(despues["teams"]) == sorted([TEAM_DEF, jef]), "2 teams, 0 usuarios", despues)
t = time.time()
lectura = None
for i in range(12):
    est, b, _ = u.call("GET", f"{AR}({a_gen})?$select=sanic_cuentadebitar,sanic_instantaneacedula")
    lectura = {k: b.get(k) for k in ("sanic_cuentadebitar", "sanic_instantaneacedula")} if est == 200 else (est, err(b)[:200])
    print("  intento", i + 1, round(time.time() - t), "s:", lectura)
    if est == 200 and b.get("sanic_cuentadebitar") is not None:
        break
    time.sleep(15)
R.v("13.8", "SPIKE4 (solo por el team default) lee la cuenta a debitar con el perfil asociado al TEAM", isinstance(lectura, dict) and lectura["sanic_cuentadebitar"] == "HUMO-cta-T201", "valor real", lectura, segundos_hasta_verlo=round(time.time() - t))
