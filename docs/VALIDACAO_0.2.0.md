# Validação 0.2.0

- Compilação do host C# e geração do APK ARM64: concluídas.
- Verificações do núcleo C#: 11 passaram.
- Testes do empacotador: 5 passaram.
- Colisões de classes DEX entre jogo e host: nenhuma.
- DEX, assets e bibliotecas ARM64 dos APKs de entrada: preservados byte a byte.
- IDs de recursos originais: 155 mantidos, nenhum alterado.
- Manifesto: Application original, UnityPlayerActivity e MonoRuntimeProvider presentes no processo padrão.
- Alinhamento ZIP de bibliotecas para páginas de 16 KB: verificado.
- Assinatura: v2 e v3 verificadas.
- Pacote: dev.mantovani.ivy; versão 0.2.0, código 3; Android mínimo API 24.

A compilação local gerou um aviso XA0033 porque o SDK não reconheceu o formato longo da versão 21.0.13.0.2 do JDK. Compilação, empacotamento e assinatura concluíram.

Pendente no aparelho: abertura da Unity, chegada ao menu, confirmação do mesmo PID e resolução de Terraria.Main pelo diagnóstico C#. Nenhum hook de gameplay foi implementado neste lote.
