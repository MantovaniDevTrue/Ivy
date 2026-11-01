# Ivy — roadmap de compatibilidade com tModLoader

Plano de 31/10/2026. Autor do projeto: Mantovani.

Objetivo: desenvolver uma API própria do Ivy, muito fiel à experiência e aos contratos públicos do tModLoader, para recompilar fontes de mods PC preservando namespaces, classes, assinaturas e comportamento. A meta dos exemplos de API pura é nenhuma mudança nos arquivos C#. Adaptações de plataforma devem ficar concentradas em build, dependências, assets e entrada.

## Base validada

Ivy 0.2.1 iniciou no Android 14 ARM64, carregou uma DLL externa (1 mod/1 sistema), encontrou C# e IL2CPP no mesmo PID, 303 assemblies, Terraria.Main e get_menuMode. Mantovani confirmou a entrada normal em um mundo. A 0.3.0 implementa a primeira ponte C++ de gameplay, com callback diagnóstico C# após DoUpdateInWorld; o relatório do Android 14 confirmou os ticks 1/60 e leitura de vida. A 0.3.1 isola o painel para sobreviver à saída do processo Unity; essa mudança e o ciclo completo ainda aguardam teste no aparelho. Hooks públicos e registro de conteúdo continuam pendentes. Testes locais não equivalem a recursos tModLoader completos. Veja [NATIVE_BRIDGE.md](NATIVE_BRIDGE.md).

Perfil inicial: **tModLoader v2026.08.3.0**, commit **c9545cf27d56c456321e1896274c02adf67d0140**, release identificado como linha **Terraria 1.4.4**. Backend: **Terraria Android 1.4.5.8.6, build 301720**. As diferenças vanilla entre versões serão adaptadas dentro do Ivy. A documentação `/stable/` é móvel; o contrato deve ser extraído da revisão fixada, que prevalece em divergências. Novos perfis serão separados.

Dados: [baseline.json](compatibility/baseline.json). O plano usa contratos públicos de referência; não importa a implementação tModLoader.

## Critério de compatibilidade

| Camada | Objetivo | Evidência |
| --- | --- | --- |
| Fonte | Os mesmos arquivos C# compilam com o SDK Ivy | Fixtures e fontes reais compilados |
| Comportamento | Mesmas regras de hooks, instâncias e resultados | Sequências de eventos e resultados comparados |
| Dados | Estado do mod sobrevive aos ciclos do Ivy | Save/reload, atualização e recuperação |
| Rede | Instâncias Ivy mantêm estado consistente | Testes cliente/servidor por perfil |

O formato de distribuição será próprio do Ivy. O fluxo de port parte dos fontes e assets do mod, seleciona as referências do SDK Ivy, resolve as diferenças de plataforma e gera um pacote Ivy. Leitura/importação de `.tmod`, execução de DLLs de mods já compiladas para tModLoader e compatibilidade binária com seus assemblies ficam fora do escopo. Bibliotecas .NET comuns podem ser usadas quando forem compatíveis com o runtime e as referências do projeto.

Classes como `ModItem`, `ModProjectile` e `ModPlayer`, hooks como `SetDefaults`/`AI` e o padrão de receitas permanecem familiares. A autoria e a implementação da API, seus assemblies, runtime e pacote são do Ivy. Os nomes públicos compatíveis reduzem alterações no C#; extensões exclusivas do Ivy ficam separadas para não se tornarem uma exigência desnecessária de cada port.

Compatibilidade de fonte não implica importar saves PC ou conectar em servidor PC tModLoader.

## Arquitetura proposta

