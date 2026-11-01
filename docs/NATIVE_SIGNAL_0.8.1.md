# Ivy 0.8.1 — isolamento do fechamento nativo

O relatório `Ivy-relatorio (7).txt` confirma que a 0.8.0 entrou no mundo, leu bodyFrame Rectangle e hairColor RGBA, executou o hook bool(int), 600 atualizações e cinco HealEffect sem erros C#. Em seguida, PID 13014 terminou com motivo 2/status 11 (SIGSEGV). O relatório não contém a pilha nativa; a causa ainda não foi identificada. A 0.8.1 não declara o problema corrigido.

Nesta build, o perfil mantém a assinatura/API do hook IsItemSlotUnlockedAndUsable, mas desativa sua instalação nativa. O relatório declara `ISOLAMENTO 0.8.1`. O método direto continua chamando o jogo, sem detour; handlers On_ desse método não são despachados nesta build. Esta é uma diferença controlada para testar a participação do novo hook, não uma comprovação de que ele era a causa. Outros hooks, campos/arrays e structs foram preservados. Pode manter ExampleMod 1.3.0.0 e SDK 0.8.0.

Na primeira atualização do mundo, um observador nativo captura mapas do processo e registradores em SIGSEGV/SIGBUS/SIGABRT. Encaminha ao handler anterior ou ao encerramento padrão; não tenta continuar uma falha fatal. O handler não faz alocação, unwinding ou chamada C#, nem lê uma cadeia de frames potencialmente inválida. Usa buffer de stack e pwrite, com ring de 32 registros de 1 KiB e sequência numérica; sinais recuperados pelo próprio runtime também podem aparecer, portanto uma observação isolada não prova um crash. Os mapas iniciais têm limite de 256 KiB e podem não incluir bibliotecas carregadas posteriormente.

O painel reúne o arquivo atual/anterior ao exportar. Também consulta TraceInputStream da saída mais recente por SIGSEGV/crash nativo e preserva até 256 KiB em Base64 se o Android disponibilizar os bytes. Registradores/mapas não são uma pilha completa, e um handler instalado depois pode impedir a captura. O diagnóstico não muda o tratamento de SIGKILL.

Verificação: testes C++ de encaminhamento, flags preservados, snapshot e encerramento padrão por SIGABRT em subprocesso; regressão dos hooks e marshaller, build ARM64, JNI/processos e testes Python. APK assinado com a chave existente e payload original preservado byte a byte. Validação Android pendente.

Teste: atualizar por cima, manter a DLL atual, Jogar e permanecer no mundo pelo menos um minuto; depois exportar o TXT completo. Se fechar, reabrir o painel e exportar sem reinstalar ou repetir várias sessões, para preservar o contexto da tentativa.
