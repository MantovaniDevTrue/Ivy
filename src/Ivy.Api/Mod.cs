using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ivy.Core")]
[assembly: InternalsVisibleTo("Ivy.Android")]
[assembly: InternalsVisibleTo("Ivy.Tests")]

namespace Terraria.ModLoader;

public abstract class Mod
{
    public string Name => GetType().Assembly.GetName().Name ?? GetType().Name;
    public virtual void Load() { }
    public virtual void Unload() { }
}

public abstract class ModSystem
{
    public Mod Mod { get; private set; } = null!;
    public virtual void Load() { }
    public virtual void Unload() { }

    internal void AttachToLoader(Mod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (Mod is not null)
            throw new InvalidOperationException("O sistema já pertence a um mod.");
        Mod = mod;
    }
}
