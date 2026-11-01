# Dependência nativa

O backend de alteração de instruções/trampolines é ShadowHook 2.0.1, da ByteDance,
sob licença MIT. A implementação do Ivy não copia código do tModLoader.

- Projeto: https://github.com/bytedance/android-inline-hook/tree/v2.0.1
- Artefato oficial: https://repo.maven.apache.org/maven2/com/bytedance/android/shadowhook/2.0.1/shadowhook-2.0.1.aar
- SHA-256 do AAR: `2cad01ff4d59542958775e19f281981a7e84b39670b5fae75905b8511e10c7cd`
- Licença: [ShadowHook-LICENSE.txt](ShadowHook-LICENSE.txt), também incluída nos assets do APK.

O build baixa o header e as bibliotecas ARM64 do AAR, verifica o hash e inclui
libshadowhook.so/libshadowhook_nothing.so. Não inclui as classes Java do AAR:
o Ivy chama a API C exportada pelo backend. Os binários baixados/compilados não
são versionados no repositório.
