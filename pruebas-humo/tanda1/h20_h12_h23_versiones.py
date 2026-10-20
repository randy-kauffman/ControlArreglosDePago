"""H-23 (Create de Version y de Mapeo con statecode/statuscode) y H-12 parte administrador: alta de v90 y v91 en Borrador con archivo y mapeo (28 filas de MAP_V07 del mockup),
subida y reemplazo del archivo sobre un Borrador. Datos HUMO; las versiones de humo son v90 y v91 (v92 y v93 solo si H-23 necesita una fila desechable)."""
from hlib import *

PLANTILLA = os.path.join(AQUI, "..", "..", "spikes", "plantilla", "F-NIC-0000052_v07_borrador.xlsx")
HOJAS = {"FORMATO": " FORMATO ARP ELECTRONICO", "CONTROL": "CONTROL DE PAGOS"}   # la hoja del formato lleva un espacio inicial (dicc. 4.6)
SIMPLE, TOTAL, BLOQUE = 159460001, 159460002, 159460003
C = dict(fechaFormalizacion=159460001, oficial=159460002, clienteNombre=159460003, clienteCedula=159460004, clienteCorreo=159460005, clienteCelular=159460006,
         numeroProducto=159460007, tipo=159460008, numeroArreglo=159460009, saldoInicial=159460010, montoAbonado=159460011, saldoRemanente=159460012, plazo=159460013,
         cuota=159460014, totalPagar=159460015, fechaInicio=159460016, fechaFin=159460017, cuentaDebitar=159460018, comentarios=159460019,
         cuotaNumero=159460020, cuotaFecha=159460021, cuotaMonto=159460022, cuotaFechaPago=159460023, cuotaMontoPagado=159460024, totalMontoCuotas=159460025, totalMontoPagado=159460026)
MAP_V07 = [  # (hoja, celda, campo, tipo)  — MAP_V07 de mockup/index.html lineas 262-271 (28 filas)
    ("FORMATO", "B7", "fechaFormalizacion", SIMPLE), ("FORMATO", "F7", "oficial", SIMPLE), ("FORMATO", "B8", "clienteNombre", SIMPLE), ("FORMATO", "F8", "clienteCedula", SIMPLE),
    ("FORMATO", "B9", "clienteCorreo", SIMPLE), ("FORMATO", "F9", "clienteCelular", SIMPLE), ("FORMATO", "B12", "numeroProducto", SIMPLE), ("FORMATO", "D12", "tipo", SIMPLE),
    ("FORMATO", "F12", "numeroArreglo", SIMPLE), ("FORMATO", "B13", "saldoInicial", SIMPLE), ("FORMATO", "D13", "montoAbonado", SIMPLE), ("FORMATO", "F13", "saldoRemanente", SIMPLE),
    ("FORMATO", "B14", "plazo", SIMPLE), ("FORMATO", "D14", "cuota", SIMPLE), ("FORMATO", "F14", "totalPagar", SIMPLE), ("FORMATO", "B15", "fechaInicio", SIMPLE),
    ("FORMATO", "D15", "fechaFin", SIMPLE), ("FORMATO", "F15", "cuentaDebitar", SIMPLE), ("FORMATO", "A18", "comentarios", SIMPLE),
    ("CONTROL", "B4", "clienteNombre", SIMPLE), ("CONTROL", "E4", "fechaFormalizacion", SIMPLE),
    ("CONTROL", "A7", "cuotaNumero", BLOQUE), ("CONTROL", "B7", "cuotaFecha", BLOQUE), ("CONTROL", "C7", "cuotaMonto", BLOQUE), ("CONTROL", "D7", "cuotaFechaPago", BLOQUE), ("CONTROL", "E7", "cuotaMontoPagado", BLOQUE),
    ("CONTROL", "C67", "totalMontoCuotas", TOTAL), ("CONTROL", "E67", "totalMontoPagado", TOTAL),
]
assert len(MAP_V07) == 28

dv = adm()
R = Res("20_h12_h23.json")
t0 = reloj(dv) - timedelta(seconds=3)
VC = conjuntos(dv)["version"]
datos = open(PLANTILLA, "rb").read()


def version(clave, nombre, extra=None):
    cuerpo = {"sanic_nombre": nombre, "sanic_capacidad": 60, "sanic_estado": VER_BORRADOR, **OWNER}
    cuerpo.update(extra or {})
    return crear(dv, clave, "version", cuerpo, "version de humo")


def mapeo(clave, ver, hoja, celda, campo, tipo, extra=None):
    cuerpo = {"sanic_hoja": hoja, "sanic_celda": celda, "sanic_tipo": tipo, "sanic_campo": C[campo], "sanic_nombre": "x", **OWNER,
              "sanic_versionplantillaid@odata.bind": f"/{VC}({ver})"}
    if tipo == BLOQUE:
        cuerpo["sanic_filasmaximas"] = 60
    cuerpo.update(extra or {})
    return crear(dv, clave, "mapeo", cuerpo, "mapeo de humo")


