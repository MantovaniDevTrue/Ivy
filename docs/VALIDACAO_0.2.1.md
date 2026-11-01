# Validação 0.2.1

## Falha confirmada

A 0.2.0 fechou no Android 14 com `java.lang.UnsatisfiedLinkError` em `MainActivity.n_onCreate`. O código Java gerado não chamava `Runtime.register`; a tabela LLVM de marshal methods continha apenas o callback TypeManager.Activate, sem os callbacks da MainActivity. A build 0.1.0 que funcionou tinha a MainActivity na tabela.

## Alteração

O projeto define `AndroidEnableMarshalMethods=false`. O stub gerado registra explicitamente `n_onCreate`, `n_onActivityResult` e `n_onResume` em `Runtime.register`, antes da construção da Activity. Foi feita uma compilação limpa do projeto Android.

O script `verify_jni.py` rejeita a ausência do registro dinâmico, do callback onCreate ou de mapeamentos para métodos nativos. Ele é executado pela build antes de copiar o APK para artifacts. Os testes incluem uma regressão do stub sem registro observado na 0.2.0.

Referência da propriedade: https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-properties#androidenablemarshalmethods . O modo dinâmico troca o mecanismo de registro na inicialização; os diagnósticos IL2CPP e a API C# permanecem os mesmos.

A validação estática e os testes não comprovam a abertura no aparelho. O próximo teste é chegar ao painel Ivy e depois tocar em Abrir Terraria.

## Retorno do teste no aparelho

Mantovani confirmou que entrou normalmente em um mundo. A captura mostrou C# e IL2CPP no PID 19493, 303 assemblies, Assembly-CSharp.dll, Terraria.Main e get_menuMode=OK, além de 1 mod e 1 sistema carregados. Isso valida inicialização e metadados; hooks de gameplay continuam pendentes.
