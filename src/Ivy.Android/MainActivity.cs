using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Text;
using Ivy.Loader;

namespace Ivy.AndroidHost;

[Activity(Label = "Ivy", MainLauncher = true, Exported = true, Process = ":launcher", TaskAffinity = "${ivyPanelAffinity}")]
public sealed class MainActivity : Activity
{
    private TextView output = null!;
    private ModLibrary library = null!;
    private Task libraryReady = Task.CompletedTask;
    private TextView counts = null!;
    private int libraryRefreshVersion;
    private CancellationTokenSource? launchCancellation;
    private readonly GameLaunchCoordinator launchCoordinator = new();
    private readonly HashSet<string> reportedExits = new();

    private const int ImportRequest = 10;
    private const int ExportRequest = 11;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ProcessHost.Initialize(FilesDir!.AbsolutePath, gameProcess: false);
        library = new ModLibrary(FilesDir!.AbsolutePath);
        libraryReady = Task.Run(library.Initialize);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        var title = new TextView(this) { Text = "Ivy · 0.6.1", TextSize = 24 };
        layout.AddView(title);
        layout.AddView(new TextView(this) { Text = "Host C# + Terraria · ARM64\nSeleção de mods · modo sem mods disponível", TextSize = 15 });
        counts = new TextView(this) { Text = "Lendo biblioteca de mods…", TextSize = 15 };
        layout.AddView(counts);
        AddButton(layout, "Jogar", OpenGame);
        AddButton(layout, "Meus mods", () => StartActivity(new Intent(this, typeof(ModsActivity))));
        AddButton(layout, "Preparar seleção", () => SendCommand(GameControlService.Load));
        AddButton(layout, "Descarregar sessão", () => SendCommand(GameControlService.Unload));
        if (Assets!.List("")!.Contains("ExampleMod.dll"))
        {
            AddButton(layout, "Instalar exemplo (opcional)", InstallExample);
            AddButton(layout, "Parar ponte On_Main (desenvolvimento)", () => SendCommand(GameControlService.StopHook));
        }
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
        Log("Runtime Mono/.NET pronto. SDK gerado: 6 classes; On_Main e On_Player.HealEffect. Conteúdo ainda pendente.");
        _ = RefreshLibrary();
    }

    private async Task RefreshLibrary()
    {
        var version = ++libraryRefreshVersion;
        try
        {
            await libraryReady;
            var list = await Task.Run(library.List);
            if (version != libraryRefreshVersion) return;
            counts.Text = $"{list.Count} instalado(s) · {list.Count(m => m.Enabled)} selecionado(s) · {list.Count(m => m.Problem is not null)} incompatível(is)";
        }
        catch (Exception ex) { counts.Text = "Biblioteca indisponível"; Log(ex.Message); }
    }

    private async void InstallExample()
    {
        try
        {
            await libraryReady;
            var list = await Task.Run(library.List);
            if (list.Any(m => m.Name.Equals("ExampleMod", StringComparison.OrdinalIgnoreCase)))
            {
                Log("Já existe ExampleMod instalado; sua DLL foi preservada. Ative-o em Meus mods se desejar.");
                return;
            }
            using var input = Assets!.Open("ExampleMod.dll");
            using var bytes = new MemoryStream();
            await input.CopyToAsync(bytes);
            var installed = await Task.Run(() => library.Install(bytes.ToArray(), "development", replaceExisting: false));
            Log($"{installed.Name} disponível na biblioteca. A seleção existente foi preservada; confira em Meus mods.");
            await RefreshLibrary();
        }
        catch (Exception ex) { Log($"Falha ao instalar exemplo: {ex.Message}"); }
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

    private async Task<Bundle> RequestCommand(string action, CancellationToken token)
    {
        await libraryReady.WaitAsync(token);
        var receiver = new StartupResultReceiver(new Handler(Looper.MainLooper!));
        var command = new Intent(this, typeof(GameControlService));
        command.SetAction(action);
        command.PutExtra(GameControlService.ReplyKey, receiver);
        StartService(command);
        using var registration = token.Register(() => receiver.Completion.TrySetCanceled(token));
        var result = await receiver.Completion.Task.WaitAsync(token);
        GC.KeepAlive(receiver);
        return result;
    }

    private async void SendCommand(string action)
    {
        launchCancellation?.Cancel();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var result = await RequestCommand(action, timeout.Token);
            Log($"Comando concluído: {result.GetInt("mods")} mod(s) na sessão.");
        }
        catch (Exception ex) { Log($"Comando não concluído: {ex.Message}"); }
    }

    private async void OpenGame()
    {
        if (launchCoordinator.IsPending) { Log("Aguardando confirmação dos mods; abertura já solicitada."); return; }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        launchCancellation = cancellation;
        try
        {
            var intent = new Intent();
            intent.SetClassName(PackageName!, "com.unity3d.player.UnityPlayerActivity");
            if (intent.ResolveActivity(PackageManager!) is null)
                throw new InvalidOperationException("Este APK contém apenas o runtime. Use o APK GameHost para abrir o Terraria.");
            Log("Preparando mods automaticamente; aguardando confirmação do processo do jogo.");
            await launchCoordinator.LaunchAsync(async token =>
            {
                var ready = await RequestCommand(GameControlService.Prepare, token);
                var current = await Task.Run(library.Selection, token);
                if (ready.GetString("selection") != current.Fingerprint)
                    throw new InvalidOperationException("A seleção mudou. Descarregue a sessão e prepare novamente.");
                Log($"Mods confirmados antes de abrir: {ready.GetInt("mods")} mod(s), {ready.GetInt("systems")} sistema(s), PID={ready.GetInt("pid")}.");
            }, () =>
            {
                Log($"Abrindo Terraria após confirmação, painel PID={System.Environment.ProcessId}.");
                StartActivity(intent);
            }, cancellation.Token);
        }
        catch (System.OperationCanceledException)
        {
            Log("Abertura cancelada: painel saiu de foco ou o serviço não confirmou em 20 segundos. O Terraria não foi aberto automaticamente.");
        }
        catch (Exception ex) { Log($"Não foi possível preparar o jogo: {ex}"); }
        finally { if (ReferenceEquals(launchCancellation, cancellation)) launchCancellation = null; }
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
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
                writer.WriteLine("\n--- Biblioteca instalada / seleção para a próxima sessão ---");
                try
                {
                    await libraryReady;
                    var installed = await Task.Run(library.List);
                    var notes = await Task.Run(library.MigrationNotes);
                    foreach (var mod in installed)
                        writer.WriteLine($"{mod.Name} {mod.Version} · {(mod.Enabled ? "selecionado" : "desativado")} · {(mod.Problem ?? "metadados aceitos pelo SDK")} · SHA256={mod.Hash}");
                    foreach (var note in notes) writer.WriteLine("Migração: " + note);
                }
                catch (Exception ex) { writer.WriteLine("Falha na biblioteca (logs preservados): " + ex); }
                Log("Relatório exportado.");
            }
            else if (requestCode == ImportRequest)
            {
                await libraryReady;
                var uri = data.Data;
                var installed = await Task.Run(() =>
                {
                    using var stream = ContentResolver!.OpenInputStream(uri)!;
                    using var bytes = new MemoryStream();
                    var buffer = new byte[8192];
                    int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (bytes.Length + count > ModLibrary.MaxDllBytes)
                            throw new InvalidDataException("Limite inicial: 32 MB por DLL.");
                        bytes.Write(buffer, 0, count);
                    }
                    return library.Install(bytes.ToArray());
                });
                Log(installed.Problem is null
                    ? $"{installed.Name} instalado. Escolha os mods em Meus mods; uma atualização mantém a seleção anterior."
                    : $"{installed.Name} instalado e incompatível: {installed.Problem}");
                await RefreshLibrary();
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
        _ = RefreshLibrary();
    }

    protected override void OnPause()
    {
        launchCancellation?.Cancel();
        base.OnPause();
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
