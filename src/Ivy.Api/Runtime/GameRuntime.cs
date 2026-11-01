using Terraria;

namespace Ivy.Runtime;

// Host-only boundary. No pointers or IL2CPP metadata are part of the mod API.
internal interface IGameBackend
{
    Player? LocalPlayer { get; }
    int MenuMode { get; }
    int MyPlayer { get; }
    int GetLife(Player player);
    void SetLife(Player player, int value);
    int GetMaxLife(Player player);
    void InvokeWorld(Main self);
    void CallOriginal(Main self);
}

internal static class GameRuntime
{
    private static IGameBackend? backend;
    internal static IGameBackend Backend
    {
        get => backend ?? throw new InvalidOperationException("A ponte do jogo ainda não foi inicializada.");
        set => backend = value ?? throw new ArgumentNullException(nameof(value));
    }
}
