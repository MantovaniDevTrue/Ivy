using Terraria;
using Terraria.ModLoader;

namespace ExampleMod;

public sealed class ExampleMod : Mod
{
    private uint ticks;
    public override void Load() => On_Main.DoUpdateInWorld += UpdateWorld;
    public override void Unload() => On_Main.DoUpdateInWorld -= UpdateWorld;

    private void UpdateWorld(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        orig(self);
        // Demonstra acesso ao jogador real: recupera 5 de vida a cada 2 segundos de simulação.
        if (++ticks % 120 != 0) return;
        var player = Main.LocalPlayer;
        if (player is null || player.statLife <= 0) return;
        var life = player.statLife;
        var next = Math.Min(life + 5, player.statLifeMax2);
        if (next > life) player.statLife = next;
    }
}

public sealed class ExampleSystem : ModSystem
{
    public override void Load()
    {
        if (Mod.Name != "ExampleMod") throw new InvalidOperationException("Vínculo do sistema incorreto.");
    }
}
