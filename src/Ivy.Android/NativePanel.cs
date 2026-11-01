using Android.Runtime;
using Android.Views;
namespace Ivy.AndroidHost;

// A narrow JNI boundary: Java draws and emits actions, C# owns the loader.
internal sealed class NativePanel
{
    private readonly IntPtr type;
    internal View View { get; }
    internal NativePanel(global::Android.App.Activity activity,View.IOnClickListener listener,string version,bool example)
    {
        type=JNIEnv.FindClass("dev/mantovani/ivy/panel/IvyPanelView");
        var ctor=JNIEnv.GetMethodID(type,"<init>","(Landroid/app/Activity;Landroid/view/View$OnClickListener;Ljava/lang/String;Z)V");
        using var label=new Java.Lang.String(version);
        var handle=JNIEnv.NewObject(type,ctor,new JValue(activity),new JValue((Java.Lang.Object)listener),new JValue(label),new JValue(example));
        View=Java.Lang.Object.GetObject<View>(handle,JniHandleOwnership.TransferLocalRef)!;
    }
    private void Set(string method,string value)
    {
        var id=JNIEnv.GetMethodID(type,method,"(Ljava/lang/String;)V");using var text=new Java.Lang.String(value);
        JNIEnv.CallVoidMethod(View.Handle,id,new JValue(text));
    }
    internal void Status(string text)=>Set("setStatus",text);
    internal void Report(string text)=>Set("setReport",text);
    internal void Library(string text)=>Set("setLibrary",text);
    internal void Session(string text)=>Set("setSession",text);
    internal void Busy(bool value)=>JNIEnv.CallVoidMethod(View.Handle,JNIEnv.GetMethodID(type,"setBusy","(Z)V"),new JValue(value));
    internal string SaveState()=>JNIEnv.GetString(JNIEnv.CallObjectMethod(View.Handle,JNIEnv.GetMethodID(type,"saveState","()Ljava/lang/String;")),JniHandleOwnership.TransferLocalRef)??"{}";
    internal void RestoreState(string state)=>Set("restoreState",state);
    internal bool GoBack()=>JNIEnv.CallBooleanMethod(View.Handle,JNIEnv.GetMethodID(type,"goBack","()Z"));
}
