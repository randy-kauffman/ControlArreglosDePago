"""Lote B, prueba funcional de los gestos que NO necesitan navegador: llama a las mismas Custom API y escribe los mismos payloads que arma
webresources/js/comandos.js (ejecutarApi = POST de la accion global con Edm.Guid; escribirGesto = UN PATCH con solo las columnas del gesto).
Caller = el usuario administrador de Dev (el mismo de las tandas 1 y 2). Todo dato es HUMO-. Cada fila creada se registra en lote-b/estado.json."""
import base64
from blib import *

dv = adm()
YO = get(dv, "WhoAmI")["UserId"]
CV, CL, AR, CU, VC = (conjuntos(dv)[k] for k in ("conversacion", "cliente", "arreglo", "cuota", "version"))
cli = fila_de("tanda1", "cliente-01")
R = Res("10_gestos.json")
LOG = []   # (gesto, payload_js, respuesta)
API = lambda n: "sanic_carp_capi_" + n


def api(gesto, nombre, params):
    """Igual que SanicCarp.Comun.ejecutarApi: POST de la accion global con los Guid."""
    est, b, seg = llamar(dv, API(nombre), params)
    resp = sin_odata(b) if est == 200 else {"error": err(b)}
    if est == 200 and "archivo" in resp:
        resp = dict(resp, archivo=f"<base64 {len(resp['archivo'])} caracteres>")
    LOG.append({"gesto": gesto, "tipo": "customapi", "api": API(nombre), "payload": params, "http": est, "respuesta": resp, "seg": seg})
    volcar("10_gestos_log.json", LOG)
    return est, b, seg


def escribir(gesto, tabla_k, rid, datos):
    """Igual que SanicCarp.Comun.escribirGesto: UN updateRecord (PATCH) con solo las columnas del gesto."""
    est, b = actualizar(dv, tabla_k, rid, datos)
    LOG.append({"gesto": gesto, "tipo": "updateRecord", "tabla": T[tabla_k], "payload": datos, "http": est, "respuesta": ({"error": err(b)} if est >= 300 else "204")})
    volcar("10_gestos_log.json", LOG)
    return est, b


def corto(est, b):
    return (est, codigo(b), err(b)[:240] if est >= 300 else "")


BASE_CV = {"sanic_fechaprimercorreo": "2026-10-20T10:00:00Z", "sanic_correonuevoenfinalizada": False, "sanic_cantidadrespuestas": 0, **OWNER,
           "sanic_clienteid@odata.bind": f"/{CL}({cli})"}


def conv(clave, huella, **extra):
    est, b, rid = crear(dv, clave, "conversacion", dict(BASE_CV, sanic_huellamessageid=huella + "-b", **extra), "lote B")
    assert est == 201, (clave, est, err(b))
    return rid


CV_COLS = "statecode,statuscode,sanic_resultado,sanic_motivosinrespuesta,sanic_detallesinrespuesta,_sanic_oficialasignadoid_value,_sanic_finalizadoporid_value,sanic_fechafinalizacion,_sanic_unidaaid_value,sanic_correonuevoenfinalizada"

# =========================================================================================================== CONVERSACION
c1 = conv("lb-conv-1", "HUMO-lb-001")
# Gesto 1 Tomar
est, b, seg = api("1 Tomar", "tomarconversacion", {"conversacionid": c1})
r = snap(dv, "conversacion", c1, CV_COLS)
R.v("G1 Tomar", "Custom API tomarconversacion", est == 200 and b.get("oficialasignadoid") == YO and b.get("yaasignada") is False and r["_sanic_oficialasignadoid_value"] == YO,
    "200, oficial=yo, yaasignada=false", (corto(est, b), sin_odata(b) if est == 200 else "", seg))
est, b, seg = api("1 Tomar (otra vez)", "tomarconversacion", {"conversacionid": c1})
R.v("G1 Tomar", "segunda vez: idempotente (yaasignada=true; el JS muestra 'Ya era suya.')", est == 200 and b.get("yaasignada") is True, "200 yaasignada=true", (corto(est, b), b.get("yaasignada") if est == 200 else ""))

# Gesto 2 Finalizar (payload: statecode 1, statuscode 2, resultado, motivo y detalle solo si no estan vacios)
datos = {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460003, "sanic_motivosinrespuesta": 159460002, "sanic_detallesinrespuesta": "HUMO lote B: sin respuesta"}
est, b = escribir("2 Finalizar (sin respuesta + motivo + detalle)", "conversacion", c1, datos)
r = snap(dv, "conversacion", c1, CV_COLS)
R.v("G2 Finalizar", "updateRecord con resultado, motivo y detalle", est == 204 and r["statecode"] == 1 and r["statuscode"] == 2 and r["sanic_resultado"] == 159460003 and r["_sanic_finalizadoporid_value"] == YO and r["sanic_fechafinalizacion"],
    "204, Finalizada 1/2, resultado 159460003, finalizadopor=yo, fecha", (corto(est, b), r))
