# Ivy 0.6.0 — acesso gerado e primeiro hook com argumentos

Candidata baseada na 0.5.0 aceita por Mantovani. A main validada permanece preservada; este lote trabalha na branch `feat/generated-native-access`.

## Entrega

| Área | Implementação | Evidência atual |
| --- | --- | --- |
| Entity, Main, Player, Item, NPC, Projectile | 2.030 campos, 1.844 métodos, 409 propriedades gerados a partir do dump fornecido | Compilam e chamam o backend; aceite Android pendente |
| Tipos | bool, char, inteiros de 8/16/32/64 bits com/sem sinal, float/double e referências às seis classes | Marshalling exercitado contra exports IL2CPP simulados |
| Assinaturas | static/instance, retorno, ordem/tipo de argumentos, nomes e defaults opcionais | Fixtures, overloads e diagnóstico de divergência |
| On_Player.HealEffect | void(int,bool), orig tipado, encadeamento, alteração dos argumentos, remoção por proprietário e drain | C# e proxy C++ testados localmente; Android pendente |
| On_Main.DoUpdateInWorld | Mantém o contrato anterior, com escopo de gameplay para os acessos | 0.5.0 validada; regressões locais passaram |

A contagem cobre declarações geradas, não comportamentos completos do jogo. Alguns membros são propriedades de acesso a campos nativos: `ref player.statMana`, reflexão de campo CLR e offsets gerenciados não equivalem ao objeto IL2CPP. O perfil anterior Main/Player permanece para compatibilidade, inclusive statLifeMax2 somente leitura. Item não herda Entity no dump fornecido; o gerador preserva essa diferença.

## Gerar novamente

```bash
bash scripts/generate_gameplay_sdk.sh /caminho/DummyDll generated/sdk
# Compare os arquivos e use MainPlayer.g.cs + TypedAccess.g.cs no Ivy.Api.
bash scripts/verify_typed_generator.sh
```

O perfil é calculado dos metadados, sem executar os inicializadores das DLLs. [Manifesto de geração e exclusões](capabilities/typed-access-0.6.0.json) registra hashes de entrada/saída. O dump e os binários originais do jogo não são publicados no repositório. A fixture de CI exercita o gerador e compila suas fachadas junto ao runtime; não substitui o dump real.

## Backend

`Il2CppAccess` resolve cada descritor pelo assembly/classe/nome/assinatura completa. O índice de métodos é construído uma vez por classe e cacheado; cada membro resolvido também é cacheado. Campos usam a API C IL2CPP e métodos usam `il2cpp_runtime_invoke`, com exceção capturada e retorno escalar desempacotado. Não há chamada arbitrária do endereço de cada método gerado por delegates de ABI adivinhada. Apenas os hooks explicitamente implementados usam proxies com ABI conhecida.

Não há varredura global por frame. A primeira utilização paga a resolução e a inicialização dos descritores da classe; o custo real no aparelho ainda precisa ser medido. Até oito argumentos por chamada; buffers escalares e ponteiros ficam na stack. Retornos por runtime_invoke podem alocar caixas no heap IL2CPP. Não há promessa de zero alocações em chamadas gerais.

Acesso é permitido dentro dos callbacks, na thread registrada por DoUpdateInWorld. HealEffect chamado por worker ou antes do primeiro callback de mundo segue diretamente ao original. Referências retornadas pelo backend geral são protegidas com gchandles e liberadas no final do callback externo, inclusive em exceção; retê-las para outro callback resulta em ObjectDisposedException. O Main e LocalPlayer do perfil anterior mantêm a política anterior. Identidade/equality universal e referências persistentes ficam para outra etapa.

A ABI interna passa de 2 para **3**. SDK público continua aditivo no assembly Ivy.Api 1.0.0.0; o exemplo novo requer Ivy 0.6.0. Atualize APK/runtime e biblioteca nativa juntos. O preflight completo de requisitos de cada API ainda não existe.

## Limites

Arrays, strings, structs, enums, delegates, interfaces, ponteiros, construtores, tipos/métodos genéricos, ref/out e outros tipos não são emitidos como stubs. As assinaturas omitidas aparecem no manifesto. Getters e setters suportados viram propriedades; indexadores não entram neste perfil.

A implementação nativa dos hooks ainda tem **dois alvos conhecidos**, Main.DoUpdateInWorld e Player.HealEffect. O evento On_Player é emitido após verificar a assinatura; não existe HookGen universal que instale automaticamente qualquer método. Ampliar os proxies depende da classificação e dos testes de cada família ABI.

Não há registro de conteúdo, novos IDs, saves de conteúdo de mod, multiplayer de mod, compilador .cs no aparelho ou importação .tmod neste lote.

## Validação local

- 110 checks C#: 11 lifecycle, 36 marshalling/acesso, 15 gameplay, 4 HealEffect, 9 startup e 35 biblioteca.
- 28 checks do gerador; a fachada da fixture também foi compilada.
- 15 testes Python; proxies C++ de mundo e HealEffect com alteração de argumentos, original omitido/duplicado, recursão, erro e desativação.
- 30 exports exigidos pelo novo backend confirmados na libil2cpp do APK fornecido.
- Build Android ARM64 e checks JNI/processos. O aviso XA0033 vem do formato da versão do JDK instalado; zero erros C#.
- Dez mil leituras escalares aquecidas alocaram zero bytes gerenciados no teste local com IL2CPP simulado. Isso não mede o heap IL2CPP, FPS ou custo Android.

## Teste no aparelho

1. Atualize Ivy SDK Test, mantendo dados. Descarregue a sessão ativa.
2. Importe `ExampleMod_0.6.0.dll` (assembly ExampleMod **1.1.0.0**). O botão de instalação do exemplo preserva DLLs antigas, portanto a atualização é explícita. A seleção existente é mantida em atualizações compatíveis.
3. Ative ExampleMod em Meus mods e entre em um mundo. Com vida incompleta, a cada dois segundos de simulação há cura de até 5 e efeito vanilla.
4. No relatório, procure `SDK gerado: Player.statMana=..., dead=..., FindBuffIndex(21)=...` e `On_Player.HealEffect`, com falhas=0. O efeito também pode ser chamado naturalmente pelo jogo.
5. Descarregue/desative o exemplo, teste zero mods, reentrada e pausa/retomada. Exporte o relatório.

O arquivo recebido da 0.5.0 confirma zero mods sem sonda e depois 600 originais/callbacks e escrita de vida, sem erros. O usuário aceitou o fluxo anterior; **esta candidata 0.6.0 ainda não foi executada no aparelho**. FPS, uso de memória Android e multiplayer continuam sem medição neste lote.
