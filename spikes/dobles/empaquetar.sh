#!/usr/bin/env bash
# Empaqueta la sonda (mini spike de los dobles). Regla de alm.md: borrar obj/ y bin/ antes de empaquetar.
set -euo pipefail
AQUI="$(cd "$(dirname "$0")" && pwd)"
rm -rf "$AQUI/src/Spk.SondaDobles/obj" "$AQUI/src/Spk.SondaDobles/bin"
cd "$AQUI/src/Spk.SondaDobles"
/home/gmaker/.dotnet/dotnet build -c Release 2>&1 | tail -8
rg -o "<AssemblyVersion>[^<]+" Spk.SondaDobles.csproj