# Gesto 3 Reabrir (payload: SOLO el estado)
est, b = escribir("3 Reabrir conversacion", "conversacion", c1, {"statecode": 0, "statuscode": 1})
r = snap(dv, "conversacion", c1, CV_COLS)
R.v("G3 Reabrir", "updateRecord con SOLO statecode/statuscode: queda Abierta y limpia resultado y finalizacion", est == 204 and r["statecode"] == 0 and r["statuscode"] == 1 and r["sanic_resultado"] is None and r["sanic_fechafinalizacion"] is None and r["_sanic_finalizadoporid_value"] is None,
    "204, Abierta, resultado/fecha/finalizadopor nulos", (corto(est, b), r))

# Gesto 2 variante: solo resultado (motivo y detalle vacios no se mandan)
c2 = conv("lb-conv-2", "HUMO-lb-002")
est, b = escribir("2 Finalizar (con arreglo, sin motivo ni detalle)", "conversacion", c2, {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460001})
r = snap(dv, "conversacion", c2, CV_COLS)
R.v("G2 Finalizar", "variante: solo resultado Con arreglo de pago (motivo y detalle no se mandan)", est == 204 and r["statecode"] == 1 and r["sanic_resultado"] == 159460001, "204, Finalizada", (corto(est, b), r))
# Gesto 2 sin resultado: el payload del JS manda sanic_resultado:null
c2b = conv("lb-conv-2b", "HUMO-lb-002b")
est, b = escribir("2 Finalizar (sin resultado: sanic_resultado null)", "conversacion", c2b, {"statecode": 1, "statuscode": 2, "sanic_resultado": None})
R.v("G2 Finalizar", "sin resultado (el JS manda sanic_resultado:null): el servidor lo rechaza con codigo del catalogo", est == 400 and codigo(b) is not None, "400 CARP-xxx", corto(est, b))

# Gesto 5 Iniciar conversacion nueva: la Finalizada con el indicador de correo nuevo (lo prende el plugin al llegar un correo; aqui se prueba si se puede sembrar)
c3 = conv("lb-conv-3", "HUMO-lb-003")
est, b = actualizar(dv, "conversacion", c3, {"sanic_correonuevoenfinalizada": True})
R.v("G5 Iniciar nueva", "preparacion: sembrar sanic_correonuevoenfinalizada=true en una Abierta", True, "informativo", corto(est, b))
est, b = escribir("2 Finalizar (preparacion de iniciar nueva)", "conversacion", c3, {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460001})
est, b, seg = api("5 Iniciar conversacion nueva", "iniciarconversacionnueva", {"conversacionid": c3})
R.v("G5 Iniciar nueva", "NO PROBADO el camino feliz: el indicador lo prende solo el plugin al llegar un correo real (sembrarlo da CARP-CNV-001) y no se crean correos. Se confirma el contrato del rechazo: Finalizada sin correos nuevos", est == 400 and codigo(b) == "CARP-INI-003", "400 CARP-INI-003", (corto(est, b), seg))
if est == 200 and b.get("nuevaconversacionid"):
    registrar("lb-conv-nueva", CV, b["nuevaconversacionid"], "creada por iniciarconversacionnueva")
    R.v("G5 Iniciar nueva", "el id de la conversacion nueva existe y esta Abierta", snap(dv, "conversacion", b["nuevaconversacionid"], CV_COLS)["statecode"] == 0, "Abierta", snap(dv, "conversacion", b["nuevaconversacionid"], CV_COLS))

# Gesto 4 Unir
u1, u2 = conv("lb-conv-u1", "HUMO-lb-u1"), conv("lb-conv-u2", "HUMO-lb-u2")
est, b, seg = api("4 Unir", "unirconversaciones", {"origenid": u1, "destinoid": u2})
r = snap(dv, "conversacion", u1, CV_COLS)
R.v("G4 Unir", "Custom API unirconversaciones (dos conversaciones HUMO): origen Unida", est == 200 and r["statecode"] == 1 and r["_sanic_unidaaid_value"] == u2, "200, origen 1/Unida, unidaaid=destino",
    (corto(est, b), sin_odata(b) if est == 200 else "", {k: r[k] for k in ("statecode", "statuscode", "_sanic_unidaaid_value")}, seg))

