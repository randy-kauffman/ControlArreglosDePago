#!/usr/bin/env python3
"""Corre TODAS las pruebas de las herramientas que no tocan el entorno (cliente simulado, archivos temporales, ninguna red) y resume.

    python3 herramientas/correr_pruebas.py

Una corrida por carpeta de pruebas (`construir/tests`, `codigo/tests`, `tests`), cada una en su propio proceso: los módulos de prueba
comparten nombres de importación con las herramientas y no deben mezclarse. Sale con código 0 solo si TODAS pasan; un total de pruebas
ejecutadas distinto de cero es parte de la condición (un descubrimiento vacío imprime `OK` y devuelve 0: no es un pase).
"""
import os
import re
import subprocess
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
CARPETAS = [os.path.join("construir", "tests"), os.path.join("codigo", "tests"), "tests"]


def correr_carpeta(carpeta):
    """Devuelve `(ejecutadas, ok, salida)` de una carpeta de pruebas."""
    r = subprocess.run([sys.executable, "-W", "ignore", "-m", "unittest", "discover", "-s", os.path.join(AQUI, carpeta)],
                       capture_output=True, text=True, env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1"})
    texto = r.stderr + r.stdout
    m = re.search(r"Ran (\d+) tests? in", texto)
    return (int(m.group(1)) if m else 0), r.returncode == 0, texto


def main():
    total, fallan = 0, []
    for carpeta in CARPETAS:
        ejecutadas, ok, texto = correr_carpeta(carpeta)
        print(f"{'OK    ' if ok and ejecutadas else 'FALLA '} {carpeta}: {ejecutadas} pruebas")
        total += ejecutadas
        if not ok or not ejecutadas:
            fallan.append(carpeta)
            # Las herramientas imprimen su línea de progreso y su JSON final también dentro de las pruebas: ruido al mostrar un fallo.
            util = [l for l in texto.splitlines() if not l.startswith(("Playbook leído y validado", '{"estado"'))]
            print("\n".join(util)[-3000:])
    print(f"\nTotal: {total} pruebas; {'todas pasan' if not fallan else 'fallan: ' + ', '.join(fallan)}")
    return 0 if not fallan else 1


if __name__ == "__main__":
    sys.exit(main())
