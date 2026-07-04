using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BPModManager;

/// <summary>Where a mod's files come from when they aren't bundled in the library folder.</summary>
public sealed class ModSource
{
    [JsonPropertyName("githubRepo")] public string? GithubRepo { get; set; } // "yukieiji/UnityExplorer"
    [JsonPropertyName("assetName")] public string? AssetName { get; set; }   // exact/prefix zip name in the release
    [JsonPropertyName("url")] public string? Url { get; set; }               // or a direct zip URL
    // The zip is expected to already contain Mods\ and/or UserLibs\ folders.
}

public sealed class ModInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("files")] public Dictionary<string, List<string>> Files { get; set; } = new();
    [JsonPropertyName("source")] public ModSource? Source { get; set; }

    [JsonIgnore] public string SourceDir { get; set; } = "";
}

public sealed class LinkRecord
{
    public string ModId { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string Method { get; set; } = "";   // symlink | hardlink | copy
}

public sealed class ManagerState
{
    public List<LinkRecord> Links { get; set; } = new();
}

/// <summary>
/// The mod library ("Library\&lt;ModId&gt;\modinfo.json" + files) and the link engine that
/// enables a mod by linking its files into the game folder. Prefers a real symlink
/// (needs Windows Developer Mode or admin), falls back to a hardlink (same drive),
/// then to a plain copy. Everything created is recorded in manager-state.json so
/// disable removes exactly what enable made.
/// </summary>
public sealed class ModLibrary
{
    public string LibraryDir { get; }
    public string StatePath { get; }
    public ManagerState State { get; private set; } = new();

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ModLibrary(string managerRoot)
    {
        LibraryDir = Path.Combine(managerRoot, "Library");
        StatePath = Path.Combine(managerRoot, "manager-state.json");
        Directory.CreateDirectory(LibraryDir);
        if (File.Exists(StatePath))
        {
            try { State = JsonSerializer.Deserialize<ManagerState>(File.ReadAllText(StatePath)) ?? new(); }
            catch { State = new(); }
        }
        PruneDeadLinks();
    }

    /// <summary>
    /// Drop link records whose target file no longer exists (e.g. the user moved/deleted
    /// the game's Mods folder by hand), so "enabled" always reflects what's actually there.
    /// </summary>
    public int PruneDeadLinks()
    {
        int before = State.Links.Count;
        State.Links.RemoveAll(l => !File.Exists(l.TargetPath));
        int removed = before - State.Links.Count;
        if (removed > 0) Save();
        return removed;
    }

    public List<ModInfo> Scan()
    {
        var mods = new List<ModInfo>();
        foreach (var dir in Directory.GetDirectories(LibraryDir))
        {
            var manifest = Path.Combine(dir, "modinfo.json");
            if (!File.Exists(manifest)) continue;
            try
            {
                var info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(manifest));
                if (info == null || string.IsNullOrWhiteSpace(info.Id)) continue;
                info.SourceDir = dir;
                mods.Add(info);
            }
            catch { /* bad manifest -> skip */ }
        }
        return mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool IsEnabled(ModInfo mod) =>
        State.Links.Any(l => l.ModId == mod.Id && File.Exists(l.TargetPath));

    /// <summary>True once the mod's declared files are physically present in its library folder.</summary>
    public bool IsStaged(ModInfo mod) =>
        mod.Files.Count > 0 && mod.Files.All(kv => kv.Value.All(f => File.Exists(Path.Combine(mod.SourceDir, f))));

    public bool NeedsDownload(ModInfo mod) => !IsStaged(mod) && mod.Source != null;

    /// <summary>Download + extract a remote mod (and its dependencies) into its library folder if needed.</summary>
    public async Task EnsureStagedAsync(ModInfo mod, Action<string> log)
    {
        if (IsStaged(mod)) return;
        if (mod.Source == null)
            throw new InvalidOperationException($"{mod.Name} has no files staged and no download source.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BluePrinceModManager");
        http.Timeout = TimeSpan.FromMinutes(5);

        var url = mod.Source.Url;
        if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(mod.Source.GithubRepo))
        {
            log($"Resolving {mod.Name} download from github/{mod.Source.GithubRepo}…");
            url = await ResolveGithubAsset(http, mod.Source.GithubRepo!, mod.Source.AssetName);
        }
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException($"Could not resolve a download for {mod.Name}.");

