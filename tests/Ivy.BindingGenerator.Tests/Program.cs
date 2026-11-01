using Ivy.BindingGenerator;
using System.Text.Json;

var assembly = CatalogReader.ReadAssembly(typeof(Terraria.Fixture.Sample).Assembly.Location, ["Terraria"]);
var sample = assembly.Types.Single(t => t.Name == "Terraria.Fixture.Sample");
Check(assembly.Types.Any(t => t.Name == "Terraria.Fixture.Sample+Nested"), "tipo aninhado e namespace");
var overloads = sample.Methods.Where(m => m.Name == "Overload").ToArray();
Check(overloads.Length == 2 && overloads.Select(m => m.ResolutionKey).Distinct().Count() == 2, "overloads com o mesmo número de parâmetros");
var references = sample.Methods.Single(m => m.Name == "References");
Check(references.Parameters[0].Type == "System.Int32&" && references.Parameters[1].Type == "System.Boolean&" && references.Parameters[1].IsOut, "ref/out preservados");
Check(references.ReviewReasons.Contains("byref-lifetime-and-writeback"), "ref/out exigem validação específica");
var arrays = sample.Methods.Single(m => m.Name == "Arrays");
Check(arrays.Parameters.Select(p => p.Type).SequenceEqual(["System.Int32[]", "System.Int32[,]"]), "ranks de arrays");
var optional = sample.Methods.Single(m => m.Name == "Optional");
Check(optional.Parameters.All(p => p.Optional) && optional.Parameters[0].Default == "Int32:7" && optional.Parameters[1].Default == "String:ok", "defaults e parâmetros opcionais");
var echo = sample.Methods.Single(m => m.Name == "Echo");
Check(echo.GenericArity == 1 && echo.ReturnType == "!!0" && echo.ReviewReasons.Contains("generic-context"), "método genérico");
var generic = assembly.Types.Single(t => t.Name == "Terraria.Fixture.GenericSample`1");
Check(generic.GenericArity == 1 && generic.Methods.Single(m => m.Name == "Echo").ReturnType == "!0", "tipo genérico");
Check(sample.Fields.Single(f => f.Name == "Constant").Constant == "Int32:42" && sample.Fields.Single(f => f.Name == "ReadOnlyField").IsReadOnly, "constante e readonly");
var property = sample.Properties.Single(p => p.Name == "Value");
Check(property.GetterToken is not null && property.SetterToken is not null && property.Type == "System.Int32", "propriedade ligada a accessors");
Check(sample.Events.Single(e => e.Name == "Changed").AddToken is not null, "evento ligado a accessor");
Check(assembly.Types.Single(t => t.Name == "Terraria.Fixture.AbstractSample").Methods.Single(m => m.Name == "Tick").ReviewReasons.Contains("abstract-no-native-body"), "método abstrato não é candidato a hook");
var again = CatalogReader.ReadAssembly(typeof(Terraria.Fixture.Sample).Assembly.Location, ["Terraria"]);
Check(JsonSerializer.Serialize(assembly) == JsonSerializer.Serialize(again), "saída determinística e leitura sem inicializadores");
Check(assembly.Types.All(t => t.Namespace.StartsWith("Terraria", StringComparison.Ordinal)), "filtro de namespaces");
Console.WriteLine("14 verificações do gerador passaram.");

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALHOU: " + name);
    Console.WriteLine("OK: " + name);
}
