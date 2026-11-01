using System.Text;
using Ivy.Loader;

namespace Ivy.AndroidHost;

internal static class ProcessHost
{
    private static readonly object gate = new();
    private static readonly StringBuilder report = new();
    private static string? logPath;
    private static string? filesPath;
    private static bool isGameProcess;
    internal static ModSession? Session { get; set; }

    internal static void Initialize(string filesDirectory, bool gameProcess)
    {
        lock (gate)
        {
            if (logPath is not null) return;
            filesPath = filesDirectory;
            isGameProcess = gameProcess;
            logPath = Path.Combine(filesDirectory, gameProcess ? "ivy-session.txt" : "ivy-launcher.txt");
            var previous = logPath + ".previous";
            if (File.Exists(logPath)) File.Move(logPath, previous, overwrite: true);
            Write($"Ivy 0.4.0 · {(gameProcess ? "jogo + C#" : "painel")} PID={Environment.ProcessId}");
        }
    }

    internal static void Write(string message)
    {
        lock (gate)
        {
            var line = $"{DateTimeOffset.Now:HH:mm:ss} {message}";
            report.AppendLine(line);
            global::Android.Util.Log.Info("Ivy", message);
            if (logPath is not null)
            {
                try { File.AppendAllText(logPath, line + Environment.NewLine); }
                catch (IOException ex) { global::Android.Util.Log.Warn("Ivy", ex.Message); }
            }
        }
    }

    internal static string GetReport()
    {
        lock (gate)
        {
            var text = isGameProcess ? NativeWorldBridge.Snapshot() + "\n" + report : report.ToString();
            if (!isGameProcess && filesPath is not null)
            {
                text += ReadLog(Path.Combine(filesPath, "ivy-session.txt"), "Jogo — última sessão registrada");
                text += ReadLog(Path.Combine(filesPath, "ivy-session.txt.previous"), "Jogo — sessão anterior");
                return text;
            }
            var previous = logPath + ".previous";
            if (File.Exists(previous))
                text += "\n--- Sessão anterior ---\n" + File.ReadAllText(previous);
            return text;
        }
    }

    private static string ReadLog(string path, string title)
    {
        try { return File.Exists(path) ? $"\n--- {title} ---\n" + File.ReadAllText(path) : ""; }
        catch (IOException) { return $"\n{title}: arquivo sendo atualizado.\n"; }
    }
}