# =========================================================================================================== ARREGLO
OFICIAL = {"sanic_oficialid@odata.bind": f"/systemusers({YO})"}


def cuerpo(num, saldo, abonado, cuota, plazo, cta):
    return {"sanic_producto": PROD_PRESTAMO, "sanic_numeroproducto": num, "sanic_tipo": TIPO_TEMPORAL, "sanic_moneda": MONEDA_COR, "sanic_saldoinicial": saldo,
            "sanic_montoabonado": abonado, "sanic_plazo": plazo, "sanic_cuotamensual": cuota, "sanic_fechainicio": "2026-11-05", "sanic_cuentadebitar": cta,
            "sanic_fechaformalizacion": "2026-10-20", **OWNER, "sanic_clienteid@odata.bind": f"/{CL}({cli})", **OFICIAL}


def alta_arr(clave, num, saldo, cuota, plazo):
    est, b, rid = crear(dv, clave, "arreglo", cuerpo(num, saldo, 0, cuota, plazo, "HUMO-" + num[-4:]), "lote B")
    assert est == 201, (clave, est, err(b))
    return rid


AR_COLS = "statecode,statuscode,sanic_fechacongelamiento,sanic_numeroprovisional,sanic_numeroarreglo,sanic_instantaneanombre,sanic_cantidadreaperturas,sanic_motivodescarte,sanic_motivoanulacion,sanic_motivoreapertura,sanic_fechatermino,_sanic_reabiertoporid_value"
XLSX = []


def generar(gesto, arr, extra=None, etiqueta=None):
    params = {"arregloid": arr}
    params.update(extra or {})
    est, b, seg = api(gesto, "generarformato", params)
    guardado = None
    if est == 200 and b.get("archivo"):
        datos = base64.b64decode(b["archivo"])
        guardado = f"{etiqueta or 'formato'}_{b.get('nombrearchivo')}"
        with open(os.path.join(EVID, guardado), "wb") as f:
            f.write(datos)
        XLSX.append((guardado, len(datos), datos[:2] == b"PK"))
    return est, b, seg, guardado


a1 = alta_arr("lb-arr-1", "HUMO-prod-LB01", 1000, 300, 4)
a2 = alta_arr("lb-arr-2", "HUMO-prod-LB02", 1000, 300, 4)
a3 = alta_arr("lb-arr-3", "HUMO-prod-LB03", 1000, 300, 4)
a4 = alta_arr("lb-arr-4", "HUMO-prod-LB04", 1000, 500, 2)

# Gesto 6 Generar formato (Borrador sin congelar: congela)
est, b, seg, arch = generar("6 Generar formato (Borrador)", a1, etiqueta="g6_borrador")
r = snap(dv, "arreglo", a1, AR_COLS)
R.v("G6 Generar", "Custom API generarformato sobre un Borrador: devuelve el xlsx y congela", est == 200 and bool(arch) and r["sanic_fechacongelamiento"] and r["sanic_numeroprovisional"],
    "200, archivo .xlsx (PK), congelado con numero provisional", (corto(est, b), {k: v for k, v in sin_odata(b).items() if k != "archivo"} if est == 200 else "", arch, XLSX[-1] if XLSX else None, {k: r[k] for k in ("statuscode", "sanic_fechacongelamiento", "sanic_numeroprovisional")}, seg), segundos=seg)
# Gesto 7 Desbloquear (SOLO sanic_fechacongelamiento = null)
est, b = escribir("7 Desbloquear", "arreglo", a1, {"sanic_fechacongelamiento": None})
r = snap(dv, "arreglo", a1, AR_COLS)
R.v("G7 Desbloquear", "updateRecord con SOLO sanic_fechacongelamiento:null", est == 204 and r["sanic_fechacongelamiento"] is None and r["sanic_numeroprovisional"] is None and r["sanic_instantaneanombre"] is None,
    "204, descongelado, instantaneas y provisional nulos", (corto(est, b), r))
est, b = escribir("7 Desbloquear (sobre Borrador ya sin congelar)", "arreglo", a1, {"sanic_fechacongelamiento": None})
R.v("G7 Desbloquear", "repetido sobre un Borrador sin congelar: lo rechaza el servidor (CARP-ARR-017, lo que el JS muestra tal cual)", est == 400 and codigo(b) == "CARP-ARR-017", "400 CARP-ARR-017", corto(est, b))
# Generar otra vez y Formalizar (SOLO el estado Vigente)
est, b, seg, arch = generar("6 Generar formato (Borrador, de nuevo)", a1, etiqueta="g6_borrador2")
R.v("G6 Generar", "segunda generacion del mismo Borrador tras desbloquear", est == 200 and bool(arch), "200 + xlsx", (corto(est, b), arch))
est, b = escribir("8 Formalizar", "arreglo", a1, {"statecode": 0, "statuscode": ARR_VIGENTE})
r = snap(dv, "arreglo", a1, AR_COLS)
R.v("G8 Formalizar", "updateRecord con SOLO statecode 0 / statuscode Vigente: queda Vigente con numero de arreglo y congelado", est == 204 and r["statuscode"] == ARR_VIGENTE and r["sanic_numeroarreglo"] is not None,
    "204, Vigente, numeroarreglo asignado", (corto(est, b), r))
