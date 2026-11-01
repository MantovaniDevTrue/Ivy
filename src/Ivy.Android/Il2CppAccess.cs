using System.Runtime.InteropServices;
using Ivy.Runtime;

namespace Ivy.AndroidHost;

// Uses the IL2CPP C API rather than guessing each generated method's machine ABI.
// All metadata lookup happens on first use and is cached by the SDK descriptor.
internal sealed unsafe partial class Il2CppAccess
    : INativeAccessBackend
{
    private sealed record Resolved(IntPtr Member, IntPtr Class, int Kind, IntPtr ReferenceClass, bool Static);
    private readonly Dictionary<NativeMember, Resolved> cache = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IntPtr, Dictionary<string, List<IntPtr>>> methodIndexes = new();
    private readonly Dictionary<string, IntPtr> images = new(StringComparer.Ordinal);
    private readonly Func<object, IntPtr> pointer;
    private readonly Func<IntPtr, object> wrap;
    private readonly Action scope;
    private readonly ClassFromName classFromName;
    private readonly ClassMethods classMethods;
    private readonly MethodName methodName;
    private readonly MethodReturn methodReturn;
    private readonly MethodType methodParam;
    private readonly MethodCount methodCount;
    private readonly MethodFlags methodFlags;
    private readonly TypeName typeName;
    private readonly Free free;
    private readonly TypeKind typeKind;
    private readonly IsGeneric isGeneric;
    private readonly IsByRef isByRef;
    private readonly ClassFromType classFromType;
    private readonly FieldFromName fieldFromName;
    private readonly FieldType fieldType;
    private readonly FieldFlags fieldFlags;
    private readonly FieldValue fieldRead, fieldWrite;
    private readonly StaticFieldValue staticRead, staticWrite;
    private readonly RuntimeInvoke runtimeInvoke;
    private readonly ObjectUnbox unbox;
    private readonly ObjectClass objectClass;
    private readonly Assignable assignable;
    private readonly ClassInit classInit;
    private readonly ClassName className;
    internal Il2CppAccess(IntPtr library, Func<object, IntPtr> pointer, Func<IntPtr, object> wrap, Action scope)
    {
        InitializeComposite(library);
        this.pointer=pointer; this.wrap=wrap; this.scope=scope;
        T Load<T>(string name) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,"il2cpp_"+name));
        classFromName=Load<ClassFromName>("class_from_name");classMethods=Load<ClassMethods>("class_get_methods");
        methodName=Load<MethodName>("method_get_name");methodReturn=Load<MethodReturn>("method_get_return_type");
        methodParam=Load<MethodType>("method_get_param");methodCount=Load<MethodCount>("method_get_param_count");
        methodFlags=Load<MethodFlags>("method_get_flags");typeName=Load<TypeName>("type_get_name");free=Load<Free>("free");
        isGeneric=Load<IsGeneric>("method_is_generic");isByRef=Load<IsByRef>("type_is_byref");
        typeKind=Load<TypeKind>("type_get_type");classFromType=Load<ClassFromType>("class_from_type");
        fieldFromName=Load<FieldFromName>("class_get_field_from_name");fieldType=Load<FieldType>("field_get_type");fieldFlags=Load<FieldFlags>("field_get_flags");
        fieldRead=Load<FieldValue>("field_get_value");fieldWrite=Load<FieldValue>("field_set_value");
        staticRead=Load<StaticFieldValue>("field_static_get_value");staticWrite=Load<StaticFieldValue>("field_static_set_value");
        runtimeInvoke=Load<RuntimeInvoke>("runtime_invoke");unbox=Load<ObjectUnbox>("object_unbox");
        objectClass=Load<ObjectClass>("object_get_class");assignable=Load<Assignable>("class_is_assignable_from");
        classInit=Load<ClassInit>("runtime_class_init");className=Load<ClassName>("class_get_name");
        var domain=Load<Domain>("domain_get")();var assemblies=Load<Assemblies>("domain_get_assemblies")(domain,out var count);
        if(count>4096||assemblies==IntPtr.Zero)throw new InvalidOperationException("Lista de assemblies IL2CPP inválida.");
        var image=Load<Image>("assembly_get_image");var imageName=Load<ImageName>("image_get_name");
        for(nuint i=0;i<count;i++)
        {
            var img=image(Marshal.ReadIntPtr(assemblies,checked((int)i*IntPtr.Size)));
            var name=Marshal.PtrToStringUTF8(imageName(img));if(name is not null) images[Path.GetFileNameWithoutExtension(name)]=img;
        }
    }
    private string Name(IntPtr type)
    {
        var text=typeName(type);if(text==IntPtr.Zero)throw new InvalidOperationException("Tipo IL2CPP sem nome.");
        try{return Marshal.PtrToStringUTF8(text)!;}finally{free(text);}
    }
    private Resolved Resolve(NativeMember m, bool requireScope = true)
    {
        if(requireScope) scope(); // before touching metadata, including cached members
        if(cache.TryGetValue(m,out var found))return found;
        if(!images.TryGetValue(m.Assembly,out var image))throw new MissingMemberException("Assembly IL2CPP ausente: "+m.Assembly);
        var split=m.Type.LastIndexOf('.');var klass=classFromName(image,m.Type[..split],m.Type[(split+1)..]);
        if(klass==IntPtr.Zero)throw new MissingMemberException(m.Type);
        IntPtr member=IntPtr.Zero, valueType=IntPtr.Zero;
        if(m.Field)
        {
            member=fieldFromName(klass,m.Name);
            if(member!=IntPtr.Zero)
            {
                valueType=fieldType(member);var flags=fieldFlags(member);
                if(Name(valueType)!=m.ValueType||((flags&0x10)!=0)!=m.Static||m.Writable&&(flags&0x60)!=0)member=IntPtr.Zero;
            }
        }
        else
        {
            if(!methodIndexes.TryGetValue(klass,out var index))
            {
                index=new(StringComparer.Ordinal);IntPtr iterator=IntPtr.Zero;int visited=0;
                for(var method=classMethods(klass,ref iterator);method!=IntPtr.Zero;method=classMethods(klass,ref iterator))
                {
                    if(++visited>100000)throw new InvalidOperationException("Enumeração de métodos IL2CPP excedeu o limite.");
                    var name=Marshal.PtrToStringUTF8(methodName(method))??throw new InvalidOperationException("Método sem nome.");
                    if(!index.TryGetValue(name,out var list))index[name]=list=[];list.Add(method);
                }
                methodIndexes.Add(klass,index);
            }
            int matches=0;
            foreach(var method in index.GetValueOrDefault(m.Name)??[])
            {
                var flags=methodFlags(method,out var impl);
                if(isGeneric(method)!=0 || (flags&0x2400)!=0 || (impl&0x1000)!=0 || methodCount(method)!=m.Parameters.Length || ((flags&0x10)!=0)!=m.Static)continue;
                var result=methodReturn(method);if(isByRef(result)!=0||Name(result)!=m.ValueType)continue;
                bool match=true;for(uint i=0;i<m.Parameters.Length;i++)if(isByRef(methodParam(method,i))!=0||Name(methodParam(method,i))!=m.Parameters[i]){match=false;break;}
                if(!match)continue;member=method;valueType=result;matches++;
            }
            if(matches!=1)member=IntPtr.Zero;
        }
        if(member==IntPtr.Zero)throw new MissingMemberException($"Assinatura IL2CPP divergente: {m.Type}.{m.Name}.");
        var kind=typeKind(valueType);var reference=kind is 18 or 29?classFromType(valueType):IntPtr.Zero;
        if(kind==17)ValidateVector(valueType);
        if(kind!=1&&kind is not (>=2 and <=13)&&kind is not (17 or 18 or 29))throw new NotSupportedException("Tipo IL2CPP não suportado neste perfil.");
        found=new(member,klass,kind,reference,m.Static);cache.Add(m,found);return found;
    }
    private IntPtr Instance(Resolved r,object? identity)
    {
        if(r.Static){classInit(r.Class);return IntPtr.Zero;}
        if(identity is null)throw new ArgumentNullException(nameof(identity));
        var target=pointer(identity);
        if(assignable(r.Class,objectClass(target))==0)throw new InvalidOperationException("Instância não corresponde ao tipo declarado.");
        return target;
    }
    private static int Size(int kind)=>kind switch{2 or 4 or 5=>1,3 or 6 or 7=>2,8 or 9 or 12=>4,10 or 11 or 13 or 18=>8,_=>0};
    private NativeValue Result(ulong bits, Resolved r)=>r.Kind is 18 or 29?new(0,bits==0?null:wrap((IntPtr)bits)):new(bits);
    private ulong Input(NativeValue value,string type)
    {
        if(!type.StartsWith("System.",StringComparison.Ordinal)||type.EndsWith("[]",StringComparison.Ordinal))
        {
            if(value.Reference is null)return 0;
            return unchecked((ulong)pointer(value.Reference));
        }
        if(value.Reference is not null)throw new InvalidOperationException("Referência recebida em parâmetro escalar.");
        return value.Bits;
    }
    public NativeValue Read(NativeMember member,object? identity)
    {
        var r=Resolve(member);var target=Instance(r,identity);ulong bits=0;
        if(r.Static)staticRead(r.Member,&bits);else fieldRead(target,r.Member,&bits);
        return Result(bits,r);
    }
    public void Write(NativeMember member,object? identity,NativeValue value)
    {
        if(!member.Writable)throw new InvalidOperationException("Campo readonly.");
        var r=Resolve(member);var target=Instance(r,identity);var bits=r.Kind==17?value.Bits:Input(value,member.ValueType);
        if(r.ReferenceClass!=IntPtr.Zero&&bits!=0&&assignable(r.ReferenceClass,objectClass((IntPtr)bits))==0)throw new InvalidOperationException("Tipo da referência incompatível com o campo.");
        // field_set_value performs the IL2CPP write barrier for reference fields.
        if(r.Static)staticWrite(r.Member,&bits);else fieldWrite(target,r.Member,&bits);
    }
    public NativeValue Invoke(NativeMember member,object? identity,ReadOnlySpan<NativeValue> arguments)
    {
        if(arguments.Length!=member.Parameters.Length||arguments.Length>8)throw new ArgumentException("Número de argumentos incompatível.");
        var r=Resolve(member);var target=Instance(r,identity);
        var values=stackalloc ulong[8];var argv=stackalloc IntPtr[8];
        for(var i=0;i<arguments.Length;i++)
        {
            values[i]=Input(arguments[i],member.Parameters[i]);
            var reference=!member.Parameters[i].StartsWith("System.",StringComparison.Ordinal);
            if(reference&&values[i]!=0&&assignable(classFromType(methodParam(r.Member,(uint)i)),objectClass((IntPtr)values[i]))==0)throw new InvalidOperationException("Tipo do argumento IL2CPP incompatível.");
            argv[i]=reference?(IntPtr)values[i]:(IntPtr)(values+i);
        }
        var result=runtimeInvoke(r.Member,target,argv,out var exception);
        if(exception!=IntPtr.Zero)throw new InvalidOperationException("Exceção IL2CPP em "+member.Type+"."+member.Name+": "+Marshal.PtrToStringUTF8(className(objectClass(exception))));
        if(r.Kind==1)return default;
        if(r.Kind==18)return new(0,result==IntPtr.Zero?null:wrap(result));
        if(result==IntPtr.Zero)throw new InvalidOperationException("Resultado escalar IL2CPP ausente.");
        var data=unbox(result);if(data==IntPtr.Zero)throw new InvalidOperationException("Resultado IL2CPP sem payload.");
        ulong bits=0;Buffer.MemoryCopy((void*)data,&bits,8,Size(r.Kind));return new(bits);
    }
    internal IntPtr ResolveMethodPointer(NativeMember member)
    {
        var r=Resolve(member, false);if(member.Field)throw new ArgumentException("Método esperado.");
        var result=Marshal.ReadIntPtr(r.Member);if(result==IntPtr.Zero)throw new MissingMethodException(member.Name);return result;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Domain();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Assemblies(IntPtr domain,out nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Image(IntPtr assembly);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ImageName(IntPtr image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromName(IntPtr image,[MarshalAs(UnmanagedType.LPUTF8Str)]string ns,[MarshalAs(UnmanagedType.LPUTF8Str)]string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassMethods(IntPtr klass,ref IntPtr iterator);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodName(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodReturn(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr MethodType(IntPtr method,uint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint MethodCount(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint MethodFlags(IntPtr method,out uint impl);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr TypeName(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Free(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte IsGeneric(IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte IsByRef(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TypeKind(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassFromType(IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr FieldFromName(IntPtr klass,[MarshalAs(UnmanagedType.LPUTF8Str)]string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr FieldType(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint FieldFlags(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FieldValue(IntPtr instance,IntPtr field,void* value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void StaticFieldValue(IntPtr field,void* value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr RuntimeInvoke(IntPtr method,IntPtr instance,IntPtr* args,out IntPtr exception);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ObjectUnbox(IntPtr instance);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ObjectClass(IntPtr instance);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte Assignable(IntPtr parent,IntPtr child);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClassInit(IntPtr klass);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassName(IntPtr klass);
}
