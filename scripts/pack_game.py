#!/usr/bin/env python3
"""Combine a local Terraria APK with the Ivy runtime without editing game bytecode."""
import argparse
import copy
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ANDROID = "{http://schemas.android.com/apk/res/android}"
ET.register_namespace("android", ANDROID[1:-1])
UNITY = "com.unity3d.player.UnityPlayerActivity"


def verify_host_processes(manifest, require_unity=False):
    package = manifest.get("package")
    app = manifest.find("application")

    def process(component):
        name = component.get(ANDROID + "process", app.get(ANDROID + "process", package))
        return package + name if name.startswith(":") else name

    launchers = [a for a in app.findall("activity") if any(
        c.get(ANDROID + "name") == "android.intent.category.LAUNCHER"
        for c in a.findall("intent-filter/category"))]
    if len(launchers) != 1:
        raise ValueError("Esperado apenas o launcher Ivy.")
    panel = launchers[0]
    if "${" in panel.get(ANDROID + "taskAffinity", ""):
        raise ValueError("Placeholder de tarefa do painel não resolvido.")
    services = [s for s in app.findall("service") if s.get(ANDROID + "name", "").endswith(".GameControlService")]
    if len(services) != 1 or services[0].get(ANDROID + "exported") != "false":
        raise ValueError("GameControlService interno ausente ou exportado.")
    if process(services[0]) != package or process(panel) == package:
        raise ValueError("Painel e serviço do jogo precisam de processos distintos.")
    runtime_processes = {process(p) for p in app.findall("provider")
                         if p.get(ANDROID + "name", "").startswith("mono.MonoRuntimeProvider")}
    if not {package, process(panel)}.issubset(runtime_processes):
        raise ValueError("Inicialização Mono ausente em um dos processos.")
    if require_unity:
        unity = next(a for a in app.findall("activity") if a.get(ANDROID + "name") == UNITY)
        if process(unity) != package:
            raise ValueError("Unity e serviço C# precisam do mesmo processo.")
        if panel.get(ANDROID + "taskAffinity", package) != unity.get(ANDROID + "taskAffinity", package):
            raise ValueError("Painel e Unity precisam compartilhar a tarefa Android.")
        if unity.get(ANDROID + "launchMode") != "singleTop":
            raise ValueError("Unity precisa de singleTop para navegação na mesma tarefa.")


def load_decoded_manifest(folder):
    manifest = ET.parse(folder / "AndroidManifest.xml").getroot()
    metadata = (folder / "apktool.yml").read_text()

    def value(key):
        match = re.search(r"(?m)^  " + key + r":\s*(.+)$", metadata)
        if not match:
            raise ValueError("Metadado Apktool ausente: " + key)
        return match[1].strip().strip("\"'")

    for name in ("versionCode", "versionName"):
        if manifest.get(ANDROID + name) is None:
            manifest.set(ANDROID + name, value(name))
    if manifest.find("uses-sdk") is None:
        sdk = ET.Element("uses-sdk")
        for name in ("minSdkVersion", "targetSdkVersion"):
            sdk.set(ANDROID + name, value(name))
        manifest.insert(0, sdk)
    return manifest


