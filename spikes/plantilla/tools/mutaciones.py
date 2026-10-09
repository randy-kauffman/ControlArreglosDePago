#!/usr/bin/env python3
"""Prueba por mutación: rompe a propósito la implementación (en una COPIA fuera del repositorio) y verifica que las pruebas fallen.

Una prueba que no falla al romper lo que cubre no cubre nada. Uso: mutaciones.py <carpeta-trabajo-temporal> [id ...]
Sale con código 1 si alguna mutación SOBREVIVE (todas las pruebas pasan con el defecto adentro).
"""
import os
import re
import shutil
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = "src/ArreglosPago.Formato/"

MUTACIONES = [
    ("M01", "decimal escrito via double (pierde precision)", SRC + "GeneradorFormato.cs",
     "var s = d.ToString(CultureInfo.InvariantCulture);", 'var s = ((double)d).ToString("R", CultureInfo.InvariantCulture);'),
    ("M02", "total acumulado en double", SRC + "GeneradorFormato.cs",
     "            decimal total = 0m;\n            foreach (var c in datos.Cuotas)\n            {\n                total += campo == Campos.TotalMontoCuotas ? c.Monto : (c.MontoPagado ?? 0m);\n            }\n            return total;",
     "            double total = 0;\n            foreach (var c in datos.Cuotas)\n            {\n                total += (double)(campo == Campos.TotalMontoCuotas ? c.Monto : (c.MontoPagado ?? 0m));\n            }\n            return (decimal)total;"),
    ("M03", "no limpiar las filas sobrantes del bloque", SRC + "GeneradorFormato.cs",
     "escritor.Escribir(bloque.Hoja, celda, valor);", "if (i <= datos.Plazo) escritor.Escribir(bloque.Hoja, celda, valor);"),
    ("M04", "no reemplazar la formula de la celda", SRC + "GeneradorFormato.cs",
     "celda.RemoveAllChildren();       // quita fórmula, valor e inline string; conserva r y s", "foreach (var h in celda.Elements<CellValue>().ToList()) h.Remove();"),
    ("M05", "perder el StyleIndex de la celda escrita", SRC + "GeneradorFormato.cs",
     "celda.DataType = null;\n                if (valor == null) return;", "celda.DataType = null; celda.StyleIndex = null;\n                if (valor == null) return;"),
    ("M06", "no verificar la huella", SRC + "GeneradorFormato.cs",
     "var esperada = (huellaEsperada ?? string.Empty).Trim().ToLowerInvariant();", "return; var esperada = (huellaEsperada ?? string.Empty).Trim().ToLowerInvariant();"),
    ("M07", "no controlar el plazo contra la capacidad", SRC + "GeneradorFormato.cs",
     "if (datos.Plazo > Campos.CapacidadMaxima)", "if (false)"),
    ("M07b", "no controlar el plazo contra la capacidad de la version", SRC + "GeneradorFormato.cs",
     "if (datos.Plazo > bloque.FilasMaximas)", "if (false)"),
    ("M07c", "no controlar el plazo (ni maximo general ni capacidad de la version, ambos)", SRC + "GeneradorFormato.cs",
     ["if (datos.Plazo > Campos.CapacidadMaxima)", "if (datos.Plazo > bloque.FilasMaximas)"], ["if (false)", "if (false)"]),
    ("M08", "Parse en vez de TryParse en el indice de fila", SRC + "HojaEditable.cs",
     "if (texto == null || !int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 1 || n > ReferenciaCelda.MaxFila)",
     "n = int.Parse(texto); if (false)"),
    ("M09", "sin frontera de error controlado", SRC + "GeneradorFormato.cs",
     "catch (Exception ex)\n            {", "catch (Exception ex) when (false)\n            {"),
    ("M10", "no eliminar calcChain", SRC + "GeneradorFormato.cs",
     "if (libro.CalculationChainPart != null) libro.DeletePart(libro.CalculationChainPart);", ""),
    ("M11", "no marcar recalculo al abrir", SRC + "GeneradorFormato.cs",
     "if (recalculo) MarcarRecalculoAlAbrir(libro);", ""),
    ("M12", "no detectar dos campos en la misma celda", SRC + "ValidadorMapeo.cs",
     "if (grupo.Count() > 1)", "if (grupo.Count() > 99)"),
    ("M13", "no detectar celda que se pisa con el bloque", SRC + "ValidadorMapeo.cs",
     "if (otra.Value.Fila >= col.Value.Fila && otra.Value.Fila <= ultima)", "if (false)"),
    ("M14", "no detectar bloque incompleto", SRC + "ValidadorMapeo.cs",
     "if (faltantes.Count > 0)", "if (false)"),
    ("M15", "ignorar el sistema de fechas 1904", SRC + "GeneradorFormato.cs",
     "(_sistema1904 ? Epoca1904 : Epoca1900)", "Epoca1900"),
    ("M16", "no acotar tamaño del contenedor", SRC + "GeneradorFormato.cs",
     "if (total > MaxBytesDescomprimidos)", "if (false)"),
    ("M17", "reemplazar una sharedStrings vacia por una nueva", SRC + "GeneradorFormato.cs",
     "if (existente != null && existente.SharedStringTable == null)", "if (false)"),
    ("M18", "toda celda cuenta como de fecha (sin advertencia)", SRC + "EstilosInfo.cs",
     "if (estilo >= _numFmtPorEstilo.Count) return false;", "if (estilo >= 0) return true;"),
    ("M19", "no validar caracteres de control en textos", SRC + "GeneradorFormato.cs",
     "if (!XmlConvert.IsXmlChar(ch) && !char.IsSurrogate(ch))", "if (false)"),
    ("M20", "aceptar double como valor", SRC + "GeneradorFormato.cs",
     "if (valor == null || valor is int || valor is long || valor is decimal) return;", "if (valor == null || valor is int || valor is long || valor is decimal || valor is double) return;"),
    ("M21", "no validar numFmtId con TryParse (usa Parse)", SRC + "EstilosInfo.cs",
     "if (texto != null && !uint.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out id))", "if (texto != null) id = uint.Parse(texto); if (false)"),
    ("M22", "estilo de celda con Parse", SRC + "HojaEditable.cs",
     "if (!uint.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out estilo))", "estilo = uint.Parse(texto); if (false)"),
    ("M23", "escribir texto como numero (sin sharedStrings)", SRC + "GeneradorFormato.cs",
     "celda.DataType = CellValues.SharedString;", "celda.DataType = null;"),
    ("M25", "no avisar de celdas en la parte oculta de una combinacion", SRC + "HojaEditable.cs",
     "if (r.Columna >= c.Inicio.Columna && r.Columna <= c.Fin.Columna && r.Fila >= c.Inicio.Fila && r.Fila <= c.Fin.Fila",
     "if (false && r.Columna >= c.Inicio.Columna && r.Columna <= c.Fin.Columna && r.Fila >= c.Inicio.Fila && r.Fila <= c.Fin.Fila"),
    ("M26", "no validar el rango de fechas del sistema 1904", SRC + "GeneradorFormato.cs",
     "if (dias < (_sistema1904 ? 0 : 61))", "if (dias < (_sistema1904 ? -99999 : 61))"),
    ("M27", "rango de combinada con Split sin validar (usa Parse)", SRC + "HojaEditable.cs",
     "if (partes.Length > 2 || !ReferenciaCelda.TryParse(partes[0], out ini)", "ini = ReferenciaCelda.Parse(partes[0]); if (partes.Length > 2 || !ReferenciaCelda.TryParse(partes[0], out ini)"),
    ("M28", "reemplazar la maestra de una formula compartida/matricial", SRC + "GeneradorFormato.cs",
     "if (celda.CellFormula != null && celda.CellFormula.Reference != null)", "if (false)"),
    ("M24", "capacidad de bloque ignorada: limpiar solo hasta el plazo", SRC + "GeneradorFormato.cs",
     "for (int i = 1; i <= bloque.FilasMaximas; i++)", "for (int i = 1; i <= datos.Plazo; i++)"),
]


