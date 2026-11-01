using System.Runtime.InteropServices;
using Ivy.Runtime;
using Terraria;

namespace Ivy.AndroidHost;

// These delegates belong to the host, never to a collectible mod assembly.
// Keep them rooted for the entire process, including callbacks racing with removal.
internal static class NativeWorldBridge
{
    private static readonly object gate = new();
    private static readonly WorldCallback callback = OnWorldUpdate;
    private static GameBackend? backend;
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
                if (AbiVersion() != 2) throw new InvalidOperationException("ABI da ponte Ivy incompatível.");
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
                var fieldFromName = Resolve<FieldFromName>(library, "il2cpp_class_get_field_from_name");
                var fieldType = Resolve<FieldType>(library, "il2cpp_field_get_type");
                var fieldFlags = Resolve<FieldFlags>(library, "il2cpp_field_get_flags");
                IntPtr IntField(string name)
                {
                    var field = fieldFromName(playerClass, name);
                    if (field == IntPtr.Zero || typeKind(fieldType(field)) != 8 || (fieldFlags(field) & 0x10) != 0)
                        throw new InvalidOperationException($"Player.{name} não é um campo de instância Int32.");
                    return field;
                }
                lifeField = IntField("statLife");
                var maxLifeField = IntField("statLifeMax2");
                fieldGetValue = Resolve<FieldGetValue>(library, "il2cpp_field_get_value");
                threadCurrent = Resolve<ThreadCurrent>(library, "il2cpp_thread_current");
                backend = new GameBackend(library, worldMethod, maxLifeField);
                GameRuntime.Backend = backend;
                // All delegates/metadata are published before the native entry point can call back.
                var result = InstallHook(Marshal.ReadIntPtr(worldMethod), callback);
                if (result != 0) throw new InvalidOperationException(Error(result));
                ProcessHost.Write("Hook C++ instalado: Main.DoUpdateInWorld. Aguardando um mundo; handlers On_ controlam orig; API tipada pronta.");
            }
            catch (Exception ex)
            {
                ProcessHost.Write($"Ponte de gameplay não ativada: {ex}");
            }
        }
    }

    private static int OnWorldUpdate(IntPtr instance, IntPtr method, ulong tick, int nativeTid)
    {
        try
        {
            Interlocked.Increment(ref managedCalls);
            var self = backend!.Enter(instance);
            try
            {
                WorldHooks.Invoke(self);
                if (tick is 1 or 60 or 600 or 3600)
                {
                    ProcessHost.Write($"On_Main concluído: tick={tick}, TID={nativeTid}.");
                    ProcessHost.Write(Snapshot().TrimEnd());
                }
            }
            finally { backend.Exit(); }
            return 0;
        }
        catch (Exception ex)
        {
            // The native frame falls back only if no original call has started.
            try { ProcessHost.Write($"Hook gerenciado suspenso: {ex}"); } catch { }
            return -1;
        }
    }

    private sealed class GameBackend : IGameBackend
    {
        private sealed class Entry(uint handle, IntPtr address)
        {
            internal uint Handle = handle;
            internal readonly IntPtr Address = address;
        }
        private readonly GcHandleNew newHandle;
        private readonly GcHandleFree freeHandle;
        private readonly GcHandleTarget handleTarget;
        private readonly FieldSetValue setValue;
        private readonly WorldInvoke invoke;
        private readonly IntPtr worldMethod, maxLifeField;
        private Entry? mainEntry, playerEntry;
        private Terraria.Main? main;
        private Player? player;
        private int writes;
        [ThreadStatic] private static int depth;
        internal GameBackend(IntPtr library, IntPtr method, IntPtr maxLife)
        {
            worldMethod = method;
            maxLifeField = maxLife;
            invoke = Marshal.GetDelegateForFunctionPointer<WorldInvoke>(Marshal.ReadIntPtr(method));
            newHandle = Resolve<GcHandleNew>(library, "il2cpp_gchandle_new");
            freeHandle = Resolve<GcHandleFree>(library, "il2cpp_gchandle_free");
            handleTarget = Resolve<GcHandleTarget>(library, "il2cpp_gchandle_get_target");
            setValue = Resolve<FieldSetValue>(library, "il2cpp_field_set_value");
        }
        private void RequireScope()
        {
            if (depth == 0) throw new InvalidOperationException("Acesse o jogo dentro do hook, na thread de gameplay.");
        }
        private Entry NewEntry(IntPtr address)
        {
            var handle = newHandle(address, 0);
            if (handle == 0) throw new InvalidOperationException("Não foi possível proteger o objeto IL2CPP.");
            return new Entry(handle, address);
        }
        private void Release(Entry? entry)
        {
            if (entry is null || entry.Handle == 0) return;
            freeHandle(entry.Handle);
            entry.Handle = 0;
        }
        private IntPtr Pointer(object identity)
        {
            RequireScope();
            if (ReferenceEquals(identity, playerEntry)) RefreshPlayer();
            if (identity is not Entry entry || entry.Handle == 0)
                throw new ObjectDisposedException("Objeto de uma sessão anterior do jogo.");
            var pointer = handleTarget(entry.Handle);
            if (pointer == IntPtr.Zero) throw new ObjectDisposedException("Objeto IL2CPP indisponível.");
            return pointer;
        }
        internal Terraria.Main Enter(IntPtr instance)
        {
            if (threadCurrent!() == IntPtr.Zero) throw new InvalidOperationException("Thread IL2CPP não registrada.");
            depth++;
            try
            {
                if (mainEntry?.Address != instance)
                {
                    Release(mainEntry);
                    mainEntry = NewEntry(instance);
                    main = new Terraria.Main(mainEntry);
                }
                return main!;
            }
            catch { depth--; throw; }
        }
        internal void Exit() => depth--;
        private void RefreshPlayer()
        {
            RequireScope();
            var address = playerGetter!(playerMethod);
            if (playerEntry?.Address == address) return;
            Release(playerEntry);
            playerEntry = null;
            player = null;
            if (address == IntPtr.Zero) return;
            playerEntry = NewEntry(address);
            player = new Player(playerEntry);
        }
        public Player? LocalPlayer { get { RefreshPlayer(); return player; } }
        public int MenuMode { get { RequireScope(); return menuGetter!(menuMethod); } }
        public int MyPlayer { get { RequireScope(); return playerIndexGetter!(playerIndexMethod); } }
        public int GetLife(Player target) { fieldGetValue!(Pointer(target.Identity), lifeField, out var value); return value; }
        public int GetMaxLife(Player target) { fieldGetValue!(Pointer(target.Identity), maxLifeField, out var value); return value; }
        public void SetLife(Player target, int value)
        {
            var address = Pointer(target.Identity);
            var report = ++writes <= 3;
            var before = 0;
            if (report) fieldGetValue!(address, lifeField, out before);
            setValue(address, lifeField, ref value);
            if (report)
            {
                fieldGetValue!(address, lifeField, out var after);
                ProcessHost.Write($"SDK Player.statLife: {before} → {after}; escrita IL2CPP e releitura concluídas.");
            }
        }
        public void InvokeWorld(Terraria.Main self) => invoke(Pointer(self.Identity), worldMethod);
        public void CallOriginal(Terraria.Main self)
        {
            var result = CallWorldOriginal(Pointer(self.Identity));
            if (result != 0) throw new InvalidOperationException(Error(result));
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
            $"Entradas no hook={Stat(0)}, originais iniciados={Stat(1)}, callbacks nativos={Stat(2)}, C#={Interlocked.Read(ref managedCalls)}\n" +
            $"TID={Stat(6)}, callbacks em curso={Stat(7)}, falhas C#={Stat(8)}, erro de controle={Stat(5)}\n";
    }

    private static string Error(int code) => $"{code}: {Marshal.PtrToStringUTF8(ErrorMessage(code))}";
    private static T Resolve<T>(IntPtr library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [DllImport("ivy-native", EntryPoint = "ivy_native_call_world_original", CallingConvention = CallingConvention.Cdecl)] private static extern int CallWorldOriginal(IntPtr instance);
    [DllImport("ivy-native", EntryPoint = "ivy_native_abi_version", CallingConvention = CallingConvention.Cdecl)] private static extern uint AbiVersion();
    [DllImport("ivy-native", EntryPoint = "ivy_native_install_world_hook", CallingConvention = CallingConvention.Cdecl)] private static extern int InstallHook(IntPtr target, WorldCallback callback);
    [DllImport("ivy-native", EntryPoint = "ivy_native_uninstall_world_hook", CallingConvention = CallingConvention.Cdecl)] private static extern int UninstallHook();
    [DllImport("ivy-native", EntryPoint = "ivy_native_get_stat", CallingConvention = CallingConvention.Cdecl)] private static extern long Stat(int key);
    [DllImport("ivy-native", EntryPoint = "ivy_native_error_message", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr ErrorMessage(int code);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint GcHandleNew(IntPtr target, byte pinned);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GcHandleFree(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GcHandleTarget(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FieldSetValue(IntPtr instance, IntPtr field, ref int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WorldInvoke(IntPtr instance, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WorldCallback(IntPtr instance, IntPtr method, ulong tick, int nativeTid);
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
