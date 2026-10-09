"""Q2 preparacion: columna cedula con seguridad por columna (se agrega DESPUES de la tabla), y una columna comun de control."""
import json
from dv import Dataverse, mostrar
from spk_lib import *
dv = Dataverse()
for schema, nombre, sec in [("spk_Cedula", "Cedula", True), ("spk_Telefono", "Telefono", False)]:
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='{schema.lower()}')?$select=LogicalName,IsSecured")
    if est == 404:
        est, b, _ = escribir(dv, "POST", "EntityDefinitions(LogicalName='spk_cliente')/Attributes", col_texto(schema, nombre, 20, secured=sec))
        mostrar(f"POST {schema} secured={sec}", est, b)
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes?$select=LogicalName,IsSecured,AttributeTypeName,CanBeSecuredForRead,CanBeSecuredForCreate,CanBeSecuredForUpdate&$filter=IsCustomAttribute eq true")
for a in b["value"]:
    print("  ", a["LogicalName"].ljust(20), a["AttributeTypeName"]["Value"].ljust(14), "IsSecured=", a["IsSecured"], "CanBeSecured R/C/U=", a["CanBeSecuredForRead"], a["CanBeSecuredForCreate"], a["CanBeSecuredForUpdate"])
