using Terraria;
namespace Ivy.Runtime;

internal static class HealHooks
{
    private sealed record Entry(HookOwner Owner, On_Player.hook_HealEffect Handler);
    private static readonly object gate = new();
    private static readonly List<Entry> entries = [];
    private static readonly On_Player.orig_HealEffect original = (self,amount,broadcast) => GameRuntime.Backend.CallHealOriginal(self,amount,broadcast);
    private static On_Player.orig_HealEffect chain = original;
    private static int count;
    internal static bool HasHandlers => Volatile.Read(ref count)!=0;
    internal static void Add(On_Player.hook_HealEffect handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var owner=WorldHooks.CurrentOwner??throw new InvalidOperationException("Registre hooks no contexto de um mod.");
        lock(gate)
        {
            owner.CheckActive();
            foreach(var part in handler.GetInvocationList())entries.Add(new(owner,(On_Player.hook_HealEffect)part));
            Rebuild();
        }
    }
    internal static void Remove(On_Player.hook_HealEffect handler)
    {
        if(handler is null)return;
        var parts=handler.GetInvocationList();
        lock(gate)
        {
            for(var end=entries.Count-parts.Length;end>=0;end--)
            {
                if(!Enumerable.Range(0,parts.Length).All(i=>entries[end+i].Handler.Equals(parts[i])&&(WorldHooks.CurrentOwner is null||ReferenceEquals(entries[end+i].Owner,WorldHooks.CurrentOwner))))continue;
                entries.RemoveRange(end,parts.Length);Rebuild();break;
            }
        }
    }
    internal static void RemoveOwner(HookOwner owner){lock(gate){entries.RemoveAll(e=>ReferenceEquals(e.Owner,owner));Rebuild();}}
    private static void Rebuild()
    {
        var next=original;
        for(var i=entries.Count-1;i>=0;i--)
        {
            var entry=entries[i];var continuation=next;
            next=(self,amount,broadcast)=>
            {
                if(!entry.Owner.TryEnter()){continuation(self,amount,broadcast);return;}
                try{using var scope=WorldHooks.EnterOwner(entry.Owner);entry.Handler(continuation,self,amount,broadcast);}
                finally{entry.Owner.Exit();}
            };
        }
        Volatile.Write(ref chain,next);Volatile.Write(ref count,entries.Count);
    }
    internal static void Invoke(Player self,int amount,bool broadcast)=>Volatile.Read(ref chain)(self,amount,broadcast);
}
