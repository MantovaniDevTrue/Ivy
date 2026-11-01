# Geração automática dos acessos nativos

O gerador automatiza o inventário dos acessos; a primeira [ponte nativa diagnóstica](NATIVE_BRIDGE.md) usa um adaptador específico, separado da saída gerada. A entrada é o conjunto de DummyDlls do dump fornecido; a leitura usa System.Reflection.Metadata/PEReader e não executa essas DLLs. Não há implementação do jogo embutida nos arquivos gerados.

A organização fica em três partes: metadados e assinaturas gerados; resolução/marshalling/chamadas e hooks no backend Mono/IL2CPP; fachadas públicas C# que os mods usam. Os mesmos metadados devem alimentar as três partes, evitando cadastrar manualmente milhares de métodos.

## Resultado inicial

Para os prefixos Terraria, Microsoft.Xna.Framework e ReLogic no dump fornecido:

| Item | Quantidade |
| --- | ---: |
| Tipos, incluindo aninhados e gerados pelo compilador | 2.683 |
| Métodos, incluindo accessors e construtores | 21.296 |
| Campos | 31.812 |
| Propriedades | 2.356 |
| Eventos | 97 |
| Assinaturas primitivas candidatas com delegate gerado | 7.959 |

Esses números incluem membros públicos e privados. A ausência de motivo de revisão não comprova que um método seja seguro para hook: ainda faltam validação da função nativa, ABI, thread, vida útil e comportamento. Nenhum hook é instalado pelo gerador e nenhum desses métodos foi executado no Android por esta ferramenta.

O catálogo preserva assembly, tipo, nome, retorno, tipos/nomes/defaults dos parâmetros, flags in/out, static/instance, aridade genérica, visibilidade, interfaces, herança e acesso a propriedades/eventos. Tokens identificam registros do arquivo de entrada; não são endereços nativos e não devem ser reutilizados entre versões do jogo sem regeneração.

As assinaturas de delegates primitivas usam ABI de baixo nível: bool como byte, char como ushort, ponteiro de instância quando aplicável e MethodInfo ao final. Essa forma é interna. Ela não é a fachada familiar usada pelo autor do mod e permanece candidata até ser testada no backend ARM64.

## Gerar e verificar

```sh
bash scripts/generate_bindings.sh /caminho/absoluto/DummyDll /caminho/absoluto/saida
```

O terceiro argumento opcional seleciona prefixos separados por vírgula. A saída contém:

- native-api.json: contratos e classificação de todos os membros selecionados.
- summary.json: contagens, motivos de revisão e estado explícito da validação.
- NativeMethodDescriptors.g.cs: descritores e delegates candidatos gerados em C#.
- Ivy.Generated.csproj: projeto que comprova que o C# gerado compila.

O teste de integração do CI usa fixtures próprias; o conjunto real do jogo é regenerado localmente. As DummyDlls e saídas completas não são versionadas como código do loader. O hash SHA-256 de cada assembly de entrada fica no catálogo.

## Etapas seguintes

1. Resolver métodos por assembly, tipo, nome, genericidade e assinatura completa, com cache por versão. Não escolher overload apenas pela quantidade de parâmetros.
2. Validar os delegates gerados com chamadas pequenas e reversíveis na thread do jogo, preservando os metadados/ABI reais.
3. Gerar o marshalling de strings, objetos, arrays, structs e ref/out com ownership e writeback. Hoje esses casos aparecem no relatório; não viram implementações vazias.
4. Gerar fachadas públicas para chamadas familiares como Terraria.Main.NewText e membros de Player/Item/NPC/Projectile. Essa sintaxe ainda é um objetivo; a saída atual é um catálogo interno, não um SDK de gameplay pronto.
5. Gerar accessors de campos/propriedades respeitando campos, mutação de structs e referências do contrato público. Não converter tudo para propriedades sem contabilizar os casos ref.
6. Usar as assinaturas e classificações para oferecer hooks sob demanda, com original/unhook e controle de vida útil. Não instalar hooks em todos os métodos do jogo na inicialização.
7. Acrescentar diff entre dumps: métodos removidos, overloads novos, tipos/flags alterados e impacto nos mods. Regerar e testar sempre que o backend do Terraria mudar.

Metadados completos não fornecem uma forma universal de transportar qualquer valor entre runtimes. Genéricos, métodos abstratos, constructors, extern/internal calls, pointers, delegates, reflection e tipos por referência exigem tratamento próprio. Layouts de valores e a disponibilidade de funções nativas também precisam de validação.

A API adicional semelhante ao tModLoader continua sendo trabalho do Ivy: ModItem, ModPlayer, registros, novos IDs, ordem dos hooks e persistência não surgem automaticamente do dump vanilla. A automação dos acessos nativos fornece a base para implementá-los.

## Verificação realizada

O gerador passou em 14 verificações de contrato, incluindo overloads com mesmo número de parâmetros, ref/out, arrays de ranks distintos, genéricos de tipo/método, constantes, readonly, properties, eventos, abstratos, filtro de namespaces e determinismo. A fixture tem um inicializador que lança exceção caso seja executado: a análise dos metadados não o executa.

O catálogo real e o projeto C# gerado compilaram com zero erros. Isso é evidência da geração e da integridade de suas estruturas; não é evidência de execução dos 21.296 métodos nem de equivalência de todas as assinaturas na ABI Android.

## Perfil executável inicial — 0.4.0

O [primeiro perfil de gameplay](GAMEPLAY_SDK.md) gera Main/Player/On_Main por template validado contra as assinaturas reais. É separado do catálogo geral de descritores e não transforma os demais membros em acessos funcionais. Os testes do gerador agora incluem três verificações desse perfil (17 no total).
