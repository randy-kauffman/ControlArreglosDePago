"""Q4: variantes: UTCNow(), horas fraccionarias (minutos/60), tipo entero, y columna de fecha sola."""
import json
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
TABLA = "spk_conversacion"
def base(schema, formula, tipo, precision=0):
    d = {"SchemaName": schema, "DisplayName": etiqueta(schema), "Description": etiqueta("formula spike"),
         "RequiredLevel": REQ_NONE, "SourceType": 3, "FormulaDefinition": formula}
    if tipo == "Decimal":
        d.update({"@odata.type": T + "DecimalAttributeMetadata", "AttributeType": "Decimal",
                  "AttributeTypeName": {"Value": "DecimalType"}, "Precision": precision})
    else:
        d.update({"@odata.type": T + "IntegerAttributeMetadata", "AttributeType": "Integer",
                  "AttributeTypeName": {"Value": "IntegerType"}, "Format": "None"})
    return d
variantes = [
    ("spk_AntigUtc", "DateDiff(spk_primercorreo, UTCNow(), TimeUnit.Hours)", "Decimal", 0),
    ("spk_AntigMin", "DateDiff(spk_primercorreo, Now(), TimeUnit.Minutes) / 60", "Decimal", 2),
    ("spk_AntigInt", "DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)", "Integer", 0),
]
for schema, f, tipo, prec in variantes:
    est, b, _ = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", base(schema, f, tipo, prec))
    mostrar(f"POST {schema} [{tipo}] {f}", est, b, 700)
