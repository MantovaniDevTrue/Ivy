using System.Runtime.InteropServices;
namespace Ivy.AndroidHost;
internal static class NativeCrashTrace
{
    internal static void Configure(string directory)
    {
        var result=ConfigureNative(Path.Combine(directory,"ivy-native-trace.txt"));
        ProcessHost.Write($"Diagnóstico nativo preparado: status={result}. Observador será armado na primeira atualização de mundo.");
    }
    internal static void Arm()=>ProcessHost.Write($"Observador de sinais nativos: status={ArmNative()}; tratamento original preservado.");
    [DllImport("ivy-native",EntryPoint="ivy_crash_trace_configure",CallingConvention=CallingConvention.Cdecl)]
    private static extern int ConfigureNative([MarshalAs(UnmanagedType.LPUTF8Str)]string path);
    [DllImport("ivy-native",EntryPoint="ivy_crash_trace_arm",CallingConvention=CallingConvention.Cdecl)]
    private static extern int ArmNative();
}
