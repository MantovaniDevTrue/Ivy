using Terraria;
using Terraria.ModLoader;

namespace ExampleMod;

public sealed class ExampleMod : Mod
{
    private uint ticks;
    public override void Load()
    {
        On_Main.DoUpdateInWorld += UpdateWorld;
        On_Player.HealEffect += HealEffect;
        On_Player.FindBuffIndex += FindBuffIndex;
    }
    public override void Unload()
    {
        On_Main.DoUpdateInWorld -= UpdateWorld;
        On_Player.HealEffect -= HealEffect;
        On_Player.FindBuffIndex -= FindBuffIndex;
    }
    private int FindBuffIndex(On_Player.orig_FindBuffIndex orig,Player self,int type)=>orig(self,type);
    private void HealEffect(On_Player.orig_HealEffect orig, Player self, int amount, bool broadcast)
    {
        // Argumentos e chamada ao original seguem o mesmo padrão dos hooks On_ do PC.
        orig(self, amount, broadcast);
    }

    private void UpdateWorld(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        orig(self);
        // Demonstra acesso ao jogador real: recupera 5 de vida a cada 2 segundos de simulação.
        if (++ticks % 120 != 0) return;
        var player = Main.LocalPlayer;
        if (player is null || player.dead || player.statLife <= 0) return;
        var inventory=player.inventory;
        if(inventory is null||inventory.Length==0)return;
        var first=inventory[0];
        var position=player.position;
        if(!float.IsFinite(position.X)||!float.IsFinite(position.Y))return;
        var life = player.statLife;
        var next = Math.Min(life + 5, player.statLifeMax2);
        if (next > life)
        {
            // FindBuffIndex é chamado pelo backend geral; dead é um bool nativo gerado.
            if (player.FindBuffIndex(21) < -1) throw new InvalidOperationException("Índice de buff inválido.");
            player.statLife = next;
            player.HealEffect(next - life, broadcast: false);
        }
    }
}

public sealed class ExampleSystem : ModSystem
{
    public override void Load()
    {
        if (Mod.Name != "ExampleMod") throw new InvalidOperationException("Vínculo do sistema incorreto.");
    }
}
