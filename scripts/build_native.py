#!/usr/bin/env python3
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = "2.0.1"
SHA256 = "2cad01ff4d59542958775e19f281981a7e84b39670b5fae75905b8511e10c7cd"
URL = f"https://repo.maven.apache.org/maven2/com/bytedance/android/shadowhook/{VERSION}/shadowhook-{VERSION}.aar"


def main():
    cache = ROOT / "native/build/dependencies"
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / f"shadowhook-{VERSION}.aar"
    if not archive.exists():
        with urllib.request.urlopen(URL, timeout=60) as source, archive.open("wb") as destination:
            shutil.copyfileobj(source, destination)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != SHA256:
        raise ValueError("Hash do ShadowHook divergente; remova o arquivo de cache e confira a origem.")
    with zipfile.ZipFile(archive) as z:
        for member in ["prefab/modules/shadowhook/include/shadowhook.h", "jni/arm64-v8a/libshadowhook.so", "jni/arm64-v8a/libshadowhook_nothing.so"]:
            (cache / Path(member).name).write_bytes(z.read(member))
    tests = ROOT / "native/build/test-native"
    subprocess.run(["g++", "-std=c++17", "-O2", "-Wall", "-Wextra", "-Werror", "-DIVY_NATIVE_TEST", "-I" + str(cache),
                    str(ROOT / "native/ivy-native/ivy_native.cpp"), str(ROOT / "native/ivy-native/test_native.cpp"),
                    "-pthread", "-ldl", "-o", str(tests)], check=True)
    subprocess.run([str(tests)], check=True)
    ndk = os.environ.get("IVY_NDK_DIR") or os.environ.get("ANDROID_NDK_HOME") or os.environ.get("ANDROID_NDK_ROOT")
    if not ndk:
        raise ValueError("Defina IVY_NDK_DIR para o NDK r27d (27.3.13750724).")
    if "Pkg.Revision = 27.3.13750724" not in (Path(ndk) / "source.properties").read_text():
        raise ValueError("Use o NDK fixado: 27.3.13750724 (r27d).")
    clang = Path(ndk) / "toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android24-clang++"
    if not clang.is_file():
        raise FileNotFoundError(clang)
    output = ROOT / "native/artifacts/arm64-v8a"
    output.mkdir(parents=True, exist_ok=True)
    for name in ["libshadowhook.so", "libshadowhook_nothing.so"]:
        shutil.copy2(cache / name, output / name)
    subprocess.run([str(clang), "-std=c++17", "-O2", "-shared", "-fPIC", "-fvisibility=hidden", "-fno-exceptions", "-fno-rtti", "-funwind-tables",
                    "-nostdlib++", "-Wall", "-Wextra", "-Werror", "-I" + str(cache), str(ROOT / "native/ivy-native/ivy_native.cpp"),
                    "-L" + str(output), "-lshadowhook", "-ldl", "-Wl,--no-undefined", "-Wl,-z,max-page-size=16384",
                    "-Wl,-z,common-page-size=16384", "-Wl,-soname,libivy-native.so", "-o", str(output / "libivy-native.so")], check=True)
    print("libivy-native.so ARM64 compilada; ShadowHook 2.0.1 verificado por SHA-256.")


if __name__ == "__main__":
    main()
