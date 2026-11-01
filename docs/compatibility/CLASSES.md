# Catálogo inicial de API e critérios de compatibilidade

Referência: perfil em [baseline.json](baseline.json), fases em [ROADMAP.md](../ROADMAP.md). Esta tabela organiza famílias, não substitui a extração completa de membros do lote 0. Nomes aqui são alvos de API; salvo a indicação explícita, não estão implementados.

| Família | Classes/tipos a cobrir | Lotes | Aceite principal |
| --- | --- | --- | --- |
| Entrada e integração | Mod, ModLoader, Mod.Call, logs esperados pelos mods | 0/3 | Identidade, dependências, calls e ciclo de carga |
| Base de tipos | ModType e formas genéricas, ModTexturedType, GlobalType, IModType, ILoadable | 0/3 | Herança, registro, templates e instâncias |
| Atributos | Autoload, AutoloadEquip, atributos do contrato | 0/3/5 | Descoberta e decisões de carga idênticas |
| Consulta de conteúdo | ModContent, ModTypeLookup e acessos públicos exigidos | 3 | Find/TryFind/GetInstance e IDs corretos |
| Entidades vanilla | Main, Entity, Player, Item, NPC, Projectile, Tile | 1/2 | Identidade/geração, campos, métodos, arrays e refs |
| IDs | ItemID, NPCID, ProjectileID, TileID, WallID, BuffID, PrefixID, AmmoID, SoundID e Sets/Search associados | 2/3 em diante | Constantes mapeadas por versão, tabelas expandidas e tipos corretos |
| Fontes de entidades | IEntitySource, EntitySource_* e métodos GetSource_* necessários | 2/4 | Origem/contexto e assinatura, inclusive overloads |
| Matemática | Microsoft.Xna.Framework.Vector2/3/4, Color, Rectangle, Point, Matrix, MathHelper, GameTime | 2 | Operações, mutação de structs, layout/marshalling |
| Utilitários | Utils, Collision, Lighting, UnifiedRandom, Point16 e helpers públicos exigidos | 2 em diante | Resultado e consumo de RNG compatíveis |
| Conteúdo compartilhado | TextureAssets, ContentSamples, tabelas Main e registros usados por mods | 3 em diante | Tipo, tamanho, ownership e conteúdo sincronizados |
| Itens | ModItem, GlobalItem | 4 | Defaults, uso, drop, clone, draw, hits, save e globais |
| Receitas | Recipe, RecipeGroup, Condition e tipos de ingrediente/condição necessários | 4 | Craft, grupos, callbacks, consumo e condições |
| Tooltips | TooltipLine, DrawableTooltipLine quando exigido | 4/8 | Texto, ordem, cor e desenho |
| Projéteis | ModProjectile, GlobalProjectile | 4 | AI, colisão, imunidade, origem, kill e instanciação |
| Buffs | ModBuff, GlobalBuff, BuffID.Sets | 4/5 | Duração, reset, aplicação, tags e jogador/NPC |
| Jogadores | ModPlayer | 5 | Instância por jogador, reset/update, eventos e persistência |
| Dano | DamageClass, StatModifier, StatInheritanceData | 5 | Herança de efeitos/stats, cálculo, retornos por ref |
| Hits e hurt | NPC.HitInfo/HitModifiers, Player.HurtInfo/HurtModifiers e associados | 5 | Mutação, ordem, knockback, crítico e cancelamento |
| Prefixos e raridade | ModPrefix, ModRarity | 5 | Aplicação, reforja, save e tooltip |
| Equipamento | EquipType, EquipTexture, EquipLoader, ModAccessorySlot, atributos | 5/8 | Slots, sprites, armaduras, acessórios e camadas |
| NPCs | ModNPC, GlobalNPC | 6 | Spawn, AI, animação, dano, dificuldade e kill |
| Bosses | ModBossBar, GlobalBossBar, ModBossBarStyle e registros de boss heads | 6/8 | Barra, ícone, retrato e progresso |
| Lojas | NPCShop, AbstractNPCShop e entradas/condições | 6 | Condição, preço, disponibilidade e atualização |
| Loot | NPCLoot, ItemLoot, IItemDropRule, ItemDropRule e condições/chains necessárias | 6 | Chance, dificuldade, quantidade e autoridade |
| Bestiário | BestiaryEntry, BestiaryDatabase e elementos necessários | 6/8 | Registro, texto, visual e desbloqueio |
| Blocos | ModBlockType, ModTile, GlobalTile | 7 | Colocação, frames, quebra, draw, wiring e IDs |
| Paredes | ModWall, GlobalWall | 7 | Colocação, conversão, mineração e persistência |
| Multitiles | TileObjectData, TileObject, PlacementHook e AnchorData | 7 | Pivôs, tamanhos, anchors e frames |
| Tile entities | ModTileEntity, TileEntity e registros | 7/9 | Identidade, validação, atualização, save e sync |
| Mundo e sistemas | ModSystem | 1/3/7 em diante | Ordem dos hooks, estado por mundo e limpeza |
| Worldgen | GenPass, PassLegacy, GenerationProgress, GameConfiguration e tipos exigidos | 7 | Ordem, progresso, RNG e mundo recuperável |
| Serialização | Terraria.ModLoader.IO.TagCompound, TagIO, TagSerializer e IO de conteúdo | 4/5/7 | Tipos, defaults, clone, erro/migração e recuperação |
| Assets | ReLogic.Content.Asset<T>, AssetRequestMode, acessos de conteúdo | 3/8 | Pedido, Value, cache, falha, thread e descarte |
| Texturas e desenho | Texture2D, SpriteBatch, SpriteFont, GraphicsDevice, DrawData e estados necessários | 3/8 | Geometria, blend, escala, recorte, recursos e contexto |
| Camadas do jogador | PlayerDrawLayer, PlayerDrawSet | 5/8 | Ordem, orientação e equipamento |
| Visuais de conteúdo | ModDust, ModGore, ModCloud, ModHair e tipos de animação | 8/10 | Spawn/draw, vida útil, atlas e cache |
| Áudio | Terraria.Audio.SoundStyle, SoundEngine, ActiveSound; ReLogic.Utilities.SlotId | 8 | Playback, variantes, slots, atualização e limpeza |
| Música | MusicLoader e assets/contratos de música | 8/10 | Troca de cena, prioridade e liberação |
| UI | UIState, UIElement, UserInterface, StyleDimension e controles necessários | 8 | Layout, clipping, scroll, drag/drop e captura de input |
| Entrada | ModKeybind, KeybindLoader, TriggersSet e PlayerInput exigidos | 8 | Press/held/release, contexto, touch e ação alternativa |
| Localização | LocalizedText, Language, LanguageManager e HJSON | 3/8 | Chaves, fallback, formatação e troca de idioma |
| Rede | ModPacket, Mod.HandlePacket, NetMessage/NetmodeID, NetSend/NetReceive/extra AI | 9 | Pacotes, IDs, autoridade e entrada tardia |
| Biomas/cenas | ModBiome, ModSceneEffect, SceneEffectPriority | 10 | Ativação, enter/leave, peso, prioridade e sync |
| Backgrounds | ModBackgroundStyle, ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, GlobalBackgroundStyle | 10 | Seleção, transições e draw |
| Água/cachoeira | ModWaterStyle, ModWaterfallStyle | 10 | Cor, estilos, recursos e troca de cena |
| Skies e filtros | Terraria.Graphics.Effects.CustomSky/Filter e dados de shaders necessários | 10/11 | Desenho e parâmetros reais, sem no-op silencioso |
| Natureza | ModTree, ModPalmTree, ModCactus, ModBiomeConversion | 7/10 | Crescimento, desenho, quebra e conversão |
| Montarias/movimento | ModMount, Mount.MountData, ExtraJump | 5/10 | Movimento, draw, slots e estado |
| Pylons | ModPylon, GlobalPylon e tipos associados | 7/10 | Colocação, validade, UI e teleporte |
| Informação/mapa | InfoDisplay, GlobalInfoDisplay, ModMapLayer | 8/10 | Conteúdo, draw, posição e visibilidade |
| Menus/recursos | ModMenu, ModResourceDisplaySet, ModResourceOverlay | 8/10 | Camadas, layout e seleção |
| Emotes | ModEmoteBubble e tipos associados do perfil | 10 | Seleção, frames e draw |
| Comandos | ModCommand, CommandCaller, CommandType | 3/10 | Parsing, contexto e respostas |
| Config | Terraria.ModLoader.Config.ModConfig, ConfigScope, atributos e controles associados | 10 | Defaults, serialização, reload, UI e client/server |
| Hooks universais | On.Terraria.*, delegates orig/hook | 11 | ABI, cadeia, ref/out, original e unhook |
| Alteração de IL | IL.*, MonoMod/Cecil/ILCursor quando exigidos | 11/12 | Categoria experimental, diagnóstico por padrão suportado |
| Gráficos avançados | Effect, RenderTarget2D, filtros, shaders e estados avançados | 11 | Efeito real, conversão quando necessária e contexto gráfico |
| Build/pacotes | Manifest, referências, generators, dependências, assets e formato .tmod/Ivy | 0/12 | Mesmos fontes, diagnóstico e carregamento reproduzível |

