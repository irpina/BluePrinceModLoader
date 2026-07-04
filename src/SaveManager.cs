using System.IO.Compression;
using System.Text.Json;

namespace BPModManager;

public sealed class SaveBackup
{
    public string Path { get; init; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public DateTime When { get; init; }
    public long Size { get; init; }
    public bool IsAuto => Name.Contains("prelaunch", StringComparison.OrdinalIgnoreCase);
    public bool IsSafety => Name.Contains("prerestore", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Backs up / restores Blue Prince's save folder (…\LocalLow\Dogubomb\Blue Prince\storage).</summary>
public static class SaveManager
{
    public static string? FindSaveDir()
    {
        var dogubomb = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Dogubomb");

        var direct = System.IO.Path.Combine(dogubomb, "Blue Prince", "storage");
        if (Directory.Exists(direct)) return direct;

        if (Directory.Exists(dogubomb))
            foreach (var d in Directory.GetDirectories(dogubomb))
            {
                var s = System.IO.Path.Combine(d, "storage");
                if (Directory.Exists(s)) return s;
                if (Directory.GetFiles(d, "*.es3").Length > 0) return d;
            }
        return null;
    }

    public static string BackupNow(string backupDir, Action<string> log, string tag = "")
    {
        var save = FindSaveDir()
            ?? throw new InvalidOperationException("Blue Prince save folder not found (has the game been run once?).");
        Directory.CreateDirectory(backupDir);
        var suffix = string.IsNullOrEmpty(tag) ? "" : "_" + tag;
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // Always a NEW file - never overwrite an existing backup. Guard same-second collisions.
        var dest = System.IO.Path.Combine(backupDir, $"BluePrince_save{suffix}_{stamp}.zip");
        for (int n = 2; File.Exists(dest); n++)
            dest = System.IO.Path.Combine(backupDir, $"BluePrince_save{suffix}_{stamp}_{n}.zip");
        ZipFile.CreateFromDirectory(save, dest, CompressionLevel.Optimal, includeBaseDirectory: false);
        log($"Backed up saves -> {System.IO.Path.GetFileName(dest)} ({new FileInfo(dest).Length / 1024} KB)");
        return dest;
    }

    public static List<SaveBackup> ListBackups(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return new();
        return Directory.GetFiles(backupDir, "BluePrince_save*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new SaveBackup { Path = f.FullName, When = f.LastWriteTime, Size = f.Length })
            .ToList();
    }

    /// <summary>Restore a backup zip over the live save folder (backing up the current state first).</summary>
    public static void Restore(string zipPath, string backupDir, Action<string> log)
    {
        var save = FindSaveDir() ?? throw new InvalidOperationException("Save folder not found.");
        // Safety net: snapshot current saves before overwriting.
        BackupNow(backupDir, log, "prerestore");
        ZipFile.ExtractToDirectory(zipPath, save, overwriteFiles: true);
        log($"Restored saves from {System.IO.Path.GetFileName(zipPath)}.");
    }

    /// <summary>
    /// Keep only the newest <paramref name="keep"/> AUTOMATIC (pre-launch) backups.
    /// Manual "Back up saves" backups and safety snapshots are never pruned - they're
    /// kept until the user deletes them, so a new backup never removes an old one.
    /// </summary>
    public static int Prune(string backupDir, int keep, Action<string> log)
    {
        var autos = ListBackups(backupDir).Where(b => b.IsAuto).ToList(); // prelaunch only
        int removed = 0;
        foreach (var old in autos.Skip(Math.Max(1, keep)))
        {
            try { File.Delete(old.Path); removed++; } catch { }
        }
        if (removed > 0) log($"Pruned {removed} old auto-backup(s) (manual backups are kept).");
        return removed;
    }
}

public sealed class ManagerSettings
{
    public bool AutoBackupBeforeLaunch { get; set; } = true;
    public int MaxBackups { get; set; } = 10;

    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static ManagerSettings Load(string path)
    {
        try { if (File.Exists(path)) return JsonSerializer.Deserialize<ManagerSettings>(File.ReadAllText(path)) ?? new(); }
        catch { }
        return new();
    }

    public void Save(string path)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(this, Opts)); } catch { }
    }
}
