"""Mapeo MAP_V07 (28 filas, mockup/index.html 262-271) y ayudas para cargar versiones de humo. Sin efectos al importar."""
import hashlib
from hlib import *

PLANTILLA = os.path.join(AQUI, "..", "..", "spikes", "plantilla", "F-NIC-0000052_v07_borrador.xlsx")
HOJAS = {"FORMATO": " FORMATO ARP ELECTRONICO", "CONTROL": "CONTROL DE PAGOS"}   # la hoja del formato lleva un espacio inicial
SIMPLE, TOTAL, BLOQUE = 159460001, 159460002, 159460003
C = dict(fechaFormalizacion=159460001, oficial=159460002, clienteNombre=159460003, clienteCedula=159460004, clienteCorreo=159460005, clienteCelular=159460006,
         numeroProducto=159460007, tipo=159460008, numeroArreglo=159460009, saldoInicial=159460010, montoAbonado=159460011, saldoRemanente=159460012, plazo=159460013,
         cuota=159460014, totalPagar=159460015, fechaInicio=159460016, fechaFin=159460017, cuentaDebitar=159460018, comentarios=159460019,
         cuotaNumero=159460020, cuotaFecha=159460021, cuotaMonto=159460022, cuotaFechaPago=159460023, cuotaMontoPagado=159460024, totalMontoCuotas=159460025, totalMontoPagado=159460026)
MAP_V07 = [
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


def datos_plantilla():
    return open(PLANTILLA, "rb").read()


def sha256_plantilla():
    return hashlib.sha256(datos_plantilla()).hexdigest()


def alta_version(dv, nombre, hojas=HOJAS, clave=None, mapeos=True, archivo=True):
    """Version Borrador de humo con archivo y mapeo MAP_V07 (con las hojas indicadas). Devuelve el id."""
    VC = conjuntos(dv)["version"]
    clave = clave or ("version-" + nombre)
    est, b, ver = crear(dv, clave, "version", {"sanic_nombre": nombre, "sanic_capacidad": 60, "sanic_estado": VER_BORRADOR, **OWNER}, "version de humo")
    assert est == 201, (nombre, est, err(b))
    if archivo:
        est, _, _ = dv.subir_archivo(f"{VC}({ver})/sanic_archivo", f"HUMO-{nombre}.xlsx", datos_plantilla())
        assert est in (200, 204), est
    if mapeos:
        for i, (h, ce, campo, tipo) in enumerate(MAP_V07):
            cuerpo = {"sanic_hoja": hojas[h], "sanic_celda": ce, "sanic_tipo": tipo, "sanic_campo": C[campo], "sanic_nombre": "x", **OWNER, "sanic_versionplantillaid@odata.bind": f"/{VC}({ver})"}
            if tipo == BLOQUE:
                cuerpo["sanic_filasmaximas"] = 60
            est, b, _ = crear(dv, f"mapeo-{nombre}-{i:02d}", "mapeo", cuerpo, "mapeo de humo")
            assert est == 201, (nombre, i, est, err(b))
    return ver