# ---------------------------------------------------------------- H-23 (a): Create de version con statecode=1
sondas = []
for nombre, extra in (("v92", {"statecode": 1}), ("v93", {"statecode": 1, "statuscode": 2})):
    est, b, rid = version("version-sonda-" + nombre, nombre, extra)
    info = {"extra": extra, "http": est, "codigo": codigo(b) if est != 201 else None}
    if est == 201:
        rel = leer(dv, "version", rid, "statecode,statuscode,sanic_estado")
        info["fila"] = {k: v for k, v in rel.items() if not k.startswith("@")}
    sondas.append(info)
    print("   sonda", nombre, info)
acepta = any(s["http"] == 201 for s in sondas)
nace_inactiva = any(s.get("fila", {}).get("statecode") == 1 for s in sondas)
R.v("H-23", "(a/b) Create de VersionPlantilla por una persona con statecode=1 (y con statecode=1/statuscode=2): ver si la plataforma lo acepta y como nace la fila", True,
    "medicion (sin veredicto previo)", sondas)

# fila anfitriona para la sonda del mapeo
host = None
for s_nombre in ("v92", "v93"):
    try:
        cand = fila("version-sonda-" + s_nombre)
        rel = leer(dv, "version", cand, "statecode,sanic_estado")
        if rel["statecode"] == 0:
            host = cand
            break
    except KeyError:
        pass
if host is None:
    est, b, host = version("version-sonda-host", "v94")
    print("   anfitriona v94 creada:", est)

# ---------------------------------------------------------------- v90 y v91
for nombre in ("v90", "v91"):
    est, b, ver = version("version-" + nombre, nombre)
    R.v("H-12/H-23", f"alta de {nombre} en Borrador (capacidad 60)", est == 201 and b.get("sanic_estado") == VER_BORRADOR and b.get("statecode") == 0, "201 Borrador activa", (est, codigo(b), err(b)[:120] if est != 201 else {k: b.get(k) for k in ("sanic_estado", "statecode", "statuscode")}))
    assert est == 201
    # H-12 parte admin: subida y reemplazo sobre Borrador
    t = time.time()
    est1, b1, _ = dv.subir_archivo(f"{VC}({ver})/sanic_archivo", f"HUMO-{nombre}-primera.xlsx", datos)
    rel1 = leer(dv, "version", ver, "sanic_archivo_name,sanic_archivo")
    est2, b2, _ = dv.subir_archivo(f"{VC}({ver})/sanic_archivo", f"HUMO-{nombre}.xlsx", datos)
    rel2 = leer(dv, "version", ver, "sanic_archivo_name,sanic_archivo")
    R.v("H-12", f"subida y REEMPLAZO del archivo (PATCH directo, 2 pasadas) sobre {nombre} Borrador, como administrador: pasan y el nombre queda el nuevo",
        est1 in (200, 204) and est2 in (200, 204) and rel2.get("sanic_archivo_name") == f"HUMO-{nombre}.xlsx", "204/204 y nombre nuevo",
        (est1, est2, rel1.get("sanic_archivo_name"), rel2.get("sanic_archivo_name"), err(b1) if est1 not in (200, 204) else "", err(b2) if est2 not in (200, 204) else ""))
    # mapeo
    t = time.time()
    malos = []
    for i, (h, ce, campo, tipo) in enumerate(MAP_V07):
        est, b, rid = mapeo(f"mapeo-{nombre}-{i:02d}", ver, HOJAS[h], ce, campo, tipo)
        if est != 201:
            malos.append((i, h, ce, est, codigo(b), err(b)[:100]))
            continue
        esperado = f"{HOJAS[h]}!{ce}"
        if b.get("sanic_nombre") != esperado:
            malos.append((i, "nombre", b.get("sanic_nombre"), esperado))
    R.v("H-23", f"carga de los 28 mapeos de MAP_V07 en {nombre} (el diseno decia 27): nombre calculado hoja!celda", not malos, "28 creados con nombre hoja!celda", (len(MAP_V07) - len(malos), malos, round(time.time() - t, 1)))

# ---------------------------------------------------------------- H-23 (mapeo con statecode)
ms = []
for k, extra in (("a", {"statecode": 1}), ("b", {"statecode": 1, "statuscode": 2})):
    est, b, rid = mapeo("mapeo-sonda-" + k, host, "HUMO-hoja", "Z9" + ("9" if k == "a" else "8"), "comentarios", SIMPLE, extra)
    info = {"extra": extra, "http": est, "codigo": codigo(b) if est != 201 else None}
    if est == 201:
        rel = leer(dv, "mapeo", rid, "statecode,statuscode,sanic_nombre")
        info["fila"] = {k2: v for k2, v in rel.items() if not k2.startswith("@")}
    ms.append(info)
    print("   sonda mapeo", k, info)
R.v("H-23", "(a/b) Create de MapeoCelda por una persona con statecode=1 (y con statecode=1/statuscode=2) sobre una version Borrador desechable", True, "medicion (sin veredicto previo)", ms)

time.sleep(5)
tr = resumen_trazas(trazas(dv, t0, esperar=90, estable=3))
volcar("20_h12_h23_trazas.json", tr)
cuenta = {}
for x in tr:
    k = (x["tipo"], x["msg"], x["ent"].replace("sanic_carp_tbl_", ""), x["depth"])
    cuenta[k] = cuenta.get(k, 0) + 1
for k, n in sorted(cuenta.items()):
    print("  traza", k, "x", n)
print("RESUMEN", sum(1 for f in R.filas if f["ok"]), "/", len(R.filas), "OK")
