using Android.App;
using Android.Content;
using Android.OS;
using Ivy.Loader;
using Android.Runtime;

namespace Ivy.AndroidHost;

// The default app process owns Unity, Mono, mods and native callbacks together.
// Only the panel lives in :launcher. Commands are explicit and not exported.
[Service(Exported = false)]
public sealed class GameControlService : Service
{
    internal const string ReplyKey = "ivy.prepare.reply";
    internal const int Ready = 1;
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
        ResultReceiver? reply = null;
        try
        {
#pragma warning disable CS0618, CA1422 // Legacy ResultReceiver parceling also supports API 24-32.
            reply = intent?.GetParcelableExtra(ReplyKey)?.JavaCast<ResultReceiver>();
#pragma warning restore CS0618, CA1422
            switch (intent?.Action)
            {
                case Prepare:
                    if (ProcessHost.Session is null) LoadMods();
                    if (ProcessHost.LoadFailures != 0)
                        throw new InvalidOperationException($"{ProcessHost.LoadFailures} mod(s) falharam ao carregar. Descarregue e corrija os mods antes de abrir.");
                    if (ProcessHost.Session!.ModCount == 0)
                        throw new InvalidOperationException("Nenhum mod foi carregado. Confira os erros do carregamento antes de abrir o jogo.");
                    Il2CppProbe.Start();
                    var result = new Bundle();
                    result.PutInt("mods", ProcessHost.Session.ModCount);
                    result.PutInt("systems", ProcessHost.Session.SystemCount);
                    result.PutInt("pid", System.Environment.ProcessId);
                    ProcessHost.Write("Preparação automática concluída; confirmando mods ao painel antes da abertura.");
                    reply?.Send((Result)Ready, result);
                    break;
                case Load:
                    LoadMods();
                    break;
                case Unload:
                    ProcessHost.Session?.Dispose();
                    ProcessHost.Session = null;
                    ProcessHost.LoadFailures = 0;
                    ProcessHost.Write("Mods descarregados no processo do jogo.");
                    break;
                case StopHook:
                    NativeWorldBridge.Stop();
                    ProcessHost.Write(NativeWorldBridge.Snapshot().TrimEnd());
                    break;
            }
        }
        catch (Exception ex)
        {
            ProcessHost.Write($"Comando do jogo falhou: {ex}");
            var result = new Bundle();
            result.PutString("error", ex.Message);
            reply?.Send(Result.Canceled, result);
        }
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
        ProcessHost.LoadFailures = 0;
        var session = new ModSession(ProcessHost.Write);
        ProcessHost.Session = session;
        foreach (var path in Directory.GetFiles(modsPath, "*.dll").Order(StringComparer.Ordinal))
        {
            try { session.LoadFile(path); }
            catch (Exception ex) { ProcessHost.LoadFailures++; ProcessHost.Write($"Erro em {Path.GetFileName(path)}: {ex}"); }
        }
        ProcessHost.Write($"Resultado: {session.ModCount} mod(s), {session.SystemCount} sistema(s). On_Main e SDK Main/Player prontos; demais APIs pendentes.");
    }
}
