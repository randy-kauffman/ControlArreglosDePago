"""Tanda 3, sin correos nuevos: el Jefe real y las dos API que quedaban.
- 2.4 / H-8 con usuarios reales: User 8 (Oficial) no puede reasignar la conversación A a otro (CARP-CNV-008); User 9 (Jefe, por el
  team Jefatura) sí.
- Unir (gesto 4) como User 8: la conversación nueva del caso C (sin cliente) se une a la A (con cliente). Correos movidos,
  recálculo del destino (cantidad de respuestas procesadas, mínimo del pendiente), el origen queda Unida con el actor.
- H-7 (parcial, sin el flujo 11.2 todavía): reconciliar llamada como la cuenta de servicio de Dev (admin@…, decisión del usuario
  del 2026-10-20) con minutosminimos 0, y con `emailid` explícito de un correo ya registrado (resultado `yatenia`)."""
from t3lib import *

dv = adm()
R = Res("50_jefe_unir_reconciliar.json")
e = est3()
a, k = e["caso_a"], e["caso_c"]
A = a["conversacion_id"]
N = k["nueva"]["salida"]["nuevaconversacionid"]
nav = navs(dv, "sanic_carp_tbl_conversacion")["sanic_oficialasignadoid"]

# Reasignar
cv = conv(dv, A)
if cv["_sanic_oficialasignadoid_value"] == OFICIAL:
    st, b = actualizar(dv, "conversacion", A, {f"{nav}@odata.bind": f"/systemusers({JEFE})"}, cabeceras={"MSCRMCallerID": OFICIAL})
    R.v("2.4", "User 8 (Oficial) no puede reasignar la conversación a otro", codigo(b) == "CARP-CNV-008", "CARP-CNV-008", (st, codigo(b)))
    st, b = actualizar(dv, "conversacion", A, {f"{nav}@odata.bind": f"/systemusers({JEFE})"}, cabeceras={"MSCRMCallerID": JEFE})
    R.v("H-8", "User 9 (Jefe por Jefatura) reasigna la conversación", st == 204, 204, st if st == 204 else (codigo(b), err(b)))
R.v("H-8", "la conversación A queda asignada a User 9", conv(dv, A)["_sanic_oficialasignadoid_value"] == JEFE, JEFE, conv(dv, A)["_sanic_oficialasignadoid_value"])

# Unir N -> A como User 8
antes_a, antes_n = conv(dv, A), conv(dv, N)
if antes_n["statuscode"] == CONV_ABIERTA:
    correos_n = get(dv, f"emails?$select=activityid,actualend&$filter=_regardingobjectid_value eq {N}")["value"]
    st, b, t = llamar(dv, "sanic_carp_capi_unirconversaciones", {"origenid": N, "destinoid": A}, caller=OFICIAL)
    k["unir"] = {"http": st, "segundos": t, "salida": {kk: v for kk, v in b.items() if not kk.startswith("@")} if st == 200 else None, "codigo": codigo(b),
                 "correos_origen": [x["activityid"] for x in correos_n], "pendientes_antes": [antes_a["sanic_sinrespuestadesde"], antes_n["sanic_sinrespuestadesde"]]}
    poner("caso_c", k)
u = k["unir"]
R.v("UNI", "Unir como User 8: 200 y 1 correo movido, 0 arreglos", u["http"] == 200 and u["salida"]["correosmovidos"] == 1 and u["salida"]["arreglosrepunteados"] == 0, "200/1/0", u)
R.v("UNI", "el correo del origen apunta al destino", all(email(dv, x)["_regardingobjectid_value"] == A for x in u["correos_origen"]), A, [email(dv, x)["_regardingobjectid_value"] for x in u["correos_origen"]])
n = conv(dv, N)
R.v("UNI", "origen Unida (1/159460001), unida a A, finalizado por User 8, sin pendiente",
    n["statecode"] == 1 and n["statuscode"] == 159460001 and n["_sanic_unidaaid_value"] == A and n["_sanic_finalizadoporid_value"] == OFICIAL and n["sanic_sinrespuestadesde"] is None,
    "Unida/A/User 8", limpio(n))
d = conv(dv, A)
minimo = min(p for p in u["pendientes_antes"] if p)
R.v("UNI", "destino recalculado: pendiente = mínimo, 1 respuesta procesada, cliente del destino se conserva",
    d["sanic_sinrespuestadesde"] == minimo and d["sanic_cantidadrespuestas"] == 1 and d["_sanic_clienteid_value"] == e["cliente_t3"] and d["statuscode"] == CONV_ABIERTA,
    {"pendiente": minimo, "cantidad": 1, "cliente": e["cliente_t3"]}, {"pendiente": d["sanic_sinrespuestadesde"], "cantidad": d["sanic_cantidadrespuestas"], "cliente": d["_sanic_clienteid_value"]})

# Reconciliar como la cuenta de servicio de Dev
svc = get(dv, "systemusers?$select=systemuserid,isdisabled,accessmode&$filter=startswith(domainname,'admin@')")["value"]
R.v("H-7", "la cuenta de servicio de Dev existe y está habilitada", len(svc) == 1 and not svc[0]["isdisabled"], 1, [{"habilitada": not s["isdisabled"], "accessmode": s["accessmode"]} for s in svc])
sid = svc[0]["systemuserid"]
roles = [r["name"] for r in get(dv, f"systemusers({sid})/systemuserroles_association?$select=name")["value"]]
st, b, t = llamar(dv, "sanic_carp_capi_reconciliarcorreos", {"minutosminimos": 0, "maximo": 20}, caller=sid)
sal = {kk: v for kk, v in b.items() if not kk.startswith("@")} if st == 200 else {"codigo": codigo(b), "error": err(b)}
R.v("H-7", "reconciliar (lote) como la cuenta de servicio: 200 y ningún correo de la BU sin conversación", st == 200 and sal.get("encontrados") == 0, "200/0",
    {"http": st, "segundos": t, "salida": sal, "roles_directos_cuenta": roles})
st, b, t = llamar(dv, "sanic_carp_capi_reconciliarcorreos", {"emailid": a["email_id"]}, caller=sid)
sal = {kk: v for kk, v in b.items() if not kk.startswith("@")} if st == 200 else {"codigo": codigo(b), "error": err(b)}
det = json.loads(sal.get("detalle") or "[]") if st == 200 else []
R.v("H-7", "reconciliar con emailid de un correo ya registrado: yatenia en su conversación",
    st == 200 and det and det[0]["resultado"] == "yatenia" and det[0]["conversacionid"] == A, "yatenia", {"http": st, "salida": sal})
poner("cuenta_servicio_dev", {"systemuserid": sid, "roles_directos": roles})
