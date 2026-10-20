"""H-29 (Custom API dentro del sandbox: identidad directa vs CallerId, parametros de salida, contexto en el trace log) con tomarconversacion,
iniciarconversacionnueva, reconciliarcorreos y unirconversaciones (generarformato va mas adelante: necesita un arreglo). Datos HUMO-.
CallerId = Randy Kauffman DEV (humano, administrador del sistema); solo se usa como cabecera MSCRMCallerID, no se modifica el usuario."""
import uuid
from hlib import *

RANDY = "12b85b09-58f3-f011-8407-000d3a316609"
dv = adm()
R = Res("11_h29_apis.json")
YO = get(dv, "WhoAmI")["UserId"]
t0 = reloj(dv) - timedelta(seconds=3)
CV, CL = conjuntos(dv)["conversacion"], conjuntos(dv)["cliente"]
cli = fila("cliente-01")
c1, c0, c4, c777 = fila("conv-01"), fila("conv-aux-unida"), fila("conv-04-poblada"), fila("conv-777777")
x_null, x_vacio = fila("conv-x-null"), fila("conv-x-vacio")
API = lambda n: "sanic_carp_capi_" + n
salidas = {}


def nombre_de(uid):
    return get(dv, f"systemusers({uid})?$select=fullname")["fullname"]


def llama(tag, api, cuerpo, caller=None):
    est, b, seg = llamar(dv, API(api), cuerpo, caller)
    salidas[tag] = {"http": est, "seg": seg, "cuerpo": {k: v for k, v in (b or {}).items() if not k.startswith("@odata")} if est == 200 else err(b)}
    return est, b, seg


def tipos(b):
    return {k: type(v).__name__ for k, v in b.items() if not k.startswith("@odata")}


# ---------------------------------------------------------------- tomarconversacion
est, b, _ = llama("tom_sin_id", "tomarconversacion", {})
R.v("H-29", "tomar sin conversacionid: CARP-TOM-001", est == 400 and codigo(b) == "CARP-TOM-001", "400 TOM-001", (est, codigo(b), err(b)[:120]))
est, b, _ = llama("tom_inexistente", "tomarconversacion", {"conversacionid": str(uuid.uuid4())})
R.v("H-29", "tomar con id inexistente: CARP-TOM-002", est == 400 and codigo(b) == "CARP-TOM-002", "400 TOM-002", (est, codigo(b)))
est, b, seg = llama("tom_directo", "tomarconversacion", {"conversacionid": x_null})
ok = est == 200 and b.get("oficialasignadoid") == YO and b.get("yaasignada") is False
R.v("H-29", "tomar DIRECTO (admin): oficial = el llamador; salidas Guid/String/Boolean", ok, "oficial=yo, yaasignada=false", (est, codigo(b), b.get("oficialasignadoid") == YO, tipos(b) if est == 200 else err(b), seg))
est, b, _ = llama("tom_directo_2", "tomarconversacion", {"conversacionid": x_null})
R.v("H-29", "tomar de nuevo (idempotente): yaasignada=true", est == 200 and b.get("yaasignada") is True, "yaasignada=true", (est, b.get("yaasignada"), codigo(b)))
est, b, seg = llama("tom_callerid", "tomarconversacion", {"conversacionid": x_vacio}, caller=RANDY)
nombre_randy = nombre_de(RANDY)
R.v("H-29", "tomar con CallerId=Randy: oficial asignado = InitiatingUserId (Randy, no el app user)", est == 200 and b.get("oficialasignadoid") == RANDY,
    "oficialasignadoid=Randy", (est, codigo(b), "Randy" if b.get("oficialasignadoid") == RANDY else ("app user" if b.get("oficialasignadoid") == YO else b.get("oficialasignadoid")), b.get("oficialnombre") if est == 200 else err(b)[:120]))
est, b, _ = llama("tom_otro_oficial", "tomarconversacion", {"conversacionid": c4}, caller=RANDY)
R.v("H-29", "tomar una conversacion ya tomada por otro (c4 la tiene el app user): CARP-TOM-004 con su nombre", est == 400 and codigo(b) == "CARP-TOM-004", "400 TOM-004", (est, codigo(b), err(b)[:200]))

# finalizar c777 (persona) para los rechazos de estado
est, b = actualizar(dv, "conversacion", c777, {"statecode": 1, "statuscode": 2, "sanic_resultado": 159460001})
R.v("H-29", "preparacion: Finalizar c777 (ConArregloDePago)", est == 204, 204, (est, codigo(b), err(b)[:150] if est != 204 else ""))
est, b, _ = llama("tom_finalizada", "tomarconversacion", {"conversacionid": c777})
R.v("H-29", "tomar una Finalizada: CARP-TOM-003", est == 400 and codigo(b) == "CARP-TOM-003", "400 TOM-003", (est, codigo(b)))

