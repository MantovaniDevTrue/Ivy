using System.Runtime.InteropServices;
namespace Microsoft.Xna.Framework;
// Public channels follow the PC API; native Android A/B/G/R bytes are converted by NativeValue.
[StructLayout(LayoutKind.Sequential,Pack=1)]
public struct Color(byte r,byte g,byte b,byte a)
{
    public byte R=r,G=g,B=b,A=a;
    public Color(byte r,byte g,byte b):this(r,g,b,(byte)255){}
    public Color(int r,int g,int b,int a=255):this((byte)Math.Clamp(r,0,255),(byte)Math.Clamp(g,0,255),(byte)Math.Clamp(b,0,255),(byte)Math.Clamp(a,0,255)){}
    public uint PackedValue {readonly get=>(uint)(R|(G<<8)|(B<<16)|(A<<24));set{R=(byte)value;G=(byte)(value>>8);B=(byte)(value>>16);A=(byte)(value>>24);}}
    public static Color Transparent=>new(0,0,0,0);
    public static Color White=>new(255,255,255,255);
    public static Color Black=>new(0,0,0,255);
}
