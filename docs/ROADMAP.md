# Próximas etapas

1. Runtime C# e DLL externa — validado no Android 14 ARM64 na base 0.1.0.
2. Ivy e build automática — compilação 0.1.1 validada localmente e no GitHub.
3. Host Terraria e diagnóstico de metadados — implementados na 0.2.0; falha JNI na abertura confirmada no aparelho. A 0.2.1 adiciona registro dinâmico dos callbacks; novo teste pendente.
4. Instalar callback de atualização na thread do jogo e validar acesso vanilla.
5. Registrar item com textura, receita e projétil; validar salvar e reabrir.
6. Expandir por contratos testados: NPCs, tiles, rede e interfaces.

Base recebida: Terraria 1.4.5.8.6, build 301720, Unity/IL2CPP. Inventário e hash em android-base.json. O jogo e seus recursos ficam fora do repositório. A compatibilidade inicial é de código-fonte, sem promessa de executar diretamente .tmod do PC.
