using System.Runtime.InteropServices;
using Ivy.Runtime;
namespace Ivy.AndroidHost;
internal sealed unsafe partial class Il2CppAccess
{
    private readonly Dictionary<IntPtr,(string Name,int Kind,int Size,IntPtr Class)> arrayTypes=[];
    private readonly Dictionary<IntPtr,int> structLayouts=[];
    private ClassSize valueSize=null!;private Offset offset=null!;private Header objectHeader=null!,arrayHeader=null!;
    private ArraySize length=null!;private ElementSize elementSize=null!;private ClassType classType=null!,elementClass=null!;private Rank rank=null!;private Barrier barrier=null!;
    private void InitializeComposite(IntPtr library)
    {
        T Load<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,"il2cpp_"+name));
        valueSize=Load<ClassSize>("class_value_size");offset=Load<Offset>("field_get_offset");objectHeader=Load<Header>("object_header_size");arrayHeader=Load<Header>("array_object_header_size");length=Load<ArraySize>("array_length");elementSize=Load<ElementSize>("array_element_size");classType=Load<ClassType>("class_get_type");elementClass=Load<ClassType>("class_get_element_class");rank=Load<Rank>("class_get_rank");barrier=Load<Barrier>("gc_wbarrier_set_field");
    }
    private int ValidateStruct(IntPtr type)
    {
        var klass=classFromType(type);if(structLayouts.TryGetValue(klass,out var cached))return cached;
        var name=Name(type);
        var (size,alignment,scalar,names)=name switch {
            "Microsoft.Xna.Framework.Vector2"=>(8,4u,"System.Single",new[]{"X","Y"}),
            "Microsoft.Xna.Framework.Rectangle"=>(16,4u,"System.Int32",new[]{"X","Y","Width","Height"}),
            "Microsoft.Xna.Framework.Graphics.Color"=>(4,1u,"System.Byte",new[]{"A","B","G","R"}),
            _=>throw new NotSupportedException("Layout de struct não suportado: "+name)
        };
        if(valueSize(klass,out var align)!=size||align!=alignment||!BitConverter.IsLittleEndian)throw new NotSupportedException("Tamanho/alinhamento de struct divergente: "+name);
        var stride=size/names.Length;
        for(var i=0;i<names.Length;i++)
        {
            var field=fieldFromName(klass,names[i]);
            if(field==IntPtr.Zero||Name(fieldType(field))!=scalar||offset(field)!=objectHeader()+(uint)(i*stride)||(fieldFlags(field)&0x10)!=0)throw new InvalidOperationException("Layout de struct divergente: "+name+"."+names[i]);
        }
        structLayouts.Add(klass,size);return size;
    }
    private (string Name,int Kind,int Size,IntPtr Class) ArrayType(IntPtr array)
    {
        var klass=objectClass(array);if(arrayTypes.TryGetValue(klass,out var info))return info;
        if(typeKind(classType(klass))!=29||rank(klass)!=1)throw new NotSupportedException("Somente arrays SZ de uma dimensão.");
        var element=elementClass(klass);var type=classType(element);var kind=typeKind(type);var name=Name(type);
        var structSize=kind==17?ValidateStruct(type):0;
        if(kind!=17&&kind is not (>=2 and <=13)&&kind!=18)throw new NotSupportedException("Elemento de array não suportado.");
        var size=kind==17?structSize:Size(kind);if(elementSize(klass)!=size)throw new InvalidOperationException("Tamanho do elemento divergente.");
        info=(name,kind,size,element);arrayTypes[klass]=info;return info;
    }
    public int ArrayLength(object identity){scope();var array=pointer(identity);ArrayType(array);return checked((int)length(array));}
    private (IntPtr Array,IntPtr Address,int Kind,int Size,IntPtr Class) Element(object identity,int index,string type)
    {
        scope();var array=pointer(identity);var info=ArrayType(array);
        if(info.Name!=type)throw new InvalidOperationException("Tipo do elemento divergente.");
        if(index<0||(nuint)index>=length(array))throw new IndexOutOfRangeException();
        var header=arrayHeader();if(header<objectHeader()+2*IntPtr.Size||header>128)throw new InvalidOperationException("Cabeçalho do array divergente.");
        return(array,array+checked((int)header+index*info.Size),info.Kind,info.Size,info.Class);
    }
    public NativeValue ReadElement(object identity,int index,string type)
    {
        var e=Element(identity,index,type);var bits=stackalloc ulong[2];bits[0]=bits[1]=0;Buffer.MemoryCopy((void*)e.Address,bits,16,e.Size);
        return e.Kind==18?new(0,bits[0]==0?null:wrap((IntPtr)bits[0])):new(bits[0],null,bits[1]);
    }
    public void WriteElement(object identity,int index,string type,NativeValue value)
    {
        var e=Element(identity,index,type);
        if(e.Kind==18)
        {
            var target=value.Reference is null?IntPtr.Zero:pointer(value.Reference);
            if(target!=IntPtr.Zero&&assignable(e.Class,objectClass(target))==0)throw new InvalidOperationException("Referência incompatível com array.");
            barrier(e.Array,e.Address,target);
        }
        else{if(value.Reference is not null)throw new ArgumentException("Valor escalar esperado.");var bits=stackalloc ulong[2];bits[0]=value.Bits;bits[1]=value.High;Buffer.MemoryCopy(bits,(void*)e.Address,e.Size,e.Size);}
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int ClassSize(IntPtr klass,out uint alignment);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate nuint Offset(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint Header();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate nuint ArraySize(IntPtr array);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int ElementSize(IntPtr klass);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr ClassType(IntPtr klass);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint Rank(IntPtr klass);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Barrier(IntPtr obj,IntPtr field,IntPtr value);
}
