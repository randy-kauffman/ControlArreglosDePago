"""Q1 paso A: clave alterna (producto, numero de producto, numero de arreglo) sobre spk_arreglo; espera al indice."""
import json
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
TABLA, CLAVE = "spk_arreglo", "spk_clave_arreglo"
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')/Keys(LogicalName='{CLAVE}')?$select=EntityKeyIndexStatus")
if est == 404:
    cuerpo = {"@odata.type": T + "EntityKeyMetadata", "SchemaName": CLAVE, "DisplayName": etiqueta("Clave arreglo"),
              "KeyAttributes": ["spk_producto", "spk_numeroproducto", "spk_numeroarreglo"]}
    est, b, h = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Keys", cuerpo)
    mostrar("POST clave", est, b)
b, seg = esperar_clave(dv, TABLA, CLAVE)
mostrar(f"RELEER clave tras {seg} s", 200, b)
est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')/Keys?$select=LogicalName,KeyAttributes,EntityKeyIndexStatus,IsManaged")
mostrar("Keys de la tabla", est, b)
