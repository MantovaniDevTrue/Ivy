using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Ivy.BindingGenerator;

public static class CatalogReader
{
    public static ApiCatalog ReadDirectory(string directory, string[] namespacePrefixes)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var files = Directory.GetFiles(directory, "*.dll").Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new InvalidDataException("Nenhuma DLL encontrada.");
        var assemblies = files.Select(p => ReadAssembly(p, namespacePrefixes)).Where(a => a.Types.Length > 0).ToArray();
        if (assemblies.Length == 0) throw new InvalidDataException("Nenhum tipo corresponde aos namespaces selecionados.");
        return new(1, "metadata-only-no-runtime-execution", assemblies);
    }

    public static AssemblyContract ReadAssembly(string path, string[] namespacePrefixes)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) throw new BadImageFormatException("DLL sem metadados CLR: " + path);
        var reader = pe.GetMetadataReader();
        var definition = reader.GetAssemblyDefinition();
        var assembly = reader.GetString(definition.Name);
        var signatures = new Signatures();
        var types = new List<TypeContract>();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            var top = type;
            while (!top.GetDeclaringType().IsNil) top = reader.GetTypeDefinition(top.GetDeclaringType());
            var ns = reader.GetString(top.Namespace);
            if (!namespacePrefixes.Any(prefix => ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))) continue;
            var name = Signatures.DefinitionName(reader, handle);
            var arity = type.GetGenericParameters().Count;
            var methods = new List<MethodContract>();
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var signature = method.DecodeSignature(signatures, null);
                var parameters = method.GetParameters().Select(p => reader.GetParameter(p)).Where(p => p.SequenceNumber != 0)
                    .ToDictionary(p => p.SequenceNumber);
                var args = signature.ParameterTypes.Select((parameterType, index) =>
                {
                    if (!parameters.TryGetValue(index + 1, out var parameter))
                        return new ParameterContract("arg" + index, parameterType, false, false, false, null);
                    return new ParameterContract(reader.GetString(parameter.Name), parameterType,
                        parameter.Attributes.HasFlag(ParameterAttributes.Out), parameter.Attributes.HasFlag(ParameterAttributes.In),
                        parameter.Attributes.HasFlag(ParameterAttributes.Optional), Constant(reader, parameter.GetDefaultValue()));
                }).ToArray();
                var token = MetadataTokens.GetToken(methodHandle);
                var methodName = reader.GetString(method.Name);
                var review = BindingPolicy.Review(methodName, signature.ReturnType, signature.ParameterTypes, method.GetGenericParameters().Count, arity).ToList();
                if (method.Attributes.HasFlag(MethodAttributes.Abstract)) review.Add("abstract-no-native-body");
                if (method.Attributes.HasFlag(MethodAttributes.PinvokeImpl) || method.ImplAttributes.HasFlag(MethodImplAttributes.InternalCall)) review.Add("extern-or-internal-call");
                if (signature.Header.CallingConvention == SignatureCallingConvention.VarArgs) review.Add("varargs");
                methods.Add(new(assembly + ":" + token.ToString("X8"), assembly, name, methodName, token,
                    signature.ReturnType, args, method.Attributes.HasFlag(MethodAttributes.Static), method.GetGenericParameters().Count,
                    (method.Attributes & MethodAttributes.MemberAccessMask).ToString(), signature.Header.CallingConvention.ToString(),
                    review.Order(StringComparer.Ordinal).ToArray()));
            }
            var fields = type.GetFields().Select(fieldHandle =>
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                return new FieldContract(reader.GetString(field.Name), field.DecodeSignature(signatures, null), MetadataTokens.GetToken(fieldHandle),
                    field.Attributes.HasFlag(FieldAttributes.Static), field.Attributes.HasFlag(FieldAttributes.Literal),
                    field.Attributes.HasFlag(FieldAttributes.InitOnly), (field.Attributes & FieldAttributes.FieldAccessMask).ToString(),
                    Constant(reader, field.GetDefaultValue()));
            }).ToArray();
            var properties = type.GetProperties().Select(propertyHandle =>
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                var signature = property.DecodeSignature(signatures, null);
                var access = property.GetAccessors();
                return new PropertyContract(reader.GetString(property.Name), signature.ReturnType, signature.ParameterTypes.ToArray(),
                    Token(access.Getter), Token(access.Setter));
            }).ToArray();
            var events = type.GetEvents().Select(eventHandle =>
            {
                var e = reader.GetEventDefinition(eventHandle);
                var access = e.GetAccessors();
                return new EventContract(reader.GetString(e.Name), signatures.Name(reader, e.Type), Token(access.Adder), Token(access.Remover));
            }).ToArray();
            var baseType = type.BaseType.IsNil ? null : signatures.Name(reader, type.BaseType);
            var kind = type.Attributes.HasFlag(TypeAttributes.Interface) ? "interface" : baseType switch
            {
                "System.Enum" => "enum", "System.ValueType" => "struct", "System.MulticastDelegate" => "delegate", _ => "class"
            };
            var interfaces = type.GetInterfaceImplementations().Select(i => signatures.Name(reader, reader.GetInterfaceImplementation(i).Interface)).ToArray();
            types.Add(new(assembly, name, ns, MetadataTokens.GetToken(handle), kind,
                (type.Attributes & TypeAttributes.VisibilityMask).ToString(), baseType, interfaces, arity,
                methods.ToArray(), fields, properties, events));
        }
        return new(assembly, definition.Version.ToString(), Path.GetFileName(path),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), types.ToArray());
    }

    private static int? Token(MethodDefinitionHandle handle) => handle.IsNil ? null : MetadataTokens.GetToken(handle);

    private static string? Constant(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil) return null;
        var value = reader.GetConstant(handle);
        var blob = reader.GetBlobReader(value.Value);
        object? decoded = value.TypeCode switch
        {
            ConstantTypeCode.Boolean => blob.ReadBoolean(), ConstantTypeCode.Char => (char)blob.ReadUInt16(),
            ConstantTypeCode.SByte => blob.ReadSByte(), ConstantTypeCode.Byte => blob.ReadByte(),
            ConstantTypeCode.Int16 => blob.ReadInt16(), ConstantTypeCode.UInt16 => blob.ReadUInt16(),
            ConstantTypeCode.Int32 => blob.ReadInt32(), ConstantTypeCode.UInt32 => blob.ReadUInt32(),
            ConstantTypeCode.Int64 => blob.ReadInt64(), ConstantTypeCode.UInt64 => blob.ReadUInt64(),
            ConstantTypeCode.Single => blob.ReadSingle(), ConstantTypeCode.Double => blob.ReadDouble(),
            ConstantTypeCode.String => blob.ReadUTF16(blob.Length), ConstantTypeCode.NullReference => null,
            _ => throw new BadImageFormatException("Constante inesperada: " + value.TypeCode)
        };
        return value.TypeCode + ":" + (decoded is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : decoded?.ToString() ?? "null");
    }
}
