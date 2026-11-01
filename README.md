# Ivy — 0.1.1

Protótipo independente de loader C# para Android ARM64. Autor: Mantovani.

Esta etapa testa o runtime e DLLs externas. Ainda não executa o Terraria, não registra itens no jogo e não carrega `.tmod` do PC.

## Teste no celular

1. Instale `Ivy_0.1.1_RuntimeTest.apk`.
2. Toque em **Carregar mods C#**.
3. O resultado esperado é **1 mod, 1 sistema**.
4. Descarregue, carregue novamente e exporte o relatório.

O ExampleMod é compilado separadamente e carregado de uma DLL fora do conjunto de assemblies do app. O APK usa um pacote próprio: `dev.mantovani.ivy`.

## Fontes e compilação

Instale .NET SDK 10.0.403, o workload `android`, JDK 21 e os componentes do Android SDK pedidos pelo projeto.

```sh
dotnet workload install android --skip-manifest-update
dotnet build src/Ivy.Android/Ivy.Android.csproj -t:InstallAndroidDependencies -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk -p:AcceptAndroidSdkLicenses=True
bash scripts/build.sh -p:AndroidSdkDirectory=/caminho/absoluto/android-sdk
```

O workflow `Android ARM64` também compila e disponibiliza os artefatos no GitHub Actions. A assinatura gerada é de desenvolvimento; antes de distribuir versões públicas, configure uma chave estável e privada.

## Organização

- `src/Ivy.Api`: contrato C# inicial com `Mod` e `ModSystem`.
- `src/Ivy.Core`: descoberta, carregamento, validação, rollback e encerramento.
- `src/Ivy.Android`: tela, importação de DLL e exportação de relatório.
- `samples/ExampleMod`: mod de teste independente.
- `tests/Ivy.Tests`: verificações de ciclo de vida e carregamento externo.

## Compatibilidade

A API está em fase inicial: somente `Mod.Load/Unload`, `Mod.Name`, `ModSystem.Load/Unload` e `ModSystem.Mod`. O namespace `Terraria.ModLoader` aproxima o contrato de código-fonte, mas não garante compatibilidade binária. Não existem classes falsas de Item/NPC nem hooks de jogo sem implementação.

As DLLs precisam referenciar `Ivy.Api.dll`. Dependências externas, compilação C# dentro do celular, atualizações de mods e sincronização multiplayer ainda não estão implementadas. O encerramento chama Unload e solicita liberação do contexto; mods que guardam referências estáticas ou threads podem impedir sua coleta. Reinicie o app ao atualizar um mod. DLLs têm os privilégios do app e devem vir de autores de confiança.

## Próxima integração

É necessário o APK base legítimo do Terraria, incluindo seus splits quando aplicável. O dump IL2CPP ajuda a conferir assinaturas, mas suas DummyDlls não executam o jogo. Abrir o Terraria instalado em outro processo não permite aplicar esses hooks: a ponte e o runtime precisam participar do processo que hospeda o jogo.

O próximo lote deverá integrar a inicialização do runtime ao host do jogo, resolver metadados IL2CPP da versão exata, instalar callbacks na thread correta e validar leitura/escrita de um campo vanilla. Depois entram registro de conteúdo, texturas, receitas, projéteis e persistência.

Referências de contrato: https://docs.tmodloader.net/docs/stable/ e https://github.com/tModLoader/tModLoader . A implementação deste protótipo é própria; nenhum código tModLoader foi incorporado.

## Validação no Android

A base 0.1.0 carregou a DLL externa no Android 14 ARM64 de Mantovani: 1 mod e 1 sistema, conforme captura enviada. A 0.1.1 aplica o nome Ivy e usa o pacote `dev.mantovani.ivy`. O teste de encerramento e recarga ainda não está comprovado pela captura.
