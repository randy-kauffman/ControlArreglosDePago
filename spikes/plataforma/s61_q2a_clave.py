"""Q2a: clave alterna con columna segura, en los dos sentidos."""
import json
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
# Sentido 1: crear una clave sobre la columna YA segura
cuerpo = {"@odata.type": T + "EntityKeyMetadata", "SchemaName": "spk_clave_cedula", "DisplayName": etiqueta("Clave cedula"), "KeyAttributes": ["spk_cedula"]}
est, b, _ = escribir(dv, "POST", "EntityDefinitions(LogicalName='spk_cliente')/Keys", cuerpo)
mostrar("POST clave sobre spk_cedula (segura)", est, b, 700)
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Keys?$select=LogicalName,KeyAttributes,EntityKeyIndexStatus")
print("RELEER claves de spk_cliente:", [(k["LogicalName"], k["EntityKeyIndexStatus"]) for k in b["value"]])

# Sentido 2: clave sobre una columna comun y despues protegerla
cuerpo = {"@odata.type": T + "EntityKeyMetadata", "SchemaName": "spk_clave_numerocliente", "DisplayName": etiqueta("Clave numero cliente"), "KeyAttributes": ["spk_numerocliente"]}
est, b, _ = escribir(dv, "POST", "EntityDefinitions(LogicalName='spk_cliente')/Keys", cuerpo)
mostrar("POST clave sobre spk_numerocliente (comun)", est, b, 400)
est, a, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_numerocliente')/Microsoft.Dynamics.CRM.StringAttributeMetadata")
a["@odata.type"] = T + "StringAttributeMetadata"; a["IsSecured"] = True
est, b, _ = escribir(dv, "PUT", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_numerocliente')", a, cabeceras={"MSCRM.MergeLabels": "true"})
mostrar("PUT IsSecured=true sobre columna que YA es parte de una clave", est, b, 700)
est, a2, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Attributes(LogicalName='spk_numerocliente')?$select=LogicalName,IsSecured")
print("RELEER spk_numerocliente.IsSecured =", a2["IsSecured"])
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_cliente')/Keys?$select=LogicalName,KeyAttributes,EntityKeyIndexStatus")
print("RELEER claves de spk_cliente:", [(k["LogicalName"], k["KeyAttributes"], k["EntityKeyIndexStatus"]) for k in b["value"]])
