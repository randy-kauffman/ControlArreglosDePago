"""Tarea B: deja el rol 'SPK Oficial Correo' EXACTAMENTE con el conjunto minimo hallado (quita sobrantes, agrega faltantes, ajusta
profundidad), re-asocia el rol y corre el intento completo. Guarda el envio como 'email_enviado' para verificarlo con s24."""
import re, subprocess, sys
from lib import *
adm = DvAdmin(); est = cargar(); uid = est["usuario_app_id"]; rid = est["rol_oficial"]
OBJETIVO = {
    "prvCreateActivity": "Basic",        # crear el Email (propio)
    "prvWriteActivity": "Basic",         # SendEmail actualiza el Email propio
    "prvAppendActivity": "Basic",        # el Email (propio) referencia a conversacion, queue y correo padre
    "prvReadActivity": "Local",          # leer el correo entrante al que responde (de otro propietario)
    "prvAppendToActivity": "Local",      # parentactivityid: el correo entrante recibe un hijo
    "prvReadQueue": "Local",             # la queue remitente
    "prvAppendToQueue": "Local",         # from = queue
    "prvAppendTospk_conversacion": "Local",   # Regarding a la conversacion
    "prvReadspk_conversacion": "Local",  # no lo pide la API, si la interfaz (formulario y timeline de la conversacion)
}
def actuales():
    s, b, _ = adm.call("GET", f"RetrieveRolePrivilegesRole(RoleId={rid})"); assert s == 200, b
    return {p["PrivilegeName"]: p["Depth"] for p in b["RolePrivileges"]}
act = actuales()
for n in [n for n in act if n not in OBJETIVO]:
    p = privilegio(adm, n)[0]
    s, b, _ = adm.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.RemovePrivilegeRole", {"Privilege": {"@odata.type": "Microsoft.Dynamics.CRM.privilege", "privilegeid": p["privilegeid"]}}); assert s in (200, 204), (n, s, b)
for n, d in OBJETIVO.items():
    if act.get(n) != d:
        p = privilegio(adm, n)[0]
        if n in act:
            adm.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.RemovePrivilegeRole", {"Privilege": {"@odata.type": "Microsoft.Dynamics.CRM.privilege", "privilegeid": p["privilegeid"]}})
        s, b, _ = adm.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {"Privileges": [{"PrivilegeId": p["privilegeid"], "Depth": d}]}, solucion=SOLUCION); assert s in (200, 204), (n, s, b)
for _ in range(12):
    fin = actuales()
    if fin == OBJETIVO: break
    time.sleep(5)
assert fin == OBJETIVO, (fin, OBJETIVO)
print("RELEER rol final:", json.dumps(fin, indent=1)); volcar("rol_oficial_privilegios.json", fin)
r = f"{adm.api}roles({rid})"
adm.call("DELETE", f"systemusers({uid})/systemuserroles_association/$ref?$id={r}"); s, b, _ = adm.call("POST", f"systemusers({uid})/systemuserroles_association/$ref", {"@odata.id": r}); assert s in (200, 204)
time.sleep(10)
dv = DvCorreo(); s, b, _ = dv.call("POST", "spk_clientes", {"spk_nombre": "x"}); print("cache del principal:", re.search(r"privilegeCount=(\d+)", str(b)).group(1), "(rol:", len(fin), ")")
r = subprocess.run([sys.executable, "s23_b_probar.py", "FINAL: conjunto minimo (9 privilegios)"], capture_output=True, text=True)
print(r.stdout[-900:])
