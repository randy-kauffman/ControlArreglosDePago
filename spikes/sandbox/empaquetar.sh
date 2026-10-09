#!/usr/bin/env bash
# Empaqueta el paquete de plugins (spike 1b). Regla de alm.md: borrar obj/ y bin/ de los DOS proyectos antes de empaquetar.
set -euo pipefail
AQUI="$(cd "$(dirname "$0")" && pwd)"
rm -rf "$AQUI/src/Spk.Plugins/obj" "$AQUI/src/Spk.Plugins/bin" "$AQUI/src/ArreglosPago.Formato/obj" "$AQUI/src/ArreglosPago.Formato/bin"
cd "$AQUI/src/Spk.Plugins"
dotnet build -c Release 2>&1 | tail -8
rg -o "<AssemblyVersion>[^<]+" Spk.Plugins.csproj