# ---------------------------------------------------------------- iniciarconversacionnueva
est, b, _ = llama("ini_sin_id", "iniciarconversacionnueva", {})
R.v("H-29", "iniciar sin id: CARP-INI-001", est == 400 and codigo(b) == "CARP-INI-001", "400 INI-001", (est, codigo(b)))
est, b, _ = llama("ini_inexistente", "iniciarconversacionnueva", {"conversacionid": str(uuid.uuid4())})
R.v("H-29", "iniciar con id inexistente: CARP-INI-002", est == 400 and codigo(b) == "CARP-INI-002", "400 INI-002", (est, codigo(b)))
est, b, _ = llama("ini_abierta", "iniciarconversacionnueva", {"conversacionid": c1})
R.v("H-29", "iniciar sobre una Abierta DIRECTO: CARP-INI-003", est == 400 and codigo(b) == "CARP-INI-003", "400 INI-003", (est, codigo(b)))
est, b, _ = llama("ini_abierta_callerid", "iniciarconversacionnueva", {"conversacionid": c1}, caller=RANDY)
R.v("H-29", "iniciar sobre una Abierta con CallerId: CARP-INI-003", est == 400 and codigo(b) == "CARP-INI-003", "400 INI-003", (est, codigo(b)))
est, b, _ = llama("ini_finalizada_sin_indicador", "iniciarconversacionnueva", {"conversacionid": c777})
R.v("H-29", "iniciar sobre una Finalizada sin indicador: CARP-INI-003", est == 400 and codigo(b) == "CARP-INI-003", "400 INI-003", (est, codigo(b)))

# ---------------------------------------------------------------- reconciliarcorreos
est, b, _ = llama("rec_max_neg", "reconciliarcorreos", {"maximo": -1})
R.v("H-29", "reconciliar maximo=-1: CARP-REC-001", est == 400 and codigo(b) == "CARP-REC-001", "400 REC-001", (est, codigo(b)))
est, b, _ = llama("rec_min_neg", "reconciliarcorreos", {"minutosminimos": -5})
R.v("H-29", "reconciliar minutosminimos=-5: CARP-REC-002", est == 400 and codigo(b) == "CARP-REC-002", "400 REC-002", (est, codigo(b)))
est, b, _ = llama("rec_email_inexistente", "reconciliarcorreos", {"emailid": str(uuid.uuid4())})
R.v("H-29", "reconciliar con emailid inexistente: CARP-REC-003", est == 400 and codigo(b) == "CARP-REC-003", "400 REC-003", (est, codigo(b)))
est, b, seg = llama("rec_directo", "reconciliarcorreos", {"maximo": 1, "minutosminimos": 0})
R.v("H-29", "reconciliar DIRECTO maximo=1 (0 candidatos en la BU del area): salidas Integer/Boolean/String", est == 200 and b.get("encontrados") == 0 and b.get("haymas") is False and b.get("detalle") == "[]",
    "encontrados=0, haymas=false, detalle='[]'", (est, codigo(b), tipos(b) if est == 200 else err(b), {k: b.get(k) for k in ("encontrados", "registrados", "conerror", "haymas", "detalle")} if est == 200 else "", seg))
est, b, _ = llama("rec_callerid", "reconciliarcorreos", {"maximo": 1, "minutosminimos": 0}, caller=RANDY)
R.v("H-29", "reconciliar con CallerId", est == 200 and b.get("encontrados") == 0, "encontrados=0", (est, codigo(b), err(b)[:150] if est != 200 else ""))

# ---------------------------------------------------------------- unirconversaciones (dos conversaciones HUMO, sin correos ni arreglos)
BASE = {"sanic_fechaprimercorreo": "2026-10-20T10:00:00Z", "sanic_correonuevoenfinalizada": False, "sanic_cantidadrespuestas": 0, **OWNER, "sanic_clienteid@odata.bind": f"/{CL}({cli})"}
ids = {}
for k, h in (("u1", "HUMO-h-021"), ("u2", "HUMO-h-022"), ("u3", "HUMO-h-023"), ("u4", "HUMO-h-024")):
    est, b, rid = crear(dv, "conv-" + k, "conversacion", dict(BASE, sanic_huellamessageid=h), "H-29 union")
    assert est == 201, (est, err(b))
    ids[k] = rid
