using Ivy.Loader;
using Ivy.Runtime;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.ModLoader;

public static class GameplayChecks
{
    private static int count;
    private static readonly List<string> events = [];
    private static readonly FakeBackend backend = new();
    private static readonly Main self = new(new object());
    public static void Run(string example)
    {
        GameRuntime.Backend = backend;
        NativeAccess.Backend = new FakeAccess();
        var a = new HookOwner(); var b = new HookOwner();
        On_Main.hook_DoUpdateInWorld first = (orig, target) => { events.Add("a+"); orig(target); events.Add("a-"); };
        On_Main.hook_DoUpdateInWorld second = (orig, target) => { events.Add("b+"); orig(target); events.Add("b-"); };
        using (WorldHooks.EnterOwner(a)) On_Main.DoUpdateInWorld += first;
        using (WorldHooks.EnterOwner(b)) On_Main.DoUpdateInWorld += second;
        Tick(); Check(events.SequenceEqual(new[] { "a+", "b+", "orig", "b-", "a-" }), "typed hook chain order");
        using (WorldHooks.EnterOwner(a)) On_Main.DoUpdateInWorld -= first;
        events.Clear(); Tick(); Check(events.SequenceEqual(new[] { "b+", "orig", "b-" }), "remove one handler preserves the other");
        a.Stop(); b.Stop();
        var omit = new HookOwner();
        using (WorldHooks.EnterOwner(omit)) On_Main.DoUpdateInWorld += (_, _) => { };
        var before = backend.Originals; Tick(); Check(backend.Originals == before, "handler may omit orig"); omit.Stop();
        var twice = new HookOwner();
        using (WorldHooks.EnterOwner(twice)) On_Main.DoUpdateInWorld += (orig, target) => { orig(target); orig(target); };
        before = backend.Originals; Tick(); Check(backend.Originals == before + 2, "handler may call orig twice"); twice.Stop();
        var failure = new HookOwner();
        using (WorldHooks.EnterOwner(failure)) On_Main.DoUpdateInWorld += (_, _) => throw new InvalidOperationException("probe");
        try { Tick(); throw new Exception("Expected hook failure"); } catch (InvalidOperationException) { }
        failure.Stop(); Tick(); Check(true, "exception releases owner accounting");
        var own = new HookOwner();
        using (WorldHooks.EnterOwner(own)) On_Main.DoUpdateInWorld += (orig, target) =>
        {
            try { own.Stop(); throw new Exception("Expected rejection"); } catch (InvalidOperationException) { }
            orig(target);
        };
        Tick(); own.Stop(); Check(true, "self-unload rejected without deadlock");
        var concurrent = new HookOwner();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using (WorldHooks.EnterOwner(concurrent)) On_Main.DoUpdateInWorld += (orig, target) => { entered.Set(); release.Wait(); orig(target); };
        var dispatch = Task.Run(Tick);
        Check(entered.Wait(TimeSpan.FromSeconds(3)), "concurrent callback entered");
        var stopping = Task.Run(concurrent.Stop);
        Check(!stopping.Wait(100), "unload waits for in-flight handler");
        release.Set(); Check(Task.WaitAll([dispatch, stopping], 3000), "in-flight handler drains before unload returns");
        events.Clear(); Tick(); Check(events.SequenceEqual(new[] { "orig" }), "removed handler never starts again");
        using (var session = new ModSession(_ => { }))
        {
            session.LoadAssembly(LeakyMod(false)); Tick();
            session.Dispose(); events.Clear(); Tick();
            Check(events.SequenceEqual(new[] { "orig" }), "session unload automatically removes forgotten hooks");
        }
        using (var session = new ModSession(_ => { }))
        {
            try { session.LoadAssembly(LeakyMod(true)); throw new Exception("Expected Load failure"); } catch (InvalidOperationException) { }
            events.Clear(); Tick(); Check(events.SequenceEqual(new[] { "orig" }), "failed Load rolls back hook registrations");
        }
        using (var session = new ModSession(_ => { }))
        {
            session.LoadFile(example); backend.Life = 91; before = backend.Originals;
            for (var i = 0; i < 120; i++) Tick();
            Check(backend.Life == 96 && backend.Originals == before + 120, "external DLL uses typed orig and player writeback");
            for (var i = 0; i < 120; i++) Tick();
            Check(backend.Life == 100, "example respects typed max life");
        }
        backend.Life = 81; for (var i = 0; i < 120; i++) Tick();
        Check(backend.Life == 81, "unloaded external mod no longer changes life");
        Console.WriteLine($"{count} gameplay/hook checks passed (fake game backend). ");
    }
    public static void RegisterForgottenHook() => On_Main.DoUpdateInWorld += (orig, target) => { events.Add("leaked"); orig(target); };
    private static Assembly LeakyMod(bool fail)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Leaky" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("m").DefineType("LeakyMod", TypeAttributes.Public, typeof(Mod));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var method = type.DefineMethod("Load", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(GameplayChecks).GetMethod(nameof(RegisterForgottenHook))!);
        if (fail) { il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!); il.Emit(OpCodes.Throw); }
        else il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(method, typeof(Mod).GetMethod("Load")!);
        type.CreateType(); return assembly;
    }
    private static void Tick() => WorldHooks.Invoke(self);
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("OK: " + name); }
    private sealed class FakeAccess : INativeAccessBackend
    {
        public NativeValue Read(NativeMember m, object? identity) => m.Name=="dead" ? NativeValue.From(false) : throw new NotSupportedException(m.Name);
        public void Write(NativeMember m, object? identity, NativeValue v) => throw new NotSupportedException(m.Name);
        public NativeValue Invoke(NativeMember m, object? identity, ReadOnlySpan<NativeValue> args)
        {
            if(m.Name=="FindBuffIndex") return NativeValue.From(-1);
            if(m.Name=="HealEffect") { HealHooks.Invoke(backend.LocalPlayer,(int)args[0].Bits,args[1].Bits!=0); return default; }
            throw new NotSupportedException(m.Name);
        }
    }
    private sealed class FakeBackend : IGameBackend
    {
        internal int Life = 100, Originals;
        private readonly Player player = new(new object());
        public Player LocalPlayer => player;
        public int MenuMode => 10;
        public int MyPlayer => 0;
        public int GetLife(Player p) => Life;
        public void SetLife(Player p, int value) => Life = value;
        public int GetMaxLife(Player p) => 100;
        public void InvokeWorld(Main target) => WorldHooks.Invoke(target);
        public void CallHealOriginal(Player target, int amount, bool broadcast) { }
        public void CallOriginal(Main target) { Interlocked.Increment(ref Originals); events.Add("orig"); }
    }
}