def correr(carpeta):
    r = subprocess.run(["dotnet", "test", "tests/ArreglosPago.Formato.Tests", "--nologo", "-v", "q"], cwd=carpeta,
                       capture_output=True, text=True, timeout=900)
    salida = r.stdout + r.stderr
    m = re.search(r"(Failed|Passed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)", salida)
    if not m:
        return None, None, salida
    return int(m.group(2)), int(m.group(3)), salida


def copiar(destino):
    if os.path.exists(destino):
        shutil.rmtree(destino)
    ignorar = shutil.ignore_patterns("bin", "obj", "comparacion", "tools", "*.pdf", "*.png", "salida-*")
    shutil.copytree(RAIZ, destino, ignore=ignorar)
    # las mutaciones dejan codigo inalcanzable: no tiene que frenar la compilacion de la copia
    csproj = os.path.join(destino, SRC, "ArreglosPago.Formato.csproj")
    t = open(csproj, encoding="utf-8").read().replace("<TreatWarningsAsErrors>true", "<TreatWarningsAsErrors>false")
    open(csproj, "w", encoding="utf-8").write(t)


def main():
    trabajo = sys.argv[1]
    args = sys.argv[2:]
    sin_base = "--sin-base" in args          # para correr varios tramos en paralelo (cada uno con su carpeta de trabajo)
    ids = set(a for a in args if not a.startswith("--"))
    copia = os.path.join(trabajo, "copia")
    resultados = []
    if not sin_base:
        copiar(copia)
        f, p, _ = correr(copia)
        print("LINEA BASE: fallan %s, pasan %s" % (f, p), flush=True)
        if f != 0:
            print("la linea base no esta en verde; no se muta")
            return 2
    for (mid, desc, archivo, viejo, nuevo) in MUTACIONES:
        if ids and mid not in ids:
            continue
        copiar(copia)
        ruta = os.path.join(copia, archivo)
        texto = open(ruta, encoding="utf-8").read()
        viejos = viejo if isinstance(viejo, list) else [viejo]
        nuevos = nuevo if isinstance(nuevo, list) else [nuevo]
        if any(v not in texto for v in viejos):
            print("%s: NO SE ENCONTRO el texto a mutar en %s" % (mid, archivo))
            resultados.append((mid, desc, "ERROR", "-"))
            continue
        for v, n in zip(viejos, nuevos):
            texto = texto.replace(v, n, 1)
        open(ruta, "w", encoding="utf-8").write(texto)
        f, p, salida = correr(copia)
        if f is None:
            estado = "NO COMPILA"
        elif f > 0:
            estado = "MATADA"
        else:
            estado = "SOBREVIVE"
        resultados.append((mid, desc, estado, "%s fallan" % f))
        print("%s %-60s %s (%s)" % (mid, desc, estado, f), flush=True)
    print("\n| Id | Defecto introducido | Resultado | Pruebas que fallan |\n|---|---|---|---|")
    for mid, desc, estado, f in resultados:
        print("| %s | %s | %s | %s |" % (mid, desc, estado, f))
    return 1 if any(r[2] != "MATADA" for r in resultados) else 0


if __name__ == "__main__":
    sys.exit(main())
