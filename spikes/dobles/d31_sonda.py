"""Dobles: corre los escenarios de la sonda (consola en modo 'sonda') y lee sus trazas de plugintracelogs (SOLO LECTURA de la traza: no se cambia la configuracion de la organizacion).
Las lineas de la sonda se llaman 'SND|escenario|etiqueta|secuencia|paso|clave=valor|...' y no llevan GUID de usuario ni texto de columnas."""
import re, sys
from dlib import *

st = leer_estado()
if "linea_base" not in st:
    sys.exit("DETENIDO: falta la linea base")
dv = adm()
if not st["linea_base"].get("plugintracelogsetting") == 2:
    print("AVISO: la traza de plugins de la organizacion no esta en 'All' (valor:", st["linea_base"].get("plugintracelogsetting"), "): puede faltar traza de las ejecuciones correctas")
t0 = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() - 5))
rc, out, err = correr_hijo("sonda", extra={"TZ": "America/Managua"})
print(out)
if err.strip():
    print("STDERR:", err[:2000])
with open(os.path.join(EVID, "31_sonda.log"), "w", encoding="utf-8") as f:
    f.write(out + ("\n--- STDERR ---\n" + err if err.strip() else ""))
print("codigo de salida del hijo:", rc)
time.sleep(8)   # las trazas se escriben cuando termina cada ejecucion

filas = paginar(dv, f"plugintracelogs?$select=plugintracelogid,createdon,messageblock,exceptiondetails,typename,messagename,primaryentity,depth,mode,operationtype,correlationid,performanceexecutionduration"
                    f"&$filter=createdon ge {t0} and contains(typename,'Spk.SondaDobles')&$orderby=createdon asc")
GUID = re.compile(r"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}")
corr_map = {}


def sin_guid(s):
    return GUID.sub("<guid>", s or "")


trazas = []
for f in filas:
    cid = corr_map.setdefault(f["correlationid"], f"corr{len(corr_map) + 1}")
    lineas = [l for l in (f.get("messageblock") or "").splitlines() if l.startswith("SND|")]
    pasos = []
    for l in lineas:
        p = l.split("|")
        kv = {}
        for x in p[5:]:
            k, _, v = x.partition("=")
            kv[k] = v
        pasos.append({"escenario": p[1], "etiqueta": p[2], "secuencia": int(p[3]), "paso": p[4], **kv})
    trazas.append({"correlacion": cid, "creado": f["createdon"], "mensaje": f["messagename"], "tabla": f["primaryentity"], "profundidad_del_log": f["depth"], "modo": f["mode"], "operacion": f["operationtype"],
                   "duracion_ms": f["performanceexecutionduration"], "lineas": pasos, "excepcion": sin_guid((f.get("exceptiondetails") or "")[:600]) or None})
volcar("31_trazas_crudas_conteo.json", {"filas_de_plugintracelogs": len(filas), "con_lineas_SND": sum(1 for t in trazas if t["lineas"]), "con_excepcion": sum(1 for t in trazas if t["excepcion"])})
with open(os.path.join(SALIDA, "31-sonda-trazas.json"), "w", encoding="utf-8") as f:
    json.dump({"cabecera": {"fuente": "plugintracelogs (solo lectura) del tipo Spk.SondaDobles.SondaPlugin", "desde_utc": t0, "nota": "sin GUID de usuarios ni texto de columnas; la correlacion es un ordinal"}, "contenido": trazas}, f, indent=1, ensure_ascii=False)
print("trazas leidas:", len(filas), "| con lineas SND:", sum(1 for t in trazas if t["lineas"]), "| con excepcion:", sum(1 for t in trazas if t["excepcion"]))
sys.exit(rc)
