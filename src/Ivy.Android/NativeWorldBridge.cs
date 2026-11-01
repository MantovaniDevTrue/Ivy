using System.Runtime.InteropServices;

namespace Ivy.AndroidHost;

// These delegates belong to the host, never to a collectible mod assembly.
// Keep them rooted for the entire process, including callbacks racing with removal.
internal static class NativeWorldBridge
{
    private static readonly object gate = new();
    private static readonly WorldCallback callback = OnWorldUpdate;
    private static bool attempted;
    private static int available;
    private static long managedCalls;
    private static IntPtr menuMethod, playerIndexMethod, playerMethod, lifeField;
    private static IntGetter? menuGetter, playerIndexGetter;
    private static PointerGetter? playerGetter;
    private static FieldGetValue? fieldGetValue;
    private static ThreadCurrent? threadCurrent;

    internal static void Install(IntPtr library, IntPtr gameImage, IntPtr main)
    {
        lock (gate)
        {
            if (attempted) return;
            attempted = true;
            try
            {
                if (AbiVersion() != 1) throw new InvalidOperationException("ABI da ponte Ivy incompatível.");
                Volatile.Write(ref available, 1);
                var findMethod = Resolve<MethodFromName>(library, "il2cpp_class_get_method_from_name");
                var parameterCount = Resolve<ParameterCount>(library, "il2cpp_method_get_param_count");
                var methodFlags = Resolve<MethodFlags>(library, "il2cpp_method_get_flags");
                var returnType = Resolve<ReturnType>(library, "il2cpp_method_get_return_type");
                var typeKind = Resolve<TypeKind>(library, "il2cpp_type_get_type");
                IntPtr Method(string name, bool isStatic, int expectedReturnType)
                {
                    var method = findMethod(main, name, 0);
                    if (method == IntPtr.Zero || parameterCount(method) != 0 ||
                        ((methodFlags(method, out _) & 0x10) != 0) != isStatic ||
                        typeKind(returnType(method)) != expectedReturnType)
                        throw new InvalidOperationException($"Assinatura IL2CPP incompatível: Main.{name}.");
                    // MethodInfo.methodPointer is the first member in this game's IL2CPP layout.
                    if (Marshal.ReadIntPtr(method) == IntPtr.Zero)
                        throw new InvalidOperationException($"Ponteiro nativo ausente: Main.{name}.");
                    return method;
                }

                var worldMethod = Method("DoUpdateInWorld", false, 1); // IL2CPP_TYPE_VOID
                menuMethod = Method("get_menuMode", true, 8); // IL2CPP_TYPE_I4
                playerIndexMethod = Method("get_myPlayer", true, 8);
                playerMethod = Method("get_LocalPlayer", true, 18); // IL2CPP_TYPE_CLASS
                menuGetter = Marshal.GetDelegateForFunctionPointer<IntGetter>(Marshal.ReadIntPtr(menuMethod));
                playerIndexGetter = Marshal.GetDelegateForFunctionPointer<IntGetter>(Marshal.ReadIntPtr(playerIndexMethod));
                playerGetter = Marshal.GetDelegateForFunctionPointer<PointerGetter>(Marshal.ReadIntPtr(playerMethod));
                var classFromName = Resolve<ClassFromName>(library, "il2cpp_class_from_name");
                var playerClass = classFromName(gameImage, "Terraria", "Player");
                if (playerClass == IntPtr.Zero) throw new InvalidOperationException("Terraria.Player ausente.");
                var classFromType = Resolve<ClassFromType>(library, "il2cpp_class_from_type");
                if (classFromType(returnType(playerMethod)) != playerClass)
                    throw new InvalidOperationException("Main.LocalPlayer não retorna Terraria.Player.");
                lifeField = Resolve<FieldFromName>(library, "il2cpp_class_get_field_from_name")(playerClass, "statLife");
                if (lifeField == IntPtr.Zero ||
                    typeKind(Resolve<FieldType>(library, "il2cpp_field_get_type")(lifeField)) != 8 ||
                    (Resolve<FieldFlags>(library, "il2cpp_field_get_flags")(lifeField) & 0x10) != 0)
                    throw new InvalidOperationException("Player.statLife não é um campo de instância Int32.");
                fieldGetValue = Resolve<FieldGetValue>(library, "il2cpp_field_get_value");
                threadCurrent = Resolve<ThreadCurrent>(library, "il2cpp_thread_current");
                // All delegates/metadata are published before the native entry point can call back.
                var result = InstallHook(Marshal.ReadIntPtr(worldMethod), callback);
                if (result != 0) throw new InvalidOperationException(Error(result));
                ProcessHost.Write("Hook C++ instalado: Main.DoUpdateInWorld. Aguardando um mundo; original antes do callback C#.");
            }
            catch (Exception ex)
            {
                ProcessHost.Write($"Ponte de gameplay não ativada: {ex}");
            }
        }
    }

