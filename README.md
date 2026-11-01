# Ivy — 0.3.0

Loader C# para Terraria Android ARM64. Autor: Mantovani.

A versão 0.2.1 já entrou em um mundo no Android 14 e confirmou C# e IL2CPP no mesmo processo. A 0.3.0 acrescenta a primeira ponte C++ → C# no método Main.DoUpdateInWorld, ainda aguardando validação no aparelho.

## Teste no celular

1. Instale `Ivy_0.3.0_GameHost.apk` e abra o Ivy.
2. Toque em **Abrir Terraria** e entre em um mundo. O ExampleMod carrega automaticamente.
3. Jogue por um minuto, pause/retome, salve e entre novamente.
4. Volte ao Ivy e exporte o relatório. Procure `Hook C++ instalado` e `Gameplay C# após original`, com contadores e leitura da vida.
5. Se fechar, reabra e exporte: a sessão anterior é preservada.

A ponte chama o original antes do callback C# e apenas lê alguns valores em quatro pontos do teste. O botão **Parar hook de diagnóstico** remove o hook; reinicie o processo para instalar novamente. Detalhes e limites: [NATIVE_BRIDGE.md](docs/NATIVE_BRIDGE.md).

O pacote `dev.mantovani.ivy` é separado do Terraria original. O APK `RuntimeTest` dos Actions contém somente o runtime: o jogo é empacotado localmente com o APK base fornecido pelo usuário.

## Compilação

Requisitos: .NET SDK 10.0.403, workload `android`, JDK 21, Android SDK, NDK r27d (27.3.13750724), Python 3 e g++ em Linux x86_64.

```sh
dotnet workload install android --skip-manifest-update
dotnet build src/Ivy.Android/Ivy.Android.csproj -t:InstallAndroidDependencies -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk -p:AcceptAndroidSdkLicenses=True
IVY_NDK_DIR=/caminho/android-ndk-r27d bash scripts/build.sh -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk
python3 -m unittest discover -s tests -p 'test_*.py'
```

O workflow **Android ARM64** executa os testes e gera o APK do runtime. Para o APK com o jogo, veja [GAMEHOST.md](docs/GAMEHOST.md).

## API inicial

`Mod.Load/Unload`, `Mod.Name`, `ModSystem.Load/Unload` e `ModSystem.Mod`, no namespace `Terraria.ModLoader`. Os mods referenciam `Ivy.Api.dll`. O núcleo descobre as classes, bloqueia duplicatas e desfaz carregamentos incompletos.

Ainda faltam registro de conteúdo no jogo, hooks públicos para mods, compilação no celular, dependências de mods e multiplayer. Não carrega `.tmod` do PC. As DLLs executam com os privilégios do app. Reinicie o processo ao atualizar uma DLL.

- `src/Ivy.Api`: contrato público inicial.
- `src/Ivy.Core`: carregamento e ciclo de vida.
- `src/Ivy.Android`: painel, sessão persistente, resolução IL2CPP e callback de gameplay.
- `native/ivy-native`: ponte C++ com backend ShadowHook 2.0.1 (MIT).
- `samples/ExampleMod`: DLL independente de teste.
- `tests`: verificações C# e de empacotamento.

Referência de contrato: https://docs.tmodloader.net/docs/stable/ . Implementação própria, sem código tModLoader incorporado.

## Roadmap de compatibilidade

[Plano completo por lotes](docs/ROADMAP.md), [catálogo inicial de classes](docs/compatibility/CLASSES.md) e [perfil de referência fixado](docs/compatibility/baseline.json). A API e o pacote são próprios do Ivy, com classes, assinaturas e comportamento muito próximos do tModLoader para portar fontes com o mínimo de alterações. O fluxo é recompilar fontes/assets contra o SDK Ivy; carregar .tmod ou DLLs de mods compiladas para PC fica fora do escopo.

## Acessos nativos gerados

[Gerador de bindings e estado da validação](docs/NATIVE_BINDINGS.md): inventário automático das DummyDlls, descritores de 21.296 métodos e 7.959 delegates primitivos candidatos. O catálogo gerado compila; resolução/chamadas gerais, marshalling completo e fachadas públicas ainda estão pendentes. O gerador não instala hooks; a primeira ponte diagnóstica é um adaptador separado para a assinatura já conferida neste backend.
