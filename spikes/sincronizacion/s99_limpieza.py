"""LIMPIEZA del Spike 3 (NO se ejecuta sola). Sin bandera hace ensayo en seco y solo lista.
 python3 s99_limpieza.py --si-borrar
Borra: (1) los Email cuyo asunto contiene '[SPIKE3]' creados el 2026-10-08 desde las 19:30 UTC (entrantes, salientes y las copias que
sincronizo la queue del buzon 2); sus Queue Items y activitymimeattachments se van con ellos. (2) las filas spk_conversacion cuyo nombre
empieza con '[SPIKE3]'. NO toca el Email 'Your mailbox is now connected...' ni las filas spk_conversacion del spike 2.
Las filas 'attachment' (blob) que queden huerfanas por borrar la conversacion antes que el Email no se pueden borrar por API
(Retrieve/Delete no soportado en 'attachment'): por eso se borran primero los Email y despues las conversaciones.
La tabla spk_conversacion (con actividades habilitadas, irreversible) y la solucion se limpian con ../plataforma/s99_limpieza.py."""
import sys
from lib import *
dv = Dataverse(); borrar = "--si-borrar" in sys.argv
s, b, _ = dv.call("GET", "emails?$select=activityid,subject,createdon&$filter=createdon ge 2026-10-08T19:30:00Z&$orderby=createdon asc")
emails = [e for e in b["value"] if "[SPIKE3]" in e["subject"]]
s, c, _ = dv.call("GET", "spk_conversacions?$select=spk_conversacionid,spk_nombre")
convs = [x for x in c["value"] if (x.get("spk_nombre") or "").startswith("[SPIKE3]")]
print(f"Emails a borrar: {len(emails)} | Conversaciones a borrar: {len(convs)} | modo: {'BORRAR' if borrar else 'ensayo en seco'}")
for e in emails:
    print(" email", e["activityid"], e["createdon"], e["subject"][:70])
    if borrar: print("   DELETE", dv.call("DELETE", f"emails({e['activityid']})")[0])
for x in convs:
    print(" conversacion", x["spk_conversacionid"], x["spk_nombre"])
    if borrar: print("   DELETE", dv.call("DELETE", f"spk_conversacions({x['spk_conversacionid']})")[0])
