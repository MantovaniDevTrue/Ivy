# Saída do Terraria — Ivy 0.3.1

O relatório recebido mostrou a ponte funcionando no Android 14: PID 11863,
callback no TID 12439, ticks 1/60 com 1/60 originais, menuMode 10, jogador 0,
vida 100. Depois o painel abriu em outro PID (12733). Não há stack trace ou
motivo de saída nesse arquivo; ele não permite distinguir quit da Unity, sinal
nativo ou encerramento pelo sistema.

Na 0.3.0, painel e Unity compartilhavam o processo. Qualquer encerramento desse
processo encerrava os dois. A 0.3.1 altera essa fronteira:

| Componente | Processo | Função |
| --- | --- | --- |
| MainActivity | dev.mantovani.ivy:launcher | Painel, importação e exportação |
| UnityPlayerActivity | dev.mantovani.ivy | Terraria/IL2CPP |
| GameControlService | dev.mantovani.ivy | Sessão de mods C#, diagnóstico e controle do hook |
| MonoRuntimeProvider | Ambos, instâncias distintas | Inicialização .NET em cada processo |

O painel tem taskAffinity própria e abre a Unity com NEW_TASK. Assim, remover
a tarefa do jogo também não remove a tarefa do painel. Nenhum método de quit,
destroy ou controle de licença do APK base foi alterado. A ponte nativa continua
dentro do processo do jogo; seus ponteiros nunca atravessam IPC.

Os botões de mods/hook enviam intents explícitas ao serviço não exportado.
O serviço encerra cada comando com StopSelf e NotSticky: encerrar a Unity não
deve provocar reinício automático do jogo por um comando antigo. A sessão de
mods é estática no processo do jogo, independente da instância do serviço.

Os logs do painel e do jogo usam arquivos diferentes. Somente iniciar um novo
processo do jogo gira ivy-session.txt para .previous. A exportação reúne painel,
última sessão do jogo e sessão anterior. O painel atualiza a leitura uma vez por
segundo enquanto está visível; a atualização é cancelada ao sair dele. Não há
polling de arquivos ou IPC por tick de gameplay.

No Android 11+, ao retomar o painel, ele consulta o histórico de saídas do próprio
pacote e registra PID, motivo, status, horário e descrição. São informações do
Android, que podem registrar inclusive sessões anteriores à atualização. Falhas
ao consultar esse histórico são registradas sem impedir a abertura do painel.

A importação escreve em .pending e renomeia antes de expor a DLL ao carregamento.
Uma DLL nova só participa da próxima sessão de mods. Não há carregamento duplicado
no painel.

## Validação local

Compilação C#/Android e teste de controle nativo passaram, assim como as 11
verificações do loader e 13 testes Python. O build verifica o JNI de MainActivity
e GameControlService e exige providers Mono nos dois processos. O empacotador
também exige painel/Unity em processos e tarefas diferentes, com Unity e serviço
no mesmo processo. A assinatura e a igualdade dos DEX/assets/libs de entrada
são conferidas após empacotar.

Isso verifica a configuração e o build. Ainda é necessário testar no aparelho:
entrar em um mundo, salvar, sair do Terraria, conferir o painel e abrir o jogo de
novo. Depois exportar o relatório. O comportamento exato da saída da Unity e a
causa do encerramento anterior continuam dependendo desse teste.
