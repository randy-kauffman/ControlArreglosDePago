"""Dobles: (f) repite 'Create SIN id enviado' por el Web API (OData POST), para separar lo que hace el ServiceClient de lo que hace la plataforma.
Crea UN cliente 'ZZF-SND-idsweb-a' (marca de la sonda), lo registra en estado.json apenas la plataforma devuelve su id, lee las trazas y lo borra."""
import sys
from dlib import *

dv = adm()
st = leer_estado()
base = st["linea_base"]
SOLO_TRAZAS = len(sys.argv) > 2 and sys.argv[1] == "--solo-trazas"   # relee las trazas de una corrida anterior desde el instante dado (UTC, ISO)
t0 = sys.argv[2] if SOLO_TRAZAS else time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() - 3))
rid, est = None, None
if not SOLO_TRAZAS:
  est, b, h = dv.call("POST", "sanic_carp_tbl_clientes", {"sanic_nombre": "ZZF-SND-idsweb-a", "sanic_nombrecompleto": "ZZF-sonda-web", "sanic_cedula": "ZZF-ced-web",
                                                           "ownerid@odata.bind": f"/teams({base['team_def_id']})"})
  rid = dv.id_creado(h)
  if rid:
    s = leer_estado()
    s.setdefault("filas", []).append(["cli-idsweb-a", TABLAS["cliente"][0], rid])
    guardar_estado(s)
  print("POST sanic_carp_tbl_clientes sin id ->", est, "" if est in (201, 204) else b)
out = []
for intento in range(12):   # las trazas tardan en aparecer: se espera hasta 3 etapas (PreValidation, PreOperation, PostOperation)
    time.sleep(5)
    filas = paginar(dv, f"plugintracelogs?$select=createdon,messageblock,messagename,primaryentity&$filter=createdon ge {t0} and contains(typename,'Spk.SondaDobles')&$orderby=createdon asc")
    if sum(1 for f in filas if "SND|idsweb|" in (f.get("messageblock") or "")) >= 3:
        break
for f in filas:
    for l in (f.get("messageblock") or "").splitlines():
        if l.startswith("SND|idsweb|") and "|ctx:" in l:
            p = l.split("|")
            kv = dict(x.partition("=")[::2] for x in p[5:])
            out.append({"paso": p[4], **{k: kv[k] for k in ("stage", "depth", "PrimaryEntityId_vacio", "Target_Id_vacio", "Target_Id_igual_a_PrimaryEntityId", "Target_tiene_atributo_id_primario", "OutputParameters_id", "IsInTransaction")}})
print(json.dumps(out, indent=1))
if rid:
    est, _, _ = dv.call("DELETE", f"sanic_carp_tbl_clientes({rid})")
    print("DELETE ->", est)
with open(os.path.join(SALIDA, "32-ids-webapi.json"), "w", encoding="utf-8") as f:
    json.dump({"cabecera": {"fuente": "Web API (POST sin id) + plugintracelogs", "nota": "mismo usuario de aplicacion que la consola; la sonda anota Target.Id y PrimaryEntityId por etapa"},
               "contenido": {"post_status_http": est, "etapas": out}}, f, indent=1, ensure_ascii=False)
