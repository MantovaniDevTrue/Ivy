namespace Terraria.Fixture;

public class Sample
{
    static Sample() => throw new InvalidOperationException("A leitura do catálogo não deve executar esta classe.");
    public int ValueField;
    public static readonly int ReadOnlyField = 8;
    public const int Constant = 42;
    public int Value { get; set; }
    public event Action? Changed;
    public static int Overload(int value) => value;
    public static float Overload(float value) => value;
    public static void References(ref int value, out bool flag) { value++; flag = true; }
    public static void Arrays(int[] vector, int[,] matrix) { }
    public static int Optional(int value = 7, string text = "ok") => value;
    public static T Echo<T>(T value) => value;
    public void Raise() => Changed?.Invoke();
    public class Nested { public void Tick() { } }
}

public class GenericSample<T> { public T Echo(T value) => value; }
public abstract class AbstractSample { public abstract int Tick(); }
