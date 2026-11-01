#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ "$#" -ne 2 ]; then
  echo 'Uso: bash scripts/generate_gameplay_sdk.sh <DummyDll> <diretorio-saida>'
  exit 2
fi
mkdir -p "$2"
dotnet build tools/Ivy.BindingGenerator/Ivy.BindingGenerator.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet tools/Ivy.BindingGenerator/bin/Release/net10.0/Ivy.BindingGenerator.dll "$1" "$2" Terraria "$2/MainPlayer.g.cs"
echo 'Perfil Main/Player gerado e validado contra os metadados. A fachada requer o runtime Ivy.Api.'
