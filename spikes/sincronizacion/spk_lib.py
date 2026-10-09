"""Constructores de cuerpos de metadatos y utilidades compartidas por los scripts del spike."""
import time
from dv import Dataverse, etiqueta, mostrar, SOLUCION

T = "Microsoft.Dynamics.CRM."
REQ_NONE = {"Value": "None", "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}
REQ_APP = {"Value": "ApplicationRequired", "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}
AUD_OFF = {"Value": False, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"}


def escribir(dv, metodo, ruta, cuerpo, solucion=SOLUCION, cabeceras=None, reintentos=12):
    """Escritura de metadatos con espera del bloqueo de personalizacion (429), como _comun.escribir_metadatos."""
    for i in range(reintentos + 1):
        est, b, h = dv.call(metodo, ruta, cuerpo, solucion=solucion, cabeceras=cabeceras)
        txt = str(b)
        if est == 429 and ("CustomizationLockException" in txt or "running at this moment" in txt) and i < reintentos:
            print(f"  (bloqueo de personalizacion, espero 20 s, intento {i+1})", flush=True)
            time.sleep(20)
            continue
        return est, b, h


def col_texto(schema, nombre, largo=100, req=REQ_NONE, secured=False):
    d = {"@odata.type": T + "StringAttributeMetadata", "SchemaName": schema, "AttributeType": "String",
         "AttributeTypeName": {"Value": "StringType"}, "FormatName": {"Value": "Text"}, "MaxLength": largo,
         "DisplayName": etiqueta(nombre), "Description": etiqueta(nombre), "RequiredLevel": req,
         "IsAuditEnabled": AUD_OFF}
    if secured:
        d["IsSecured"] = True
    return d


def col_entero(schema, nombre):
    return {"@odata.type": T + "IntegerAttributeMetadata", "SchemaName": schema, "AttributeType": "Integer",
            "AttributeTypeName": {"Value": "IntegerType"}, "Format": "None", "MinValue": 0, "MaxValue": 1000000,
            "DisplayName": etiqueta(nombre), "Description": etiqueta(nombre), "RequiredLevel": REQ_NONE,
            "IsAuditEnabled": AUD_OFF}


def col_fecha_sola(schema, nombre):
    return {"@odata.type": T + "DateTimeAttributeMetadata", "SchemaName": schema, "AttributeType": "DateTime",
            "AttributeTypeName": {"Value": "DateTimeType"}, "Format": "DateOnly",
            "DateTimeBehavior": {"Value": "DateOnly"}, "DisplayName": etiqueta(nombre),
            "Description": etiqueta(nombre), "RequiredLevel": REQ_NONE, "IsAuditEnabled": AUD_OFF}


def col_fecha_hora(schema, nombre):
    return {"@odata.type": T + "DateTimeAttributeMetadata", "SchemaName": schema, "AttributeType": "DateTime",
            "AttributeTypeName": {"Value": "DateTimeType"}, "Format": "DateAndTime",
            "DateTimeBehavior": {"Value": "UserLocal"}, "DisplayName": etiqueta(nombre),
            "Description": etiqueta(nombre), "RequiredLevel": REQ_NONE, "IsAuditEnabled": AUD_OFF}


def col_choice_local(schema, nombre, opciones):
    return {"@odata.type": T + "PicklistAttributeMetadata", "SchemaName": schema, "AttributeType": "Picklist",
            "AttributeTypeName": {"Value": "PicklistType"}, "DisplayName": etiqueta(nombre),
            "Description": etiqueta(nombre), "RequiredLevel": REQ_NONE, "IsAuditEnabled": AUD_OFF,
            "OptionSet": {"@odata.type": T + "OptionSetMetadata", "IsGlobal": False, "OptionSetType": "Picklist",
                          "Options": [{"Value": v, "Label": etiqueta(t)} for v, t in opciones]}}


def tabla(schema, nombre, plural, primaria_schema, atributos):
    prim = col_texto(primaria_schema, "Nombre", 100)
    prim["IsPrimaryName"] = True
    return {"@odata.type": T + "EntityMetadata", "SchemaName": schema, "DisplayName": etiqueta(nombre),
            "DisplayCollectionName": etiqueta(plural), "Description": etiqueta("Spike 2 - descartable"),
            "OwnershipType": "UserOwned", "HasActivities": False, "HasNotes": False,
            "IsActivity": False, "PrimaryNameAttribute": primaria_schema.lower(),
            "Attributes": [prim] + atributos}


def esperar_clave(dv, tabla_logica, clave_logica, maximo=420):
    t0 = time.time()
    while time.time() - t0 < maximo:
        est, b, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{tabla_logica}')/Keys(LogicalName='{clave_logica}')?$select=EntityKeyIndexStatus,KeyAttributes")
        if est == 200 and b.get("EntityKeyIndexStatus") in ("Active", "Failed"):
            return b, int(time.time() - t0)
        time.sleep(10)
    return b, int(time.time() - t0)
