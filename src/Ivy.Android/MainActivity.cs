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
    private readonly StringBuilder report = new();
    private TextView output = null!;
    private ModSession? session;
    private string modsPath = "";
    private const int ImportRequest = 10;
    private const int ExportRequest = 11;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        modsPath = Path.Combine(FilesDir!.AbsolutePath, "mods");
        Directory.CreateDirectory(modsPath);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        var title = new TextView(this) { Text = "Ivy · 0.1.1", TextSize = 24 };
        layout.AddView(title);
        layout.AddView(new TextView(this) { Text = "Teste do runtime C# · ARM64\nIntegração com o Terraria: pendente", TextSize = 15 });
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
        Log("O teste usa Mono/.NET; não carrega o jogo nem arquivos .tmod.");
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
            try { action(); }
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
        session = new ModSession(Log);
        foreach (var path in Directory.GetFiles(modsPath, "*.dll").Order(StringComparer.Ordinal))
        {
            try { session.LoadFile(path); }
            catch (Exception ex) { Log($"Erro em {Path.GetFileName(path)}: {ex}"); }
        }
        Log($"Resultado: {session.ModCount} mod(s), {session.SystemCount} sistema(s). Ponte do jogo: ausente.");
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
                writer.Write(report.ToString());
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
        report.AppendLine($"{DateTimeOffset.Now:HH:mm:ss} {message}");
        output.Text = report.ToString();
        global::Android.Util.Log.Info("Ivy", message);
    }

    protected override void OnDestroy()
    {
        session?.Dispose();
        session = null;
        base.OnDestroy();
    }
}
