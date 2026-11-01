# Ivy — 0.2.0

Loader C# para Terraria Android ARM64. Autor: Mantovani.

A base já carregou uma DLL externa no Android 14: 1 mod e 1 sistema. Este lote adiciona o host do Terraria e um diagnóstico de metadados IL2CPP. A inicialização integrada ainda precisa ser validada no aparelho.

## Teste no celular

1. Instale `Ivy_0.2.0_GameHost.apk` e abra o Ivy.
2. Toque em **Abrir Terraria**. O ExampleMod é carregado automaticamente.
3. Confira se o jogo chega ao menu.
4. Volte ao Ivy e exporte o relatório. Se o app fechar, abra novamente e exporte: a sessão anterior é preservada.

O diagnóstico deve registrar C# e IL2CPP no mesmo PID, a classe `Terraria.Main` e os metadados de `get_menuMode`. Ele não chama esse método e ainda não instala hooks de gameplay.

O pacote `dev.mantovani.ivy` é separado do Terraria original. O APK `RuntimeTest` dos Actions contém somente o runtime: o jogo é empacotado localmente com o APK base fornecido pelo usuário.

## Compilação

Requisitos: .NET SDK 10.0.403, workload `android`, JDK 21 e Android SDK.

```sh
dotnet workload install android --skip-manifest-update
dotnet build src/Ivy.Android/Ivy.Android.csproj -t:InstallAndroidDependencies -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk -p:AcceptAndroidSdkLicenses=True
bash scripts/build.sh -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk
python3 -m unittest discover -s tests -p 'test_*.py'
```

O workflow **Android ARM64** executa os testes e gera o APK do runtime. Para o APK com o jogo, veja [GAMEHOST.md](docs/GAMEHOST.md).

## API inicial

`Mod.Load/Unload`, `Mod.Name`, `ModSystem.Load/Unload` e `ModSystem.Mod`, no namespace `Terraria.ModLoader`. Os mods referenciam `Ivy.Api.dll`. O núcleo descobre as classes, bloqueia duplicatas e desfaz carregamentos incompletos.

Ainda faltam registro de conteúdo no jogo, hooks, compilação no celular, dependências de mods e multiplayer. Não carrega `.tmod` do PC. As DLLs executam com os privilégios do app. Reinicie o processo ao atualizar uma DLL.

- `src/Ivy.Api`: contrato público inicial.
- `src/Ivy.Core`: carregamento e ciclo de vida.
- `src/Ivy.Android`: painel, sessão persistente e diagnóstico IL2CPP.
- `samples/ExampleMod`: DLL independente de teste.
- `tests`: verificações C# e de empacotamento.

Referência de contrato: https://docs.tmodloader.net/docs/stable/ . Implementação própria, sem código tModLoader incorporado.
