using System.Reflection;
using System.Reflection.Emit;
using Ivy.Loader;
using Terraria.ModLoader;

var events = Probe.Events;
var logs = new List<string>();
var assembly = MakeAssembly("Lifecycle", true, false);
using (var session = new ModSession(logs.Add))
{
    session.LoadAssembly(assembly);
    Check(session.ModCount == 1 && session.SystemCount == 1, "registro de mod e sistema");
    Check(events.SequenceEqual(new[] { "Mod.Load", "System.Load" }), "ordem de inicialização");
    Throws<InvalidOperationException>(() => session.LoadAssembly(assembly), "rejeição de duplicata");
    session.Dispose();
    Check(events.SequenceEqual(new[] { "Mod.Load", "System.Load", "System.Unload", "Mod.Unload" }), "ordem inversa de encerramento");
    session.Dispose();
    Check(events.Count == 4, "encerramento idempotente");
    Throws<ObjectDisposedException>(() => session.LoadAssembly(assembly), "sessão encerrada bloqueada");
}
events.Clear();
using (var session = new ModSession(logs.Add))
{
    Throws<InvalidOperationException>(() => session.LoadAssembly(MakeAssembly("Failure", true, true)), "erro de Load propagado");
    Check(session.ModCount == 0 && session.SystemCount == 0, "contadores após falha");
    Check(events.SequenceEqual(new[] { "Mod.Load", "System.Load", "System.Unload", "Mod.Unload" }), "rollback após falha parcial");
    Throws<InvalidOperationException>(() => session.LoadAssembly(typeof(Probe).Assembly), "DLL sem Mod rejeitada");
}
if (args.Length != 1) throw new ArgumentException("Informe o caminho de ExampleMod.dll.");
using (var session = new ModSession(logs.Add))
{
    session.LoadFile(args[0]);
    Check(session.ModCount == 1 && session.SystemCount == 1, "DLL externa e identidade compartilhada da API");
}
Console.WriteLine("11 verificações passaram.");

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALHOU: " + name);
    Console.WriteLine("OK: " + name);
}
static void Throws<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { Console.WriteLine("OK: " + name); return; }
    throw new Exception("FALHOU: " + name);
}
static Assembly MakeAssembly(string name, bool system, bool fail)
{
    var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
    var module = assembly.DefineDynamicModule(name);
    AddType("TestMod", typeof(Mod), "Mod", false);
    if (system) AddType("TestSystem", typeof(ModSystem), "System", fail);
    return assembly;

    void AddType(string typeName, Type parent, string label, bool shouldFail)
    {
        var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Sealed, parent);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        foreach (var hook in new[] { "Load", "Unload" })
        {
            var method = type.DefineMethod(hook, MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldstr, label + "." + hook);
            il.Emit(OpCodes.Call, typeof(Probe).GetMethod(nameof(Probe.Record))!);
            if (shouldFail && hook == "Load")
            {
                il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Throw);
            }
            else il.Emit(OpCodes.Ret);
            type.DefineMethodOverride(method, parent.GetMethod(hook)!);
        }
        type.CreateType();
    }
}
public static class Probe
{
    public static List<string> Events { get; } = [];
    public static void Record(string value) => Events.Add(value);
}
