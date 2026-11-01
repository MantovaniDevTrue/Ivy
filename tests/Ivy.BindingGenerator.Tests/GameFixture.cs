namespace Terraria;
public class Main
{
    public static Player LocalPlayer => new();
    public static int menuMode => 10;
    public static int myPlayer => 0;
    private void DoUpdateInWorld() { }
}
public class Entity { public bool active; public float width; }
public class Player : Entity { public int statLife; public int statLifeMax2; public int statMana; public bool dead; public void HealEffect(int amount, bool broadcast = true) { }
    public readonly int ReadonlyProbe;
    public string? UnsupportedText;
    public int Overload(int number) => number;
    public int Overload(bool flag) => flag?1:0;
    public void Reference(ref int value) { }
    public int Optional(int amount=7, bool broadcast=true) => amount;
    public T Generic<T>(T value) => value;
    public Item? HeldItem { get; set; }
 }
public class Item : Entity { public int damage; public int type; }
public class NPC : Entity { public int life; }
public class Projectile : Entity { public bool friendly; }
