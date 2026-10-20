"""Lectura de metadatos de las 7 tablas del area en Dev (solo GET): columnas, tipo, requerida, lookups y su navigation property."""
from hlib import *

dv = adm()
salida = {}
for k, ln in T.items():
    ent = get(dv, f"EntityDefinitions(LogicalName='{ln}')?$select=EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute")
    attrs = get(dv, f"EntityDefinitions(LogicalName='{ln}')/Attributes?$select=LogicalName,AttributeType,RequiredLevel,IsValidForCreate,IsValidForUpdate,AttributeOf")["value"]
    cols = []
    for a in attrs:
        if a.get("AttributeOf") or not (a["LogicalName"].startswith("sanic_") or a["LogicalName"] in ("statecode", "statuscode", "ownerid")):
            continue
        cols.append(f"{a['LogicalName']}:{a['AttributeType']}:{a['RequiredLevel']['Value'][0]}{'' if a['IsValidForCreate'] else ':noC'}{'' if a['IsValidForUpdate'] else ':noU'}")
    salida[k] = {"set": ent["EntitySetName"], "pk": ent["PrimaryIdAttribute"], "nombre": ent["PrimaryNameAttribute"], "navs": navs(dv, ln), "cols": sorted(cols)}
    print("==", k, ent["EntitySetName"], ent["PrimaryNameAttribute"])
    print("  cols:", " ".join(sorted(cols)))
    print("  navs:", salida[k]["navs"])
volcar("00_metadatos.json", salida)
