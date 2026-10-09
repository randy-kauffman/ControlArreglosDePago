"""Paso 1: crea las tablas del spike (idempotente: si existe, no la toca) y las lee de vuelta."""
import json, sys
from dv import Dataverse, mostrar
from spk_lib import *

dv = Dataverse()
PROD = [(928380000, "Prestamo"), (928380001, "Tarjeta")]
TABLAS = {
    "spk_arreglo": ("Arreglo spike", "Arreglos spike", "spk_Nombre", [
        col_choice_local("spk_Producto", "Producto", PROD),
        col_texto("spk_NumeroProducto", "Numero de producto", 50),
        col_entero("spk_NumeroArreglo", "Numero de arreglo"),
        col_fecha_sola("spk_FechaSola", "Fecha sola")]),
    "spk_cuota": ("Cuota spike", "Cuotas spike", "spk_Nombre", [
        col_fecha_sola("spk_FechaVencimiento", "Fecha de vencimiento"),
        col_fecha_sola("spk_FechaPago", "Fecha de pago")]),
    "spk_cliente": ("Cliente spike", "Clientes spike", "spk_Nombre", [
        col_texto("spk_NumeroCliente", "Numero de cliente", 20)]),
    "spk_conversacion": ("Conversacion spike", "Conversaciones spike", "spk_Nombre", [
        col_fecha_hora("spk_PrimerCorreo", "Primer correo")]),
}
for logica, (n, p, prim, attrs) in TABLAS.items():
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{logica}')?$select=LogicalName,MetadataId,OwnershipType")
    if est == 200:
        print(logica, "ya existia"); continue
    if est != 404:
        print("ERROR consultando", logica, est, b); sys.exit(1)
    est, b, h = escribir(dv, "POST", "EntityDefinitions", tabla(logica, n, p, prim, attrs))
    mostrar(f"POST tabla {logica}", est, b)
for logica in TABLAS:
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{logica}')?$select=LogicalName,MetadataId,OwnershipType,EntitySetName,PrimaryNameAttribute,IsCustomEntity")
    mostrar(f"RELEER {logica}", est, b)
    est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{logica}')/Attributes?$select=LogicalName,AttributeTypeName,IsCustomAttribute,IsSecured&$filter=IsCustomAttribute eq true")
    print("   columnas:", [(a["LogicalName"], a["AttributeTypeName"]["Value"]) for a in b.get("value", [])])