| Camada interna | Função |
| --- | --- |
| Ivy.Api | Contratos Terraria.ModLoader, Config, IO e relacionados |
| Ivy.Terraria | Terraria.*, extensões tModLoader e adaptação vanilla por versão |
| Ivy.Framework | Contratos XNA/ReLogic, matemática e ponte para recursos gráficos/áudio existentes |
| Ivy.Runtime | Dependências, carga, registros, instâncias e dispatch |
| Ivy.IL2Cpp | Metadados, ABI, marshalling, handles, scheduler, hooks e original |
| Ivy.Content | IDs, tabelas, assets, receitas e localização |
| Ivy.Persistence / Ivy.Network | Dados persistentes, migração, pacotes e autoridade |
| Ivy.Build | SDK, compilação, análise de APIs/dependências e diagnóstico de port |
| Ivy.Android | Inicialização, painel, touch, armazenamento e relatórios |

São fronteiras propostas, não projetos já implementados. A divisão final de assemblies depende dos contratos e das medições. Os mods continuam usando a API pública esperada.

O dump enviado já contém tipos Microsoft.Xna.Framework como Vector2, Color, SpriteBatch, Texture2D, Effect e SoundEffect. A primeira opção é aproveitar os caminhos nativos existentes onde forem adequados. Objetos CLR e IL2CPP continuam exigindo conversão, e a presença de Effect não comprova suporte a shaders FNA do PC.

## Lote 0 — contrato e ferramentas

Prioridade atual: gerar automaticamente a base de acessos do jogo. O primeiro gerador lê as DummyDlls, cataloga os membros e produz descritores/delegates; detalhes e limites em [NATIVE_BINDINGS.md](NATIVE_BINDINGS.md). As chamadas nativas e fachadas públicas ainda precisam ser implementadas e validadas.

Entregar um perfil reproduzível de API e um analisador do que cada mod exige.

- Inventariar namespaces, tipos, herança, interfaces, visibilidade, campos/propriedades, overloads, genéricos, atributos, defaults, ref/out/in e retornos por referência.
- Incluir Terraria.*, XNA e ReLogic, além de Mod*. A API Terraria alterada pelo tModLoader também precisa de implementação.
- Criar SDK/projeto de build gerado a partir dos fontes originais, sem manter um segundo port divergente. Ajustes de targets ficam no adaptador de build.
- Fazer preflight sem executar inicializadores do mod: símbolos ausentes, reflection privada, IL hooks, DLLs nativas, shaders, runtime, source generators e dependências.
- Informar arquivo/linha quando disponível, API faltante, categoria e alternativa prevista. Um símbolo declarado não equivale a funcionalidade suportada.

Aceite: uma mudança incompatível de assinatura falha no CI; o mesmo perfil produz sempre o mesmo inventário; os exemplos usam os nomes originais da API.

## Lote 1 — ponte e primeiro hook real

Primeiro adaptador implementado na 0.3.0: libivy-native.so + ShadowHook 2.0.1, contadores e leitura diagnóstica na thread do mundo. O aceite completo abaixo continua pendente; não há dispatch para mods nesta versão.

Entregar execução C# no ponto correto do loop do Terraria.

- Resolver métodos por assembly/tipo/assinatura/versão. Validar ABI, parâmetros implícitos, sobrecargas e passagem de structs.
- Hook de atualização preservando o original, ownership, ordem, encadeamento e retirada segura. Delegates/handles vivem enquanto o motor pode chamá-los.
- Diferenciar thread Android, gameplay e contexto de render. Anexar uma thread ao IL2CPP não a transforma na thread do jogo.
- Fila limitada apenas para operações que podem ser adiadas. Hooks com retorno/ref executam sincronamente no ponto original.
- Contadores distinguem ticks de simulação e frames renderizados; sem resolução de metadados ou logs por tick.

Aceite: original chamado uma vez, callbacks na thread correta, pausa/retomada/reentrada corretas e nenhum callback depois do unload.

## Lote 2 — entidades e fidelidade de acesso

Entregar fachadas Main, Entity, Player, Item, NPC, Projectile, Tile e os acessos necessários a WorldGen, Collision, Lighting, Utils e NetMessage.