def merge_manifests(base, host, label="Ivy"):
    old_package = base.get("package")
    package = host.get("package")
    base.set("package", package)
    for name in ("versionCode", "versionName"):
        base.set(ANDROID + name, host.get(ANDROID + name))
    base_sdk, host_sdk = base.find("uses-sdk"), host.find("uses-sdk")
    base_sdk.set(ANDROID + "minSdkVersion", str(max(
        int(base_sdk.get(ANDROID + "minSdkVersion", "1")),
        int(host_sdk.get(ANDROID + "minSdkVersion", "1")))))
    base_sdk.set(ANDROID + "targetSdkVersion", host_sdk.get(ANDROID + "targetSdkVersion"))
    app, host_app = base.find("application"), host.find("application")
    unity = next((x for x in app.findall("activity") if x.get(ANDROID + "name") == UNITY), None)
    if unity is None:
        raise ValueError("UnityPlayerActivity não encontrada no APK base.")
    if unity.get(ANDROID + "process") not in (None, package):
        raise ValueError("A atividade Unity precisa executar no mesmo processo do Ivy.")
    if app.get(ANDROID + "process") not in (None, old_package, package):
        raise ValueError("Processo personalizado no APK base não suportado nesta etapa.")
    app.attrib.pop(ANDROID + "process", None)
    unity.set(ANDROID + "taskAffinity", package)
    unity.set(ANDROID + "launchMode", "singleTop")
    for activity in app.findall("activity") + app.findall("activity-alias"):
        for intent in list(activity.findall("intent-filter")):
            if any(x.get(ANDROID + "name") in (
                    "android.intent.category.LAUNCHER", "android.intent.category.LEANBACK_LAUNCHER")
                    for x in intent.findall("category")):
                activity.remove(intent)
    for provider in app.findall("provider"):
        authority = provider.get(ANDROID + "authorities", "")
        provider.set(ANDROID + "authorities", authority.replace(old_package + ".", package + "."))
    for child in host_app:
        name = child.get(ANDROID + "name")
        if any(x.tag == child.tag and x.get(ANDROID + "name") == name for x in app):
            raise ValueError("Componente Android duplicado: " + str(name))
        item = copy.deepcopy(child)
        if item.tag == "activity":
            if not item.get(ANDROID + "theme"):
                item.set(ANDROID + "theme", "@android:style/Theme.Material.NoActionBar")
            item.set(ANDROID + "label", label)
        app.append(item)
    app.set(ANDROID + "label", label)
    app.set(ANDROID + "allowBackup", "false")
    app.set(ANDROID + "extractNativeLibs", "true")
    app.set(ANDROID + "icon", "@drawable/ivy_icon")
    app.set(ANDROID + "roundIcon", "@drawable/ivy_icon")
    return base


def merge_payload(base_dir, host_dir):
    dex_files = sorted(host_dir.glob("classes*.dex"))
    if not dex_files:
        raise ValueError("APK do runtime sem DEX.")
    indices = [int(re.fullmatch(r"classes(\d*)\.dex", p.name)[1] or 1)
               for p in base_dir.glob("classes*.dex")]
    next_index = max(indices, default=0) + 1
    for source in dex_files:
        shutil.copy2(source, base_dir / f"classes{next_index}.dex")
        next_index += 1
    for directory in ("assets", "lib/arm64-v8a"):
        for source in (host_dir / directory).rglob("*"):
            if not source.is_file():
                continue
            target = base_dir / source.relative_to(host_dir)
            if target.exists() and target.read_bytes() != source.read_bytes():
                raise ValueError("Colisão de arquivos: " + str(source.relative_to(host_dir)))
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    for abi in (base_dir / "lib").iterdir():
        if abi.is_dir() and abi.name != "arm64-v8a":
            shutil.rmtree(abi)


def merge_panel_resources(base_dir, host_dir):
    # Rebuild only Ivy-owned names. Java looks up IDs by name after AAPT reassigns them.
    for source in (host_dir / "res").rglob("*"):
        if not source.is_file():
            continue
        relative = source.relative_to(host_dir / "res")
        if relative.parts[0].startswith("values") and source.suffix == ".xml" and source.name != "public.xml":
            tree = ET.parse(source).getroot()
            selected = [copy.deepcopy(item) for item in tree if item.get("name", "").startswith("ivy_")]
            if selected:
                target = base_dir / "res" / relative.parts[0] / ("ivy_" + source.name)
                target.parent.mkdir(parents=True, exist_ok=True)
                root = ET.Element("resources")
                root.extend(selected)
                ET.ElementTree(root).write(target, encoding="utf-8", xml_declaration=True)
        elif not relative.parts[0].startswith("values") and source.name.startswith("ivy_"):
            target = base_dir / "res" / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != source.read_bytes():
                raise ValueError("Colisão de recurso Ivy: " + str(relative))
            shutil.copy2(source, target)


def run(command):
    subprocess.run([str(x) for x in command], check=True)


def verify_dex_classes(tool, base_apk, host_apk):
    sets = []
    for apk in (base_apk, host_apk):
        result = subprocess.run([str(tool), str(apk)], check=True, capture_output=True, text=True, errors="replace")
        names = re.findall(r"Class descriptor\s*:\s*'([^']+)'", result.stdout)
        if not names or len(names) != len(set(names)):
            raise ValueError("Classes DEX inválidas ou duplicadas: " + str(apk))
        sets.append(set(names))
    duplicates = sets[0] & sets[1]
    if duplicates:
        raise ValueError("Colisão de classes Java: " + ", ".join(sorted(duplicates)[:5]))


