"""V-1 (Update con statecode/statuscode dispara steps de Update; que MessageName ve el plugin), V-2 (Update de columnas que no son estado sobre filas INACTIVAS
como SYSTEM y como administrador) y V-5 (Terminado -> Vigente con un solo Update). Como administrador. Las trazas salen de plugintracelogs (el trace de la org ya
estaba en 'All'; no se cambio). Los datos son ficticios (ZZV-)."""
from vlib import *

dv = adm()
yo = mi_id(dv)
C = Corrida(dv, '30_estado_v1_v2_v5.json')
paso = C.paso


def patch(conj, rid, cuerpo):
    return lambda: dv.call('PATCH', f'{conj}({rid})', cuerpo)[:2]


cli = crear_cliente(dv, "e2")
ver = crear_version(dv, "e2")
c1 = crear_conversacion(dv, "e2a")
c2 = crear_conversacion(dv, "e2b")
a1 = crear_arreglo(dv, "e2", cli, ver, yo)
CV, AR = ES["conversacion"], ES["arreglo"]
rl_c = lambda r: (lambda: estado_fila(dv, CV, r, ",sanic_correonuevoenfinalizada,sanic_detallesinrespuesta"))
rl_a = lambda r: (lambda: estado_fila(dv, AR, r, ",sanic_comentarios,_sanic_conversacionid_value"))
print("filas:", cli, ver, c1, c2, a1)

# ------------------------------------------------------------------ V-1 Conversacion
paso("C0 control: PATCH de columna que NO es estado (detalle) en C2 activa", patch(CV, c2, {"sanic_detallesinrespuesta": "ZZV-c0"}), rl_c(c2))
paso("C1 PATCH statecode=1,statuscode=2 (Finalizada) en C1  [Web API = Xrm.WebApi = formulario]", patch(CV, c1, {"statecode": 1, "statuscode": 2}), rl_c(c1))
# V-2 conversacion: filas inactivas
paso("C2 V-2 admin: PATCH correonuevoenfinalizada=true sobre C1 INACTIVA", patch(CV, c1, {"sanic_correonuevoenfinalizada": True}), rl_c(c1))
paso("C3 V-2 SYSTEM: updbool correonuevoenfinalizada=false sobre C1 INACTIVA", lambda: cmd(dv, cli, "c3", "updbool", "sys", LN["conversacion"], c1, "sanic_correonuevoenfinalizada", "false"), rl_c(c1))
paso("C3b V-2 SYSTEM: updbool =true otra vez (marcar el indicador, RF-15)", lambda: cmd(dv, cli, "c3b", "updbool", "sys", LN["conversacion"], c1, "sanic_correonuevoenfinalizada", "true"), rl_c(c1))
paso("C3c V-2 SYSTEM: upd detalle de C1 INACTIVA", lambda: cmd(dv, cli, "c3c", "upd", "sys", LN["conversacion"], c1, "sanic_detallesinrespuesta", "ZZV-c3c"), rl_c(c1))
paso("C3d V-2 contraste usuario del contexto (admin) via SDK: upd detalle de C1 INACTIVA", lambda: cmd(dv, cli, "c3d", "upd", "usr", LN["conversacion"], c1, "sanic_detallesinrespuesta", "ZZV-c3d"), rl_c(c1))
paso("C4 PATCH reabrir C1: statecode=0,statuscode=1 en UN Update", patch(CV, c1, {"statecode": 0, "statuscode": 1}), rl_c(c1))
paso("C5 SDK SetStateRequest (usuario del contexto = admin) C1 -> Finalizada", lambda: cmd(dv, cli, "c5", "setstate", "usr", LN["conversacion"], c1, "1", "2"), rl_c(c1))
paso("C6 SDK SetStateRequest (SYSTEM) C1 -> Abierta", lambda: cmd(dv, cli, "c6", "setstate", "sys", LN["conversacion"], c1, "0", "1"), rl_c(c1))
paso("C7 SDK Update statecode+statuscode (usuario del contexto) C1 -> Finalizada", lambda: cmd(dv, cli, "c7", "updstate", "usr", LN["conversacion"], c1, "1", "2"), rl_c(c1))
paso("C8 SDK Update statecode+statuscode (SYSTEM) C1 -> Abierta", lambda: cmd(dv, cli, "c8", "updstate", "sys", LN["conversacion"], c1, "0", "1"), rl_c(c1))
paso("C9 PATCH solo statecode=1 en C2 (sin statuscode)", patch(CV, c2, {"statecode": 1}), rl_c(c2))
paso("C10 PATCH solo statecode=0 en C2 (sin statuscode)", patch(CV, c2, {"statecode": 0}), rl_c(c2))
paso("C11 Web API: accion SetState bound (POST conversacion/Microsoft.Dynamics.CRM.SetState)", lambda: dv.call("POST", f"{CV}({c2})/Microsoft.Dynamics.CRM.SetState", {"State": 1, "Status": 2})[:2], rl_c(c2))
paso("C12 Web API: accion SetState no ligada con EntityMoniker", lambda: dv.call("POST", "SetState", {"EntityMoniker": {"@odata.type": "Microsoft.Dynamics.CRM.sanic_carp_tbl_conversacion", "sanic_carp_tbl_conversacionid": c2}, "State": 1, "Status": 2})[:2], rl_c(c2))

