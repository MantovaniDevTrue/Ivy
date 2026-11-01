# SDK Ivy — acesso tipado e hooks On_

Objetivo definido por Mantovani em 01/11/2026: mods C# executando no Mono dentro
do processo do Terraria, com acesso tipado a todas as classes, campos e métodos
gerenciados do jogo e hooks no estilo On_ do tModLoader. A API e o pacote são do
Ivy; o fluxo continua sendo portar/recompilar fontes, sem carregar .tmod.

## Contrato do SDK

- Gerar tipos C# com namespaces, nomes, herança, interfaces, overloads, propriedades,
  eventos e tipos dos parâmetros/retornos correspondentes ao perfil do jogo.
  Inventariar também membros não públicos; definir sua exposição na fachada
  gerada sem perder a identidade do membro original.
- Fornecer assemblies de referência e documentação XML para autocomplete e
  verificação de tipos no editor. A superfície do SDK e a implementação de
  runtime devem vir do mesmo contrato.
- Ler/escrever o estado real de objetos IL2CPP. Objetos Mono separados ou campos
  C# que apenas guardam cópias não atendem ao requisito.
- Mapear objetos, arrays, strings, structs, enums, delegates, ref/out/in e
  contextos genéricos. Preservar identidade, ownership, vida útil e writeback.
  Tipos básicos compartilhados com o CLR exigem mapeamento, não duplicação cega.
- Oferecer On_Tipo.Metodo += handler e -= handler, com delegates tipados
  orig_Metodo e hook_Metodo. Instância, argumentos, retorno e ref/out permanecem
  tipados; IntPtr e MethodInfo ficam internos ao backend.
- Resolver overloads por assinatura completa e fixar o perfil de versão.
  Gerar nomes determinísticos quando nomes CLR não forem representáveis em C#.
- Instalar hooks sob demanda. A assinatura determina se existe corpo nativo e
  como preservá-lo. Abstratos, internal calls, genéricos compartilhados,
  construtores e funções inline precisam de tratamento específico; não prometer
  interceptação de toda chamada somente porque existe um descritor.

A meta cobre todos os tipos gerenciados do perfil do jogo, incluindo dependências
necessárias para representar suas assinaturas. O filtro atual Terraria/XNA/ReLogic
é um ponto de partida, não a definição final de cobertura.

## Camadas de implementação

| Camada | Responsabilidade |
| --- | --- |
| Gerador de contratos | Inventário completo, dependências e diferenças entre versões |
| Fachadas C# geradas | Sintaxe familiar e acesso tipado usado pelos mods |
| Runtime Mono/IL2CPP | Resolução em cache, conversões, handles e execução na thread correta |
| HookGen Ivy | Eventos On_, originais tipados, encadeamento e registro por mod |
| Ponte C++ | Instalação/remoção nativa e trampolines conforme cada ABI |

C# continua sendo a linguagem dos mods. A biblioteca C++ é interna e não deve
exigir que autores de mods escrevam bindings, ponteiros ou código nativo.

Um acesso com sintaxe player.statLife deve alcançar o campo do jogo. Se a fachada
usar um accessor gerado, é necessário tratar separadamente ref player.statLife,
mutações de structs aninhadas e referências a arrays: converter campos para
propriedades não preserva automaticamente esses comportamentos de C#.

## Semântica dos hooks

O handler recebe orig e decide quando chamá-lo, quantas vezes ou se deve omiti-lo,
conforme o contrato do método. Não se limita a um callback obrigatório depois do
original. Múltiplos handlers exigem ordem e encadeamento definidos e testados.

Remover um handler deve preservar os outros. Unload remove todos os registros
daquele mod, impede novas entradas e trata chamadas em curso antes de liberar
assemblies/delegates. Em geral, hooks com retorno/ref executam sincronamente na
thread original; enfileirar para outra thread muda o comportamento.

Falhas de um handler precisam de uma política explícita. Não repetir o original
automaticamente após uma exceção se ele já tiver sido executado.

## Estado atual e próximo lote

Estado histórico até a 0.3.1: o gerador produz descritores e delegates primitivos candidatos; ainda não
gera essas fachadas nem eventos On_. O único hook executável é o adaptador
diagnóstico de Main.DoUpdateInWorld, com original antes do callback. Isso ainda
não equivale ao contrato de orig controlado pelo handler descrito acima.

Os relatórios confirmaram os callbacks iniciais no Android 14, e Mantovani
confirmou a saída normal sem crash na 0.3.1. Reentrada completa, unload de hooks
de mods e cobertura geral continuam exigindo testes próprios.

Primeiro lote executável: gerar as fachadas mínimas Main/Player e o evento
On_Main.DoUpdateInWorld a partir do contrato; conectar leitura/escrita tipadas e
o original chamável pelo handler; comprovar isso com um mod externo, dois handlers,
remoção individual, unload e saída/reentrada. Depois ampliar a geração por famílias
de assinatura, preservando a mesma arquitetura para todo o inventário.

Cada membro terá estado separado: catalogado, gerado/compilável, conectado e
validado no runtime. Toda limitação deve ser diagnosticada no build/preflight.
Autocomplete ou compilação isolados não contam como implementação funcional.

## Primeiro lote — 0.4.0

[Implementação e limites](GAMEPLAY_SDK.md): perfil Main/Player validado contra metadados, accessors tipados, evento On_Main, ABI 2 com orig controlável e unload por mod. A cobertura geral e a execução da nova ABI no aparelho continuam pendentes.
