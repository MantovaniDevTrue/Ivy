#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 scripts/build_native.py
dotnet build samples/ExampleMod/ExampleMod.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet build tests/Fixtures/MetadataUpdate/MetadataUpdate.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet build tests/Fixtures/FailingMod/FailingMod.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet build tests/Ivy.Tests/Ivy.Tests.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet tests/Ivy.Tests/bin/Release/net10.0/Ivy.Tests.dll samples/ExampleMod/bin/Release/net10.0/ExampleMod.dll tests/Fixtures/MetadataUpdate/bin/Release/net10.0/ExampleMod.dll tests/Fixtures/FailingMod/bin/Release/net10.0/FailingMod.dll
# Avoid stale Android assets when switching public/development variants.
dotnet clean src/Ivy.Android/Ivy.Android.csproj -c Release -m:1 "$@"
dotnet build src/Ivy.Android/Ivy.Android.csproj -c Release -t:SignAndroidPackage -m:1 -p:UseSharedCompilation=false "$@"
python3 scripts/verify_jni.py src/Ivy.Android/obj/Release/net10.0-android/android-arm64/android/src
python3 scripts/verify_jni.py src/Ivy.Android/obj/Release/net10.0-android/android-arm64/android/src GameControlService
python3 scripts/verify_android_host.py src/Ivy.Android/obj/Release/net10.0-android/android-arm64/android/AndroidManifest.xml
python3 scripts/verify_jni.py src/Ivy.Android/obj/Release/net10.0-android/android-arm64/android/src StartupResultReceiver n_onReceiveResult
python3 scripts/verify_jni.py src/Ivy.Android/obj/Release/net10.0-android/android-arm64/android/src ModsActivity
mkdir -p artifacts
cp src/Ivy.Android/bin/Release/net10.0-android/android-arm64/*-Signed.apk artifacts/Ivy_0.6.1_RuntimeTest.apk
cp samples/ExampleMod/bin/Release/net10.0/ExampleMod.dll artifacts/
