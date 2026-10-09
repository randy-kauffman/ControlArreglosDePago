"""Errores controlados: cada caso debe devolver un error de negocio claro (InvalidPluginExecutionException con mensaje),
nunca una excepcion cruda ni un archivo parcial. Imprime estado HTTP, codigo y mensaje exactos."""
import uuid
from sbx_lib import *
dv = Dataverse(); e = leer_estado(); F = e["filas"]
casos = [
    ("huella distinta", {"VersionId": F["v07 con huella incorrecta"], "Plazo": 6}),
    ("archivo que no es xlsx", {"VersionId": F["archivo que no es xlsx (huella correcta)"], "Plazo": 6}),
    ("fila sin archivo", {"VersionId": F["sin archivo"], "Plazo": 6}),
    ("plazo 0", {"VersionId": F["v07 borrador (huella correcta)"], "Plazo": 0}),
    ("plazo 61", {"VersionId": F["v07 borrador (huella correcta)"], "Plazo": 61}),
    ("plazo negativo", {"VersionId": F["v07 borrador (huella correcta)"], "Plazo": -3}),
    ("version inexistente", {"VersionId": str(uuid.uuid4()), "Plazo": 6}),
    ("falta el plazo", {"VersionId": F["v07 borrador (huella correcta)"]}),
    ("falta la version", {"Plazo": 6}),
    ("plazo como texto", {"VersionId": F["v07 borrador (huella correcta)"], "Plazo": "seis"}),
]
for nombre, cuerpo in casos:
    est, datos, _ = raw(dv, "POST", API, json.dumps(cuerpo).encode(), {"Content-Type": "application/json"})
    try:
        j = json.loads(datos)
    except Exception:
        j = {"_crudo": datos[:300].decode(errors="replace")}
    if est == 200:
        print(f"[{nombre}] HTTP 200 (!!) claves {list(j)}")
        continue
    err = j.get("error", j)
    print(f"[{nombre}] HTTP {est} code={err.get('code')} | {str(err.get('message'))[:420]}")
    crudo = datos.decode(errors="replace")
    print(f"    contiene traza de pila: {'   at ' in crudo or 'StackTrace' in crudo} | largo de la respuesta: {len(crudo)}")
