using System.Runtime.InteropServices;

namespace Ivy.AndroidHost;

internal static class Il2CppProbe
{
    private static int started;
    private static readonly HashSet<string> stages = [];
    private static void Stage(string name)
    {
        if (stages.Add(name)) ProcessHost.Write("Inicialização IL2CPP: " + name);
    }

    internal static void ArmRuntime()
    {
        var result = WatchRuntime();
        if (result != 0) throw new InvalidOperationException($"Não foi possível observar a inicialização IL2CPP: {result}.");
    }
    internal static void Start()
    {
        if (Interlocked.Exchange(ref started, 1) != 0) return;
        ArmRuntime();
        stages.Clear();
        _ = Task.Run(async () =>
        {
            try
            {
                ProcessHost.Write("Aguardando libil2cpp.so no processo do Ivy (limite: 90 s).");
                ProcessHost.Write("Aguardando retorno bem-sucedido de il2cpp_init; nenhuma API de metadados será consultada antes disso.");
                var detected = false;
                for (var attempt = 0; attempt < 180; attempt++)
                {
                    if (RuntimeReady() == 1)
                    {
                        if (!detected)
                        {
                            ProcessHost.Write("Inicialização IL2CPP confirmada pelo observador nativo; consultando Assembly-CSharp.");
                            detected = true;
                        }
                        if (TryInspect()) return;
                    }
                    await Task.Delay(500);
                }
                ProcessHost.Write("Ponte não ativada: não foi observado um il2cpp_init concluído em 90 s. Feche o jogo e reabra pelo Ivy; consultas antecipadas foram bloqueadas.");
            }
            catch (Exception ex) { ProcessHost.Write($"Diagnóstico IL2CPP falhou: {ex}"); }
            finally { Interlocked.Exchange(ref started, 0); }
        });
    }

    private static bool TryInspect()
    {
        if (RuntimeReady() != 1) return false;
        Stage("abrindo referência da biblioteca após init concluído");
        if (!NativeLibrary.TryLoad("libil2cpp.so", out var library)) return false;
        try
        {
            Stage("domínio capturado na thread Unity; anexando thread Ivy antes de consultar metadados");
            var domain = RuntimeDomain();
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
                Stage("il2cpp_domain_get_assemblies");
                var assemblies = assembliesGet(domain, out var count);
                Stage("enumeração de imagens");
                if (assemblies == IntPtr.Zero || count == 0) return false;
                if (count > 1024) throw new InvalidOperationException($"Quantidade de assemblies inválida: {count}.");
                var gameImage = IntPtr.Zero;
                string? gameImageName = null;
                for (var i = 0; i < (int)count; i++)
                {
                    var image = assemblyGetImage(Marshal.ReadIntPtr(assemblies, i * IntPtr.Size));
                    if (image == IntPtr.Zero) continue;
                    var imageName = Marshal.PtrToStringUTF8(imageGetName(image));
                    if (imageName is not ("Assembly-CSharp.dll" or "Assembly-CSharp")) continue;
                    gameImage = image;
                    gameImageName = imageName;
                    break;
                }
                if (gameImage == IntPtr.Zero) return false;
                var classFromName = Resolve<ClassFromName>(library, "il2cpp_class_from_name");
                var methodFromName = Resolve<MethodFromName>(library, "il2cpp_class_get_method_from_name");
                var main = classFromName(gameImage, "Terraria", "Main");
                if (main == IntPtr.Zero) return false;
                var menuGetter = methodFromName(main, "get_menuMode", 0);
                ProcessHost.Write($"IL2CPP e C# no mesmo processo: PID={Environment.ProcessId}, {count} assemblies.");
                ProcessHost.Write($"Metadados encontrados: {gameImageName}, Terraria.Main; get_menuMode={(menuGetter != IntPtr.Zero ? "OK" : "ausente")}.");
                Stage("metadados resolvidos; instalando ponte SDK");
                NativeWorldBridge.Install(library, gameImage, main);
                return true;
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

    [DllImport("ivy-native", EntryPoint = "ivy_native_watch_runtime", CallingConvention = CallingConvention.Cdecl)] private static extern int WatchRuntime();
    [DllImport("ivy-native", EntryPoint = "ivy_native_runtime_ready", CallingConvention = CallingConvention.Cdecl)] private static extern int RuntimeReady();
    [DllImport("ivy-native", EntryPoint = "ivy_native_runtime_domain", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr RuntimeDomain();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ThreadCurrent();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ThreadAttach(IntPtr domain);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ThreadDetach(IntPtr thread);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AssembliesGet(IntPtr domain, out nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AssemblyGetImage(IntPtr assembly);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ImageGetName(IntPtr image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromName(IntPtr image, [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodFromName(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int parameterCount);
}
