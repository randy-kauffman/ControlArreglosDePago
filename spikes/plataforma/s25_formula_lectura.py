"""Q4: relectura de definiciones y valores de TODAS las columnas de formula de spk_conversacion; dos lecturas separadas 70 s."""
import json, time, datetime as dt
from dv import Dataverse
dv = Dataverse()
ids = json.load(open("/tmp/claude-1000/-home-gmaker-projects-ControlArreglosDePago/2a1c9540-94de-4279-b899-1e8a4eb38d2a/scratchpad/q4_ids.json"))
est, b, _ = dv.call("GET", "EntityDefinitions(LogicalName='spk_conversacion')/Attributes?$select=LogicalName,SourceType,AttributeTypeName&$filter=IsCustomAttribute eq true")
formulas = sorted(a["LogicalName"] for a in b["value"] if a.get("SourceType") == 3)
print("columnas con SourceType=3 (releidas):", formulas)
for n in formulas:
    cast = "DecimalAttributeMetadata" if "int" not in n else "IntegerAttributeMetadata"
    est, a, _ = dv.call("GET", f"EntityDefinitions(LogicalName='spk_conversacion')/Attributes(LogicalName='{n}')/Microsoft.Dynamics.CRM.{cast}?$select=FormulaDefinition,SourceTypeMask")
    print("  ", n, est, a.get("FormulaDefinition"), "mask=", a.get("SourceTypeMask"))
# la columna TZI recibe los mismos instantes que la UserLocal
for k, i in ids.items():
    est, r, _ = dv.call("GET", f"spk_conversacions({i})?$select=spk_primercorreo")
    dv.call("PATCH", f"spk_conversacions({i})", {"spk_primercorreotzi": r["spk_primercorreo"]})
sel = ",".join(["spk_primercorreo", "spk_primercorreotzi"] + formulas)
def leer(tag):
    print(f"--- lectura {tag} a las {dt.datetime.now(dt.timezone.utc).strftime('%H:%M:%S')} UTC")
    for k, i in ids.items():
        est, r, _ = dv.call("GET", f"spk_conversacions({i})?$select={sel}")
        print(" ", k.ljust(10), {f: r.get(f) for f in formulas}, "tzi=", r.get("spk_primercorreotzi"))
leer("1")
time.sleep(70)
leer("2")
