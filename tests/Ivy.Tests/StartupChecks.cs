using Ivy.Loader;

internal static class StartupChecks
{
    private static int count;
    internal static async Task Run()
    {
        var coordinator = new GameLaunchCoordinator();
        var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launches = 0; var prepares = 0;
        var first = coordinator.LaunchAsync(_ => { prepares++; return reply.Task; }, () => launches++, CancellationToken.None);
        Check(!first.IsCompleted && coordinator.IsPending && launches == 0, "launch waits for service acknowledgement");
        var duplicate = await coordinator.LaunchAsync(_ => { prepares++; return Task.CompletedTask; }, () => launches++, CancellationToken.None);
        Check(!duplicate && prepares == 1 && launches == 0, "double tap cannot queue a second launch");
        reply.SetResult();
        Check(await first && launches == 1 && !coordinator.IsPending, "acknowledgement permits exactly one launch");
        try
        {
            await coordinator.LaunchAsync(_ => Task.FromException(new InvalidOperationException("load failure")), () => launches++, CancellationToken.None);
            throw new Exception("Expected prepare failure");
        }
        catch (InvalidOperationException) { }
        Check(launches == 1 && !coordinator.IsPending, "load error never opens the game and releases pending state");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try
        {
            await coordinator.LaunchAsync(_ => { prepares++; return Task.CompletedTask; }, () => launches++, cancelled.Token);
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException) { }
        Check(launches == 1 && prepares == 1 && !coordinator.IsPending, "already cancelled launch does not prepare or open");
        using var timeout = new CancellationTokenSource();
        var lateReply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timed = coordinator.LaunchAsync(token => lateReply.Task.WaitAsync(token), () => launches++, timeout.Token);
        timeout.Cancel();
        try { await timed; throw new Exception("Expected timeout cancellation"); } catch (OperationCanceledException) { }
        lateReply.SetResult();
        Check(launches == 1 && !coordinator.IsPending, "late acknowledgement after cancellation never opens the game");
        await coordinator.LaunchAsync(_ => Task.CompletedTask, () => launches++, CancellationToken.None);
        Check(launches == 2, "retry after cancellation works");
        try
        {
            await coordinator.LaunchAsync(_ => throw new InvalidOperationException(), () => launches++, CancellationToken.None);
            throw new Exception("Expected synchronous failure");
        }
        catch (InvalidOperationException) { }
        Check(!coordinator.IsPending && launches == 2, "synchronous prepare failure releases gate");
        try
        {
            await coordinator.LaunchAsync(_ => Task.CompletedTask, () => throw new InvalidOperationException(), CancellationToken.None);
            throw new Exception("Expected launch failure");
        }
        catch (InvalidOperationException) { }
        Check(!coordinator.IsPending, "activity launch failure releases gate");
        Console.WriteLine($"{count} automatic startup checks passed.");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        count++; Console.WriteLine("OK: " + message);
    }
}
