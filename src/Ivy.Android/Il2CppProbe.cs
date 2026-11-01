using System.Runtime.InteropServices;

namespace Ivy.AndroidHost;

internal static class Il2CppProbe
{
    private static int started;

    internal static void Start()
    {
        if (Interlocked.Exchange(ref started, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                ProcessHost.Write("Aguardando libil2cpp.so no processo do Ivy (limite: 90 s).");
                for (var attempt = 0; attempt < 180; attempt++)
                {
                    if (File.ReadLines("/proc/self/maps").Any(line => line.Contains("libil2cpp.so", StringComparison.Ordinal)))
                    {
                        if (TryInspect()) return;
                    }
                    await Task.Delay(500);
                }
                ProcessHost.Write("Diagnóstico encerrado: o runtime IL2CPP não ficou pronto em 90 s.");
            }
            catch (Exception ex) { ProcessHost.Write($"Diagnóstico IL2CPP falhou: {ex}"); }
            finally { Interlocked.Exchange(ref started, 0); }
        });
    }

    private static bool TryInspect()
    {
        if (!NativeLibrary.TryLoad("libil2cpp.so", out var library)) return false;
        try
        {
            var domainGet = Resolve<DomainGet>(library, "il2cpp_domain_get");
            var domain = domainGet();
            if (domain == IntPtr.Zero) return false;
            var threadCurrent = Resolve<ThreadCurrent>(library, "il2cpp_thread_current");
            var threadAttach = Resolve<ThreadAttach>(library, "il2cpp_thread_attach");
            var threadDetach = Resolve<ThreadDetach>(library, "il2cpp_thread_detach");
            var owned = threadCurrent() == IntPtr.Zero;
            var thread = owned ? threadAttach(domain) : threadCurrent();
            if (thread == IntPtr.Zero) return false;
            try
            {
                var assembliesGet = Resolve<AssembliesGet>(library, "il2cpp_domain_get_assemblies");
                var assemblyGetImage = Resolve<AssemblyGetImage>(library, "il2cpp_assembly_get_image");
                var imageGetName = Resolve<ImageGetName>(library, "il2cpp_image_get_name");
                var classFromName = Resolve<ClassFromName>(library, "il2cpp_class_from_name");
                var methodFromName = Resolve<MethodFromName>(library, "il2cpp_class_get_method_from_name");
                var assemblies = assembliesGet(domain, out var count);
                if (assemblies == IntPtr.Zero || count == 0) return false;
                if (count > 1024) throw new InvalidOperationException($"Quantidade de assemblies inválida: {count}.");
                for (var i = 0; i < (int)count; i++)
                {
                    var image = assemblyGetImage(Marshal.ReadIntPtr(assemblies, i * IntPtr.Size));
                    if (image == IntPtr.Zero) continue;
                    var imageName = Marshal.PtrToStringUTF8(imageGetName(image));
                    if (imageName is not ("Assembly-CSharp.dll" or "Assembly-CSharp")) continue;
                    var main = classFromName(image, "Terraria", "Main");
                    if (main == IntPtr.Zero) return false;
                    var menuGetter = methodFromName(main, "get_menuMode", 0);
                    ProcessHost.Write($"IL2CPP e C# no mesmo processo: PID={Environment.ProcessId}, {count} assemblies.");
                    ProcessHost.Write($"Metadados encontrados: {imageName}, Terraria.Main; get_menuMode={(menuGetter != IntPtr.Zero ? "OK" : "ausente")}.");
                    ProcessHost.Write("Diagnóstico de metadados concluído. Nenhum método de gameplay foi executado; hooks ainda pendentes.");
                    return true;
                }
                return false;
            }
            finally { if (owned) threadDetach(thread); }
        }
        finally { NativeLibrary.Free(library); }
    }

    private static T Resolve<T>(IntPtr library, string name) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(library, name, out var address))
            throw new MissingMethodException($"Export IL2CPP ausente: {name}.");
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr DomainGet();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ThreadCurrent();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ThreadAttach(IntPtr domain);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ThreadDetach(IntPtr thread);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AssembliesGet(IntPtr domain, out nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AssemblyGetImage(IntPtr assembly);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ImageGetName(IntPtr image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromName(IntPtr image, [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodFromName(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int parameterCount);
}
