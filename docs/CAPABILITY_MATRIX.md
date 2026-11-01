# Auditoria e matriz de capacidade

A main foi conferida em 3b715472 (0.4.1). O relatório Android enviado após essa
versão confirma a abertura automática e a escrita nativa do exemplo; não é mais
uma pendência para repetir antes de evoluir o projeto.

A [matriz estruturada](capabilities/matrix.json) mantém separados catálogo, geração,
compilação, backend, execução Android, comportamento e persistência/rede.

| Área | Confirmado | Limite/próxima validação |
| --- | --- | --- |
| Runtime e primeiro SDK | Mono + IL2CPP, Main/Player e 3.600 callbacks/originais no Android 14 | Não cobre todo o jogo |
| Vida do jogador | 86 → 91 → 96 → 100 com releitura nativa | Não prova outros campos/structs/ref |
| Abertura 0.4.1 | 1 mod/1 sistema confirmados antes do jogo | Não prova todo ciclo de reentrada/cancelamento |
| Hooks/unload | Originais, cadeia, rollback e drain testados localmente | Casos de borda Android ainda pendentes |
| Gerador | Inventário e primeiro template validado | Delegates candidatos não equivalem a chamadas gerais |
| Seleção/zero mods 0.5.0 | Implementados com testes de disco, sessão e concorrência | Novo fluxo Android em teste na branch |
| UI Meus mods | Implementada e compilada, lista reciclada | Teste de touch/Activity Android pendente |
| API de conteúdo e rede | Planejadas | Nenhum registro de conteúdo/MP novo neste lote |
| Compilação .cs e distribuição | Planejadas | Importação atual de DLL; sem Roslyn/.ivymod/Workshop |
| Desempenho | Sem busca de metadados no hook; polling de relatório removido do painel | Sem baseline de FPS/p95/GC medido; sem promessa de 60 FPS universal |

A primeira etapa ainda incompleta era a gestão dos mods habilitados: a main
bloqueava zero mods, carregava todas as DLLs e semeava o exemplo obrigatoriamente.
Ela está sendo tratada em [MOD_SELECTION_0.5.0.md](MOD_SELECTION_0.5.0.md), mantendo
a base validada e sem introduzir classes vazias de conteúdo.

Depois da validação desse lote: generalizar contratos/resolução/marshalling e
HookGen por famílias verificáveis de assinatura; em paralelo evoluir limites de
logs, recuperação, perfis e SDK de fontes. O plano completo recebido continua
sendo um objetivo de várias etapas, não uma descrição de recursos já entregues.

## Atualização 0.6.0

Seleção/zero mods da 0.5.0 foram aceitos no aparelho pelo usuário. O novo perfil gera 2.030 campos, 1.844 métodos e 409 propriedades em seis classes; são declarações ligadas ao backend, com validação nativa sob demanda, e não 4.283 comportamentos Android validados. [Detalhes e exclusões](GENERATED_ACCESS_0.6.0.md).
