# Mods selecionados e modo sem mods — 0.5.0 em teste

Branch: feat/mod-selection-vanilla. A main 0.4.1 validada foi mantida.
Este lote implementa a primeira pendência da etapa A do plano recebido.

## Mudanças de comportamento

Jogar prepara apenas os mods ativados em Meus mods. Uma seleção vazia é válida:
o serviço confirma 0 mods/0 sistemas e abre o Terraria. Nesse caso não solicita
uma nova sonda IL2CPP. Se a ponte já havia sido usada na mesma sessão do processo,
seus callbacks C# são desativados; o trampoline nativo continua preservado e pode
voltar a despachar quando houver handlers. Em um processo novo sem mods não há
instalação do hook.

Instalar uma DLL não executa seus inicializadores e não a ativa. A inspeção usa
PEReader/System.Reflection.Metadata: assembly, referência Ivy.Api, versão, classe
Mod e construtor público sem argumentos. Compatível na lista significa aceito
nesse preflight, não validação de todas as APIs que o mod pode chamar. Dependências
entre mods, herdadas de DLLs externas e o preflight completo de símbolos ainda
não estão implementados.

Meus mods usa ListView com reciclagem de linhas, seleção individual e Desativar
todos. A lista distingue desativados/selecionados e problemas de compatibilidade.
Ela lê o catálogo em ações explícitas; o painel deixou de reler o relatório completo
a cada segundo. Pesquisa, abas, autor/ícone, perfis e desinstalação continuam pendentes.

## Instalação, atualização e dados existentes

A biblioteca fica em mod-library/installed.json. As DLLs são armazenadas por hash
em mod-library/assemblies. A identidade inicial é derivada do nome do assembly;
não é ainda o manifesto definitivo de um pacote .ivymod.

- Novas importações começam desativadas.
- Reimportar/atualizar a mesma identidade preserva a seleção.
- Uma atualização incompatível não substitui a instalação anterior.
- Versões antigas dos blobs permanecem disponíveis para sessões já carregadas.
- O catálogo usa arquivo temporário, flush e rename, com cópia .previous.
- Escritas/leitura de snapshots têm bloqueio entre processos; hashes são validados
  ao ativar e preparar a sessão. As chaves de armazenamento só aceitam SHA-256 hexadecimal.
- Catálogo inválido é preservado e a preparação falha; não é redefinido silenciosamente.

Na primeira abertura, DLLs da pasta antiga mods são migradas como desativadas.
Os arquivos originais permanecem intactos. Uma versão importada explicitamente
tem preferência sobre o ExampleMod.dll copiado automaticamente pelas versões
anteriores. DLLs inválidas ficam preservadas no legado, com diagnóstico da migração.

O app não copia mais ExampleMod em OnCreate. Builds públicos não embutem a DLL.
No APK de desenvolvimento, Instalar exemplo é uma ação opcional e não substitui
um mod de mesmo nome, inclusive em uma corrida com outra importação.

O limite inicial é 32 MB por DLL e 512 registros. Pacotes, assets, dependências e
compilação .cs no aparelho pertencem aos próximos lotes; não há stubs de compilador.
A importação atual exige uma DLL compilada para o SDK Ivy.

## Sessões e alteração da seleção

A carga usa um snapshot determinístico da seleção. Falha em um mod desfaz o lote
já carregado e seus hooks; não desinstala arquivos nem altera a seleção persistida.
O serviço devolve o fingerprint da sessão e o painel confere a seleção novamente
antes de abrir a Unity.

Se a seleção ou a DLL ativa mudou depois da preparação, o jogo não abre usando uma
sessão antiga silenciosamente. Descarregue a sessão no painel e prepare/Jogue de
novo. O novo catálogo não apaga jogadores, mundos, arquivos legados ou configurações.
Não há suporte novo de persistência de conteúdo de mods neste lote.

## Evidência e limites

A 0.4.1 foi validada no Android 14: confirmação automática de 1 mod/1 sistema,
3.600 originais/callbacks correspondentes e vida 86 → 91 → 96 → 100 relida no IL2CPP.
Isso não valida automaticamente a seleção/migração/tela da 0.5.0.

Os testes locais incluem sessão vazia, ativação persistida, DLL não registrada,
atualização e rollback, inspeção sem executar cctor, exemplo sem sobrescrita,
concorrência e bloqueio entre dois processos, conteúdo alterado, catálogo inválido,
falha parcial de carga e migração mantendo os arquivos de origem. A build verifica
JNI também para ModsActivity e a variante pública sem o asset de exemplo.

Não há números de FPS/overhead medidos neste lote. Ring buffer/limites de arquivo,
modo seguro após crash, pacote .ivymod, compilador no aparelho, Workshop, conteúdo
novo e multiplayer continuam na matriz como pendentes.

## Teste integrado no Android

1. Atualize Ivy SDK Test. Os mods anteriores aparecem instalados e desativados.
2. Sem ativar nada, toque Jogar: deve confirmar 0 mods e abrir sem o efeito de cura.
3. Volte ao painel, descarregue a sessão, ative ExampleMod em Meus mods e jogue.
   Receba dano e confira a cura do exemplo.
4. Descarregue a sessão, desative o exemplo e jogue novamente: a cura adicional deve parar.
5. Reabra o painel e confira a seleção persistida; exporte o relatório com a biblioteca.

O exemplo é opcional e está disponível neste APK de desenvolvimento. A versão
pública do runtime foi compilada separadamente sem o asset. A instalação usa o
mesmo pacote/chave do SDK Test 0.4.1 para preservar seus dados.
