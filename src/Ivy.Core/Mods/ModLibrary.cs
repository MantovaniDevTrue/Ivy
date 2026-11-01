using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ivy.Loader;

public sealed record InstalledMod(string Id, string Name, string Version, string Hash, bool Enabled, string? ApiVersion,
    bool HasEntryPoint, string? Problem, string Origin);
public sealed record ModSelection(string Fingerprint, IReadOnlyList<SelectedMod> Mods);
public sealed record SelectedMod(string Name, string Hash, string Path);

public sealed class ModLibrary
{
    public const int MaxDllBytes = 32 * 1024 * 1024;
    private const int MaxEntries = 512;
    private sealed record Manifest(int Schema, List<InstalledMod> Mods, List<string> MigrationNotes);
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string root, blobs, manifestPath, legacy;
    public ModLibrary(string filesDirectory)
    {
        root = Path.Combine(filesDirectory, "mod-library");
        blobs = Path.Combine(root, "assemblies");
        manifestPath = Path.Combine(root, "installed.json");
        legacy = Path.Combine(filesDirectory, "mods");
    }
    public void Initialize() { using var lease = Lock(); Read(); }
    public IReadOnlyList<InstalledMod> List()
    {
        using var lease = Lock();
        return Read().Mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(CurrentStatus).ToArray();
    }
    public IReadOnlyList<string> MigrationNotes() { using var lease = Lock(); return Read().MigrationNotes.ToArray(); }
    public InstalledMod Install(byte[] content, string origin = "import", bool replaceExisting = true)
    {
        CheckSize(content.Length);
        if (content[0] != 'M' || content[1] != 'Z')
            throw new InvalidDataException("Importe uma DLL .NET compilada para o SDK Ivy. Compilação de fontes .cs no aparelho ainda não está disponível.");
        var metadata = ModInspector.Inspect(content); // Metadata only: never execute an initializer during installation.
        using var lease = Lock();
        var manifest = Read();
        var entry = CreateEntry(metadata, content, origin);
        var old = manifest.Mods.Find(m => m.Id == entry.Id);
        if (old is not null && !replaceExisting) return CurrentStatus(old);
        if (old is not null && entry.Problem is not null)
            throw new InvalidDataException("Atualização incompatível recusada; a instalação anterior foi preservada. " + entry.Problem);
        if (old is null && manifest.Mods.Count >= MaxEntries) throw new InvalidDataException("Limite inicial: 512 mods instalados.");
        entry = entry with { Enabled = old?.Enabled ?? false };
        StoreBlob(entry.Hash, content);
        manifest.Mods.RemoveAll(m => m.Id == entry.Id);
        manifest.Mods.Add(entry);
        Save(manifest);
        return entry;
    }
    public void SetEnabled(string id, bool enabled)
    {
        using var lease = Lock();
        var manifest = Read();
        var index = manifest.Mods.FindIndex(m => m.Id == id);
        if (index < 0) throw new KeyNotFoundException("Mod não instalado.");
        var entry = manifest.Mods[index];
        if (enabled) ValidateBlob(entry);
        manifest.Mods[index] = entry with { Enabled = enabled };
        Save(manifest);
    }
    public void DisableAll()
    {
        using var lease = Lock();
        var manifest = Read();
        for (var i = 0; i < manifest.Mods.Count; i++) manifest.Mods[i] = manifest.Mods[i] with { Enabled = false };
        Save(manifest);
    }
    public ModSelection Selection()
    {
        using var lease = Lock();
        var selected = Read().Mods.Where(m => m.Enabled).OrderBy(m => m.Id, StringComparer.Ordinal).ToArray();
        foreach (var entry in selected) ValidateBlob(entry);
        var fingerprint = Hash(Encoding.UTF8.GetBytes(ModInspector.ApiVersion + "\n" + string.Join("\n", selected.Select(m => m.Id + ":" + m.Hash))));
        return new(fingerprint, selected.Select(m => new SelectedMod(m.Name, m.Hash, BlobPath(m.Hash))).ToArray());
    }
    public static ModSession LoadSelection(ModSelection selection, Action<string> log)
    {
        var session = new ModSession(log);
        try
        {
            foreach (var mod in selection.Mods)
            {
                CheckSize(new FileInfo(mod.Path).Length);
                var bytes = File.ReadAllBytes(mod.Path);
                if (Hash(bytes) != mod.Hash) throw new InvalidDataException($"Conteúdo alterado antes da carga: {mod.Name}.");
                try { session.LoadFile(mod.Path); }
                catch (Exception ex) { throw new InvalidOperationException($"Falha no mod {mod.Name}: {ex.Message}", ex); }
            }
            return session;
        }
        catch { session.Dispose(); throw; }
    }
    private InstalledMod CurrentStatus(InstalledMod entry)
    {
        var problem = !File.Exists(BlobPath(entry.Hash)) ? "DLL instalada ausente." :
            entry.ApiVersion != ModInspector.ApiVersion ? "DLL precisa ser recompilada para esta versão do SDK Ivy." : entry.Problem;
        return entry with { Problem = problem };
    }
    private void ValidateBlob(InstalledMod entry)
    {
        var status = CurrentStatus(entry);
        if (status.Problem is not null) throw new InvalidDataException($"{entry.Name}: {status.Problem}");
        var path = BlobPath(entry.Hash);
        CheckSize(new FileInfo(path).Length);
        var content = File.ReadAllBytes(path);
        if (Hash(content) != entry.Hash) throw new InvalidDataException($"Hash divergente: {entry.Name}. Reimporte a DLL.");
        var metadata = ModInspector.Inspect(content);
        if (metadata.Problem is not null || Id(metadata.AssemblyName) != entry.Id)
            throw new InvalidDataException($"Metadados incompatíveis: {entry.Name}. {metadata.Problem}");
    }
    private Manifest Read()
    {
        if (!File.Exists(manifestPath)) return Migrate();
        if (new FileInfo(manifestPath).Length > 1024 * 1024) throw new InvalidDataException("Catálogo excede o tamanho permitido.");
        Manifest manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), json) ?? throw new JsonException(); }
        catch (JsonException ex) { throw new InvalidDataException("Catálogo inválido; o arquivo foi preservado. Nenhum mod foi ativado automaticamente.", ex); }
        if (manifest.Schema != 1 || manifest.Mods is null || manifest.MigrationNotes is null || manifest.Mods.Count > MaxEntries)
            throw new InvalidDataException("Formato de catálogo não suportado; dados preservados.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in manifest.Mods)
            if (m is null || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 160 || m.Id != Id(m.Name) || !ValidHash(m.Hash) || !ids.Add(m.Id))
                throw new InvalidDataException("Entrada inválida no catálogo; dados preservados.");
        return manifest;
    }
    private Manifest Migrate()
    {
        var manifest = new Manifest(1, [], []);
        if (Directory.Exists(legacy))
        {
            // Prefer explicitly imported versions over the old automatically copied development sample.
            var paths = Directory.GetFiles(legacy, "*.dll").OrderBy(p => Path.GetFileName(p).Equals("ExampleMod.dll", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal);
            foreach (var path in paths.Take(MaxEntries))
            {
                try
                {
                    CheckSize(new FileInfo(path).Length);
                    var content = File.ReadAllBytes(path);
                    var entry = CreateEntry(ModInspector.Inspect(content), content, "legacy");
                    if (manifest.Mods.Any(m => m.Id == entry.Id)) continue;
                    StoreBlob(entry.Hash, content);
                    manifest.Mods.Add(entry); // Legacy files remain untouched. Selection must be explicit.
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or BadImageFormatException or InvalidOperationException or ArgumentException)
                { manifest.MigrationNotes.Add($"Arquivo legado preservado e não instalado: {Path.GetFileName(path)} — {ex.Message}"); }
            }
            if (manifest.Mods.Count != 0) manifest.MigrationNotes.Add("Mods legados preservados e inicialmente desativados. Ative somente os desejados.");
        }
        Save(manifest);
        return manifest;
    }
    private static InstalledMod CreateEntry(ModMetadata metadata, byte[] content, string origin) =>
        new(Id(metadata.AssemblyName), metadata.AssemblyName, metadata.Version, Hash(content), false,
            metadata.ApiVersion, metadata.HasEntryPoint, metadata.Problem, origin);
    private static string Id(string name) => Hash(Encoding.UTF8.GetBytes(name.ToUpperInvariant()));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private string BlobPath(string hash) => Path.Combine(blobs, hash + ".dll");
    private static void CheckSize(long length)
    {
        if (length < 2 || length > MaxDllBytes) throw new InvalidDataException("Limite inicial: 32 MB por DLL .NET.");
    }
    private FileStream Lock()
    {
        Directory.CreateDirectory(blobs);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(Path.Combine(root, "catalog.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(5)) { Thread.Sleep(20); }
        }
    }
    private void StoreBlob(string hash, byte[] content)
    {
        var path = BlobPath(hash);
        if (File.Exists(path) && Hash(File.ReadAllBytes(path)) == hash) return;
        AtomicWrite(path, content);
    }
    private void Save(Manifest manifest)
    {
        if (File.Exists(manifestPath)) AtomicWrite(manifestPath + ".previous", File.ReadAllBytes(manifestPath));
        AtomicWrite(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, json));
    }
    private static void AtomicWrite(string path, byte[] content)
    {
        var temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(content); file.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