def verify_payload(base_apk, host_apk, result_apk):
    with zipfile.ZipFile(base_apk) as base, zipfile.ZipFile(host_apk) as host, zipfile.ZipFile(result_apk) as result:
        for source in (base, host):
            for name in source.namelist():
                if name.endswith("/"):
                    continue
                if name.startswith(("assets/", "lib/arm64-v8a/")):
                    if hashlib.sha256(source.read(name)).digest() != hashlib.sha256(result.read(name)).digest():
                        raise ValueError("Conteúdo modificado durante empacotamento: " + name)
        for source in (base, host):
            for name in source.namelist():
                if re.fullmatch(r"classes\d*\.dex", name):
                    digest = hashlib.sha256(source.read(name)).digest()
                    if not any(hashlib.sha256(result.read(n)).digest() == digest for n in result.namelist()
                               if re.fullmatch(r"classes\d*\.dex", n)):
                        raise ValueError("DEX original não preservado: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--label", default="Ivy")
    parser.add_argument("--base-apk", required=True, type=Path)
    parser.add_argument("--host-apk", required=True, type=Path)
    parser.add_argument("--apktool", required=True, type=Path)
    parser.add_argument("--build-tools", required=True, type=Path)
    parser.add_argument("--java-home", required=True, type=Path)
    parser.add_argument("--keystore", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not os.environ.get("IVY_KEYSTORE_PASSWORD"):
        parser.error("Defina IVY_KEYSTORE_PASSWORD para assinar o APK.")
    java = args.java_home / "bin/java"
    verify_dex_classes(args.build_tools / "dexdump", args.base_apk, args.host_apk)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="ivy-game-") as folder:
        root = Path(folder)
        base, host = root / "base", root / "host"
        for source, target in ((args.base_apk, base), (args.host_apk, host)):
            run([java, "-jar", args.apktool, "d", "-s", "-f", source, "-o", target])
        manifest = merge_manifests(load_decoded_manifest(base), load_decoded_manifest(host), args.label)
        verify_host_processes(manifest, require_unity=True)
        ET.ElementTree(manifest).write(base / "AndroidManifest.xml", encoding="utf-8", xml_declaration=True)
        merge_payload(base, host)
        merge_panel_resources(base, host)
        metadata = base / "apktool.yml"
        text = metadata.read_text()
        for key in ("versionCode", "versionName"):
            value = manifest.get(ANDROID + key)
            text = re.sub(r"(?m)^  " + key + r":.*$", "  " + key + ": " + value, text)
        minimum = manifest.find("uses-sdk").get(ANDROID + "minSdkVersion")
        text = re.sub(r"(?m)^  minSdkVersion:.*$", "  minSdkVersion: " + minimum, text)
        metadata.write_text(text)
        icon = base / "res/drawable/ivy_icon.xml"
        icon.parent.mkdir(parents=True, exist_ok=True)
        icon.write_text('''<vector xmlns:android="http://schemas.android.com/apk/res/android" android:width="108dp" android:height="108dp" android:viewportWidth="108" android:viewportHeight="108"><path android:fillColor="#14281F" android:pathData="M0,0h108v108h-108z"/><path android:fillColor="#8DD49B" android:pathData="M83,25C48,22 22,39 28,63C33,83 65,85 76,60C81,49 80,36 83,25Z"/><path android:strokeColor="#14281F" android:strokeWidth="4" android:strokeLineCap="round" android:fillColor="#00000000" android:pathData="M32,81L68,43M48,64L46,49M55,57L69,58"/></vector>''')
        unsigned, aligned = root / "unsigned.apk", root / "aligned.apk"
        run([java, "-jar", args.apktool, "b", base, "--aapt", args.build_tools / "aapt2", "-o", unsigned])
        run([args.build_tools / "zipalign", "-P", "16", "-f", "4", unsigned, aligned])
        run([java, "-jar", args.build_tools / "lib/apksigner.jar", "sign", "--ks", args.keystore,
             "--ks-pass", "env:IVY_KEYSTORE_PASSWORD", "--out", args.output, aligned])
        run([java, "-jar", args.build_tools / "lib/apksigner.jar", "verify", "--verbose", args.output])
        verify_payload(args.base_apk, args.host_apk, args.output)
        print("APK assinado. DEX, assets e bibliotecas ARM64 preservados byte a byte.")


if __name__ == "__main__":
    main()
