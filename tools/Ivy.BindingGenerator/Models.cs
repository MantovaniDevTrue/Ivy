namespace Ivy.BindingGenerator;

public sealed record ParameterContract(string Name, string Type, bool IsOut, bool IsIn, bool Optional, string? Default);
public sealed record MethodContract(string Id, string Assembly, string DeclaringType, string Name,
    int Token, string ReturnType, ParameterContract[] Parameters, bool IsStatic, int GenericArity,
    string Visibility, string CallingConvention, string[] ReviewReasons)
{
    public string ResolutionKey => $"{Assembly}|{DeclaringType}|{Name}``{GenericArity}|{ReturnType}|{string.Join(";", Parameters.Select(p => p.Type))}|{IsStatic}";
}
public sealed record FieldContract(string Name, string Type, int Token, bool IsStatic, bool IsLiteral,
    bool IsReadOnly, string Visibility, string? Constant);
public sealed record PropertyContract(string Name, string Type, string[] IndexTypes, int? GetterToken, int? SetterToken);
public sealed record EventContract(string Name, string Type, int? AddToken, int? RemoveToken);
public sealed record TypeContract(string Assembly, string Name, string Namespace, int Token, string Kind,
    string Visibility, string? BaseType, string[] Interfaces, int GenericArity, MethodContract[] Methods,
    FieldContract[] Fields, PropertyContract[] Properties, EventContract[] Events);
public sealed record AssemblyContract(string Name, string Version, string File, string Sha256, TypeContract[] Types);
public sealed record ApiCatalog(int SchemaVersion, string Mode, AssemblyContract[] Assemblies);

public static class BindingPolicy
{
    private static readonly HashSet<string> primitive = new(StringComparer.Ordinal)
    {
        "System.Void", "System.Boolean", "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Char", "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single",
        "System.Double", "System.IntPtr", "System.UIntPtr"
    };

    public static string[] Review(string name, string result, IEnumerable<string> parameters, int genericArity, int typeArity)
    {
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        if (genericArity != 0 || typeArity != 0) reasons.Add("generic-context");
        if (name is ".ctor" or ".cctor") reasons.Add("construction-or-type-initialization");
        foreach (var value in parameters.Prepend(result))
        {
            if (value.Contains('&')) reasons.Add("byref-lifetime-and-writeback");
            if (value.Contains('[')) reasons.Add("array-marshalling");
            if (value.Contains('*') || value.Contains("fnptr(", StringComparison.Ordinal)) reasons.Add("pointer-or-function-pointer");
            var bare = value.TrimEnd('&');
            if (bare == "System.String") reasons.Add("il2cpp-string-conversion");
            else if (!primitive.Contains(bare)) reasons.Add("object-or-value-type-marshalling");
        }
        return reasons.ToArray();
    }
}
