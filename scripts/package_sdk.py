#!/usr/bin/env python3
"""Package only Ivy-owned SDK artifacts; never copies game assemblies or dumps."""
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]

def main():
    output=ROOT/'artifacts/Ivy_SDK_0.7.0.zip'
    output.parent.mkdir(exist_ok=True)
    files={
        'lib/Ivy.Api.dll': ROOT/'src/Ivy.Api/bin/Release/net10.0/Ivy.Api.dll',
        'lib/Ivy.Api.xml': ROOT/'src/Ivy.Api/bin/Release/net10.0/Ivy.Api.xml',
        'ExampleMod/ExampleMod.cs': ROOT/'samples/ExampleMod/ExampleMod.cs',
        'ExampleMod/ExampleMod.dll': ROOT/'samples/ExampleMod/bin/Release/net10.0/ExampleMod.dll',
        'docs/NATIVE_PANEL_FAMILIES_0.7.0.md': ROOT/'docs/NATIVE_PANEL_FAMILIES_0.7.0.md',
        'docs/capabilities/families-0.7.0.json': ROOT/'docs/capabilities/families-0.7.0.json',
    }
    project='''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><Version>1.2.0</Version></PropertyGroup>
  <ItemGroup><Reference Include="Ivy.Api"><HintPath>../lib/Ivy.Api.dll</HintPath><Private>false</Private></Reference></ItemGroup>
</Project>
'''
    readme='''Ivy SDK 0.7.0 — Mantovani

Abra ExampleMod/ExampleMod.csproj no IDE para autocomplete. Compile com .NET SDK 10:
    dotnet build ExampleMod/ExampleMod.csproj -c Release
Importe somente a DLL do mod no Ivy 0.7.0; Ivy.Api.dll é fornecida pelo host.
O exemplo pré-compilado está em ExampleMod/ExampleMod.dll (versão 1.2.0.0).

Há campos de arrays SZ e Vector2 nas seis classes; outros structs, ref/out e HookGen universal continuam pendentes. Use os acessos dentro dos hooks, na thread de gameplay. Referências retornadas pelo backend geral expiram ao terminar o callback externo.
Consulte docs/NATIVE_PANEL_FAMILIES_0.7.0.md para cobertura, exclusões e testes.
'''
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as z:
        for name,path in files.items():z.write(path,name)
        z.writestr('ExampleMod/ExampleMod.csproj',project)
        z.writestr('LEIA-ME.txt',readme)
    print(output)
if __name__=='__main__':main()
