import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("pack_game", Path(__file__).parents[1] / "scripts/pack_game.py")
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)
A = pack.ANDROID


class GamePackagingTests(unittest.TestCase):
    def test_load_apktool_sdk_and_version_metadata(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "AndroidManifest.xml").write_text('<manifest package="dev.mantovani.ivy"><application/></manifest>')
            (root / "apktool.yml").write_text("sdkInfo:\n  minSdkVersion: 24\n  targetSdkVersion: 36\nversionInfo:\n  versionCode: '3'\n  versionName: 0.2.0\n")
            manifest = pack.load_decoded_manifest(root)
            self.assertEqual(manifest.get(A + "versionCode"), "3")
            self.assertEqual(manifest.get(A + "versionName"), "0.2.0")
            self.assertEqual(manifest.find("uses-sdk").get(A + "minSdkVersion"), "24")

    def manifests(self):
        base = ET.fromstring('''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="original.game"><uses-sdk android:minSdkVersion="23" android:targetSdkVersion="36"/><uses-permission android:name="com.android.vending.CHECK_LICENSE"/><application android:name="original.Application"><activity android:name="com.unity3d.player.UnityPlayerActivity"><intent-filter><action android:name="android.intent.action.MAIN"/><category android:name="android.intent.category.LAUNCHER"/></intent-filter></activity><provider android:name="original.Documents" android:authorities="original.game.documents"/></application></manifest>''')
        host = ET.fromstring('''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="dev.mantovani.ivy" android:versionCode="3" android:versionName="0.2.0"><uses-sdk android:minSdkVersion="24" android:targetSdkVersion="36"/><application><activity android:name="ivy.MainActivity"><intent-filter><action android:name="android.intent.action.MAIN"/><category android:name="android.intent.category.LAUNCHER"/></intent-filter></activity><provider android:name="mono.MonoRuntimeProvider" android:authorities="dev.mantovani.ivy.mono_init"/></application></manifest>''')
        return base, host

    def test_launcher_runtime_and_original_application(self):
        result = pack.merge_manifests(*self.manifests())
        app = result.find("application")
        self.assertEqual(app.get(A + "name"), "original.Application")
        self.assertIsNotNone(result.find("uses-permission"))
        self.assertEqual(result.find("uses-sdk").get(A + "minSdkVersion"), "24")
        self.assertEqual(len(app.findall("activity/intent-filter/category")), 1)
        self.assertEqual(app.find("provider").get(A + "authorities"), "dev.mantovani.ivy.documents")
        self.assertTrue(any(x.get(A + "name") == "mono.MonoRuntimeProvider" for x in app.findall("provider")))

    def test_reject_separate_game_process(self):
        base, host = self.manifests()
        base.find("application/activity").set(A + "process", ":game")
        with self.assertRaises(ValueError):
            pack.merge_manifests(base, host)

    def isolated_manifest(self):
        base, host = self.manifests()
        app = host.find("application")
        panel = app.find("activity")
        panel.set(A + "process", ":launcher")
        panel.set(A + "taskAffinity", "dev.mantovani.ivy")
        ET.SubElement(app, "service", {A + "name": "ivy.GameControlService", A + "exported": "false"})
        ET.SubElement(app, "provider", {A + "name": "mono.MonoRuntimeProvider_1", A + "process": ":launcher"})
        return pack.merge_manifests(base, host)

    def test_independent_panel_and_game_runtime(self):
        pack.verify_host_processes(self.isolated_manifest(), require_unity=True)

    def test_reject_shared_panel_process(self):
        manifest = self.isolated_manifest()
        panel = manifest.find("application").findall("activity")[1]
        panel.attrib.pop(A + "process")
        with self.assertRaisesRegex(ValueError, "processos distintos"):
            pack.verify_host_processes(manifest, require_unity=True)

    def test_reject_missing_panel_mono_initialization(self):
        manifest = self.isolated_manifest()
        app = manifest.find("application")
        app.remove(next(p for p in app.findall("provider") if p.get(A + "process") == ":launcher"))
        with self.assertRaisesRegex(ValueError, "Inicialização Mono"):
            pack.verify_host_processes(manifest)

    def test_reject_separate_tasks(self):
        manifest = self.isolated_manifest()
        manifest.find("application").findall("activity")[1].set(A + "taskAffinity", "other.task")
        with self.assertRaisesRegex(ValueError, "compartilhar a tarefa"):
            pack.verify_host_processes(manifest, require_unity=True)

    def test_unity_single_top_in_shared_task(self):
        manifest = self.isolated_manifest()
        unity = manifest.find("application/activity")
        self.assertEqual(unity.get(A + "launchMode"), "singleTop")
        unity.set(A + "launchMode", "singleTask")
        with self.assertRaisesRegex(ValueError, "singleTop"):
            pack.verify_host_processes(manifest, require_unity=True)

    def test_reject_exported_game_commands(self):
        manifest = self.isolated_manifest()
        manifest.find("application/service").set(A + "exported", "true")
        with self.assertRaisesRegex(ValueError, "exportado"):
            pack.verify_host_processes(manifest)

    def test_payload_dex_and_arm64(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            base, host = root / "base", root / "host"
            for target in (base, host):
                (target / "lib/arm64-v8a").mkdir(parents=True)
                (target / "assets").mkdir()
            (base / "classes.dex").write_bytes(b"game")
            (host / "classes.dex").write_bytes(b"runtime")
            (base / "lib/armeabi-v7a").mkdir()
            (host / "assets/mod.dll").write_bytes(b"mod")
            pack.merge_payload(base, host)
            self.assertEqual((base / "classes.dex").read_bytes(), b"game")
            self.assertEqual((base / "classes2.dex").read_bytes(), b"runtime")
            self.assertFalse((base / "lib/armeabi-v7a").exists())
            self.assertEqual((base / "assets/mod.dll").read_bytes(), b"mod")

    def test_reject_different_asset_with_same_path(self):
        with tempfile.TemporaryDirectory() as folder:
            base, host = Path(folder) / "base", Path(folder) / "host"
            for target in (base, host):
                (target / "assets").mkdir(parents=True)
                (target / "classes.dex").write_bytes(b"dex")
            (base / "assets/test").write_bytes(b"game")
            (host / "assets/test").write_bytes(b"host")
            with self.assertRaises(ValueError):
                pack.merge_payload(base, host)


if __name__ == "__main__":
    unittest.main()