- Matemática/valores: Vector2/3/4, Rectangle, Point, Color, Matrix, MathHelper, GameTime, Point16 e random. Qualificar tipos para distinguir XNA de Unity.
- Converter strings, arrays, structs e objetos entre os runtimes. Validar alinhamento e tamanho; um objeto Mono não é um objeto IL2CPP.
- Identidade canônica por objeto/slot/geração. Estado de um projétil morto não pode migrar para o próximo ocupante do slot.
- Main.player/npc/projectile, inventários e ai/localAI precisam de acesso e mutação reais, com clone e vida útil corretos.

Bloqueador: trocar todos os campos por propriedades quebra casos como `ref player.statLife`, campos de structs, reflection e referências guardadas. Antes de expandir a API, comparar fachadas, espelhos limitados por callback e eventual transformação de IL na compilação. Espelhos publicam alterações antes de chamadas nativas dependentes e atualizam leituras após mutações nativas. Transformações só serão adotadas com testes e limites explícitos.

Aceite: leitura/escrita/ref/out, arrays, native calls no meio de um hook, clone e reciclagem de slot passam. Evitar cópia integral de entidades e mapa a cada frame. Limitações restantes entram no relatório do mod.

## Lote 3 — núcleo de conteúdo e assets básicos

Completar Mod, ModType e suas formas genéricas, GlobalType, ModTexturedType, interfaces, atributos e ModContent.

- Name, FullName, Mod, Type, templates versus instâncias, tipos abstratos/genéricos, carga condicional e descoberta automática.
- Passes globais de registro/setup/finalização conforme a revisão fixada. O runner inicial Load/Unload será ampliado; não representa o ciclo completo tModLoader.
- Dependências obrigatórias/opcionais, ordenação, ciclos, Mod.Call, TryGetMod, logs e isolamento de falhas.
- ReLogic.Content.Asset<T>, pedidos/cache de assets, texturas e desenho básico, paths/case, LocalizedText, Language e HJSON.
- IDs persistentes por mod/nome, separados dos IDs da sessão. Auditar arrays, caches e caminhos nativos com limites fixos; aumentar um array isolado não basta.

Aceite: dois mods referenciam conteúdo um do outro; localização e textura funcionam; IDs persistentes não mudam quando a ordem dos mods muda. GetInstance/templates não substituem instâncias por jogador ou entidade.

## Lote 4 — itens, projéteis, receitas e buff básico

Implementar ModItem/GlobalItem, ModProjectile/GlobalProjectile, ModBuff/GlobalBuff, Recipe, RecipeGroup, Condition, TooltipLine e os IDs/Sets necessários.

- Defaults, uso, munição/mana, disparo, spawn, AI, colisão, hit, kill, tooltip e desenho básico.
- Instâncias, clones, InstancePerEntity, AppliesToEntity e estado separado entre mods.
- Cada hook preserva sua ordem, valor padrão, bool?, cancelamento, ref/out e lado de execução. Não inventar uma regra de ordem única para todos os hooks.
- Novo conteúdo precisa funcionar em inventário, drop, baú e mundo. Persistência mínima e recuperação de conteúdo ausente entram junto.
- Direção do jogador sincronizada com a mira antes do disparo; pivô e origem coerentes com touch.

Aceite: espada, arma, projétil próprio, buff e receita, com dois mods ativos e save/reload. Registrar conteúdo próprio, sem usar substituição permanente de item vanilla como produto final. Fixtures de API pura mantêm seus fontes C#.

## Lote 5 — jogador, combate e equipamento

Implementar ModPlayer, DamageClass, StatModifier, StatInheritanceData, ModPrefix, ModRarity, ModAccessorySlot, EquipLoader, EquipTexture e atributos.

- Reset/equipamentos/buffs/update, dano/crítico/knockback/velocidade, hurt/dodge, morte, respawn e movimento.
- NPC.HitInfo/HitModifiers e Player.HurtInfo/HurtModifiers com mutações e ordem corretas.
- Armaduras, acessórios compostos, loadouts, minions/sentries, slots, chicotes/tag buffs e imunidade.
- Prefixos e estado por jogador persistem corretamente. Conferir cálculo e arredondamento, não apenas tooltip.

