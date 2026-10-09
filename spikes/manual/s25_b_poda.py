"""Tarea B: busca el MINIMO por eliminacion. Para cada privilegio candidato (o cada cambio de profundidad): lo quita (o baja) del rol,
re-asocia el rol al usuario (la cache de privilegios del principal NO se refresca sola en minutos; ver RESULTADO), confirma con una
sonda (alta prohibida en spk_cliente) que la cache tiene el recuento esperado, corre el intento completo y anota el resultado y el
mensaje exacto; luego RESTAURA el privilegio. Uso: python3 s25_b_poda.py nombre1[=ProfundidadAprobar] ...
  - 'prvX'            : quita el privilegio
  - 'prvX:Basic'      : baja la profundidad a Basic (la restaura despues)
"""
import re, subprocess, sys
from lib import *
adm = DvAdmin(); est = cargar(); uid = est["usuario_app_id"]; bu = est["bu_raiz"]; rid = est["rol_oficial"]
def actuales():
    s, b, _ = adm.call("GET", f"RetrieveRolePrivilegesRole(RoleId={rid})"); assert s == 200, b
    return {p["PrivilegeName"]: p["Depth"] for p in b["RolePrivileges"]}
def quitar(n):
    p = privilegio(adm, n)[0]
    s, b, _ = adm.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.RemovePrivilegeRole", {"Privilege": {"@odata.type": "Microsoft.Dynamics.CRM.privilege", "privilegeid": p["privilegeid"]}}); assert s in (200, 204), (s, b)
def poner(n, d):
    p = privilegio(adm, n)[0]
    s, b, _ = adm.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {"Privileges": [{"PrivilegeId": p["privilegeid"], "Depth": d}]}); assert s in (200, 204), (s, b)
def reasociar():
    r = f"{adm.api}roles({rid})"
    adm.call("DELETE", f"systemusers({uid})/systemuserroles_association/$ref?$id={r}")
    s, b, _ = adm.call("POST", f"systemusers({uid})/systemuserroles_association/$ref", {"@odata.id": r}); assert s in (200, 204), (s, b)
def cache_count():
    dv = DvCorreo(); s, b, _ = dv.call("POST", "spk_clientes", {"spk_nombre": "x"})
    m = re.search(r"privilegeCount=(\d+)", str(b)); return int(m.group(1)) if m else None
resumen = []
for arg in sys.argv[1:]:
    if "+" in arg:   # grupo: quita varios a la vez
        grupo = arg.split("+"); antes = actuales(); orig_g = {n: antes[n] for n in grupo}
        for n in grupo: quitar(n)
        esperado = len(antes) - len(grupo); n = None; nueva = ""; orig = None
    else:
        n, _, nueva = arg.partition(":")
        antes = actuales(); assert n in antes, f"{n} no esta en el rol"
        orig = antes[n]
        if nueva:
            quitar(n); poner(n, nueva); esperado = len(antes)
        else:
            quitar(n); esperado = len(antes) - 1
    time.sleep(3); reasociar(); time.sleep(10)
    cc = cache_count()
    print(f"\n##### {arg}: rol con {len(actuales())} privilegios, cache del principal con {cc} (esperado {esperado})", flush=True)
    volcar("rol_oficial_privilegios.json", actuales())
    if cc != esperado:
        time.sleep(60); reasociar(); time.sleep(10); cc = cache_count(); print("   reintento de cache:", cc, flush=True)
    r = subprocess.run([sys.executable, "s23_b_probar.py", f"poda: sin {arg} (rol {len(actuales())} priv, cache {cc})"], capture_output=True, text=True, env={**os.environ, "SPK_NO_GUARDAR_ENVIO": "1"})
    ult = json.loads(open(os.path.join(EVID, "b_intentos.jsonl")).read().strip().splitlines()[-1])
    err = next((p for p in ult["pasos"] if not p["ok"]), None)
    print("   =>", ult["resultado"], "" if not err else f"| fallo en '{err['paso']}': {err['detalle'][:600]}", flush=True)
    resumen.append({"prueba": arg, "cache": cc, "esperado": esperado, "resultado": ult["resultado"], "paso_fallido": err["paso"] if err else None, "mensaje": err["detalle"] if err else None})
    # restaurar
    if n is None:
        for k, d in orig_g.items(): poner(k, d)
        reasociar(); time.sleep(5)
        assert all(actuales().get(k) == d for k, d in orig_g.items()), "no se restauro el grupo"
    else:
        quitar(n) if nueva else None
        poner(n, orig); reasociar(); time.sleep(5)
        assert actuales().get(n) == orig, f"no se restauro {n}"
prev = []
if os.path.exists(os.path.join(EVID, "b_poda.json")): prev = json.load(open(os.path.join(EVID, "b_poda.json")))
volcar("b_poda.json", prev + resumen)
volcar("rol_oficial_privilegios.json", actuales())
print("\nrol restaurado con", len(actuales()), "privilegios")