est, b, seg, arch = generar("6 Generar formato (Vigente congelado)", a1, etiqueta="g6_vigente")
R.v("G6 Generar", "sobre un Vigente congelado: genera sin cambiar el arreglo", est == 200 and bool(arch) and snap(dv, "arreglo", a1, AR_COLS)["statuscode"] == ARR_VIGENTE, "200 + xlsx, sigue Vigente", (corto(est, b), arch))

# Gesto 9 Descartar (statecode 1, statuscode Descartado, motivo)
est, b = escribir("9 Descartar (motivo vacio: el JS manda null)", "arreglo", a2, {"statecode": 1, "statuscode": ARR_DESCARTADO, "sanic_motivodescarte": None})
R.v("G9 Descartar", "motivo vacio: el servidor lo rechaza con CARP-ARR-013", est == 400 and codigo(b) == "CARP-ARR-013", "400 CARP-ARR-013", corto(est, b))
est, b = escribir("9 Descartar", "arreglo", a2, {"statecode": 1, "statuscode": ARR_DESCARTADO, "sanic_motivodescarte": "HUMO lote B: descarte"})
r = snap(dv, "arreglo", a2, AR_COLS)
R.v("G9 Descartar", "updateRecord con estado Descartado + motivo: queda Descartado sin consumir numero", est == 204 and r["statecode"] == 1 and r["statuscode"] == ARR_DESCARTADO and r["sanic_numeroarreglo"] is None, "204, Descartado 1/159460003, sin numero", (corto(est, b), r))

# Gesto 10 Anular (Vigente -> Anulado + motivo)
generar("6 Generar formato (preparacion de anular)", a3, etiqueta="g6_a3")
escribir("8 Formalizar (preparacion de anular)", "arreglo", a3, {"statecode": 0, "statuscode": ARR_VIGENTE})
est, b = escribir("10 Anular (motivo vacio: el JS manda null)", "arreglo", a3, {"statecode": 1, "statuscode": ARR_ANULADO, "sanic_motivoanulacion": None})
R.v("G10 Anular", "motivo vacio: el servidor lo rechaza con CARP-ARR-014", est == 400 and codigo(b) == "CARP-ARR-014", "400 CARP-ARR-014", corto(est, b))
est, b = escribir("10 Anular", "arreglo", a3, {"statecode": 1, "statuscode": ARR_ANULADO, "sanic_motivoanulacion": "HUMO lote B: anulacion"})
r = snap(dv, "arreglo", a3, AR_COLS)
R.v("G10 Anular", "updateRecord con estado Anulado + motivo", est == 204 and r["statecode"] == 1 and r["statuscode"] == ARR_ANULADO and r["sanic_motivoanulacion"], "204, Anulado 1/159460002", (corto(est, b), r))
est, b, seg, arch = generar("6 Generar formato (Anulado)", a3, etiqueta="g6_anulado")
R.v("G6 Generar", "sobre un Anulado: el boton esta siempre disponible; el servidor decide", est in (200, 400), "informativo", (corto(est, b), arch))

# Gesto 12 Registrar pago (guardar nativo de la cuota) + Gesto 11 Reabrir
cu = get(dv, f"{CU}?$select=sanic_carp_tbl_cuotaid,sanic_numero&$filter=_sanic_arregloid_value eq {a4}&$orderby=sanic_numero asc")["value"]
generar("6 Generar formato (preparacion a4)", a4, etiqueta="g6_a4")
escribir("8 Formalizar (preparacion a4)", "arreglo", a4, {"statecode": 0, "statuscode": ARR_VIGENTE})
for i, c in enumerate(cu):
    cid = c["sanic_carp_tbl_cuotaid"]
    registrar(f"lb-cuota-{i + 1}-de-arr4", CU, cid, "cuota generada por el servidor; el lote B la modifica (pago)")
    datos = {"sanic_fechapago": f"2026-1{i + 1}-05", "sanic_montopagado": 500}
    est, b, _ = dv.call("PATCH", f"{CU}({cid})", datos)
    LOG.append({"gesto": "12 Registrar pago (guardar nativo de la cuota)", "tipo": "PATCH", "tabla": "sanic_carp_tbl_cuota", "payload": datos, "http": est, "respuesta": "204" if est == 204 else {"error": err(b)}})
    volcar("10_gestos_log.json", LOG)
    R.v("G12 Registrar pago", f"cuota {i + 1}: fecha de pago + monto pagado juntos", est == 204, 204, corto(est, b))
