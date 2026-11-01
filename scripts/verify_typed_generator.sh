#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build tests/Ivy.BindingGenerator.Tests -c Release -m:1 -p:UseSharedCompilation=false
dotnet tests/Ivy.BindingGenerator.Tests/bin/Release/net10.0/Ivy.BindingGenerator.Tests.dll
out=generated/typed-fixture
mkdir -p "$out"
dotnet tools/Ivy.BindingGenerator/bin/Release/net10.0/Ivy.BindingGenerator.dll tests/Ivy.BindingGenerator.Tests/bin/Release/net10.0 "$out/descriptors" Terraria "$out/MainPlayer.g.cs" "$out/TypedAccess.g.cs"
cat > "$out/TypedFixture.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="*.g.cs" />
    <Compile Include="../../src/Ivy.Api/Mod.cs" />
    <Compile Include="../../src/Ivy.Api/Generated/*Hooks.g.cs" /><Compile Include="../../src/Ivy.Api/Generated/ReturnHookFamilies.g.cs" />
    <Compile Include="../../src/Ivy.Api/Runtime/*.cs" />
  </ItemGroup>
</Project>
PROJECT
dotnet build "$out/TypedFixture.csproj" -c Release -m:1 -p:UseSharedCompilation=false
