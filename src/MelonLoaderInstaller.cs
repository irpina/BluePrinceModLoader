using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace BPModManager;

/// <summary>Detects and installs MelonLoader (portable x64 zip) into the game folder.</summary>
public static class MelonLoaderInstaller
{
    const string ReleaseApi = "https://api.github.com/repos/LavaGang/MelonLoader/releases/latest";
    const string AssetName = "MelonLoader.x64.zip";

    public static string? InstalledVersion(string gameDir)
    {
        var dll = Path.Combine(gameDir, "MelonLoader", "net6", "MelonLoader.dll");
        var proxy = Path.Combine(gameDir, "version.dll");
        if (!File.Exists(dll) || !File.Exists(proxy)) return null;
        try { return FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "unknown"; }
        catch { return "unknown"; }
    }

    public static async Task InstallAsync(string gameDir, Action<string> log)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BluePrinceModManager");
        http.Timeout = TimeSpan.FromMinutes(5);

        log("Querying latest MelonLoader release...");
        using var relDoc = JsonDocument.Parse(await http.GetStringAsync(ReleaseApi));
        var tag = relDoc.RootElement.GetProperty("tag_name").GetString() ?? "?";
        string? url = null;
        foreach (var asset in relDoc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == AssetName)
            { url = asset.GetProperty("browser_download_url").GetString(); break; }
        }
        if (url == null)
            throw new InvalidOperationException($"Release {tag} has no asset named {AssetName}.");

        log($"Downloading MelonLoader {tag} ({AssetName})...");
        var tmp = Path.Combine(Path.GetTempPath(), $"MelonLoader_{tag}.zip");
        await using (var src = await http.GetStreamAsync(url))
        await using (var dst = File.Create(tmp))
            await src.CopyToAsync(dst);

        log("Extracting into game folder...");
        ZipFile.ExtractToDirectory(tmp, gameDir, overwriteFiles: true);
        File.Delete(tmp);

        Directory.CreateDirectory(Path.Combine(gameDir, "Mods"));
        Directory.CreateDirectory(Path.Combine(gameDir, "UserLibs"));
        log($"MelonLoader {tag} installed. First game launch will take a few minutes " +
            "while it generates assemblies - that is normal.");
    }
}