r = snap(dv, "arreglo", a4, AR_COLS)
R.v("G12 Registrar pago", "pagada la ultima cuota el arreglo pasa a Terminado", r["statecode"] == 1 and r["statuscode"] == ARR_TERMINADO, "Terminado 1/2", r)

# Reabrir (jefatura). Vigente congelado: SOLO el motivo. Terminado: Vigente + motivo.
est, b = escribir("11 Reabrir arreglo (Vigente congelado: solo el motivo)", "arreglo", a1, {"sanic_motivoreapertura": "HUMO lote B: reapertura"})
r = snap(dv, "arreglo", a1, AR_COLS)
R.v("G11 Reabrir arreglo", "Vigente congelado, SOLO el motivo, con el usuario administrador", est == 204 and r["sanic_fechacongelamiento"] is None and r["sanic_cantidadreaperturas"] == 1, "204, descongelado, reaperturas 1", (corto(est, b), r))
est, b = escribir("11 Reabrir arreglo (Terminado: Vigente + motivo)", "arreglo", a4, {"sanic_motivoreapertura": "HUMO lote B: reapertura", "statecode": 0, "statuscode": ARR_VIGENTE})
r = snap(dv, "arreglo", a4, AR_COLS)
R.v("G11 Reabrir arreglo", "Terminado -> Vigente + motivo, con el usuario administrador", est == 204 and r["statecode"] == 0 and r["statuscode"] == ARR_VIGENTE and r["sanic_fechatermino"] is None, "204, Vigente", (corto(est, b), r))

# =========================================================================================================== PLANTILLA
v95 = alta_version(dv, "v95")
n_map = len(get(dv, f"{conjuntos(dv)['mapeo']}?$select=sanic_nombre&$filter=_sanic_versionplantillaid_value eq {v95}&$top=100")["value"])
R.v("G13 Publicar", "preparacion: v95 Borrador con archivo y 28 mapeos", n_map == 28, 28, n_map)
# Gesto 14 Vista previa: generarformato con arregloid + versionpreviaid (no escribe nada)
antes = snap(dv, "arreglo", a3, AR_COLS)
est, b, seg, arch = generar("14 Vista previa", a3, {"versionpreviaid": v95}, etiqueta="g14_vistaprevia")
despues = snap(dv, "arreglo", a3, AR_COLS)
R.v("G14 Vista previa", "generarformato con versionpreviaid de un Borrador: devuelve el xlsx y no cambia el arreglo", est == 200 and bool(arch) and antes == despues, "200 + xlsx, arreglo igual", (corto(est, b), arch, "arreglo sin cambios" if antes == despues else "ARREGLO CAMBIO"), segundos=seg)
# Gesto 13 Publicar (SOLO sanic_estado = Vigente)
est, b = escribir("13 Publicar version", "version", v95, {"sanic_estado": VER_VIGENTE})
r95 = snap(dv, "version", v95, "sanic_estado,statecode,sanic_huella,sanic_fechaaprobacion,_sanic_aprobadoporid_value")
r91 = snap(dv, "version", fila_de("tanda1", "version-v91"), "sanic_estado,statecode")
R.v("G13 Publicar", "updateRecord con SOLO sanic_estado=Vigente: v95 Vigente con huella, la anterior pasa a Historica", est == 204 and r95["sanic_estado"] == VER_VIGENTE and r95["sanic_huella"] and r91["sanic_estado"] == VER_HISTORICA,
    "204, v95 Vigente, v91 Historica", (corto(est, b), r95, r91))
registrar_cambio("version-v91", VC, fila_de("tanda1", "version-v91"), "lote B: pasa de Vigente a Historica al publicar v95")
est, b, seg, arch = generar("6 Generar formato (con la v95 Vigente)", a3, etiqueta="g6_v95")
R.v("G6 Generar", "tras publicar, generarformato usa la Vigente v95", est == 200 and bool(arch), "200 + xlsx", (corto(est, b), arch))

R.v("xlsx", "los archivos generados son zip/OOXML validos (cabecera PK)", all(x[2] for x in XLSX) and len(XLSX) >= 5, "todos PK", XLSX)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
