"""Q4: intenta crear una columna de formula (Power Fx) con Now() / UTCNow() por Web API, con varias formas de FormulaDefinition."""
import json, sys
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
TABLA = "spk_conversacion"

def col(schema, formula, tipo="Decimal"):
    base = {"SchemaName": schema, "DisplayName": etiqueta(schema), "Description": etiqueta("formula spike"),
            "RequiredLevel": REQ_NONE, "SourceType": 3, "FormulaDefinition": formula}
    if tipo == "Decimal":
        base.update({"@odata.type": T + "DecimalAttributeMetadata", "AttributeType": "Decimal",
                     "AttributeTypeName": {"Value": "DecimalType"}, "Precision": 0})
    elif tipo == "Integer":
        base.update({"@odata.type": T + "IntegerAttributeMetadata", "AttributeType": "Integer",
                     "AttributeTypeName": {"Value": "IntegerType"}, "Format": "None"})
    return base

intentos = [
    ("spk_AntigNow_plano", "DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)", "Decimal"),
    ("spk_AntigNow_yaml", "FormulaDefinition: =DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)", "Decimal"),
    ("spk_AntigNow_igual", "=DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)", "Decimal"),
    ("spk_AntigNow_int", "DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)", "Integer"),
]
for schema, f, tipo in intentos:
    est, b, h = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", col(schema, f, tipo))
    mostrar(f"{schema} [{tipo}] {f!r}", est, b, 500)
    if est in (200, 201, 204):
        break
