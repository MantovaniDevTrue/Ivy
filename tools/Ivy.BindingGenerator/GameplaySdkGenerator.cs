namespace Ivy.BindingGenerator;

public static class GameplaySdkGenerator
{
    // Deliberately narrow first profile. All emitted members must match the input metadata.
    public static string Generate(ApiCatalog catalog)
    {
        var types = catalog.Assemblies.SelectMany(a => a.Types).ToArray();
        TypeContract Type(string name) => types.Single(t => t.Name == name && t.Kind == "class" && t.GenericArity == 0);
        var main = Type("Terraria.Main");
        var player = Type("Terraria.Player");
        void Method(string name, string result, bool isStatic)
        {
            if (main.Methods.Count(m => m.Name == name && m.ReturnType == result && m.IsStatic == isStatic &&
                m.GenericArity == 0 && m.Parameters.Length == 0) != 1)
                throw new InvalidDataException("Assinatura incompatível no perfil de gameplay: Main." + name);
        }
        Method("DoUpdateInWorld", "System.Void", false);
        Method("get_LocalPlayer", "Terraria.Player", true);
        Method("get_menuMode", "System.Int32", true);
        Method("get_myPlayer", "System.Int32", true);
        foreach (var name in new[] { "statLife", "statLifeMax2" })
            if (player.Fields.Count(f => f.Name == name && f.Type == "System.Int32" && !f.IsStatic && !f.IsReadOnly && !f.IsLiteral) != 1)
                throw new InvalidDataException("Campo incompatível no perfil de gameplay: Player." + name);
        using var stream = typeof(GameplaySdkGenerator).Assembly.GetManifestResourceStream("Ivy.BindingGenerator.Gameplay.g.cs.txt")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
