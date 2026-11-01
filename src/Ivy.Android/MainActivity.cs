using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Ivy.Loader;
using System.Security.Cryptography;
using System.Text;

namespace Ivy.AndroidHost;

[Activity(Label = "Ivy", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private TextView output = null!;
    private ModSession? session { get => ProcessHost.Session; set => ProcessHost.Session = value; }
    private string modsPath = "";
    private const int ImportRequest = 10;
    private const int ExportRequest = 11;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ProcessHost.Initialize(FilesDir!.AbsolutePath);
        modsPath = Path.Combine(FilesDir!.AbsolutePath, "mods");
        Directory.CreateDirectory(modsPath);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        var title = new TextView(this) { Text = "Ivy · 0.2.1", TextSize = 24 };
        layout.AddView(title);
        layout.AddView(new TextView(this) { Text = "Host C# + Terraria · ARM64\nTeste de inicialização e metadados IL2CPP", TextSize = 15 });
        AddButton(layout, "Abrir Terraria", OpenGame);
        AddButton(layout, "Carregar mods C#", LoadMods);
        AddButton(layout, "Descarregar mods", () => { session?.Dispose(); session = null; });
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
        Log("Runtime Mono/.NET pronto. Mods .tmod e hooks de gameplay ainda não são suportados.");
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

    private void LoadMods()
    {
        if (session is not null)
        {
            Log("Já existe uma sessão. Descarregue antes de carregar novamente.");
            return;
        }
        session = new ModSession(ProcessHost.Write);
        foreach (var path in Directory.GetFiles(modsPath, "*.dll").Order(StringComparer.Ordinal))
        {
            try { session.LoadFile(path); }
            catch (Exception ex) { Log($"Erro em {Path.GetFileName(path)}: {ex}"); }
        }
        Log($"Resultado: {session.ModCount} mod(s), {session.SystemCount} sistema(s). Hooks de gameplay: pendentes.");
    }

    private void OpenGame()
    {
        var intent = new Intent();
        intent.SetClassName(PackageName!, "com.unity3d.player.UnityPlayerActivity");
        if (intent.ResolveActivity(PackageManager!) is null)
            throw new InvalidOperationException("Este APK contém apenas o runtime. Use o APK GameHost para abrir o Terraria.");
        if (session is null) LoadMods();
        Log($"Abrindo UnityPlayerActivity no pacote {PackageName}, PID C#={System.Environment.ProcessId}.");
        StartActivity(intent);
        Il2CppProbe.Start();
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
                if (session is not null)
                    throw new InvalidOperationException("Descarregue os mods antes de importar.");
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
                File.WriteAllBytes(Path.Combine(modsPath, $"mod-{hash}.dll"), content);
                Log("DLL importada. Toque em Carregar mods C#.");
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
        if (output is not null) output.Text = ProcessHost.GetReport();
    }
}
