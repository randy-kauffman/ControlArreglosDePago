"""2026-10-20: verifica el arreglo del bug 'Cannot convert the literal undefined to Edm.Guid' (Tomar y Generar formato en el
navegador). Manda a Dev el MISMO cuerpo que arma Xrm con la peticion corregida de comandos.js (Edm.Guid desde {guid}: en el
cable viaja el Guid como texto), como User 8 (MSCRMCallerID). Tambien prueba el cuerpo del bug ('undefined'), para dejar
evidencia de que es exactamente el error reportado. Solo filas HUMO; no envia correos; nunca imprime secretos."""
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "tanda1"))
import hlib as _h  # noqa: E402

_h.EVID = os.path.join(AQUI, "evidencia")
dv = _h.adm()
RAIZ = os.path.join(AQUI, "..")
u8 = json.load(open(os.path.join(RAIZ, "lote-0", "estado.json")))["usuarios_13_7"]["oficial"]["systemuserid"]
conv = json.load(open(os.path.join(RAIZ, "tanda3", "estado.json")))["caso_c"]["conversacion_id"]
arr = json.load(open(os.path.join(RAIZ, "lote-0", "estado-plantilla.json")))

def id_arreglo():
    for x in arr.get("filas", []):
        if x.get("clave") == "arreglo-l0-v07b":
            return x["id"]
    raise KeyError("arreglo-l0-v07b")

aid = id_arreglo()
out = {"fecha": "2026-10-20", "caller": "User 8", "conversacion": "tanda3 caso_c", "arreglo": "arreglo-l0-v07b"}

est, b, s = _h.llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": "undefined"}, caller=u8)
out["bug_tomar"] = {"http": est, "error": (b.get("error") or "")[:160]}
est, b, s = _h.llamar(dv, "sanic_carp_capi_tomarconversacion", {"conversacionid": conv}, caller=u8)
out["tomar"] = {"http": est, "seg": s, "salida": {k: v for k, v in b.items() if not k.startswith("@")} if est == 200 else b}
est, b, s = _h.llamar(dv, "sanic_carp_capi_generarformato", {"arregloid": aid}, caller=u8)
out["generar"] = {"http": est, "seg": s,
                  "salida": {"nombrearchivo": b.get("nombrearchivo"), "bytes_base64": len(b.get("archivo") or ""),
                             "congelado": b.get("congelado"), "advertencias": b.get("advertencias")} if est == 200 else b}
_h.volcar("20_guid_execute.json", out)
print(json.dumps(out, indent=1, ensure_ascii=False))
