using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Security.Cryptography;
using System.Text;

namespace Ivy.AndroidHost;

[Activity(Label = "Ivy", MainLauncher = true, Exported = true, Process = ":launcher", TaskAffinity = "dev.mantovani.ivy.launcher")]
public sealed class MainActivity : Activity
{
    private TextView output = null!;
    private CancellationTokenSource? refresh;
    private readonly HashSet<string> reportedExits = new();
    private string modsPath = "";
    private const int ImportRequest = 10;
    private const int ExportRequest = 11;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ProcessHost.Initialize(FilesDir!.AbsolutePath, gameProcess: false);
        modsPath = Path.Combine(FilesDir!.AbsolutePath, "mods");
        Directory.CreateDirectory(modsPath);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        var title = new TextView(this) { Text = "Ivy · 0.3.1", TextSize = 24 };
        layout.AddView(title);
        layout.AddView(new TextView(this) { Text = "Host C# + Terraria · ARM64\nTeste do hook C++ → C# no mundo", TextSize = 15 });
        AddButton(layout, "Abrir Terraria", OpenGame);
        AddButton(layout, "Carregar mods C#", () => SendCommand(GameControlService.Load));
        AddButton(layout, "Descarregar mods", () => SendCommand(GameControlService.Unload));
        AddButton(layout, "Parar hook de diagnóstico", () => SendCommand(GameControlService.StopHook));
        AddButton(layout, "Importar mod (.dll)", () =>
        {
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            StartActivityForResult(intent, ImportRequest);
        });
        AddButton(layout, "Exportar relatório", () =>
        {
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("text/plain");
            intent.PutExtra(Intent.ExtraTitle, "Ivy-relatorio.txt");
            StartActivityForResult(intent, ExportRequest);
        });
        output = new TextView(this) { TextSize = 13 };
        var scroll = new ScrollView(this);
        scroll.AddView(output);
        layout.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        SetContentView(layout);
        Log($"Android {Build.VERSION.Release} · {string.Join(", ", Build.SupportedAbis!)}");
        Log($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Log("Runtime Mono/.NET pronto. Ponte de gameplay em diagnóstico; API de conteúdo ainda pendente.");
        try
        {
            var samplePath = Path.Combine(modsPath, "ExampleMod.dll");
            using var input = Assets!.Open("ExampleMod.dll");
            using var destination = File.Create(samplePath);
            input.CopyTo(destination);
            Log("ExampleMod.dll preparado para carregamento externo.");
        }
        catch (Exception ex) { Log($"Falha ao preparar exemplo: {ex}"); }
    }

    private void AddButton(LinearLayout parent, string text, Action action)
    {
        var button = new Button(this) { Text = text };
        button.Click += (_, _) =>
        {
            try
            {
                action();
                output.Text = ProcessHost.GetReport();
            }
            catch (Exception ex) { Log(ex.ToString()); }
        };
        parent.AddView(button);
    }

    private void SendCommand(string action)
    {
        var command = new Intent(this, typeof(GameControlService));
        command.SetAction(action);
        StartService(command);
    }

    private void OpenGame()
    {
        var intent = new Intent();
        intent.SetClassName(PackageName!, "com.unity3d.player.UnityPlayerActivity");
        intent.AddFlags(ActivityFlags.NewTask);
        if (intent.ResolveActivity(PackageManager!) is null)
            throw new InvalidOperationException("Este APK contém apenas o runtime. Use o APK GameHost para abrir o Terraria.");
        SendCommand(GameControlService.Prepare);
        Log($"Abrindo Terraria em processo separado do painel PID={System.Environment.ProcessId}.");
        StartActivity(intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is null) return;
        try
        {
            if (requestCode == ExportRequest)
            {
                using var stream = ContentResolver!.OpenOutputStream(data.Data)!;
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(ProcessHost.GetReport());
                Log("Relatório exportado.");
            }
            else if (requestCode == ImportRequest)
            {
                using var stream = ContentResolver!.OpenInputStream(data.Data)!;
                using var bytes = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bytes.Length + count > 32 * 1024 * 1024)
                        throw new InvalidDataException("Limite inicial: 32 MB por DLL.");
                    bytes.Write(buffer, 0, count);
                }
                var content = bytes.ToArray();
                if (content.Length < 2 || content[0] != 'M' || content[1] != 'Z')
                    throw new InvalidDataException("Selecione uma DLL .NET compilada para esta API.");
                var hash = Convert.ToHexString(SHA256.HashData(content));
                var temporary = Path.Combine(modsPath, $"mod-{hash}.pending");
                File.WriteAllBytes(temporary, content);
                File.Move(temporary, Path.Combine(modsPath, $"mod-{hash}.dll"), overwrite: true);
                Log("DLL importada. Descarregue e carregue os mods para iniciar uma nova sessão.");
            }
        }
        catch (Exception ex) { Log(ex.ToString()); }
    }

    private void Log(string message)
    {
        ProcessHost.Write(message);
        output.Text = ProcessHost.GetReport();
    }

    protected override void OnResume()
    {
        base.OnResume();
        RecordGameExits();
        if (output is not null) output.Text = ProcessHost.GetReport();
        refresh = new CancellationTokenSource();
        _ = RefreshReport(refresh.Token);
    }

    protected override void OnPause()
    {
        refresh?.Cancel();
        refresh?.Dispose();
        refresh = null;
        base.OnPause();
    }

    private async Task RefreshReport(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(1000, token);
                output.Text = ProcessHost.GetReport();
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void RecordGameExits()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return;
        try
        {
            var manager = (ActivityManager)GetSystemService(ActivityService)!;
            foreach (var exit in manager.GetHistoricalProcessExitReasons(PackageName, 0, 8))
            {
                if (exit.ProcessName != PackageName || !reportedExits.Add($"{exit.Pid}:{exit.Timestamp}")) continue;
                ProcessHost.Write($"Saída do jogo registrada pelo Android: PID={exit.Pid}, motivo={exit.Reason}, status={exit.Status}, data={DateTimeOffset.FromUnixTimeMilliseconds(exit.Timestamp):O}, descrição={exit.Description}.");
            }
        }
        catch (Exception ex) { ProcessHost.Write($"Não foi possível consultar a saída do jogo: {ex.Message}"); }
    }
}