Implementação atual: `Mod` e `ModSystem` têm somente o ciclo inicial de DLL e vínculo básico. Isso é suporte parcial, sem a hierarquia e todos os contratos do perfil. As demais linhas são trabalho planejado. Tipos internos só entram quando forem necessários para um contrato público ou para um recurso de compatibilidade explicitamente suportado.

## Ficha obrigatória de cada hook

Assinatura completa e revisão de origem; hook/posição nativa; thread; ordem entre vanilla/global/conteúdo; defaults; bool?/cancelamento; ref/out/in; cliente/servidor/proprietário; instância/template; clone; save/rede aplicáveis; limpeza; custo; exemplos e evidência no aparelho.

Cada símbolo deve ter estados independentes de declaração, compilação, backend, comportamento e validação Android. Implementações vazias não podem elevar a cobertura. Valores padrão vazios só são aceitáveis quando esse for o comportamento do contrato original.

## Contrato de uso que queremos preservar

Este exemplo demonstra a sintaxe de destino. **Ainda não compila no Ivy 0.2.1**, pois ModItem e Recipe estão planejados.

```csharp
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

public sealed class IvyBlade : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.damage = 24;
        Item.DamageType = DamageClass.Melee;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.IronBar, 10)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
```

A configuração de build seleciona as referências Ivy; o código do item conserva os nomes e a estrutura esperados. Mudanças de plataforma, quando necessárias, aparecem no relatório de port.

## Corpus real já consultado

O arquivo fornecido de fontes decompilados do Split contém 1.882 arquivos C#. O inventário simples encontrou subclasses diretas de ModItem (648), ModProjectile (260), ModTile (100), ModBuff (90), ModNPC (79), ModSystem (20) e outras famílias. Há 1.127 diretivas using de Microsoft.Xna.Framework, 688 de gráficos e 499 de Terraria.Audio.

Esses números são contagens de texto por expressão regular, sem resolução de herança; não são prova de compilação ou funcionamento e não representam todos os membros exigidos. O inventário e sua proveniência estão em [split-source-inventory.json](split-source-inventory.json). Nenhum fonte do mod foi copiado para o Ivy.
