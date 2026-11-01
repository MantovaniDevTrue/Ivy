# Ivy — 0.4.1

Loader C# para Terraria Android ARM64. Autor: Mantovani.

A 0.3.1 teve a saída normal confirmada sem crash. A 0.4.0 acrescenta o primeiro SDK Main/Player tipado, evento On_Main.DoUpdateInWorld com original controlado pelo handler, unload de registros por mod e ExampleMod externo com recuperação de vida. A ABI nativa 2 e a escrita no jogo ainda aguardam teste no aparelho. [Escopo, limites e teste](docs/GAMEPLAY_SDK.md).

## Teste no celular

1. Instale Ivy_0.4.1_SDK_Test.apk ao lado do Ivy anterior (pacote dev.mantovani.ivy.sdktest).
2. Toque diretamente em Abrir Terraria: a 0.4.1 espera a confirmação dos mods antes de abrir o jogo. No mundo de teste, receba dano e confira a recuperação adicional de até 5 de vida a cada 120 ticks.
3. Descarregue os mods pelo painel e confirme que o efeito para; carregue novamente.
4. Salve, saia/reentre e exporte o relatório. Procure On_Main concluído e SDK Player.statLife.

A chave antiga não estava disponível no ambiente recriado; este APK de teste usa pacote/assinatura separados e tem dados próprios. Não precisa desinstalar o Ivy validado.

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

O primeiro hook público e acessos tipados estão em [GAMEPLAY_SDK.md](docs/GAMEPLAY_SDK.md). Ainda faltam registro de conteúdo, cobertura geral de hooks/acessos, compilação no celular, dependências de mods e multiplayer. Não carrega `.tmod` do PC. As DLLs executam com os privilégios do app. Reinicie o processo ao atualizar uma DLL.

- `src/Ivy.Api`: contrato público inicial.
- `src/Ivy.Core`: carregamento e ciclo de vida.
- `src/Ivy.Android`: painel em :launcher; serviço interno, sessão, resolução IL2CPP e callback no processo Unity. Os logs de ambos entram no relatório.
- `native/ivy-native`: ponte C++ com backend ShadowHook 2.0.1 (MIT).
- `samples/ExampleMod`: DLL independente de teste.
- `tests`: verificações C# e de empacotamento.

Referência de contrato: https://docs.tmodloader.net/docs/stable/ . Implementação própria, sem código tModLoader incorporado.

## Roadmap de compatibilidade

[Plano completo por lotes](docs/ROADMAP.md), [catálogo inicial de classes](docs/compatibility/CLASSES.md) e [perfil de referência fixado](docs/compatibility/baseline.json). A API e o pacote são próprios do Ivy, com classes, assinaturas e comportamento muito próximos do tModLoader para portar fontes com o mínimo de alterações. O fluxo é recompilar fontes/assets contra o SDK Ivy; carregar .tmod ou DLLs de mods compiladas para PC fica fora do escopo.

## Acessos nativos gerados

[Gerador de bindings e estado da validação](docs/NATIVE_BINDINGS.md): inventário automático das DummyDlls, descritores de 21.296 métodos e 7.959 delegates primitivos candidatos. O catálogo gerado compila; resolução/chamadas gerais, marshalling completo e fachadas públicas ainda estão pendentes. O gerador não instala hooks; a primeira ponte diagnóstica é um adaptador separado para a assinatura já conferida neste backend.

## Saída do jogo

[Correção de ciclo de vida da 0.3.1](docs/LIFECYCLE_0.3.1.md): painel em processo/tarefa independentes, inicialização Mono nos dois processos, comandos internos para mods e diagnóstico de saídas pelo Android.

## Objetivo do SDK

[Contrato de acesso tipado e hooks On_](docs/SDK_TARGET.md): C# no Mono dentro do jogo, fachadas geradas com autocomplete e acesso ao estado IL2CPP real, com originais tipados e encadeamento por mod. Esse contrato define o objetivo; a primeira fatia Main/Player/On_Main está implementada na 0.4.0; a cobertura geral continua pendente.

## Abertura automática — 0.4.1

[Confirmação entre painel e serviço](docs/STARTUP_0.4.1.md): o painel abre a Unity somente após os mods concluírem a carga no processo do jogo. O SDK e a ponte ABI 2 não mudaram. A atualização usa o mesmo pacote e chave do Ivy SDK Test 0.4.0.
