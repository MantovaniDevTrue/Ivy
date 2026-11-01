#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ "$#" -lt 2 ]; then
  echo 'Uso: bash scripts/generate_bindings.sh <diretorio-DummyDll> <diretorio-saida> [prefixos]'
  exit 2
fi
dotnet build tools/Ivy.BindingGenerator/Ivy.BindingGenerator.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet build tests/Ivy.BindingGenerator.Tests/Ivy.BindingGenerator.Tests.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet tests/Ivy.BindingGenerator.Tests/bin/Release/net10.0/Ivy.BindingGenerator.Tests.dll
dotnet tools/Ivy.BindingGenerator/bin/Release/net10.0/Ivy.BindingGenerator.dll "$@"
dotnet build "$2/Ivy.Generated.csproj" -c Release -m:1 -p:UseSharedCompilation=false
