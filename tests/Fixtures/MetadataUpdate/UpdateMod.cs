using Terraria.ModLoader;
public sealed class MetadataOnlyMod : Mod
{
    static MetadataOnlyMod() => throw new InvalidOperationException("Metadata inspection must not execute this initializer.");
}
