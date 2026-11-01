using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Ivy.Loader;
using Ivy.Runtime;

namespace Ivy.AndroidHost;

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
#pragma warning disable CS0618, CA1422 // Framework ResultReceiver parceling, including API 24-32.
            reply = intent?.GetParcelableExtra(ReplyKey)?.JavaCast<ResultReceiver>();
#pragma warning restore CS0618, CA1422
            var library = new ModLibrary(FilesDir!.AbsolutePath);
            switch (intent?.Action)
            {
                case Prepare:
                case Load:
                    var selection = library.Selection();
                    if (ProcessHost.Session is not null && ProcessHost.SelectionFingerprint != selection.Fingerprint)
                        throw new InvalidOperationException("A seleção mudou. Descarregue a sessão antes de carregar a nova seleção.");
                    if (ProcessHost.Session is null)
                    {
                        ProcessHost.Session = ModLibrary.LoadSelection(selection, ProcessHost.Write);
                        ProcessHost.SelectionFingerprint = selection.Fingerprint;
                    }
                    // Empty selection is a valid session. No metadata scan or C# callback is needed for vanilla.
                    NativeWorldBridge.RequestDispatch(WorldHooks.HasHandlers);
                    if (WorldHooks.HasHandlers) Il2CppProbe.Start();
                    else ProcessHost.Write("Nenhum handler On_Main registrado: callbacks da ponte desativados; nenhuma nova sonda IL2CPP solicitada.");
                    ProcessHost.Write($"Seleção preparada: {ProcessHost.Session.ModCount} mod(s), {ProcessHost.Session.SystemCount} sistema(s); fingerprint={selection.Fingerprint}.");
                    if (library.Selection().Fingerprint != selection.Fingerprint)
                        throw new InvalidOperationException("A seleção mudou durante a preparação. Descarregue e tente novamente.");
                    break;
                case Unload:
                    ProcessHost.Session?.Dispose();
                    ProcessHost.Session = null;
                    ProcessHost.SelectionFingerprint = null;
                    NativeWorldBridge.RequestDispatch(false);
                    ProcessHost.Write("Sessão descarregada. Os mods instalados foram preservados.");
                    break;
                case StopHook:
                    NativeWorldBridge.Stop();
                    ProcessHost.Write(NativeWorldBridge.Snapshot().TrimEnd());
                    break;
                default: throw new InvalidOperationException("Comando do jogo desconhecido.");
            }
            var result = new Bundle();
            result.PutInt("mods", ProcessHost.Session?.ModCount ?? 0);
            result.PutInt("systems", ProcessHost.Session?.SystemCount ?? 0);
            result.PutInt("pid", System.Environment.ProcessId);
            result.PutString("selection", ProcessHost.SelectionFingerprint);
            reply?.Send((Result)Ready, result);
        }
        catch (Exception ex)
        {
            ProcessHost.Write($"Comando do jogo falhou: {ex}");
            var result = new Bundle();
            result.PutString("error", ex.Message);
            reply?.Send(Result.Canceled, result);
        }
        finally { StopSelf(startId); }
        return StartCommandResult.NotSticky;
    }
}
