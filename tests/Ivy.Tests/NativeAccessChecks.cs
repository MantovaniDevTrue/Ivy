using System.Runtime.InteropServices;
using Ivy.AndroidHost;
using Ivy.Runtime;
using Terraria;

internal static class NativeAccessChecks
{
    private static int checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("OK: "+name);}
    private static void Reject<T>(Action action,string name)where T:Exception{try{action();throw new Exception("Expected "+typeof(T).Name);}catch(T){Check(true,name);}}
    internal static void Run()
    {
        var lib=NativeLibrary.Load(Path.GetFullPath("native/build/libtest-il2cpp.so"));
        T Export<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib,name));
        var identity=Export<Pointer>("ivy_test_player")();var wrong=Export<Pointer>("ivy_test_item")();
        var lookup=Export<Counter>("ivy_test_lookups");var scope=true;var live=true;
        var backend=new Il2CppAccess(lib, o=>live?(IntPtr)o:throw new ObjectDisposedException("expired"),p=>p,()=>{if(!scope)throw new InvalidOperationException("scope");});
        NativeAccess.Backend=backend;
        var player=new Player(identity);
        player.statMana=-123;Check(player.statMana==-123,"generated Player scalar writes native field");
        player.dead=true;Check(player.dead,"generated bool field uses one native byte");
        player.dead=false;Check(!player.dead,"bool field false roundtrip");
        NativeMember Field(string name,string type,bool st=false,bool write=true)=>new("Assembly-CSharp","Terraria.Player",name,type,st,true,write);
        var samples=new[]{("F1","System.SByte",NativeValue.From((sbyte)-113),255UL),("F2","System.Byte",NativeValue.From((byte)231),255UL),
            ("F3","System.Int16",NativeValue.From((short)-21987),65535UL),("F4","System.UInt16",NativeValue.From((ushort)64567),65535UL),
            ("F5","System.Char",NativeValue.From('界'),65535UL),("F6","System.UInt32",NativeValue.From(uint.MaxValue-3),uint.MaxValue),
            ("F7","System.Int64",NativeValue.From(long.MinValue+7),ulong.MaxValue),("F8","System.UInt64",NativeValue.From(ulong.MaxValue-9),ulong.MaxValue),
            ("F9","System.Single",NativeValue.From(-13.25f),uint.MaxValue),("F10","System.Double",NativeValue.From(1.125e120),ulong.MaxValue)};
        foreach(var (name,type,value,mask) in samples){var f=Field(name,type);backend.Write(f,identity,value);Check(backend.Read(f,identity).Bits==(value.Bits&mask),"native scalar width "+type);}
        var global=Field("global","System.Int32",true);backend.Write(global,null,NativeValue.From(49));Check(backend.Read(global,null).Bits==49,"static field initialization/read/write");
        var before=lookup();backend.Read(global,null);backend.Read(global,null);Check(lookup()==before,"field metadata cached after first use");
        Reject<MissingMemberException>(()=>backend.Read(Field("dead","System.Int32"),identity),"field type mismatch rejected");
        Reject<MissingMemberException>(()=>backend.Read(Field("global","System.Int32"),identity),"field static mismatch rejected");
        Reject<MissingMemberException>(()=>backend.Write(Field("readonly","System.Int32"),identity,default),"readonly flag mismatch rejected");
        NativeMember Method(string name,string result,bool st=false,params string[] args)=>new("Assembly-CSharp","Terraria.Player",name,result,st,false,false,args);
        var ints=Method("Overload","System.Int32",false,"System.Int32","System.Int32");var boolean=Method("Overload","System.Int32",false,"System.Int32","System.Boolean");
        Check(unchecked((int)backend.Invoke(ints,identity,[NativeValue.From(-9),NativeValue.From(5)]).Bits)==-4,"exact overload int,int and signed result");
        Check(backend.Invoke(boolean,identity,[NativeValue.From(7),NativeValue.From(true)]).Bits==107,"same-arity overload int,bool");
        before=lookup();backend.Invoke(ints,identity,[default,default]);Check(lookup()==before,"method resolution cached by descriptor");
        Reject<MissingMemberException>(()=>backend.Invoke(Method("Overload","System.Boolean",false,"System.Int32","System.Int32"),identity,[default,default]),"method return type mismatch rejected");
        Reject<ArgumentException>(()=>backend.Invoke(ints,identity,[default]),"argument count mismatch rejected before invocation");
        Check(backend.Invoke(Method("IsZero","System.Boolean",false,"System.Int32"),identity,[NativeValue.From(0)]).Bits==1,"boxed native bool return decoded");
        Reject<InvalidOperationException>(()=>backend.Invoke(Method("Throw","System.Void"),identity,[]),"native exception reported to managed caller");
        Reject<InvalidOperationException>(()=>backend.Invoke(Method("EmptyResult","System.Int32"),identity,[]),"missing scalar box rejected");
        var result=backend.Invoke(Method("GetPlayer","Terraria.Player",true),null,[]);Check(Equals(result.Reference,identity),"class result returns protected opaque identity");
        var accept=Method("AcceptPlayer","System.Boolean",true,"Terraria.Player");
        Check(backend.Invoke(accept,null,[result]).Bits==1,"reference argument uses object pointer rather than pointer-to-pointer");
        Check(backend.Invoke(accept,null,[default]).Bits==0,"null reference argument supported");
        Reject<InvalidOperationException>(()=>backend.Invoke(accept,null,[new(0,wrong)]),"wrong reference argument class rejected");
        var reference=Field("reference","Terraria.Player",true);backend.Write(reference,null,result);Check(Equals(backend.Read(reference,null).Reference,identity),"reference field roundtrip via IL2CPP field API");
        backend.Write(reference,null,default);Check(backend.Read(reference,null).Reference is null,"reference field null roundtrip");
        Reject<InvalidOperationException>(()=>backend.Read(Field("statMana","System.Int32"),wrong),"wrong instance class rejected");
        var warm=player.statMana;
        var allocated=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<10000;i++)warm=player.statMana;
        var delta=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(warm==-123 && delta==0,"10000 warmed scalar reads allocate zero managed bytes in local fake-native test");
        var vectorField=Field("position","Microsoft.Xna.Framework.Vector2");
        var vector=new Microsoft.Xna.Framework.Vector2(12.25f,-9.5f);
        backend.Write(vectorField,identity,NativeValue.From(vector));var vectorRead=backend.Read(vectorField,identity).Vector();
        Check(vectorRead.X==vector.X&&vectorRead.Y==vector.Y,"Vector2 layout checked and native field roundtrip");
        var rectangle=new Microsoft.Xna.Framework.Rectangle(-101,202,303,404);
        var rf=Field("rectangle","Microsoft.Xna.Framework.Rectangle");
        backend.Write(rf,identity,NativeValue.From(rectangle));var rr=backend.Read(rf,identity).Rectangle();
        Check(rr.X==-101&&rr.Y==202&&rr.Width==303&&rr.Height==404,"16-byte Rectangle preserves all signed components");
        var color=new Microsoft.Xna.Framework.Color(11,37,83,129);
        var cf=Field("color","Microsoft.Xna.Framework.Graphics.Color");
        backend.Write(cf,identity,NativeValue.From(color));var cr=backend.Read(cf,identity).Color();
        Check(cr.R==11&&cr.G==37&&cr.B==83&&cr.A==129,"native ABGR channels converted to public RGBA");
        Check(NativeValue.From(color).Bits==0x0b255381,"native Color bytes use verified A/B/G/R order");
        foreach(var (symbol,type,value) in new[]{("ivy_test_rectangle_array","Microsoft.Xna.Framework.Rectangle",NativeValue.From(rectangle)),("ivy_test_color_array","Microsoft.Xna.Framework.Graphics.Color",NativeValue.From(color))})
        {
            var array=Export<Pointer>(symbol)();backend.WriteElement(array,1,type,value);
            Check(backend.ReadElement(array,1,type)==value,"struct array roundtrip "+type);
            Check(backend.ReadElement(array,0,type)==default(NativeValue),"adjacent array element preserved "+type);
        }
        var intsIdentity=Export<Pointer>("ivy_test_int_array")();
        var intsArray=new NativeArray<int>(intsIdentity,"System.Int32",v=>unchecked((int)v.Bits),v=>NativeValue.From(v));
        Check(intsArray.Length==4,"native array length");intsArray[1]=-231;Check(intsArray[1]==-231,"native int array signed read/write");
        var arrayField=Field("ints","System.Int32[]",true);Check(Equals(backend.Read(arrayField,null).Reference,intsIdentity),"array field returns opaque native identity");
        Reject<IndexOutOfRangeException>(()=>{var ignored=intsArray[-1];},"negative index rejected");
        Reject<IndexOutOfRangeException>(()=>intsArray[4]=7,"upper bound rejected");
        Reject<InvalidOperationException>(()=>backend.ReadElement(intsIdentity,0,"System.Boolean"),"wrong array element type rejected");
        var refs=Export<Pointer>("ivy_test_player_array")();var barriers=Export<Counter>("ivy_test_barriers");var beforeBarrier=barriers();
        backend.WriteElement(refs,0,"Terraria.Player",new(0,identity));
        Check(Equals(backend.ReadElement(refs,0,"Terraria.Player").Reference,identity)&&barriers()==beforeBarrier+1,"reference array write uses native GC barrier");
        backend.WriteElement(refs,0,"Terraria.Player",default);Check(backend.ReadElement(refs,0,"Terraria.Player").Reference is null,"reference array null write");
        Reject<InvalidOperationException>(()=>backend.WriteElement(refs,0,"Terraria.Player",new(0,wrong)),"reference array rejects wrong class");
        Reject<NotSupportedException>(()=>backend.ArrayLength(Export<Pointer>("ivy_test_bad_array")()),"multidimensional array rejected");
        var layoutSwitch=Export<Flag>("ivy_test_wrong_layout");layoutSwitch(1);
        var fresh=new Il2CppAccess(lib,o=>(IntPtr)o,p=>p,()=>{});
        Reject<NotSupportedException>(()=>fresh.Read(vectorField,identity),"changed Vector2 layout rejected before field read");
        Reject<NotSupportedException>(()=>fresh.Read(rf,identity),"changed Rectangle layout rejected");
        Reject<NotSupportedException>(()=>fresh.Read(cf,identity),"changed Color layout rejected");layoutSwitch(0);
        scope=false;Reject<InvalidOperationException>(()=>{var ignored=intsArray.Length;},"cached native array outside callback rejected");scope=true;
        scope=false;Reject<InvalidOperationException>(()=>backend.Read(global,null),"cached static access outside gameplay rejected");scope=true;
        live=false;Reject<ObjectDisposedException>(()=>backend.Read(Field("statMana","System.Int32"),identity),"expired native identity rejected");
        Console.WriteLine($"{checks} native-access checks passed (real C# marshaller, fake IL2CPP exports).");
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Flag(int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Pointer();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Counter();
}
