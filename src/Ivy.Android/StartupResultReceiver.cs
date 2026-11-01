using Android.OS;

namespace Ivy.AndroidHost;

public sealed class StartupResultReceiver(Handler handler) : ResultReceiver(handler)
{
    internal TaskCompletionSource<Bundle> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override void OnReceiveResult(int resultCode, Bundle? resultData)
    {
        if (resultCode == GameControlService.Ready && resultData is not null)
            Completion.TrySetResult(resultData);
        else
            Completion.TrySetException(new InvalidOperationException(resultData?.GetString("error") ?? "O serviço do jogo não confirmou os mods."));
    }
}
