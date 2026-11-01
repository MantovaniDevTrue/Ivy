using Android.App;
using Android.Content;
using Android.OS;
using Ivy.Loader;

namespace Ivy.AndroidHost;

// The default app process owns Unity, Mono, mods and native callbacks together.
// Only the panel lives in :launcher. Commands are explicit and not exported.
[Service(Exported = false)]
public sealed class GameControlService : Service
{
    internal const string Prepare = "ivy.prepare";
    internal const string Load = "ivy.load";
    internal const string Unload = "ivy.unload";
    internal const string StopHook = "ivy.stop-hook";

    public override void OnCreate()
    {
        base.OnCreate();
        ProcessHost.Initialize(FilesDir!.AbsolutePath, gameProcess: true);
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            switch (intent?.Action)
            {
                case Prepare:
                    if (ProcessHost.Session is null) LoadMods();
                    Il2CppProbe.Start();
                    break;
                case Load:
                    LoadMods();
                    break;
                case Unload:
                    ProcessHost.Session?.Dispose();
                    ProcessHost.Session = null;
                    ProcessHost.Write("Mods descarregados no processo do jogo.");
                    break;
                case StopHook:
                    NativeWorldBridge.Stop();
                    ProcessHost.Write(NativeWorldBridge.Snapshot().TrimEnd());
                    break;
            }
        }
        catch (Exception ex) { ProcessHost.Write($"Comando do jogo falhou: {ex}"); }
        finally { StopSelf(startId); }
        // A normal Unity quit must not cause Android to restart the game service.
        return StartCommandResult.NotSticky;
    }

    private void LoadMods()
    {
        if (ProcessHost.Session is not null)
        {
            ProcessHost.Write("Já existe uma sessão. Descarregue antes de carregar novamente.");
            return;
        }
        var modsPath = Path.Combine(FilesDir!.AbsolutePath, "mods");
        Directory.CreateDirectory(modsPath);
        var session = new ModSession(ProcessHost.Write);
        ProcessHost.Session = session;
        foreach (var path in Directory.GetFiles(modsPath, "*.dll").Order(StringComparer.Ordinal))
        {
            try { session.LoadFile(path); }
            catch (Exception ex) { ProcessHost.Write($"Erro em {Path.GetFileName(path)}: {ex}"); }
        }
        ProcessHost.Write($"Resultado: {session.ModCount} mod(s), {session.SystemCount} sistema(s). On_Main e SDK Main/Player prontos; demais APIs pendentes.");
    }
}
