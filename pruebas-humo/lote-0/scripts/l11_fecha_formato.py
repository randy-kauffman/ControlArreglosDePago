"""2026-10-20, opcion B del usuario (diseno/03 4.3 punto 19): con el paquete 1.0.1.0 en Dev, genera el formato de arreglo-l0-v07b como
User 8 (MSCRMCallerID) y comprueba con openpyxl que cada fecha de pago de CONTROL DE PAGOS (columna D desde D7) tiene formato de fecha y
que ya no viene la advertencia. Ademas cuenta los steps del ensamblado habilitados (37 propios + 5 de las Custom API).
Solo filas HUMO; no envia correos; nunca imprime secretos."""
import base64
import io
import json
import os
import sys

import openpyxl

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.join(AQUI, "..", "..")
sys.path.insert(0, os.path.join(RAIZ, "tanda1"))
import hlib as _h  # noqa: E402

_h.EVID = os.path.join(AQUI, "..", "evidencia")
dv = _h.adm()
u8 = json.load(open(os.path.join(RAIZ, "lote-0", "estado.json")))["usuarios_13_7"]["oficial"]["systemuserid"]
filas = json.load(open(os.path.join(RAIZ, "lote-0", "estado-plantilla.json"))).get("filas", [])
aid = next(x["id"] for x in filas if x.get("clave") == "arreglo-l0-v07b")

out = {"fecha": "2026-10-20", "caller": "User 8", "arreglo": "arreglo-l0-v07b", "paquete": "1.0.1.0"}

ens = _h.get(dv, "pluginassemblies?$select=name,version&$filter=name eq 'Sanic.Carp.Plugins'")["value"]
out["ensamblado"] = [{"name": e["name"], "version": e["version"]} for e in ens]
pasos = _h.get(dv, "sdkmessageprocessingsteps?$select=name,statecode,stage&$filter=plugintypeid/assemblyname eq 'Sanic.Carp.Plugins'")["value"]
out["steps"] = {"total": len(pasos), "habilitados": sum(1 for p in pasos if p["statecode"] == 0),
                "deshabilitados": sorted(p["name"] for p in pasos if p["statecode"] != 0)}

est, b, seg = _h.llamar(dv, "sanic_carp_capi_generarformato", {"arregloid": aid}, caller=u8)
out["generar"] = {"http": est, "seg": seg}
if est == 200:
    datos = base64.b64decode(b["archivo"])
    with open(os.path.join(_h.EVID, "11_F-NIC-0000052-v07-HUMO-fecha.xlsx"), "wb") as f:
        f.write(datos)
    out["generar"].update({"nombrearchivo": b.get("nombrearchivo"), "advertencias": b.get("advertencias")})
    hoja = openpyxl.load_workbook(io.BytesIO(datos))["CONTROL DE PAGOS"]
    fechas = []
    for fila in range(7, 67):
        celda = hoja["D%d" % fila]
        if celda.value is not None:
            fechas.append({"celda": celda.coordinate, "valor": str(celda.value), "number_format": celda.number_format,
                           "es_fecha": celda.is_date})
    out["fechas_de_pago"] = fechas
    out["veredicto"] = {"todas_con_formato_de_fecha": bool(fechas) and all(x["es_fecha"] for x in fechas),
                        "sin_advertencia": not b.get("advertencias"),
                        "steps_habilitados": out["steps"]["habilitados"] == out["steps"]["total"] == 42}
else:
    out["generar"]["error"] = b
_h.volcar("11_fecha_formato.json", out)
print(json.dumps(out, indent=1, ensure_ascii=False))
