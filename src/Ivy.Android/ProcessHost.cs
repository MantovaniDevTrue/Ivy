using System.Text;
using Ivy.Loader;

namespace Ivy.AndroidHost;

internal static class ProcessHost
{
    private static readonly object gate = new();
    private static readonly StringBuilder report = new();
    private static string? logPath;
    internal static ModSession? Session { get; set; }

    internal static void Initialize(string filesDirectory)
    {
        lock (gate)
        {
            if (logPath is not null) return;
            logPath = Path.Combine(filesDirectory, "ivy-session.txt");
            var previous = logPath + ".previous";
            if (File.Exists(logPath)) File.Move(logPath, previous, overwrite: true);
            Write($"Ivy 0.2.0 · C# PID={Environment.ProcessId}");
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
            var text = report.ToString();
            var previous = logPath + ".previous";
            if (File.Exists(previous))
                text += "\n--- Sessão anterior ---\n" + File.ReadAllText(previous);
            return text;
        }
    }
}
