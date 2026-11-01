using System.Runtime.InteropServices;
namespace Microsoft.Xna.Framework;
[StructLayout(LayoutKind.Sequential,Pack=4)]
public struct Rectangle(int x,int y,int width,int height)
{
    public int X=x,Y=y,Width=width,Height=height;
    public static Rectangle Empty=>default;
    public readonly int Left=>X;
    public readonly int Right=>X+Width;
    public readonly int Top=>Y;
    public readonly int Bottom=>Y+Height;
    public readonly bool IsEmpty=>X==0&&Y==0&&Width==0&&Height==0;
    public readonly bool Contains(int x,int y)=>x>=X&&x<Right&&y>=Y&&y<Bottom;
    public readonly bool Intersects(Rectangle other)=>other.Left<Right&&Left<other.Right&&other.Top<Bottom&&Top<other.Bottom;
    public void Offset(int x,int y){X+=x;Y+=y;}
    public void Inflate(int x,int y){X-=x;Y-=y;Width+=2*x;Height+=2*y;}
}
