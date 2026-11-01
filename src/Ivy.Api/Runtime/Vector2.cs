using System.Runtime.InteropServices;
namespace Microsoft.Xna.Framework;
[StructLayout(LayoutKind.Sequential,Pack=4)]
public struct Vector2(float x,float y)
{
    public float X=x,Y=y;
    public Vector2(float value):this(value,value){}
    public static Vector2 Zero=>new(0,0);
    public static Vector2 One=>new(1,1);
    public readonly float LengthSquared()=>X*X+Y*Y;
    public readonly float Length()=>MathF.Sqrt(LengthSquared());
    public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.X+b.X,a.Y+b.Y);
    public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.X-b.X,a.Y-b.Y);
    public static Vector2 operator *(Vector2 a,float scale)=>new(a.X*scale,a.Y*scale);
    public static Vector2 operator *(float scale,Vector2 a)=>a*scale;
    public static Vector2 operator /(Vector2 a,float scale)=>new(a.X/scale,a.Y/scale);
}
