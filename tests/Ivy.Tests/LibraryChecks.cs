using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Ivy.Loader;
using Ivy.Runtime;

internal static class LibraryChecks
{
    private static int count;
    internal static void Run(string examplePath, string updatePath, string failingPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "ivy-library-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var example = File.ReadAllBytes(examplePath);
            var update = File.ReadAllBytes(updatePath);
            var failing = File.ReadAllBytes(failingPath);
            var library = new ModLibrary(root);
            library.Initialize();
            Check(library.List().Count == 0 && library.Selection().Mods.Count == 0, "fresh library selects zero mods");
            using (var empty = ModLibrary.LoadSelection(library.Selection(), _ => { }))
                Check(empty.ModCount == 0 && !WorldHooks.HasHandlers, "vanilla session succeeds without hooks");
            Throws(() => library.Install(Encoding.UTF8.GetBytes("using Terraria; class SourceMod {}")), "source files are rejected explicitly until a compiler exists");
            var installed = library.Install(example);
            Check(!installed.Enabled && installed.Problem is null, "import does not execute or enable a mod");
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            File.WriteAllBytes(Path.Combine(root, "mods", "rogue.dll"), failing);
            Check(library.Selection().Mods.Count == 0 && library.List().Count == 1, "unregistered DLLs never enter selection");
            library.SetEnabled(installed.Id, true);
            var original = library.Selection();
            Check(new ModLibrary(root).Selection().Fingerprint == original.Fingerprint && original.Mods.Count == 1, "selection survives a new library instance");
            using (var session = ModLibrary.LoadSelection(original, _ => { }))
                Check(session.ModCount == 1 && WorldHooks.HasHandlers, "only selected DLL creates a runtime session");
            Check(!WorldHooks.HasHandlers, "selected session unload releases mod hooks");
            library.Install(example);
            Check(library.List().Single().Enabled, "idempotent import keeps activation");
            var updated = library.Install(update);
            Check(updated.Enabled && updated.Version == "2.0.0.0" && updated.Hash != installed.Hash, "compatible update keeps stable identity and activation");
            Check(library.Selection().Fingerprint != original.Fingerprint && File.ReadAllBytes(original.Mods[0].Path).SequenceEqual(example), "updates preserve immutable files used by previous sessions");
            Check(ModInspector.Inspect(update).Problem is null, "metadata inspection never runs a throwing static initializer");
            var beforeExample = library.List().Single().Hash;
            library.Install(example, "development", replaceExisting: false);
            Check(library.List().Single().Hash == beforeExample, "development example cannot overwrite an installed mod");
            var invalidUpdate = (byte[])update.Clone();
            var reference = Encoding.UTF8.GetBytes("Ivy.Api\0");
            var offset = invalidUpdate.AsSpan().IndexOf(reference);
            Check(offset >= 0, "invalid update fixture has an API reference to alter");
            Encoding.UTF8.GetBytes("Bad.Api\0").CopyTo(invalidUpdate.AsSpan(offset));
            Throws(() => library.Install(invalidUpdate), "incompatible update is rejected");
            Check(library.List().Single().Hash == beforeExample && library.List().Single().Enabled, "failed update preserves installed version and activation");
            var incompatible = library.Install(File.ReadAllBytes(typeof(Terraria.ModLoader.Mod).Assembly.Location));
            Check(incompatible.Problem is not null && !incompatible.Enabled, "valid non-mod DLL is listed as incompatible and disabled");
            Throws(() => library.SetEnabled(incompatible.Id, true), "incompatible installation cannot be activated");
            library.DisableAll();
            Check(library.Selection().Mods.Count == 0, "disable all returns to vanilla without uninstalling");
            var fail = library.Install(failing);
            Parallel.Invoke(() => new ModLibrary(root).SetEnabled(updated.Id, true), () => new ModLibrary(root).SetEnabled(fail.Id, true));
            Check(library.Selection().Mods.Count == 2, "concurrent writers preserve distinct toggles");
            library.Install(example); // Restore a loadable version; keep its stable id.
            var selected = library.Selection();
            var ordered = selected with { Mods = selected.Mods.OrderBy(m => m.Name == "FailingMod").ToArray() };
            Throws(() => ModLibrary.LoadSelection(ordered, _ => { }), "partial selected batch fails explicitly");
            Check(!WorldHooks.HasHandlers && library.List().Count == 3, "partial failure rolls back hooks and keeps installed files");
            library.SetEnabled(fail.Id, false);
            var active = library.Selection().Mods.Single();
            File.WriteAllBytes(active.Path, [1, 2, 3]);
            Throws(() => library.Selection(), "tampered content never reaches the loader");
            library.SetEnabled(installed.Id, false);
            Check(library.Selection().Mods.Count == 0, "corrupt selected content can be disabled without deleting it");
            library.Install(example);
            var manifest = Path.Combine(root, "mod-library", "installed.json");
            var backup = manifest + ".previous";
            File.Delete(backup); Directory.CreateDirectory(backup);
            var bytesBefore = File.ReadAllBytes(manifest);
            Throws(() => library.Install(update), "failed catalog commit refuses an update");
            Check(File.ReadAllBytes(manifest).SequenceEqual(bytesBefore), "failed commit preserves the previous manifest");
            Directory.Delete(backup);
            var node = JsonNode.Parse(File.ReadAllText(manifest))!;
            node["mods"]![0]!["hash"] = "../../outside";
            File.WriteAllText(manifest, node.ToJsonString());
            var invalidManifest = File.ReadAllBytes(manifest);
            Throws(() => library.List(), "catalog rejects invalid storage keys");
            Check(File.ReadAllBytes(manifest).SequenceEqual(invalidManifest), "invalid catalog is preserved rather than silently reset");
            CrossProcessLock(root, bytesBefore, manifest);
            Migration(example, update);
            Console.WriteLine($"{count} mod-library checks passed.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    private static void CrossProcessLock(string root, byte[] validManifest, string path)
    {
        File.WriteAllBytes(path, validManifest);
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardInput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--hold-library-lock"); start.ArgumentList.Add(root);
        using var child = Process.Start(start)!;
        try
        {
            Check(child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult() == "locked", "second process acquired the catalog lock");
            var read = Task.Run(() => new ModLibrary(root).List());
            Check(!read.Wait(100), "catalog lock excludes a different process");
            child.StandardInput.WriteLine(); child.StandardInput.Flush();
            Check(read.Wait(3000), "catalog access resumes after other process releases lock");
            child.WaitForExit(3000);
        }
        finally { if (!child.HasExited) child.Kill(); }
    }
    private static void Migration(byte[] example, byte[] update)
    {
        var root = Path.Combine(Path.GetTempPath(), "ivy-migrate-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, "mods"); Directory.CreateDirectory(legacy);
        try
        {
            File.WriteAllBytes(Path.Combine(legacy, "ExampleMod.dll"), example);
            File.WriteAllBytes(Path.Combine(legacy, "mod-imported.dll"), update);
            File.WriteAllBytes(Path.Combine(legacy, "bad.dll"), [1, 2]);
            var library = new ModLibrary(root); library.Initialize();
            Check(library.List().Count == 1 && library.List()[0].Version == "2.0.0.0", "migration prefers the imported version over the seeded example");
            Check(!library.List()[0].Enabled && library.Selection().Mods.Count == 0, "migration requires explicit activation");
            Check(Directory.GetFiles(legacy).Length == 3 && File.ReadAllBytes(Path.Combine(legacy, "ExampleMod.dll")).SequenceEqual(example), "migration leaves every legacy file intact");
            Check(library.MigrationNotes().Count > 0, "migration records rejected files and disabled selection");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool success, string message) { if (!success) throw new Exception(message); count++; Console.WriteLine("OK: " + message); }
    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or BadImageFormatException) { Check(true, message); return; }
        throw new Exception(message);
    }
}
