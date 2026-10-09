#!/usr/bin/env bash
# Mide arranques en frio: espera inactividad (6 min) antes de la primera llamada de cada plazo, luego 5 llamadas seguidas.
cd "$(dirname "$0")"
for p in 60 30 6; do
  sleep 360
  python3 s20_medir.py --n 6 --plazos $p --etiqueta "frio-tras-6min-inactivo"
done
