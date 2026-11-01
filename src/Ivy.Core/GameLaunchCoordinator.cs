namespace Ivy.Loader;

// Preparing a service command is asynchronous; queuing it is not an acknowledgement.
public sealed class GameLaunchCoordinator
{
    private int pending;
    public bool IsPending => Volatile.Read(ref pending) != 0;
    public async Task<bool> LaunchAsync(Func<CancellationToken, Task> prepare, Action launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(launch);
        if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) return false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await prepare(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            launch();
            return true;
        }
        finally { Volatile.Write(ref pending, 0); }
    }
}