    private static int OnWorldUpdate(IntPtr instance, ulong tick, ulong originalCalls, int nativeTid)
    {
        try
        {
            Interlocked.Increment(ref managedCalls);
            if (tick is not (1 or 60 or 600 or 3600)) return 0;
            if (threadCurrent!() == IntPtr.Zero)
                throw new InvalidOperationException("Callback fora de uma thread IL2CPP registrada.");
            // Read only, on the gameplay thread. No offsets, reflection, scans or per-frame logging.
            var menu = menuGetter!(menuMethod);
            var playerIndex = playerIndexGetter!(playerIndexMethod);
            var player = playerGetter!(playerMethod);
            int? life = null;
            if (player != IntPtr.Zero)
            {
                fieldGetValue!(player, lifeField, out var value);
                life = value;
            }
            ProcessHost.Write($"Gameplay C# após original: tick={tick}, originais={originalCalls}, TID={nativeTid}, menuMode={menu}, jogador={playerIndex}, vida={life?.ToString() ?? "indisponível"}.");
            return 0;
        }
        catch (Exception ex)
        {
            // Never let a managed exception cross the reverse P/Invoke boundary.
            try { ProcessHost.Write($"Callback de gameplay suspenso: {ex}"); } catch { }
            return -1;
        }
    }

    internal static void Stop()
    {
        lock (gate)
        {
            if (Volatile.Read(ref available) == 0) return;
            var result = UninstallHook();
            ProcessHost.Write(result == 0
                ? "Hook removido. Um callback já iniciado pode terminar; reinicie o app para instalar novamente."
                : $"Callbacks suspensos; remoção do hook falhou: {Error(result)}.");
        }
    }

    internal static string Snapshot()
    {
        if (Volatile.Read(ref available) == 0) return "Ponte nativa: ainda não inicializada.\n";
        return $"Ponte nativa: instalada={Stat(4)}, callbacks ativos={Stat(3)}, instalações={Stat(9)}\n" +
            $"Ticks concluídos={Stat(0)}, originais iniciados={Stat(1)}, callbacks nativos={Stat(2)}, C#={Interlocked.Read(ref managedCalls)}\n" +
            $"TID={Stat(6)}, callbacks em curso={Stat(7)}, falhas C#={Stat(8)}, erro de controle={Stat(5)}\n";
    }

    private static string Error(int code) => $"{code}: {Marshal.PtrToStringUTF8(ErrorMessage(code))}";
    private static T Resolve<T>(IntPtr library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [DllImport("ivy-native", EntryPoint = "ivy_native_abi_version", CallingConvention = CallingConvention.Cdecl)] private static extern uint AbiVersion();
    [DllImport("ivy-native", EntryPoint = "ivy_native_install_world_hook", CallingConvention = CallingConvention.Cdecl)] private static extern int InstallHook(IntPtr target, WorldCallback callback);
    [DllImport("ivy-native", EntryPoint = "ivy_native_uninstall_world_hook", CallingConvention = CallingConvention.Cdecl)] private static extern int UninstallHook();
    [DllImport("ivy-native", EntryPoint = "ivy_native_get_stat", CallingConvention = CallingConvention.Cdecl)] private static extern long Stat(int key);
    [DllImport("ivy-native", EntryPoint = "ivy_native_error_message", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr ErrorMessage(int code);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WorldCallback(IntPtr instance, ulong tick, ulong originals, int nativeTid);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodFromName(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint ParameterCount(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint MethodFlags(IntPtr method, out uint implementationFlags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ReturnType(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TypeKind(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromType(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromName(IntPtr image, [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr FieldFromName(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr FieldType(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint FieldFlags(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FieldGetValue(IntPtr instance, IntPtr field, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ThreadCurrent();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntGetter(IntPtr methodInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PointerGetter(IntPtr methodInfo);
}
