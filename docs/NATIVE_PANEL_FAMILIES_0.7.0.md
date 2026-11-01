# Ivy 0.7.0 — painel Java/XML e famílias nativas

Candidata derivada da 0.6.1 aceita no Android pelo usuário. A inicialização com observador il2cpp_init, anexação da thread antes de metadados, IPC de preparação e separação de processos não foram reescritas. Nenhum merge para main é realizado nesta etapa.

## Painel

O painel usa Java + Android Views/XML. Não há Avalonia, Compose, MAUI, Flutter ou bibliotecas extras de interface.

- **Início:** identidade Ivy, resumo real da seleção, Jogar, preparação manual e última preparação confirmada. A última preparação não é um monitor de sessão ativa.
- **Mods:** lista nativa reciclada, pesquisa, switches, importação e detalhes com versão, compatibilidade e hash reais. O catálogo atual não tem autor/descrição/ícone de cada mod; esses dados não foram inventados.
- **Ajustes:** tamanho de texto persistente, unload, seleção vazia e ferramentas de desenvolvimento apenas quando o exemplo opcional está no APK.
- **Relatórios:** atualização por ação, exportação SAF e cópia via clipboard. A tela limita a exibição às últimas 16 mil posições; cópia e exportação mantêm o relatório completo disponível no núcleo.

A camada `NativePanel.cs` contém apenas a comunicação JNI. Java emite identificadores de ação e recebe estado; a Activity C# reutiliza o catálogo, a preparação e o serviço existentes. IO de catálogo/importação continua fora da thread da UI. Página e busca são preservadas ao recriar a Activity. As cores, dimensões, estilos, ícone e textos estáticos principais estão nos recursos `ivy_*`; o botão voltar troca a página antes de sair do painel.

Os recursos são mesclados pelo empacotador por nome reservado ivy_, sem importar os números do R/public.xml do APK host. A UI Java resolve os IDs por nome no APK final. DEX, assets e libs originais do jogo continuam preservados; a Application original não é substituída. Só há uma tarefa Android, com painel :launcher e Unity/C# no processo padrão.

## Lote técnico

O gerador agora emite **2.315 campos**, incluindo **257 campos de array** e **28 campos Vector2**, nas seis classes anteriores. Permanecem 1.844 métodos e 409 propriedades do perfil anterior. São declarações conectadas ao backend, não milhares de comportamentos Android validados.

### Arrays e Vector2

`NativeArray<T>` oferece Length e indexador de leitura/escrita para arrays SZ unidimensionais, com elementos escalares, referências às classes do perfil e Vector2. O backend valida tipo, rank, tamanho do elemento, limites e cabeçalho usando exports IL2CPP. Escrita de referência usa a barreira de GC nativa. Metadados são cacheados por classe; referências retornadas expiram ao terminar o callback externo, como no perfil anterior.

Vector2 tem X/Y e operações matemáticas básicas próprias. Campos nativos desse tipo exigem tamanho 8, alinhamento 4 e offsets/tipos X/Y conferidos no runtime. Acesso a campos com structs é por cópia e escrita de volta: `player.position += offset` funciona; para editar X/Y individualmente, leia um valor local, altere e atribua a posição completa. Não é um ref ao campo nativo.

Ainda não há criação de arrays, conversão implícita de arrays CLR, arrays multidimensionais/aninhados, struct arbitrário, Rectangle/Color, ref/out, strings ou marshalling de argumentos/retornos de structs. Métodos com esses contratos continuam omitidos, com motivo; este lote expande campos.

### Hooks por família

`tools/hook-families/profile.json` seleciona Player.FindBuffIndex: int(instance,int). `scripts/generate_hook_families.py` gera contrato/evento/encadeamento C#, interface do host, ligação JNI/PInvoke, proxy C++ e os agregadores de instalação/ativação/limpeza. Uma nova entrada da família implementada ganha os três lados; a existência da assinatura também é validada contra os metadados pelo gerador C#. Famílias desconhecidas são recusadas. O build verifica os arquivos gerados para impedir divergência.

O proxy permite alterar argumento/retorno, omitir ou repetir originais, reentrar e remover proprietários. Erro antes do original usa fallback uma vez; erro após um original retorna seu último resultado, sem repeti-lo. Threads fora de gameplay seguem diretamente ao original. On_Main e HealEffect permanecem nos caminhos anteriores. Isso não é HookGen universal nem validação de toda ABI ARM64.

## Evidência

- 127 checks C#: 11 lifecycle, 49 acesso/marshalling, 15 gameplay, 4 HealEffect, 4 retorno, 9 startup, 35 catálogo.
- 30 checks do gerador e compilação da fachada de fixture.
- 18 testes Python, incluindo recursos novos com preservação dos recursos originais e rejeição de colisões.
- Testes nativos: readiness, mundo, HealEffect e int(int), com resultados, argumentos, recursão e fallback.
- Build ARM64, registro JNI e verificação de processos; pacote integrado assinado e payload original conferido.

A interface Java compila e seus recursos são verificados no APK. Não foi executada nem inspecionada visualmente em um Android neste ambiente; navegação, permissões SAF, fonte/tamanho, retorno do jogo e renderização precisam de teste no aparelho. Os mocks não substituem Unity/IL2CPP reais. FPS, RAM, aquecimento e tempo de abertura não foram medidos nesta candidata.

## Teste no aparelho

Atualize o mesmo pacote, sem desinstalar. A identificação visual passa a Ivy Loader. Confira navegação, busca, texto maior, detalhes e exportação/cópia de relatório. Teste Jogar direto e o fluxo com zero mods.

Para exercitar o lote técnico, importe explicitamente ExampleMod_0.7.0.dll (assembly ExampleMod 1.2.0.0), ative e entre no mundo com vida incompleta. O exemplo lê inventário/posição e preserva os dados. Procure no relatório `SDK famílias: inventário=... item[0]=... posição=...` e `On_Player.FindBuffIndex`, com retorno e falhas=0. Uma boa sessão desse exemplo comprova esses caminhos; não aprova todos os novos campos nem todos os cenários de hook.