Aceite: armadura, acessório, prefixo reforjável e summon próprios; dois jogadores sem estado compartilhado; morte/respawn e save/reload corretos.

## Lote 6 — NPCs, bosses e progressão

Implementar ModNPC/GlobalNPC, ModBossBar/GlobalBossBar/ModBossBarStyle, shops, bestiário e registros associados.

- NPCs próprios, AI/fases, animação, ataques, colisão, dificuldade, morte, barra, ícones e retratos.
- NPCLoot, ItemLoot, regras/condições de drop e flags de progressão.
- Town NPCs, diálogo, nomes, lojas e condições de chegada.
- Integração entre mods por API pública, incluindo checklist quando seu contrato estiver suportado.

Aceite: inimigo, town NPC e boss próprios com sprites, loot, barra e progressão persistente. A validade em rede será acrescentada no lote 9.

## Lote 7 — mapa, tiles e persistência completa

Implementar ModTile/GlobalTile, ModWall/GlobalWall, ModBlockType, ModTileEntity, TileObjectData, placement hooks e estruturas associadas.

- Blocos, plataformas, multitiles, crafting stations, baús, iluminação, mineração, enquadramento, mapa, wiring e interação.
- ModSystem para mundo/worldgen, GenPass, PassLegacy, GenerationProgress, configuração e conversões.
- Armazenamento esparso/regiões alteradas, evitando um espelho CLR de todo o mapa.
- TagCompound, TagIO e serializadores para item/jogador/mundo/tile entity; schema, migração e gravação temporária/atômica.
- Conteúdo ausente preserva identidade, frames e dados recuperáveis. Não destruir conteúdo desconhecido ao abrir sem um mod.

Aceite: tiles alinhados, baú e tile entity persistentes, tarefa de geração, reabertura com/sem mod e recuperação após reativação. Atualização de mod não corrompe silenciosamente dados. Formato Ivy compatível em API não é promessa de formato binário igual ao PC.

## Lote 8 — desenho, áudio, interface e touch

Expandir Texture2D, SpriteBatch, SpriteFont, estados de desenho, DrawData, TextureAssets, animações e recursos ReLogic.

- PlayerDrawLayer/PlayerDrawSet, ModHair, ModDust/ModGore/ModCloud, trails e desenho customizado.
- UIState/UIElement/UserInterface, painéis, listas, barras, StyleDimension, clipping, escala, foco, scroll e drag/drop.
- SoundStyle, SoundEngine, ActiveSound, SlotId, músicas, cache e liberação. Mapear representação/contratos de áudio PC para mobile.
- ModKeybind, KeybindLoader e triggers recebem controles touch. AltFunctionUse e ações de armadura continuam pela API comum.
- Respeitar o contexto/thread dos recursos gráficos e aproveitar as implementações nativas adequadas.

Aceite: UI completa, input capturado corretamente, arma alinhada, trails e áudio sem duplicação; pause/resume preserva recursos. Comparar visual e medir custo mantendo efeitos.

## Lote 9 — multiplayer Ivy

Implementar ModPacket, Mod.HandlePacket, NetSend/NetReceive, extra AI e sincronização de jogador, entidades e mundo.

- Handshake de perfil/API, lista/hash de mods, configs e mapeamento de conteúdo/IDs.
- Autoridade de spawn/dano/loot/geração e distinção singleplayer, cliente, servidor e proprietário por contrato.
- Entrada tardia, desconexão/reentrada, perda de pacotes, tamanhos/valores inválidos e estado antigo.
- Host dedicado Ivy planejado como backend próprio; não anunciar dedicated server por existir um cliente Android.

Aceite: dois clientes Ivy com host/servidor, sem duplicação de dano/drop/spawn; conteúdo dos lotes anteriores sincronizado. Incompatibilidades detectadas antes de entrar no mundo.

