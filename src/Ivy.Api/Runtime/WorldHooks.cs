using Terraria;

namespace Ivy.Runtime;

internal sealed class HookOwner
{
    private readonly object gate = new();
    private bool active = true;
    private int calls;
    [ThreadStatic] private static List<HookOwner>? executing;
    internal bool IsExecutingHere => executing?.Contains(this) == true;
    internal bool TryEnter()
    {
        lock (gate)
        {
            if (!active) return false;
            calls++;
        }
        (executing ??= []).Add(this);
        return true;
    }
    internal void Exit()
    {
        executing!.RemoveAt(executing.Count - 1);
        lock (gate) { calls--; if (calls == 0) Monitor.PulseAll(gate); }
    }
    internal void CheckActive() { lock (gate) ObjectDisposedException.ThrowIf(!active, this); }
    internal void Stop()
    {
        if (IsExecutingHere) throw new InvalidOperationException("Descarregue o mod fora de seu próprio hook.");
        lock (gate) active = false;
        WorldHooks.RemoveOwner(this);
        HealHooks.RemoveOwner(this);
        ReturnHookFamilies.RemoveOwner(this);
        lock (gate) while (calls != 0) Monitor.Wait(gate);
        // A handler which entered before Stop may have attempted registration.
        WorldHooks.RemoveOwner(this);
        HealHooks.RemoveOwner(this);
        ReturnHookFamilies.RemoveOwner(this);
    }
}

internal static class WorldHooks
{
    private sealed record Entry(HookOwner Owner, On_Main.hook_DoUpdateInWorld Handler);
    private static readonly object gate = new();
    private static readonly List<Entry> entries = [];
    [ThreadStatic] private static HookOwner? current;
    private static readonly On_Main.orig_DoUpdateInWorld original = self => GameRuntime.Backend.CallOriginal(self);
    private static On_Main.orig_DoUpdateInWorld chain = original;
    private static int handlerCount;
    internal static HookOwner? CurrentOwner => current;
    internal static bool HasHandlers => Volatile.Read(ref handlerCount) != 0 || HealHooks.HasHandlers || ReturnHookFamilies.HasHandlers;
    internal static OwnerScope EnterOwner(HookOwner owner)
    {
        var previous = current;
        current = owner;
        return new OwnerScope(previous);
    }
    internal static void Add(On_Main.hook_DoUpdateInWorld handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var owner = current ?? throw new InvalidOperationException("Registre hooks em Mod.Load, ModSystem.Load ou no contexto de um hook do mod.");
        lock (gate)
        {
            owner.CheckActive();
            foreach (var part in handler.GetInvocationList()) entries.Add(new(owner, (On_Main.hook_DoUpdateInWorld)part));
            Rebuild();
        }
    }
    internal static void Remove(On_Main.hook_DoUpdateInWorld handler)
    {
        if (handler is null) return;
        var parts = handler.GetInvocationList();
        lock (gate)
        {
            for (var end = entries.Count - parts.Length; end >= 0; end--)
            {
                if (!Enumerable.Range(0, parts.Length).All(i => entries[end + i].Handler.Equals(parts[i]) &&
                    (current is null || ReferenceEquals(entries[end + i].Owner, current)))) continue;
                entries.RemoveRange(end, parts.Length);
                Rebuild();
                break;
            }
        }
    }
    internal static void RemoveOwner(HookOwner owner)
    {
        lock (gate) { entries.RemoveAll(e => ReferenceEquals(e.Owner, owner)); Rebuild(); }
    }
    private static void Rebuild()
    {
        var next = original;
        // First subscriber is outermost. Rebuild only on registration changes.
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            var continuation = next;
            next = self =>
            {
                if (!entry.Owner.TryEnter()) { continuation(self); return; }
                try { using var scope = EnterOwner(entry.Owner); entry.Handler(continuation, self); }
                finally { entry.Owner.Exit(); }
            };
        }
        Volatile.Write(ref chain, next);
        Volatile.Write(ref handlerCount, entries.Count);
    }
    internal static void Invoke(Main self) => Volatile.Read(ref chain)(self);
    internal readonly struct OwnerScope(HookOwner? previous) : IDisposable { public void Dispose() => current = previous; }
}
