# Ivy 0.8.0 — structs e hooks bool(int)

O gerador acrescenta 10 campos Rectangle, 22 campos Color e três arrays de Color ao perfil Entity/Main/Player/Item/NPC/Projectile: 2.350 campos, 1.844 métodos e 409 propriedades. A contagem é de declarações. Propriedades/métodos com structs, ref/out, arrays multidimensionais e construção de arrays ainda não são emitidos.

Rectangle usa os quatro int32 X/Y/Width/Height, com payload de 16 bytes. Vector2 continua com 8 bytes. O Color mobile é `Microsoft.Xna.Framework.Graphics.Color`, bytes A/B/G/R em offsets 0/1/2/3. A fachada pública usa `Microsoft.Xna.Framework.Color`, com R/G/B/A, construtores básicos e PackedValue no padrão RGBA; o codec converte a representação explicitamente. Este é um subconjunto XNA, sem métodos gráficos, catálogos completos de cores nem garantia de compatibilidade binária.

Antes do primeiro acesso, o backend valida tamanho, alinhamento, tipo, nomes e offsets dos campos contra os exports IL2CPP; depois mantém cache. Fields e arrays transportam até 128 bits sem boxes. Referências mantêm o write barrier anterior. Structs retornam cópias: altere uma variável local e atribua o valor inteiro ao campo/elemento para persistir. Não altera campos do jogo automaticamente.

O perfil de hooks agora gera int(int) e bool(int), começando por Player.FindBuffIndex e Player.IsItemSlotUnlockedAndUsable. A assinatura nativa de bool usa bool C++; a ponte C# usa int32 normalizado para evitar depender do marshaling implícito de bool em delegates. Owner, encadeamento, orig, remoção, fallback e registro seguem o mesmo gerador. Outras assinaturas são rejeitadas.

ExampleMod 1.3.0.0 mantém a cura de cinco pontos, lê bodyFrame/hairColor e chama o hook de slot sem mudar seu resultado. O host registra `SDK structs` nas primeiras demonstrações de cura. A importação da DLL nova é explícita. O painel e o diagnóstico pré-Mono da 0.7.1 foram preservados.

## Verificação

138 verificações C# (incluindo 58 de acesso nativo e seis de hooks de retorno), 32 do gerador e compilação da fachada de fixture; testes C++ das duas famílias, inicialização, mundo e HealEffect; 18 testes Python. Backend C# real exercitado contra exports IL2CPP de teste, incluindo payload Rectangle completo, canais ABGR/RGBA, arrays sem sobrescrever vizinhos e rejeição de layout divergente. Build ARM64, JNI, processos, assinatura e preservação do conteúdo original verificados no empacotamento.

Execução dos structs/hook novos e estabilidade da 0.8.0 no Android ainda pendentes. FPS/RAM não medidos. A sessão aceita da 0.7.1 não explica os crashes anteriores à confirmação do serviço; o diagnóstico permanece ativo.
