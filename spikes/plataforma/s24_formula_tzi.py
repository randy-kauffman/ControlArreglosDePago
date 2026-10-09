"""Q4: columna fecha-hora TimeZoneIndependent + formula con UTCNow(); y la cruzada Now() contra TZI (debe rechazarse segun la doc)."""
from dv import Dataverse, etiqueta, mostrar
from spk_lib import *
dv = Dataverse()
TABLA = "spk_conversacion"
c = col_fecha_hora("spk_PrimerCorreoTzi", "Primer correo TZI")
c["DateTimeBehavior"] = {"Value": "TimeZoneIndependent"}
est, b, _ = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", c)
mostrar("POST columna TZI", est, b, 400)
def f(schema, formula):
    return {"@odata.type": T + "DecimalAttributeMetadata", "AttributeType": "Decimal", "AttributeTypeName": {"Value": "DecimalType"},
            "Precision": 0, "SchemaName": schema, "DisplayName": etiqueta(schema), "Description": etiqueta("formula spike"),
            "RequiredLevel": REQ_NONE, "SourceType": 3, "FormulaDefinition": formula}
for schema, formula in [("spk_AntigTziUtc", "DateDiff(spk_primercorreotzi, UTCNow(), TimeUnit.Hours)"),
                        ("spk_AntigTziNow", "DateDiff(spk_primercorreotzi, Now(), TimeUnit.Hours)")]:
    est, b, _ = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", f(schema, formula))
    mostrar(f"POST {schema} {formula}", est, b, 500)