est, b, _ = llama("uni_sin_ids", "unirconversaciones", {})
R.v("H-29", "unir sin ids: CARP-UNI-001", est == 400 and codigo(b) == "CARP-UNI-001", "400 UNI-001", (est, codigo(b)))
est, b, _ = llama("uni_iguales", "unirconversaciones", {"origenid": ids["u1"], "destinoid": ids["u1"]})
R.v("H-29", "unir consigo misma: CARP-UNI-002", est == 400 and codigo(b) == "CARP-UNI-002", "400 UNI-002", (est, codigo(b)))
est, b, seg = llama("uni_directo", "unirconversaciones", {"origenid": ids["u1"], "destinoid": ids["u2"]})
rel = leer(dv, "conversacion", ids["u1"], "statecode,statuscode,_sanic_unidaaid_value,_sanic_finalizadoporid_value,sanic_fechafinalizacion,sanic_sinrespuestadesde")
ok = est == 200 and rel["statecode"] == 1 and rel["statuscode"] == 159460001 and rel["_sanic_unidaaid_value"] == ids["u2"] and rel["_sanic_finalizadoporid_value"] == YO
R.v("H-29", "unir DIRECTO: origen Unida, unidaaid=destino, finalizadoporid=el llamador; los steps de Conversacion aceptan la escritura de SYSTEM de la API", ok,
    "origen 1/159460001, finalizadopor=yo", (est, codigo(b), tipos(b) if est == 200 else err(b), {k: v for k, v in rel.items() if not k.startswith("@")}, seg))
est, b, seg = llama("uni_callerid", "unirconversaciones", {"origenid": ids["u3"], "destinoid": ids["u4"]}, caller=RANDY)
rel = leer(dv, "conversacion", ids["u3"], "statecode,_sanic_finalizadoporid_value")
R.v("H-29", "unir con CallerId=Randy: finalizadoporid = InitiatingUserId (Randy)", est == 200 and rel["_sanic_finalizadoporid_value"] == RANDY, "finalizadopor=Randy",
    (est, codigo(b), "Randy" if rel["_sanic_finalizadoporid_value"] == RANDY else ("app user" if rel["_sanic_finalizadoporid_value"] == YO else rel["_sanic_finalizadoporid_value"]), err(b)[:150] if est != 200 else ""))
est, b, _ = llama("uni_origen_unida", "unirconversaciones", {"origenid": ids["u1"], "destinoid": ids["u2"]})
R.v("H-29", "unir con origen ya Unida: CARP-UNI-004", est == 400 and codigo(b) == "CARP-UNI-004", "400 UNI-004", (est, codigo(b)))
est, b, _ = llama("uni_destino_unida", "unirconversaciones", {"origenid": c1, "destinoid": ids["u1"]})
R.v("H-29", "unir con destino Unida: CARP-UNI-005", est == 400 and codigo(b) == "CARP-UNI-005", "400 UNI-005", (est, codigo(b)))
est, b, _ = llama("uni_inexistente", "unirconversaciones", {"origenid": c1, "destinoid": str(uuid.uuid4())})
R.v("H-29", "unir con destino inexistente: CARP-UNI-003", est == 400 and codigo(b) == "CARP-UNI-003", "400 UNI-003", (est, codigo(b)))

volcar("11_h29_salidas.json", salidas)

# ---------------------------------------------------------------- contexto en el trace log
time.sleep(5)
tr = resumen_trazas(trazas(dv, t0, esperar=60))
apis = [x for x in tr if x["tipo"].endswith("Api")]
volcar("11_h29_trazas.json", tr)
print("trazas del paquete:", len(tr), "| de Custom API:", len(apis))
vistos = {}
for x in apis:
    vistos.setdefault((x["tipo"], x["msg"], x["ent"], x["depth"], x["mode"]), 0)
    vistos[(x["tipo"], x["msg"], x["ent"], x["depth"], x["mode"])] += 1
for k, n in vistos.items():
    print("  API trace:", k, "x", n)
R.v("H-29", "contexto de la traza de la API: MessageName = uniquename, PrimaryEntityName, Mode sincrono, Depth", len(apis) > 0, "visible en plugintracelog", [list(k) + [n] for k, n in vistos.items()])
profs = sorted({x["depth"] for x in tr})
print("profundidades vistas en todas las trazas:", profs)
for x in tr:
    if x["tipo"].endswith("Step") and x["depth"] >= 2:
        mostrar([x])
        break
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
