"""Resume salida/mediciones.jsonl: por plazo, arranque en frio (llamada nro 1 del proceso del sandbox) y llamadas en caliente (nro >= 2)."""
import json, statistics, math
filas = [json.loads(l) for l in open("salida/mediciones.jsonl")]
filas = [f for f in filas if f.get("fila", "v07 borrador (huella correcta)").startswith("v07")]
def p95(x):
    x = sorted(x); return x[max(0, math.ceil(len(x) * 0.95) - 1)]
def lin(nombre, v):
    return f"{nombre}: n={len(v)} mediana {statistics.median(v):.0f} | p95 {p95(v):.0f} | min {min(v)} | max {max(v)}" if v else f"{nombre}: sin muestras"
for plazo in (6, 30, 60):
    fr = [f for f in filas if f["plazo"] == plazo and f["llamada_en_proceso"] == 1]
    ca = [f for f in filas if f["plazo"] == plazo and f["llamada_en_proceso"] >= 2]
    print(f"== plazo {plazo} | {filas[[f['plazo'] for f in filas].index(plazo)]['bytes']} bytes xlsx, {filas[[f['plazo'] for f in filas].index(plazo)]['base64_chars']} chars Base64")
    print("  frio    ", lin("plugin ms", [f["plugin_ms"] for f in fr]), "|", lin("generacion ms", [f["generacion_ms"] for f in fr]), "|", lin("http ms", [round(f["http_ms"]) for f in fr]))
    print("  caliente", lin("plugin ms", [f["plugin_ms"] for f in ca]), "|", lin("generacion ms", [f["generacion_ms"] for f in ca]), "|", lin("descarga ms", [f["descarga_ms"] for f in ca]), "|", lin("http ms", [round(f["http_ms"]) for f in ca]))
