# SDK de gameplay — Ivy 0.4.0

Primeiro lote executável do contrato SDK_TARGET: Main/Player tipados e
On_Main.DoUpdateInWorld. Ainda é uma fatia pequena da API; não representa acesso
completo aos 21 mil métodos do inventário.

## Superfície entregue

| Membro | Implementação |
| --- | --- |
| Main.LocalPlayer | Fachada do jogador IL2CPP atual, com identidade em cache |
| Main.menuMode / Main.myPlayer | Getters estáticos nativos |
| Main.DoUpdateInWorld() | Chamada tipada à entrada do jogo; passa pelo hook |
| Player.statLife | Leitura/escrita do campo real via API IL2CPP |
| Player.statLifeMax2 | Visão de leitura do máximo de vida efetivo |
| On_Main.DoUpdateInWorld | Evento com orig_DoUpdateInWorld e hook_DoUpdateInWorld |

A fachada é gerada de um template específico após conferir tipos, assinaturas,
flags e campos no catálogo de metadados. Não é um gerador universal de fachadas:
novas famílias e marshalling continuam pendentes. Para reproduzir:

```sh
bash scripts/generate_gameplay_sdk.sh /caminho/DummyDll /caminho/saida
cmp /caminho/saida/MainPlayer.g.cs src/Ivy.Api/Generated/MainPlayer.g.cs
```

O SDK contém Ivy.Api.dll, documentação XML e o exemplo externo. Referencie a DLL
no editor para autocomplete; importe somente a DLL do mod no Ivy. A fachada
precisa do backend do host para executar. Campos nativos aparecem como accessors:
ref player.statLife, structs, arrays e membros não listados acima ainda não são
suportados. Player ainda não implementa a hierarquia Entity completa.

## Exemplo de mod

samples/ExampleMod/ExampleMod.cs registra um handler em Load e remove em Unload.
O handler chama orig(self), consulta Main.LocalPlayer e recupera até 5 de vida a
cada 120 ticks de simulação, sem exceder statLifeMax2. A cura só escreve quando
há vida a recuperar. As primeiras três escritas são relidas pelo backend e
registradas no relatório para conferir o estado nativo. Não grava um bônus em
saves e não cria itens ou conteúdo.

## Hooks e unload

ABI nativa 2: o proxy entra no C# antes do original. Cada handler controla orig;
pode executar antes/depois dele, omiti-lo ou chamá-lo mais de uma vez. O primeiro
registro é o handler externo da cadeia. Essa ordem é explícita do Ivy neste lote,
ainda sem equivalência integral validada com todas as regras do tModLoader.

A cadeia é reconstruída apenas quando há registros/remoções. O dispatch usa o
snapshot atual e não cria delegates ou scopes no caminho normal por tick.
Registre no contexto síncrono de Mod.Load, ModSystem.Load ou de um handler do mod;
registro em tarefas sem esse contexto ainda não é oferecido.

Registros pertencem ao mod. Falha parcial em Load e unload removem todos, mesmo
que o mod esqueça -=. Unload impede novas entradas e espera handlers em curso
antes de chamar Unload/liberar o contexto. Descarregar a sessão dentro de seu
próprio hook é rejeitado para evitar espera circular. O backend nativo e seu
callback são do host e permanecem vivos ao descarregar mods.

Uma exceção gerenciada sai da cadeia até o limite do callback, é registrada e
suspende o dispatch futuro. O proxy executa o original como fallback apenas se
nenhuma chamada a ele foi iniciada naquele frame: não repete um original já
executado. Isso não captura falhas nativas. O fallback é uma política inicial de
estabilidade do Ivy, não uma regra universal de hooks tModLoader.

Acesso ao jogo e orig exigem o escopo do hook na thread de gameplay. Ponteiros e
MethodInfo não fazem parte da API do mod. O backend mantém handles IL2CPP fortes
para Main e o jogador atual; troca/libera os anteriores ao detectar substituição.
Wrappers antigos são invalidados. Os últimos handles do cache vivem até a troca
ou o fim do processo; não há ainda um evento completo de unload de mundo.

## Verificação e teste no celular

Passaram 11 verificações do loader, 15 de gameplay/hooks com backend simulado,
17 do gerador, 13 Python e o teste C++ com backend de hooks simulado. O perfil
real foi gerado do dump fornecido; a fixture e os descritores também compilaram.
JNI de Activity/Service e processos/providers Mono são conferidos no build.
Essas verificações não comprovam a execução da ABI 2 no Android nem 60 FPS.

Este APK é o Ivy SDK Test (dev.mantovani.ivy.sdktest), instalado ao lado do Ivy
0.3.1. O ambiente foi recriado e a chave antiga não estava disponível; não é uma
atualização assinada para instalar sobre a versão anterior. O pacote separado
preserva o app/dados existentes e tem dados próprios de jogadores/mundos.

Entre em um mundo de teste, receba dano e observe a recuperação adicional a cada
2 segundos de simulação. Exporte o relatório: procure On_Main concluído e
SDK Player.statLife com releitura. Volte ao painel e descarregue os mods; ao voltar
ao mundo a recuperação adicional deve parar. Carregue novamente, teste salvar,
sair/reentrar e exporte o relatório. A ponte continua no processo do jogo e o
painel permanece isolado, como na 0.3.1.

## Atualização 0.4.1

O usuário relatou funcionamento após carregar manualmente na 0.4.0. A [0.4.1](STARTUP_0.4.1.md) corrige a ordem de abertura automática com confirmação do serviço. A API e a ponte C++ são as mesmas; o ZIP SDK 0.4.0 continua sendo a referência deste perfil.