Crossplay com servidor PC tModLoader será investigação separada de protocolo, versão vanilla e dependências.

## Lote 10 — ambiente e famílias complementares

Expandir ModBiome, ModSceneEffect, backgrounds, águas/cachoeiras, skies, conversões, músicas e prioridades de cena.

- ModMount, ExtraJump, pets, pesca, eventos e sistemas/achievements exigidos pelo perfil.
- Árvores/cactos/palmeiras, pylons, emotes, informação, map layers, menus e overlays.
- ModConfig e atributos, defaults, persistência, reload e autoridade client/server.
- Revisar famílias menos frequentes pelo inventário de contrato; a matriz inicial não é uma lista fechada da API.

Aceite: exemplos por família com ciclo de vida, troca de cena/bioma e limpeza; configurações persistem e sincronizam conforme o contrato.

## Lote 11 — hooks universais e gráficos avançados

- Fachadas On.Terraria.* nos métodos suportados, delegates orig/hook, ref/out, encadeamento e unhook.
- O backend usa detours nativos auditados, com diagnóstico para métodos/genéricos não suportados.
- IL.*, MonoMod/Cecil e ILCursor exigem tratamento separado: o corpo nativo IL2CPP não é o IL CLR do PC. Não prometer tradução universal de patches IL.
- Effect, RenderTarget2D, filtros, shaders e pipelines avançados após o desenho básico. Bytecode PC exige estratégia própria validada para Android.
- Auditar reflection pública sobre fachadas; internals privados, ponteiros e bibliotecas nativas de PC podem exigir adaptação.

Aceite: dois mods encadeiam hook e restauram original; recursos gráficos sobrevivem ao ciclo do app. Recurso não implementado tem diagnóstico. API vazia ou efeito desativado não conta como compatibilidade.

## Lote 12 — compilação no telefone e pacotes

- Compilador C# Android com referências do perfil e diagnósticos por arquivo/linha; o SDK desktop continua para CI.
- Cache por hash de fontes, referências, perfil e opções. Carga termina antes do mundo; recompilar apenas o necessário.
- Source generators/analyzers executados em pipeline suportado ou resultados gerados incorporados corretamente; nunca ignorar código gerado.
- Pacote Ivy com manifest/dependências/fontes/DLLs/assets, instalação, ativação/desativação e atualização.
- Ferramentas de port trabalham com fontes e assets, geram a configuração de build Ivy e apontam adaptações pequenas por arquivo/linha. Dependências específicas do tModLoader precisam de equivalentes ou recompilação para Ivy.
- Suporte a contêineres `.tmod` e execução de DLLs de mods compiladas para PC não integram este lote nem os marcos futuros deste roadmap.
- Relatório por mod concentra diferenças de plataforma e mudanças necessárias, preservando os fontes de gameplay nos casos suportados.

Aceite: mod pequeno e intermediário compilados/importados no celular; segunda carga usa cache; dependências ausentes impedem ativação com diagnóstico; troca de mods não deixa callbacks nativos ativos.

## Lote 13 — paridade por corpus e beta

Fixtures próprias de contrato e comportamento acompanham todos os lotes. Usar os fontes de mods fornecidos para auditoria/port, mantendo créditos e condições aplicáveis.

- Progredir de conteúdo pequeno a combinações de sistemas, UI/storage e expansões grandes.
- Split é um corpus representativo; não será declarado automaticamente compatível pela existência de ModItem.
- Comparar compilação, eventos, instâncias, combate, saves, rede, áudio, animações e efeitos com múltiplos mods.
- Medir cobertura por símbolos necessários, comportamento, famílias e corpus separadamente. Classes vazias/linhas de código não medem paridade.
- Medir arquivos/linhas originais alterados em cada port e por quê. Meta: gameplay preservado nos casos suportados e adaptações concentradas.

Aceite: publicar matriz por versão e por mod, evidências e limites conhecidos. O rótulo vale para perfis/cenários testados, não para qualquer mod PC.

