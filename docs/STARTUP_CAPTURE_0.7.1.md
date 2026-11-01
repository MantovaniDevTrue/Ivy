# Ivy 0.7.1 — diagnóstico de inicialização

A 0.7.0 falhou no aparelho antes da confirmação do serviço: Android registrou motivo 4 (crash), enquanto o painel expirou após 20 segundos. As imagens não incluem a exceção. A causa ainda não está identificada; esta versão não declara o crash corrigido.

Um ContentProvider Java interno, no processo do jogo, instala um capturador antes de MonoRuntimeProvider (initOrder 2147483647). Preserva e encaminha ao handler original do Android, sem esconder ou reiniciar o crash. Grava a pilha Java em `ivy-android-startup.txt`; a exportação reúne esse arquivo, uma rotação anterior e os logs C#. O serviço registra os passos anteriores à carga dos mods. O arquivo gira após 128 KiB. Falhas nativas/sinais não são interceptados por esse handler Java.

Atualizar por cima da instalação existente, tocar Jogar uma vez, voltar ao painel e exportar o relatório completo. Não é necessário reimportar ExampleMod. A validação decisiva depende dessa execução no aparelho. Package ID e certificado SDK Test preservados; versionCode 13.
