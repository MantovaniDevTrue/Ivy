using Terraria.ModLoader;

namespace ExampleMod;

public sealed class ExampleMod : Mod
{
    public override void Load() => Console.WriteLine("ExampleMod: Load executado.");
    public override void Unload() => Console.WriteLine("ExampleMod: Unload executado.");
}

public sealed class ExampleSystem : ModSystem
{
    public override void Load()
    {
        if (Mod.Name != "ExampleMod")
            throw new InvalidOperationException("Vínculo do sistema incorreto.");
        Console.WriteLine("ExampleSystem: vínculo com o mod validado.");
    }
}