## Matriz, performance e entregas

Catálogo inicial: [CLASSES.md](compatibility/CLASSES.md). Cada símbolo evolui separadamente: inventariado → declarado → compila → ligado ao backend → comportamento verificado → save/rede quando aplicáveis → validado no mobile. Itens não aplicáveis exigem justificativa.

Cada hook registra assinatura, ponto nativo, thread, ordem, default, cancelamento, null, proprietário, client/server, instâncias e limpeza. Fase concluída exige seu aceite integrado, não apenas classes criadas.

Em todos os lotes:

- Medir cenário vanilla equivalente e delta do Ivy: frame time, p95/p99, alocações, heap, texturas e custo por hook. Alvo de 60 FPS nos cenários acordados, sem garantia independente de hardware/conteúdo.
- Resolver metadados na carga, cachear delegates e despachar somente interessados. Evitar reflection, nomes e varreduras globais por tick.
- Reusar buffers, limitar filas, controlar ownership de caches/recursos e evitar duplicar mundo/entidades entre runtimes.
- Otimizar mantendo efeitos: batching, pooling, culling correto, cache e atualização por eventos; verificar visual e função junto com performance.
- Testar menu/mundo/saída/reentrada, pause/resume, tela desligada, troca de mods e longa sessão. Conservar os checks de JNI e do APK final.
- Referência inicial: Xiaomi Mi 11 Lite 5G NE, Android 14 ARM64; ampliar ABI/Android depois com builds apropriadas.

Os lotes são dependências, não datas prometidas. Fazer CI entre alterações e consolidar APKs por comportamento completo ou bloqueador nativo, evitando um APK por classe.

**Próxima validação (0.6.0):** o fluxo 0.5.0 foi aceito no Android. Testar o perfil gerado de seis classes, runtime_invoke e On_Player.HealEffect com o exemplo atualizado. Depois ampliar famílias de marshalling (arrays/structs/ref/out), identidade persistente e HookGen, antes de declarar acesso universal. [Estado atual](GENERATED_ACCESS_0.6.0.md).

## Referências

- Release: https://github.com/tModLoader/tModLoader/releases/tag/v2026.08.3.0
- Revisão fixa: https://github.com/tModLoader/tModLoader/tree/c9545cf27d56c456321e1896274c02adf67d0140
- Navegação da API: https://docs.tmodloader.net/docs/stable/
- ModType: https://docs.tmodloader.net/docs/stable/class_mod_type.html
- ModContent: https://docs.tmodloader.net/docs/stable/class_mod_content.html
- ModItem: https://docs.tmodloader.net/docs/stable/class_mod_item.html
- GlobalItem: https://docs.tmodloader.net/docs/stable/class_global_item.html
- ModPlayer: https://docs.tmodloader.net/docs/stable/class_mod_player.html
- ModSystem: https://docs.tmodloader.net/docs/stable/class_mod_system.html
- ModProjectile: https://docs.tmodloader.net/docs/stable/class_mod_projectile.html
- ModNPC: https://docs.tmodloader.net/docs/stable/class_mod_n_p_c.html
- ModTile: https://docs.tmodloader.net/docs/stable/class_mod_tile.html
- ModTileEntity: https://docs.tmodloader.net/docs/stable/class_mod_tile_entity.html
- TagCompound: https://docs.tmodloader.net/docs/stable/class_tag_compound.html
- ModPacket: https://docs.tmodloader.net/docs/stable/class_mod_packet.html
- ModConfig: https://docs.tmodloader.net/docs/stable/class_mod_config.html

## Entrega 0.4.0 — primeiro perfil tipado

[Estado e limites](GAMEPLAY_SDK.md): Main/Player e On_Main.DoUpdateInWorld, orig controlável, encadeamento e unload por mod implementados. Gerador valida um template estreito contra o dump; não gera ainda fachadas universais. A execução da ABI 2 e a escrita de vida no aparelho são a próxima validação.
