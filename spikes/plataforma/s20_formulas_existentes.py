"""Q4 (previo, solo lectura): busca columnas de formula ya existentes en el entorno para ver la forma de FormulaDefinition."""
import json, urllib.parse
from dv import Dataverse
dv = Dataverse()
q = {"Properties": {"PropertyNames": ["LogicalName", "Attributes"]},
     "AttributeQuery": {"Properties": {"PropertyNames": ["LogicalName", "SourceType", "FormulaDefinition", "AttributeType"]},
                        "Criteria": {"FilterOperator": "And", "Conditions": [
                            {"PropertyName": "SourceType", "ConditionOperator": "Equals", "Value": {"Value": "3", "Type": "System.Int32"}}]}}}
ruta = "RetrieveMetadataChanges(Query=@p1)?@p1=" + json.dumps(q, separators=(",", ":"))
est, b, _ = dv.call("GET", ruta)
print(est)
if est == 200:
    for e in b.get("EntityMetadata", []):
        for a in e.get("Attributes", []):
            print(e["LogicalName"], a["LogicalName"], a.get("AttributeType"), repr(a.get("FormulaDefinition"))[:600])
else:
    print(str(b)[:800])
