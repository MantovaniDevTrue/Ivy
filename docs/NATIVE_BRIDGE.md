# Ponte nativa — Ivy 0.3.0

Esta versão implementa o primeiro hook C++ → C# de gameplay. Foi compilada e
verificada localmente; a execução no Android com o jogo ainda precisa ser testada.
A entrada normal em um mundo já confirmada por Mantovani pertence à versão 0.2.1.

## Caminho implementado

Depois que Assembly-CSharp está disponível, a thread de diagnóstico se anexa ao
IL2CPP e resolve `Terraria.Main.DoUpdateInWorld`. Confere zero parâmetros explícitos,
instância e retorno void. Usa o primeiro membro de MethodInfo como methodPointer,
de acordo com o header do dump deste backend. A biblioteca C++ também verifica
alinhamento e pertencimento à libil2cpp.so. Não usa RVA fixo.

O ShadowHook 2.0.1 em modo UNIQUE instala o trampoline. O proxy chama o original
uma vez, com os mesmos ponteiros de instância e MethodInfo, e depois chama o C#
sincronamente na mesma thread. Não redireciona para a thread Android ou Task.Run.

O callback é interno ao host. Nos ticks 1, 60, 600 e 3600, ele consulta menuMode,
myPlayer, LocalPlayer e o campo statLife. Os getters têm flags/retorno verificados;
LocalPlayer deve retornar Player. A leitura de statLife usa a API IL2CPP de campos,
sem offset fixo. Não altera vida, inventário, efeitos ou saves. Nos outros ticks,
o caminho C# apenas incrementa um contador; não resolve metadados nem escreve logs.

Isso ainda não implementa `ModSystem.PostUpdateEverything`: o nome e a posição de
um hook público exigem validar a ordem completa do loop. As classes de conteúdo,
o marshalling geral e os bindings gerados continuam sendo etapas seguintes.

## Controle e vida útil

- Callback e delegates de getters pertencem ao host e ficam enraizados por toda
  a vida do processo. Nenhum callback aponta para assemblies descarregáveis de mods.
- Exceções gerenciadas são capturadas antes de voltar ao C++; uma falha suspende
  novos callbacks, mantendo o caminho original. Isso não captura falhas nativas.
- **Parar hook de diagnóstico** suspende callbacks e pede a remoção ao backend.
  Um callback que já começou pode terminar. A ponte não libera delegates ou
  ponteiros enquanto chamadas antigas podem existir.
- A reinstalação depois da remoção exige reiniciar o processo nesta primeira versão.
- O descarregamento de mods não remove a ponte: ainda não há dispatch de gameplay
  para os mods. Esse isolamento terá de ser substituído por controle de sessões
  e chamadas em curso antes de oferecer unload de hooks públicos.
- Este adaptador assume o layout IL2CPP do APK base documentado. As verificações
  de assinatura não tornam layouts arbitrários de outras versões compatíveis.

## Build reproduzível

Use Linux x86_64, g++, Python 3 e Android NDK r27d (27.3.13750724):

```sh
export IVY_NDK_DIR=/caminho/absoluto/android-ndk-r27d
python3 scripts/build_native.py
bash scripts/build.sh -p:AndroidSdkDirectory=/caminho/android-sdk
```

O primeiro comando executa o teste de controle com backend simulado e gera as
três bibliotecas ARM64 em native/artifacts/arm64-v8a. O build do APK inclui as três.
Detalhes de licença/hash em [NOTICE.md](../native/third-party/NOTICE.md).
libivy-native.so usa páginas ELF de 16 KB e não depende de libc++_shared.so.

## Evidência local e limite

Passaram os testes de controle nativo com backend simulado, 11 verificações do
loader, 8 testes Python de empacotamento/JNI e 14 verificações do gerador.
O catálogo de fixture e os projetos C#/Android compilaram. O verificador JNI
confirmou o registro dos callbacks da Activity. O empacotador verifica igualdade
dos payloads dos dois APKs de entrada; a assinatura é verificada após o build.

O teste nativo local não modifica instruções ARM64: ele comprova a lógica de
ordenação, contagem, suspensão por erro, instalação idempotente e retirada.
Não comprova instalação do hook no celular, 60 FPS ou fidelidade ao tModLoader.

## Teste no aparelho

1. Instale Ivy_0.3.0_GameHost.apk, abra o Terraria e entre em um mundo.
2. Jogue por cerca de um minuto, pause/retome e saia salvando. Entre novamente.
3. Volte ao painel Ivy e exporte o relatório. Em caso de fechamento, reabra o Ivy
   e exporte: ele também inclui a sessão anterior.
4. Procure `Hook C++ instalado` e `Gameplay C# após original`. O relatório mostra
   ticks concluídos, originais iniciados, callbacks nativos/C#, TID e falhas.
   Durante execução, os contadores são amostras independentes e podem diferir
   temporariamente por uma chamada em curso; não são contagens de frames renderizados.
5. Opcionalmente pare o hook pelo painel, reabra o mundo e confira que os contadores
   deixaram de avançar. Reinicie o app para um novo teste de instalação.

Se a instalação for rejeitada, o log deve mostrar código/mensagem do backend ou
a assinatura ausente. Não trate apenas a abertura do mundo como prova do hook.
