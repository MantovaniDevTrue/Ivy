using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Terraria.ModLoader;

namespace Ivy.Loader;

public sealed record ModMetadata(string AssemblyName, string Version, string? ApiVersion, bool HasEntryPoint, string? Problem);

public static class ModInspector
{
    public static string ApiVersion => typeof(Mod).Assembly.GetName().Version!.ToString();
    public static ModMetadata Inspect(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) throw new InvalidDataException("A DLL não contém metadados .NET.");
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly) throw new InvalidDataException("Selecione um assembly .NET, não um módulo.");
        var definition = reader.GetAssemblyDefinition();
        var name = reader.GetString(definition.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || name.Any(char.IsControl))
            throw new InvalidDataException("Nome de assembly inválido.");
        var api = reader.AssemblyReferences.Select(reader.GetAssemblyReference)
            .Where(a => reader.GetString(a.Name) == "Ivy.Api").Select(a => a.Version.ToString()).SingleOrDefault();
        bool InheritsMod(TypeDefinitionHandle handle, HashSet<TypeDefinitionHandle> seen)
        {
            if (!seen.Add(handle)) return false;
            var parent = reader.GetTypeDefinition(handle).BaseType;
            if (parent.IsNil) return false;
            if (parent.Kind == HandleKind.TypeDefinition) return InheritsMod((TypeDefinitionHandle)parent, seen);
            if (parent.Kind != HandleKind.TypeReference) return false;
            var type = reader.GetTypeReference((TypeReferenceHandle)parent);
            return reader.GetString(type.Namespace) == "Terraria.ModLoader" && reader.GetString(type.Name) == "Mod" &&
                type.ResolutionScope.Kind == HandleKind.AssemblyReference &&
                reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name) == "Ivy.Api";
        }
        var entries = reader.TypeDefinitions.Where(h =>
        {
            var type = reader.GetTypeDefinition(h);
            return !type.Attributes.HasFlag(TypeAttributes.Abstract) && type.GetGenericParameters().Count == 0 && InheritsMod(h, []);
        }).ToArray();
        var constructible = false;
        if (entries.Length == 1)
        {
            var type = reader.GetTypeDefinition(entries[0]);
            var visibility = type.Attributes & TypeAttributes.VisibilityMask;
            constructible = visibility is TypeAttributes.Public or TypeAttributes.NestedPublic && type.GetMethods().Any(h =>
            {
                var method = reader.GetMethodDefinition(h);
                if (reader.GetString(method.Name) != ".ctor" || (method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) return false;
                var signature = reader.GetBlobReader(method.Signature);
                var header = signature.ReadSignatureHeader();
                return !header.IsGeneric && signature.ReadCompressedInteger() == 0;
            });
        }
        var problem = api is null ? "Não referencia Ivy.Api; recompile os fontes com o SDK Ivy." :
            api != ApiVersion ? $"Ivy.Api {api} incompatível com {ApiVersion}." :
            !constructible ? $"É necessário um Mod concreto público com construtor sem argumentos (encontrados: {entries.Length})." : null;
        return new(name, definition.Version.ToString(), api, constructible, problem);
    }
}