# ------------------------------------------------------------------ V-1 / V-2 / V-5 Arreglo
paso("A0 control: PATCH comentarios (NO estado) en A1 (Borrador)", patch(AR, a1, {"sanic_comentarios": "ZZV-a0"}), rl_a(a1))
paso("A1 PATCH solo statuscode=159460001 (Borrador -> Vigente, mismo statecode)", patch(AR, a1, {"statuscode": 159460001}), rl_a(a1))
paso("A2 PATCH statecode=1,statuscode=2 (Terminado)", patch(AR, a1, {"statecode": 1, "statuscode": 2}), rl_a(a1))
paso("A3 V-2 admin: PATCH comentarios sobre A1 INACTIVA (Terminado)", patch(AR, a1, {"sanic_comentarios": "ZZV-a3-admin"}), rl_a(a1))
paso("A4 V-2 SYSTEM: upd comentarios sobre A1 INACTIVA", lambda: cmd(dv, cli, "a4", "upd", "sys", LN["arreglo"], a1, "sanic_comentarios", "ZZV-a4-sys"), rl_a(a1))
paso("A5 V-2 SYSTEM: updref sanic_conversacionid de A1 INACTIVA -> C2 (repuntar, unir)", lambda: cmd(dv, cli, "a5", "updref", "sys", LN["arreglo"], a1, "sanic_conversacionid", LN["conversacion"], c2), rl_a(a1))
paso("A5b V-2 admin Web API: repuntar lookup de A1 INACTIVA -> C1", patch(AR, a1, {f"{navs(dv, LN['arreglo'])['sanic_conversacionid']}@odata.bind": f"/{CV}({c1})"}), rl_a(a1))
paso("A5c V-2 contraste SDK con el usuario del contexto (admin): upd comentarios sobre A1 INACTIVA", lambda: cmd(dv, cli, "a5c", "upd", "usr", LN["arreglo"], a1, "sanic_comentarios", "ZZV-a5c-usr"), rl_a(a1))
paso("A6 V-5 PATCH reabrir Terminado -> Vigente: statecode=0,statuscode=159460001 en UN Update", patch(AR, a1, {"statecode": 0, "statuscode": 159460001}), rl_a(a1))
paso("A7 de nuevo a Terminado", patch(AR, a1, {"statecode": 1, "statuscode": 2}), rl_a(a1))
paso("A8 V-5 variante: PATCH SOLO statecode=0 sobre Terminado (que statuscode queda)", patch(AR, a1, {"statecode": 0}), rl_a(a1))
paso("A9 PATCH statecode=0,statuscode=159460001 (dejarlo Vigente)", patch(AR, a1, {"statecode": 0, "statuscode": 159460001}), rl_a(a1))
paso("A10 V-5 variante SDK: SetStateRequest Terminado (usuario ctx)", lambda: cmd(dv, cli, "a10", "setstate", "usr", LN["arreglo"], a1, "1", "2"), rl_a(a1))
paso("A11 V-5 variante SDK: Update statecode+statuscode juntos (SYSTEM) Terminado -> Vigente", lambda: cmd(dv, cli, "a11", "updstate", "sys", LN["arreglo"], a1, "0", "159460001"), rl_a(a1))
paso("A12 V-5 variante: SetStateRequest a Anulado y reapertura a Vigente por un unico PATCH", lambda: cmd(dv, cli, "a12", "setstate", "sys", LN["arreglo"], a1, "1", "159460002"), rl_a(a1))
paso("A13 PATCH Anulado -> Vigente", patch(AR, a1, {"statecode": 0, "statuscode": 159460001}), rl_a(a1))
C.cerrar()
print("\nFIN")
