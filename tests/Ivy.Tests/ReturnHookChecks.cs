using Ivy.Runtime;
using Terraria;
internal static class ReturnHookChecks
{
 internal static void Run()
 {
  var a=new HookOwner();var b=new HookOwner();var player=new Player(new object());
  On_Player.hook_FindBuffIndex first=(orig,self,value)=>orig(self,value+1)+2;
  On_Player.hook_FindBuffIndex second=(orig,self,value)=>value*3;
  using(WorldHooks.EnterOwner(a))On_Player.FindBuffIndex+=first;
  using(WorldHooks.EnterOwner(b))On_Player.FindBuffIndex+=second;
  if(FindBuffHooks.Invoke(player,5)!=20)throw new Exception("Return chain/argument mutation failed");
  a.Stop();if(FindBuffHooks.Invoke(player,5)!=15)throw new Exception("Return owner removal failed");b.Stop();
  var c=new HookOwner();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
  using(WorldHooks.EnterOwner(c))On_Player.FindBuffIndex+=(orig,self,value)=>{entered.Set();release.Wait();return 71;};
  var dispatch=Task.Run(()=>FindBuffHooks.Invoke(player,4));if(!entered.Wait(3000))throw new Exception("Return hook did not enter");
  var stop=Task.Run(c.Stop);if(stop.Wait(100))throw new Exception("Return hook did not drain");release.Set();if(!Task.WaitAll([dispatch,stop],3000)||dispatch.Result!=71)throw new Exception("Return hook drain failed");
  if(FindBuffHooks.HasHandlers)throw new Exception("Return handler leaked");
  var d=new HookOwner();using(WorldHooks.EnterOwner(d))On_Player.IsItemSlotUnlockedAndUsable+=(orig,self,value)=>!orig(self,value-1);
  if(!ItemSlotUsableHooks.Invoke(player,0)||ItemSlotUsableHooks.Invoke(player,2))throw new Exception("Bool hook result/argument chain failed");
  d.Stop();if(ItemSlotUsableHooks.HasHandlers)throw new Exception("Bool hook owner leaked");
  Console.WriteLine("6 return-hook checks passed: arguments/result, owner removal, in-flight drain and no leaked handlers.");
 }
}
