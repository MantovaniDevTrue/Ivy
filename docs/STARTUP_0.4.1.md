# Carregamento automático — Ivy 0.4.1

Mantovani relatou que a 0.4.0 funcionou depois de carregar os mods manualmente.
A captura também registra duas saídas com sinal 11, antes da confirmação da
ponte. Ela não fornece stack trace nem prova a causa dessas saídas.

O painel enviava o comando Prepare ao serviço e abria a Unity imediatamente.
StartService apenas agenda o comando; não significa que Mod.Load/ModSystem.Load
terminaram no outro processo. A abertura agora usa uma confirmação explícita:

1. O painel prepara o exemplo e envia Prepare com um ResultReceiver.
2. O serviço carrega os mods, ou reaproveita a sessão já carregada. Se houver
   falhas de carga ou nenhum mod, envia erro em vez de confirmar.
3. Depois da carga síncrona, o serviço responde com contagem de mods/sistemas e PID.
4. Só ao receber sucesso o painel abre a Unity. Não usa atraso fixo para simular readiness.

Dois toques não geram duas aberturas. Se a preparação falhar, exceder 20 segundos,
ou o painel sair de foco, a abertura é cancelada. Confirmações tardias não abrem
o jogo; é possível tentar novamente. Comandos manuais cancelam a abertura pendente.

O SDK Main/Player, o exemplo e a ponte C++ ABI 2 são os mesmos da 0.4.0. Esta mudança
não tenta corrigir um ponto nativo não identificado. O probe registra uma vez
cada etapa crítica (biblioteca, domínio, assemblies, imagens e instalação da ponte)
para localizar a operação anterior a uma eventual nova queda.

## Verificação

Passaram 9 verificações de coordenação: espera por confirmação, toque duplicado,
sucesso, erro de carga, cancelamento prévio, confirmação tardia, nova tentativa,
erro síncrono e erro ao abrir a atividade. Os 11 testes de lifecycle e 15 de
hooks/gameplay também passaram. Os 15 testes Python e o registro JNI de Activity,
Service e StartupResultReceiver foram conferidos. O C++ não mudou e seu teste de
controle com backend simulado continua passando.

O fluxo de Binder/ResultReceiver e a abertura automática ainda precisam do teste
no aparelho. Instale sobre Ivy SDK Test 0.4.0 e toque diretamente em Abrir Terraria,
sem primeiro usar Carregar mods. O relatório deve mostrar Mods confirmados antes
de abrir com 1 mod/1 sistema antes da mensagem de abertura. Depois confira o efeito
no mundo. Se ainda fechar, exporte o relatório para conferir a última etapa IL2CPP.
