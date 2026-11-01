using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ivy.Loader;

namespace Ivy.AndroidHost;

[Activity(Label = "Meus mods", Exported = false, Process = ":launcher", TaskAffinity = "${ivyPanelAffinity}")]
public sealed class ModsActivity : Activity
{
    private ModLibrary library = null!;
    private ModAdapter adapter = null!;
    private TextView status = null!;
    private readonly SemaphoreSlim mutations = new(1, 1);
    private int refreshVersion;
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        library = new ModLibrary(FilesDir!.AbsolutePath);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(16 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        layout.AddView(new TextView(this) { Text = "Meus mods", TextSize = 24 });
        layout.AddView(new TextView(this) { Text = "A seleção vale para a próxima sessão. Se ela já foi preparada, descarregue a sessão no painel antes de jogar com outra seleção.", TextSize = 14 });
        status = new TextView(this) { TextSize = 14 };
        layout.AddView(status);
        var disable = new Button(this) { Text = "Desativar todos — jogar sem mods" };
        disable.Click += async (_, _) =>
        {
            await mutations.WaitAsync();
            try { await Task.Run(library.DisableAll); await Reload(); }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { mutations.Release(); }
        };
        layout.AddView(disable);
        adapter = new ModAdapter(this, ChangeSelection);
        var list = new ListView(this) { Adapter = adapter };
        layout.AddView(list, new LinearLayout.LayoutParams(-1, 0, 1));
        SetContentView(layout);
    }
    protected override void OnResume() { base.OnResume(); _ = Reload(); }
    private async Task Reload()
    {
        var version = ++refreshVersion;
        try
        {
            var mods = await Task.Run(library.List);
            if (version != refreshVersion) return;
            adapter.Items = mods;
            adapter.NotifyDataSetChanged();
            status.Text = mods.Count == 0 ? "Nenhum mod instalado. Você já pode jogar sem mods." :
                $"{mods.Count} instalado(s) · {mods.Count(m => m.Enabled)} selecionado(s). Importados começam desativados.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    private async void ChangeSelection(InstalledMod mod, bool enabled)
    {
        await mutations.WaitAsync();
        try { await Task.Run(() => library.SetEnabled(mod.Id, enabled)); await Reload(); }
        catch (Exception ex) { await Reload(); status.Text = ex.Message; }
        finally { mutations.Release(); }
    }
    private sealed class ModAdapter(Activity context, Action<InstalledMod, bool> change) : BaseAdapter<InstalledMod>
    {
        internal IReadOnlyList<InstalledMod> Items { get; set; } = [];
        public override int Count => Items.Count;
        public override InstalledMod this[int position] => Items[position];
        public override long GetItemId(int position) => position;
        private sealed class Row : Java.Lang.Object
        {
            internal required TextView Title;
            internal required TextView Detail;
            internal required Switch Toggle;
            internal InstalledMod? Mod;
            internal bool Binding;
        }
        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var row = convertView?.Tag as Row;
            if (row is null)
            {
                var layout = new LinearLayout(context) { Orientation = Orientation.Vertical };
                layout.SetPadding(8, 12, 8, 12);
                row = new Row { Title = new TextView(context) { TextSize = 18 }, Detail = new TextView(context) { TextSize = 13 }, Toggle = new Switch(context) { Text = "Selecionado para a próxima sessão" } };
                layout.AddView(row.Title); layout.AddView(row.Detail); layout.AddView(row.Toggle);
                var holder = row;
                row.Toggle.CheckedChange += (_, e) => { if (!holder.Binding && holder.Mod is not null) { holder.Toggle.Enabled = false; change(holder.Mod, e.IsChecked); } };
                layout.Tag = row;
                convertView = layout;
            }
            var mod = Items[position];
            row.Binding = true;
            row.Mod = mod;
            row.Title.Text = $"{mod.Name} · {mod.Version}";
            row.Detail.Text = mod.Problem is null ? "Metadados compatíveis com o SDK. Comportamento depende do mod." : "Incompatível: " + mod.Problem;
            row.Toggle.Checked = mod.Enabled;
            row.Toggle.Enabled = mod.Enabled || mod.Problem is null;
            row.Binding = false;
            return convertView!;
        }
    }
}
