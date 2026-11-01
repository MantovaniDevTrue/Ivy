using System.Reflection;
using System.Runtime.Loader;
using Terraria.ModLoader;

namespace Ivy.Loader;

public sealed class ModSession : IDisposable
{
    private readonly Action<string> log;
    private readonly List<(Action Unload, string Name)> loaded = [];
    private readonly List<AssemblyLoadContext> contexts = [];
    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;
    public int ModCount { get; private set; }
    public int SystemCount { get; private set; }

    public ModSession(Action<string> log) => this.log = log;

    public void LoadFile(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var context = new ModLoadContext();
        try
        {
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
            LoadAssembly(assembly);
            contexts.Add(context);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    public void LoadAssembly(Assembly assembly)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var types = assembly.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters)
            .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        var entries = types.Where(t => typeof(Mod).IsAssignableFrom(t)).ToArray();
        if (entries.Length != 1)
            throw new InvalidOperationException($"Esperado um Mod por DLL; encontrados {entries.Length}.");
        var mod = (Mod)Activator.CreateInstance(entries[0])!;
        if (!names.Add(mod.Name))
            throw new InvalidOperationException($"Mod já carregado: {mod.Name}.");
        var checkpoint = loaded.Count;
        var systemsAdded = 0;
        try
        {
            loaded.Add((mod.Unload, mod.Name));
            mod.Load();
            foreach (var type in types.Where(t => typeof(ModSystem).IsAssignableFrom(t)))
            {
                var system = (ModSystem)Activator.CreateInstance(type)!;
                system.AttachToLoader(mod);
                loaded.Add((system.Unload, type.Name));
                system.Load();
                systemsAdded++;
            }
            ModCount++;
            SystemCount += systemsAdded;
            log($"C# carregou: {mod.Name}, {systemsAdded} sistema(s).");
        }
        catch
        {
            UnloadTo(checkpoint);
            names.Remove(mod.Name);
            throw;
        }
    }

    private void UnloadTo(int count)
    {
        for (var i = loaded.Count - 1; i >= count; i--)
        {
            try { loaded[i].Unload(); }
            catch (Exception ex) { log($"Falha ao descarregar {loaded[i].Name}: {ex}"); }
            loaded.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnloadTo(0);
        foreach (var context in contexts) context.Unload();
        contexts.Clear();
        names.Clear();
        ModCount = SystemCount = 0;
        log("Sessão C# encerrada.");
    }

    private sealed class ModLoadContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var api = typeof(Mod).Assembly;
            if (assemblyName.Name != api.GetName().Name) return null;
            if (assemblyName.Version != api.GetName().Version)
                throw new FileLoadException("Versão da API do mod incompatível.");
            return api;
        }
    }
}