        log($"Downloading {mod.Name}…");
        var tmp = Path.Combine(Path.GetTempPath(), $"{mod.Id}_{Guid.NewGuid():N}.zip");
        await using (var s = await http.GetStreamAsync(url))
        await using (var d = File.Create(tmp))
            await s.CopyToAsync(d);

        log("Extracting…");
        foreach (var sub in new[] { "Mods", "UserLibs" })
        {
            var p = Path.Combine(mod.SourceDir, sub);
            if (Directory.Exists(p)) Directory.Delete(p, true);
        }
        ZipFile.ExtractToDirectory(tmp, mod.SourceDir, overwriteFiles: true);
        File.Delete(tmp);

        // Build the files map from the extracted Mods\ and UserLibs\ folders.
        var files = new Dictionary<string, List<string>>();
        foreach (var sub in new[] { "Mods", "UserLibs" })
        {
            var p = Path.Combine(mod.SourceDir, sub);
            if (!Directory.Exists(p)) continue;
            var list = Directory.GetFiles(p, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(mod.SourceDir, f).Replace('\\', '/'))
                .ToList();
            if (list.Count > 0) files[sub] = list;
        }
        if (files.Count == 0)
            throw new InvalidOperationException($"{mod.Name}'s download had no Mods\\ or UserLibs\\ folder.");

        mod.Files = files;
        SaveModInfo(mod);
        log($"{mod.Name} downloaded ({files.Sum(kv => kv.Value.Count)} files, incl. dependencies).");
    }

