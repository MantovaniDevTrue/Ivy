# Próximas etapas

1. Runtime C# e DLL externa — validado no Android 14 ARM64 na base 0.1.0.
2. Ivy, repositório e build automática — lote atual.
3. Integrar o runtime ao processo do Terraria e resolver metadados IL2CPP da versão exata.
4. Validar um hook de atualização e acesso a campo vanilla na thread do jogo.
5. Adicionar item próprio com textura, receita e projétil; validar salvar e reabrir.
6. Expandir a API por contratos testados, incluindo NPCs, tiles, rede e interfaces.

O APK base recebido foi identificado como Terraria 1.4.5.8.6, build 301720, Unity/IL2CPP, com bibliotecas ARM64 e ARMv7. O inventário e o hash estão em android-base.json. O APK e os recursos do jogo ficam fora do repositório.

A compatibilidade inicial será de código-fonte com uma versão fixada da API do tModLoader. Não há promessa de carregar diretamente mods .tmod do PC.
