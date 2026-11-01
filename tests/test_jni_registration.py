import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("verify_jni", Path(__file__).parents[1] / "scripts/verify_jni.py")
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)


class JniRegistrationTests(unittest.TestCase):
    def test_reject_missing_registration_seen_in_020(self):
        with self.assertRaisesRegex(ValueError, "Runtime.register"):
            verify.verify_source("public class MainActivity { private native void n_onCreate(android.os.Bundle p0); }")

    def test_accept_registered_lifecycle_callbacks(self):
        source = '''public class MainActivity {
          public static String __md_methods;
          static { __md_methods = "n_onCreate:(Landroid/os/Bundle;)V:GetOnCreateHandler\\n" + "n_onResume:()V:GetOnResumeHandler\\n";
            mono.android.Runtime.register("Ivy.AndroidHost.MainActivity, Ivy.Android", MainActivity.class, __md_methods); }
          private native void n_onCreate(android.os.Bundle p0);
          private native void n_onResume();
        }'''
        self.assertEqual(verify.verify_source(source), {"n_onCreate", "n_onResume"})

    def test_reject_unmapped_native_callback(self):
        source = '''static { __md_methods = "n_onCreate:(Landroid/os/Bundle;)V:GetOnCreateHandler\\n";
            mono.android.Runtime.register("Ivy.AndroidHost.MainActivity, Ivy.Android", MainActivity.class, __md_methods); }
          private native void n_onCreate(android.os.Bundle p0);
          private native void n_onResume();'''
        with self.assertRaisesRegex(ValueError, "n_onResume"):
            verify.verify_source(source)

    def test_receiver_callback_registration(self):
        source = '''static { __md_methods = "n_onReceiveResult:(ILandroid/os/Bundle;)V:GetOnReceiveResultHandler\\n";
          mono.android.Runtime.register("Ivy.StartupResultReceiver, Ivy.Android", StartupResultReceiver.class, __md_methods); }
          private native void n_onReceiveResult(int result, android.os.Bundle data);'''
        self.assertEqual(verify.verify_source(source, "n_onReceiveResult"), {"n_onReceiveResult"})

    def test_receiver_requires_expected_callback(self):
        source = '''static { __md_methods = "n_onCreate:(Landroid/os/Bundle;)V:GetOnCreateHandler\\n";
          mono.android.Runtime.register("Ivy.StartupResultReceiver, Ivy.Android", StartupResultReceiver.class, __md_methods); }
          private native void n_onCreate(android.os.Bundle data);'''
        with self.assertRaisesRegex(ValueError, "n_onReceiveResult"):
            verify.verify_source(source, "n_onReceiveResult")


if __name__ == "__main__":
    unittest.main()
