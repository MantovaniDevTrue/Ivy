# Host Android — 0.4.0

O empacotador combina um APK base local com o APK do runtime Ivy. Preserva a classe Application original, as permissões, os DEX e todos os assets e bibliotecas ARM64 do jogo. Os controles de licença do APK base não são removidos nem alterados.

O manifesto usa `dev.mantovani.ivy`. A entrada principal é o painel em `:launcher`, com tarefa própria. UnityPlayerActivity e GameControlService ficam no processo padrão: Mono e IL2CPP continuam juntos para os hooks. Há um MonoRuntimeProvider para cada processo. O painel envia comandos explícitos ao serviço interno não exportado; não executa os mods. Não há dependência de JavaScript.

## Empacotar

Instale o Apktool 3.0.3 pelo projeto oficial https://apktool.org/ . O jogo e o APKTool não são incluídos no repositório. Gere primeiro o runtime com `scripts/build.sh`. Defina `IVY_KEYSTORE_PASSWORD` no ambiente com a senha da chave de assinatura.

```sh
python3 scripts/pack_game.py \
  --base-apk /caminho/Terraria.apk \
  --host-apk artifacts/Ivy_0.4.0_RuntimeTest.apk \
  --apktool /caminho/apktool_3.0.3.jar \
  --build-tools /caminho/android-sdk/build-tools/36.0.0 \
  --java-home /caminho/jdk-21 \
  --keystore /caminho/assinatura.keystore \
  --output artifacts/Ivy_0.4.0_GameHost.apk
```

O script reconstrói os recursos mantendo os IDs públicos, acrescenta os DEX do host, mantém somente ARM64, alinha as entradas para páginas de 16 KB e assina o resultado. Depois compara os bytes dos DEX, assets e bibliotecas ARM64 com os dois APKs de entrada. A chave deve ficar fora do repositório.

## Diagnóstico

Ao abrir o jogo, o painel inicia o serviço interno e a atividade Unity. O C# do processo do jogo aguarda por até 90 segundos a biblioteca IL2CPP. Antes de anexar a thread, espera a imagem Assembly-CSharp ficar disponível. O diagnóstico usa os exports do runtime e anexa a thread de trabalho ao domínio apenas para consultar metadados. Resolve `Assembly-CSharp` e `Terraria.Main`, valida assinaturas e instala a [ponte nativa](NATIVE_BRIDGE.md) para `DoUpdateInWorld`. As leituras de gameplay ocorrem no callback da própria thread do jogo, depois do original. Não usa RVAs fixos. A thread de diagnóstico é desanexada ao concluir.

A sessão de mods pertence ao processo Unity e não é descartada quando o serviço termina ou o painel é recriado. Os comandos de serviço não são reiniciados automaticamente pelo Android. O relatório do jogo é salvo em `files/ivy-session.txt`, com `.previous`; o painel usa `ivy-launcher.txt` e reúne os logs ao exportar. Reabrir apenas o painel não gira nem apaga o log do jogo. O ciclo completo ainda depende do teste no aparelho.

## Limites deste lote

Abrir a Unity e resolver metadados não comprova execução do novo hook: confira os contadores e as mensagens do callback no aparelho. Esta ponte ainda não oferece hooks públicos, registro de itens ou compatibilidade com mods do PC. Alterações de assinatura/pacote podem ser rejeitadas pela inicialização do APK base; esse resultado deve ser diagnosticado pelos logs, sem desativar os controles do jogo.

Para o APK paralelo deste lote, compile com `-p:ApplicationId=dev.mantovani.ivy.sdktest` e empacote com `--label "Ivy SDK Test"`. A afinidade da tarefa do painel acompanha ApplicationId, evitando compartilhar a tarefa com outra instalação. Veja [GAMEPLAY_SDK.md](GAMEPLAY_SDK.md).
