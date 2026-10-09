"""Tarea B: crea el rol de prueba 'SPK Oficial Correo' (BU raiz, en la solucion) y le agrega SOLO los privilegios que faltan
(AddPrivilegesRole pisa el alcance de los existentes, asi que nunca se reenvian los que ya estan).
Uso: python3 s21_b_rol.py prvCreateActivity=Local prvReadQueue=Local ...   (sin argumentos: solo relee y muestra el rol)
Relee con RetrieveRolePrivilegesRole y muestra los privilegios agregados respecto de los 9 que trae un rol nuevo."""
import sys
from lib import *
dv = DvAdmin(); est = cargar(); bu = raiz_bu(dv)
NOMBRE = "SPK Oficial Correo"
rol = rol_por_nombre(dv, NOMBRE, bu)
if not rol:
    s, b, _ = dv.call("POST", "roles", {"name": NOMBRE, "description": "Spike 4: rol de prueba del oficial (minimo para enviar con la queue como remitente). Descartable.",
                                         "businessunitid@odata.bind": f"/businessunits({bu})"}, solucion=SOLUCION)
    print("POST rol:", s, "" if s in (200, 204) else b)
    rol = rol_por_nombre(dv, NOMBRE, bu)
assert len(rol) == 1 and rol[0]["name"] == NOMBRE and not rol[0]["ismanaged"], rol
rid = rol[0]["roleid"]; est["rol_oficial"] = rid

def actuales():
    s, b, _ = dv.call("GET", f"roles({rid})/Microsoft.Dynamics.CRM.RetrieveRolePrivilegesRole()" if False else f"RetrieveRolePrivilegesRole(RoleId={rid})")
    assert s == 200, (s, b)
    return {p["PrivilegeName"]: p["Depth"] for p in b["RolePrivileges"]}

antes = actuales()
pedidos = dict(a.split("=") for a in sys.argv[1:])
nuevos = {n: d for n, d in pedidos.items() if n not in antes}
ya = {n: (antes[n], d) for n, d in pedidos.items() if n in antes}
if ya: print("ya estaban (no se tocan):", ya)
if nuevos:
    lista = []
    for n, d in nuevos.items():
        p = privilegio(dv, n); assert len(p) == 1, (n, p)
        assert p[0]["canbe" + d.lower()], f"{n} no admite {d}"
        lista.append({"PrivilegeId": p[0]["privilegeid"], "Depth": d})
    s, b, _ = dv.call("POST", f"roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {"Privileges": lista}, solucion=SOLUCION)
    print("AddPrivilegesRole", list(nuevos), "->", s, "" if s in (200, 204) else b)
despues = actuales()
for n, d in pedidos.items():
    assert despues.get(n) == d or n in ya, f"RELECTURA: {n} no quedo en {d} (esta en {despues.get(n)})"
base = {"prvReadPluginAssembly", "prvReadPluginType", "prvReadSdkMessage", "prvReadSdkMessageProcessingStep",
        "prvReadSdkMessageProcessingStepImage", "prvReadSharePointData", "prvCreateSharePointData", "prvReadSharePointDocument",
        "prvWriteSharePointData"}  # los 9 que trae todo rol nuevo (observado)
print("RELEER privilegios (sin los de base):", {n: d for n, d in sorted(despues.items()) if n not in base})
print("privilegios de base presentes:", sorted(n for n in despues if n in base))
print("cantidad total:", len(despues))
volcar("rol_oficial_privilegios.json", despues)
guardar(est)
