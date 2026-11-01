namespace Ivy.Runtime;

/// <summary>Array nativo unidimensional. A referência é válida durante o callback atual.</summary>
public sealed class NativeArray<T> : NativeObject
{
    private readonly string element;
    private readonly Func<NativeValue,T> read;
    private readonly Func<T,NativeValue> write;
    internal NativeArray(object identity,string element,Func<NativeValue,T> read,Func<T,NativeValue> write):base(identity)
    {this.element=element;this.read=read;this.write=write;}
    public int Length=>NativeAccess.Backend.ArrayLength(Identity);
    public T this[int index]
    {
        get=>read(NativeAccess.Backend.ReadElement(Identity,index,element));
        set=>NativeAccess.Backend.WriteElement(Identity,index,element,write(value));
    }
}