    static async Task<string?> ResolveGithubAsset(HttpClient http, string repo, string? assetName)
    {
        using var doc = JsonDocument.Parse(
            await http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest"));
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var n = a.GetProperty("name").GetString();
            if (n == null) continue;
            if (string.IsNullOrEmpty(assetName)
                || n.Equals(assetName, StringComparison.OrdinalIgnoreCase)
                || n.StartsWith(assetName, StringComparison.OrdinalIgnoreCase))
                return a.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    void SaveModInfo(ModInfo mod) =>
        File.WriteAllText(Path.Combine(mod.SourceDir, "modinfo.json"), JsonSerializer.Serialize(mod, JsonOpts));

    /// <summary>Stage (download if remote) then enable.</summary>
    public async Task EnableAsync(ModInfo mod, string gameDir, Action<string> log)
    {
        await EnsureStagedAsync(mod, log);
        Enable(mod, gameDir, log);
    }

    /// <summary>Disable and delete the mod's library folder entirely.</summary>
    public void RemoveFromLibrary(ModInfo mod, Action<string> log)
    {
        Disable(mod, log);
        try { if (Directory.Exists(mod.SourceDir)) Directory.Delete(mod.SourceDir, true); log($"Removed {mod.Name} from the library."); }
        catch (Exception e) { log($"Could not delete library folder: {e.Message}"); }
    }

    public void Enable(ModInfo mod, string gameDir, Action<string> log)
    {
        if (IsEnabled(mod)) { log($"{mod.Name} is already enabled."); return; }

        var created = new List<LinkRecord>();
        try
        {
            foreach (var (subdir, files) in mod.Files)
            {
                var targetDir = Path.Combine(gameDir, subdir);
                Directory.CreateDirectory(targetDir);
                foreach (var file in files)
                {
                    var source = Path.Combine(mod.SourceDir, file);
                    if (!File.Exists(source))
                        throw new FileNotFoundException($"Mod file missing from library: {source}");
                    var target = Path.Combine(targetDir, Path.GetFileName(file));
                    if (File.Exists(target))
                    {
                        if (FilesIdentical(source, target))
                        {
                            // Same mod already sitting there (e.g. installed by hand earlier).
                            // Take ownership: replace it with our managed link.
                            File.Delete(target);
                            log($"  adopting existing {Path.GetFileName(file)} (identical to library copy)");
                        }
                        else
                        {
                            throw new IOException(
                                $"'{target}' already exists and differs from the library copy " +
                                "(a different version installed manually?). Remove it first, or use " +
                                "'Import mods from game folder' to adopt it.");
                        }
                    }
                    var method = LinkFile(source, target);
                    created.Add(new LinkRecord { ModId = mod.Id, TargetPath = target, Method = method });
                    log($"  {Path.GetFileName(file)} -> {subdir}\\ ({method})");
                }
            }
        }
        catch
        {
            foreach (var rec in created) TryDelete(rec.TargetPath); // roll back partial enable
            throw;
        }

        State.Links.AddRange(created);
        Save();
        log($"{mod.Name} enabled.");
    }

    public void Disable(ModInfo mod, Action<string> log)
    {
        var mine = State.Links.Where(l => l.ModId == mod.Id).ToList();
        if (mine.Count == 0) { log($"{mod.Name} is not enabled (no links recorded)."); return; }
        foreach (var rec in mine)
        {
            if (TryDelete(rec.TargetPath))
                log($"  removed {rec.TargetPath} ({rec.Method})");
            State.Links.Remove(rec);
        }
        Save();
        log($"{mod.Name} disabled.");
    }

    // ---------------- importing mods the user already has ----------------

    bool IsManagedTarget(string fullPath) =>
        State.Links.Any(l => string.Equals(
            Path.GetFullPath(l.TargetPath), Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase));

    /// <summary>Loose .dll files in the game's Mods folder that this manager isn't tracking.</summary>
    public List<string> ListUnmanagedGameMods(string gameDir)
    {
        var modsDir = Path.Combine(gameDir, "Mods");
        if (!Directory.Exists(modsDir)) return new();
        return Directory.GetFiles(modsDir, "*.dll")
            .Where(f => !IsManagedTarget(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Adopt mods already installed in the game's Mods folder: move each loose DLL into the
    /// library and link it back, so the manager can enable/disable it from then on.
    /// (UserLibs is left alone - those are shared dependencies, not mods.)
    /// </summary>
    public int AdoptFromGame(string gameDir, Action<string> log)
    {
        int adopted = 0;
        foreach (var dll in ListUnmanagedGameMods(gameDir))
        {
            var id = SanitizeId(Path.GetFileNameWithoutExtension(dll));
            var modDir = Path.Combine(LibraryDir, id);
            if (Directory.Exists(modDir)) { log($"skip '{Path.GetFileName(dll)}': '{id}' already in library."); continue; }
            try
            {
                Directory.CreateDirectory(modDir);
                var dest = Path.Combine(modDir, Path.GetFileName(dll));
                File.Move(dll, dest); // adopt the user's original into the library (one copy)
                WriteModInfo(modDir, id, Path.GetFileName(dll), "Mods");
                var mod = LoadModInfo(modDir)!;
                Enable(mod, gameDir, log); // link it back into Mods
                adopted++;
            }
            catch (Exception e)
            {
                log($"Could not adopt '{Path.GetFileName(dll)}': {e.Message}");
                try { if (Directory.Exists(modDir) && !Directory.EnumerateFileSystemEntries(modDir).Any()) Directory.Delete(modDir); } catch { }
            }
        }
        return adopted;
    }

    /// <summary>Import a mod from a .dll the user has elsewhere, or a folder of DLLs / a folder with modinfo.json.</summary>
    public List<ModInfo> ImportPath(string path, Action<string> log)
    {
        var result = new List<ModInfo>();
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var m = ImportOneFile(path, "Mods", log);
            if (m != null) result.Add(m);
        }
        else if (Directory.Exists(path))
        {
            if (File.Exists(Path.Combine(path, "modinfo.json")))
            {
                var m = ImportModFolder(path, log);
                if (m != null) result.Add(m);
            }
            else
            {
                foreach (var dll in Directory.GetFiles(path, "*.dll"))
                {
                    var m = ImportOneFile(dll, "Mods", log);
                    if (m != null) result.Add(m);
                }
                if (result.Count == 0) log("No .dll files found in that folder.");
            }
        }
        else log("Not a .dll file or a folder: " + path);
        return result;
    }

    ModInfo? ImportOneFile(string file, string subfolder, Action<string> log)
    {
        var id = SanitizeId(Path.GetFileNameWithoutExtension(file));
        var modDir = Path.Combine(LibraryDir, id);
        if (Directory.Exists(modDir)) { log($"'{id}' is already in the library - skipped."); return null; }
        Directory.CreateDirectory(modDir);
        File.Copy(file, Path.Combine(modDir, Path.GetFileName(file)));
        WriteModInfo(modDir, id, Path.GetFileName(file), subfolder);
        log($"Imported '{Path.GetFileName(file)}' as '{id}' (disabled).");
        return LoadModInfo(modDir);
    }

    ModInfo? ImportModFolder(string folder, Action<string> log)
    {
        var info = LoadModInfo(folder);
        if (info == null) { log("Folder has an unreadable modinfo.json."); return null; }
        var dest = Path.Combine(LibraryDir, SanitizeId(info.Id));
        if (Directory.Exists(dest)) { log($"'{info.Id}' is already in the library - skipped."); return null; }
        CopyDir(folder, dest);
        log($"Imported mod '{info.Name}' ({info.Id}) into the library (disabled).");
        return LoadModInfo(dest);
    }

    ModInfo? LoadModInfo(string dir)
    {
        var manifest = Path.Combine(dir, "modinfo.json");
        if (!File.Exists(manifest)) return null;
        try
        {
            var info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(manifest));
            if (info == null) return null;
            info.SourceDir = dir;
            return info;
        }
        catch { return null; }
    }

    static void WriteModInfo(string dir, string id, string fileName, string subfolder)
    {
        var info = new ModInfo
        {
            Id = id,
            Name = id,
            Version = "unknown",
            Description = "Imported mod.",
            Files = new() { [subfolder] = new() { fileName } }
        };
        File.WriteAllText(Path.Combine(dir, "modinfo.json"), JsonSerializer.Serialize(info, JsonOpts));
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from))
            CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    static string SanitizeId(string s)
    {
        var cleaned = new string(s.Select(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' ? ch : '_').ToArray()).Trim();
        return cleaned.Length == 0 ? "Mod" : cleaned;
    }

    // ---------------- backing up the mod library itself ----------------

    /// <summary>Zip the whole mod Library (+ enable-state) so a user can restore their mod setup.</summary>
    public string BackupLibrary(string backupDir, Action<string> log)
    {
        Directory.CreateDirectory(backupDir);
        var name = $"BluePrince_mods_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var dest = Path.Combine(backupDir, name);
        using (var zip = ZipFile.Open(dest, ZipArchiveMode.Create))
        {
            foreach (var f in Directory.GetFiles(LibraryDir, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(f, "Library/" + Path.GetRelativePath(LibraryDir, f).Replace('\\', '/'));
            if (File.Exists(StatePath)) zip.CreateEntryFromFile(StatePath, "manager-state.json");
        }
        log($"Backed up mod library -> {name} ({new FileInfo(dest).Length / 1024} KB)");
        return dest;
    }

    public static List<SaveBackup> ListModBackups(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return new();
        return Directory.GetFiles(backupDir, "BluePrince_mods_*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new SaveBackup { Path = f.FullName, When = f.LastWriteTime, Size = f.Length })
            .ToList();
    }

    /// <summary>Restore a mod-library backup. Disables everything first so no stale links dangle.</summary>
    public void RestoreLibrary(string zipPath, string backupDir, Action<string> log)
    {
        BackupLibrary(backupDir, log); // snapshot current library first
        foreach (var mod in Scan().ToList())
            if (IsEnabled(mod)) Disable(mod, log);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var e in zip.Entries)
            {
                if (e.Name.Length == 0) continue; // directory entry
                string dest;
                if (e.FullName.StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
                    dest = Path.Combine(LibraryDir, e.FullName["Library/".Length..].Replace('/', Path.DirectorySeparatorChar));
                else if (e.FullName.Equals("manager-state.json", StringComparison.OrdinalIgnoreCase))
                    dest = StatePath;
                else continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                e.ExtractToFile(dest, overwrite: true);
            }
        }
        // reload restored state
        if (File.Exists(StatePath))
        {
            try { State = JsonSerializer.Deserialize<ManagerState>(File.ReadAllText(StatePath)) ?? new(); } catch { }
        }
        PruneDeadLinks();
        log($"Restored mod library from {Path.GetFileName(zipPath)}.");
    }

    void Save() => File.WriteAllText(StatePath, JsonSerializer.Serialize(State, JsonOpts));

    static bool FilesIdentical(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            using var sa = File.OpenRead(a);
            using var sb = File.OpenRead(b);
            byte[] ba = new byte[65536], bb = new byte[65536];
            int ra;
            while ((ra = sa.Read(ba, 0, ba.Length)) > 0)
            {
                int off = 0;
                while (off < ra) { int n = sb.Read(bb, off, ra - off); if (n <= 0) return false; off += n; }
                for (int i = 0; i < ra; i++) if (ba[i] != bb[i]) return false;
            }
            return true;
        }
        catch { return false; }
    }

    static bool TryDelete(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); return true; } }
        catch { /* locked (game running?) - leave it, stays in state */ }
        return false;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    static string LinkFile(string source, string target)
    {
        try
        {
            File.CreateSymbolicLink(target, source);
            return "symlink";
        }
        catch { /* needs Developer Mode or admin */ }

        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase)
            && CreateHardLink(target, source, IntPtr.Zero))
            return "hardlink";

        File.Copy(source, target);
        return "copy";
    }
}
