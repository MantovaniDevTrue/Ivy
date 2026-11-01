namespace Ivy.Runtime;

// Opaque identities are owned by the host; mods never receive IL2CPP pointers.
public abstract class NativeObject
{
    internal object Identity { get; }
    internal NativeObject(object identity) => Identity = identity;
}

internal sealed record NativeMember(string Assembly, string Type, string Name, string ValueType,
    bool Static, bool Field, bool Writable, params string[] Parameters);

internal readonly record struct NativeValue(ulong Bits, object? Reference = null, ulong High = 0)
{
    internal static NativeValue From(bool v) => new(v ? 1UL : 0UL);
    internal static NativeValue From(byte v) => new(v);
    internal static NativeValue From(sbyte v) => new(unchecked((ulong)v));
    internal static NativeValue From(short v) => new(unchecked((ulong)v));
    internal static NativeValue From(ushort v) => new(v);
    internal static NativeValue From(char v) => new(v);
    internal static NativeValue From(int v) => new(unchecked((ulong)v));
    internal static NativeValue From(uint v) => new(v);
    internal static NativeValue From(long v) => new(unchecked((ulong)v));
    internal static NativeValue From(ulong v) => new(v);
    internal static NativeValue From(float v) => new(BitConverter.SingleToUInt32Bits(v));
    internal static NativeValue From(double v) => new(BitConverter.DoubleToUInt64Bits(v));
    internal static NativeValue From(Microsoft.Xna.Framework.Vector2 v)=>new((ulong)BitConverter.SingleToUInt32Bits(v.X)|((ulong)BitConverter.SingleToUInt32Bits(v.Y)<<32));
    internal Microsoft.Xna.Framework.Vector2 Vector()=>new(BitConverter.UInt32BitsToSingle((uint)Bits),BitConverter.UInt32BitsToSingle((uint)(Bits>>32)));
    internal static NativeValue From(Microsoft.Xna.Framework.Rectangle v)=>new((uint)v.X|((ulong)(uint)v.Y<<32),null,(uint)v.Width|((ulong)(uint)v.Height<<32));
    internal Microsoft.Xna.Framework.Rectangle Rectangle()=>new(unchecked((int)Bits),unchecked((int)(Bits>>32)),unchecked((int)High),unchecked((int)(High>>32)));
    internal static NativeValue From(Microsoft.Xna.Framework.Color v)=>new((uint)(v.A|(v.B<<8)|(v.G<<16)|(v.R<<24)));
    internal Microsoft.Xna.Framework.Color Color()=>new((byte)(Bits>>24),(byte)(Bits>>16),(byte)(Bits>>8),(byte)Bits);
    internal static NativeValue From(NativeObject? v) => new(0, v?.Identity);
}

internal interface INativeAccessBackend
{
    int ArrayLength(object identity)=>throw new NotSupportedException("Array backend unavailable.");
    NativeValue ReadElement(object identity,int index,string type)=>throw new NotSupportedException("Array backend unavailable.");
    void WriteElement(object identity,int index,string type,NativeValue value)=>throw new NotSupportedException("Array backend unavailable.");
    NativeValue Read(NativeMember member, object? instance);
    void Write(NativeMember member, object? instance, NativeValue value);
    NativeValue Invoke(NativeMember member, object? instance, ReadOnlySpan<NativeValue> arguments);
}

internal static class NativeAccess
{
    internal static INativeAccessBackend Backend { get; set; } = new NotReady();
    private sealed class NotReady : INativeAccessBackend
    {
        private static Exception Error() => new InvalidOperationException("Acesso gerado indisponível antes da inicialização IL2CPP ou fora do hook.");
        public NativeValue Read(NativeMember m, object? i) => throw Error();
        public void Write(NativeMember m, object? i, NativeValue v) => throw Error();
        public NativeValue Invoke(NativeMember m, object? i, ReadOnlySpan<NativeValue> a) => throw Error();
    }
}
