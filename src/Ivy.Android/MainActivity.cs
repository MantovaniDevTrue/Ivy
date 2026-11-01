using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Text;
using Ivy.Loader;
using Android.Views;
using System.Text.Json;

namespace Ivy.AndroidHost;

[Activity(Theme = "@style/ivy_panel_theme", Label = "Ivy Loader", MainLauncher = true, Exported = true, Process = ":launcher", TaskAffinity = "${ivyPanelAffinity}")]
public sealed class MainActivity : Activity, View.IOnClickListener
{
    private NativePanel panel = null!;
    private readonly SemaphoreSlim libraryMutations = new(1,1);
    private ModLibrary library = null!;
    private Task libraryReady = Task.CompletedTask;
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
        panel=new NativePanel(this,this,"0.7.1 diagnóstico",Assets!.List("")!.Contains("ExampleMod.dll"));
        SetContentView(panel.View);
        if(savedInstanceState?.GetString("ivy.panel") is {} savedPanel)panel.RestoreState(savedPanel);
        Log($"Android {Build.VERSION.Release} · {string.Join(", ", Build.SupportedAbis!)}");
        Log($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        ProcessHost.Write("Runtime Mono/.NET pronto. SDK gerado; API de conteúdo pendente.");
        panel.Status("Escolha seus mods ou jogue sem mods.");
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
            panel.Library(JsonSerializer.Serialize(list,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase }));
        }
        catch (Exception ex) { Log("Biblioteca indisponível: "+ex.Message); }
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

    public async void OnClick(View? view)
    {
        try
        {
            var action=view?.Tag?.ToString()??"";
            switch(action)
            {
                case "play": OpenGame(); break;
                case "prepare": SendCommand(GameControlService.Load); break;
                case "unload": SendCommand(GameControlService.Unload); break;
                case "example": InstallExample(); break;
                case "stop_bridge": SendCommand(GameControlService.StopHook); break;
                case "refresh_reports": panel.Report(ProcessHost.GetReport()); break;
                case "import": PickDocument(false); break;
                case "export": PickDocument(true); break;
                default:
                    await libraryMutations.WaitAsync();
                    try
                    {
                        await libraryReady;
                        if(action=="disable_all")await Task.Run(library.DisableAll);
                        else if(action.StartsWith("toggle|",StringComparison.Ordinal))
                        {
                            var parts=action.Split('|');
                            if(parts.Length!=3)throw new InvalidDataException("Ação de seleção inválida.");
                            await Task.Run(()=>library.SetEnabled(parts[1],parts[2]=="1"));
                        }
                        else throw new InvalidOperationException("Ação desconhecida do painel.");
                    }
                    finally { libraryMutations.Release(); await RefreshLibrary(); }
                    break;
            }
        }
        catch(Exception ex) { Log(ex.Message); }
    }
    private void PickDocument(bool export)
    {
        var intent=new Intent(export?Intent.ActionCreateDocument:Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);intent.SetType(export?"text/plain":"*/*");
        if(export)intent.PutExtra(Intent.ExtraTitle,"Ivy-relatorio.txt");
        StartActivityForResult(intent,export?ExportRequest:ImportRequest);
    }
    protected override void OnSaveInstanceState(Bundle outState)
    {
        if(panel is not null)outState.PutString("ivy.panel",panel.SaveState());
        base.OnSaveInstanceState(outState);
    }
#pragma warning disable CA1422 // Keep the API 24+ Activity fallback until predictive-back device coverage.
    public override void OnBackPressed()
    {
        if(panel is null||!panel.GoBack())base.OnBackPressed();
    }

#pragma warning restore CA1422

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
        panel.Session(action==GameControlService.Unload?"Sessão descarregada":$"Última preparação: {result.GetInt("mods")} mod(s)");
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
        panel.Busy(true);
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
        finally { panel.Busy(false); if (ReferenceEquals(launchCancellation, cancellation)) launchCancellation = null; }
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
        panel.Status(message);
        panel.Report(ProcessHost.GetReport());
    }

    protected override void OnResume()
    {
        base.OnResume();
        RecordGameExits();
        if (panel is not null) panel.Report(ProcessHost.GetReport());
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
