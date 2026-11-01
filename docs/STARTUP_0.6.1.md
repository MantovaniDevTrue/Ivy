# Ivy 0.6.1 — inicialização e tarefa Android

O relatório da 0.6.0 registra duas falhas nativas status 11, ambas com última etapa `il2cpp_domain_get`, antes de instalar os hooks de gameplay. Mantovani informou que preparar a sessão manualmente antes de Jogar permite abrir o jogo. Isso evidencia uma diferença de timing; não comprova que separar o painel em outro processo causou o crash. Sem tombstone, a instrução exata da falha continua desconhecida.

## Correção

O caminho anterior identificava libil2cpp em /proc/self/maps e consultava o domínio de uma thread Mono antes de anexá-la ao IL2CPP. Uma biblioteca mapeada não significa VM inicializada. A inspeção ARM64 do APK fornecido também mostrou que domain_get tem um caminho de criação tardia de objeto, além de ler estado global.

Agora Preparar e Jogar armam o mesmo observador nativo antes de abrir a Unity. ShadowHook registra hooks pendentes para il2cpp_init e il2cpp_init_utf16, sem carregar o jogo antecipadamente. Os proxies preservam parâmetros/retorno e chamam o original uma vez. Somente após retorno bem-sucedido, a thread Unity já inicializada obtém o domínio e publica a confirmação. A thread Ivy aguarda essa confirmação, anexa-se ao domínio e só então enumera assemblies ou acessa metadados. Não há callback C# dentro do proxy de inicialização.

O observador também é armado nas sessões vazias, para permitir ativar mods depois no mesmo processo. Zero mods continuam sem sonda C# de metadados e sem hooks de gameplay; existe agora o observador nativo de inicialização. Não é uma espera fixa em segundos. Se o retorno não for observado (por exemplo, observador instalado tarde demais), a consulta antecipada permanece bloqueada e o relatório informa o timeout, em vez de tentar adivinhar readiness.

## Uma tarefa, dois processos

Os mods C# e o Terraria **já compartilham o processo padrão**. O painel fica em :launcher para sobreviver ao encerramento do Unity e preservar relatórios. Essa separação permanece.

Painel e Unity agora compartilham taskAffinity. A abertura remove NEW_TASK e a atividade Unity passa de singleTask a singleTop. Isso permite a transição entre telas na mesma tarefa Android, preservando a classe Java, os DEX, assets, bibliotecas, controles e configuração de renderização do jogo. O comportamento visual e o retorno ao painel ainda precisam ser testados no aparelho.

## Validação

Build ARM64 e checks JNI/processos passaram; 110 verificações C# existentes e 16 testes Python passaram. O teste nativo cobre bloqueio antes do init, retorno falso, captura do domínio após sucesso, preservação do resultado/argumentos, caminho UTF16 e preparação repetida sem duplicar os registros. Os testes anteriores de hooks também passaram. Estes testes não reproduzem o startup real da Unity no Xiaomi.

Instale sobre a 0.6.0 (mesmo pacote/chave). Pode manter ExampleMod 1.1.0.0 já importado e o SDK 0.6.0. Primeiro teste **Jogar diretamente**, sem Preparar sessão; depois reentrada e o caminho manual. Confira no relatório `Inicialização IL2CPP confirmada pelo observador nativo`, domínio capturado/anexação da thread e os callbacks dos hooks. Nenhuma melhoria de FPS é declarada. A confirmação da correção do crash depende desse teste Android.
