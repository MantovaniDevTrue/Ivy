using Ivy.Runtime;
using Terraria;
internal static class HealHookChecks
{
    internal static void Run()
    {
        var events=new List<string>();var a=new HookOwner();var b=new HookOwner();var player=new Player(new object());
        On_Player.hook_HealEffect first=(orig,self,amount,broadcast)=>{events.Add("a:"+amount+":"+broadcast);orig(self,amount+2,!broadcast);};
        On_Player.hook_HealEffect second=(orig,self,amount,broadcast)=>{events.Add("b:"+amount+":"+broadcast);};
        using(WorldHooks.EnterOwner(a))On_Player.HealEffect+=first;
        using(WorldHooks.EnterOwner(b))On_Player.HealEffect+=second;
        HealHooks.Invoke(player,5,true);
        if(!events.SequenceEqual(new[]{"a:5:True","b:7:False"}))throw new Exception("Heal argument chain failed");
        a.Stop();events.Clear();HealHooks.Invoke(player,5,true);
        if(!events.SequenceEqual(new[]{"b:5:True"}))throw new Exception("Heal owner cleanup failed");
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var c=new HookOwner();
        using(WorldHooks.EnterOwner(c))On_Player.HealEffect+=(orig,self,amount,broadcast)=>{entered.Set();release.Wait();};
        using(WorldHooks.EnterOwner(b))On_Player.HealEffect-=second;b.Stop();
        var dispatch=Task.Run(()=>HealHooks.Invoke(player,3,false));if(!entered.Wait(3000))throw new Exception("Heal dispatch did not enter");
        var stop=Task.Run(c.Stop);if(stop.Wait(100))throw new Exception("Heal unload did not drain");release.Set();if(!Task.WaitAll([stop,dispatch],3000))throw new Exception("Heal unload deadlock");
        if(HealHooks.HasHandlers||WorldHooks.HasHandlers)throw new Exception("Heal registrations leaked");
        Console.WriteLine("4 heal-hook checks passed: arguments/omit original, owner cleanup, in-flight drain and no leaked registrations.");
    }
}
