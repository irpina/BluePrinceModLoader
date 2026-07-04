using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BPModManager;

/// <summary>Finds the Blue Prince install via the Steam registry key and library folders.</summary>
public static class GameLocator
{
    public const string AppId = "1569580";
    public const string ExeName = "BLUE PRINCE.exe";

    public static string? FindGameDir()
    {
        foreach (var steam in SteamRoots())
        {
            var dir = SearchSteamLibraries(steam);
            if (dir != null) return dir;
        }
        return null;
    }

    public static bool LooksLikeGameDir(string dir) =>
        File.Exists(Path.Combine(dir, ExeName));

    static IEnumerable<string> SteamRoots()
    {
        string? reg = null;
        try
        {
            reg = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")
                          ?.GetValue("SteamPath") as string;
        }
        catch { /* registry unavailable */ }
        if (!string.IsNullOrEmpty(reg))
            yield return reg.Replace('/', '\\');

        yield return @"C:\Program Files (x86)\Steam";
        yield return @"C:\Program Files\Steam";
    }

    static string? SearchSteamLibraries(string steamRoot)
    {
        var apps = Path.Combine(steamRoot, "steamapps");
        if (!Directory.Exists(apps)) return null;

        var libraries = new List<string> { apps };
        var vdf = Path.Combine(apps, "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                var lib = Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps");
                if (Directory.Exists(lib) && !libraries.Contains(lib, StringComparer.OrdinalIgnoreCase))
                    libraries.Add(lib);
            }
        }

        foreach (var lib in libraries)
        {
            var manifest = Path.Combine(lib, $"appmanifest_{AppId}.acf");
            if (!File.Exists(manifest)) continue;
            var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
            if (!m.Success) continue;
            var dir = Path.Combine(lib, "common", m.Groups[1].Value);
            if (LooksLikeGameDir(dir)) return dir;
        }

        // manifest missing but folder present
        var guess = Path.Combine(apps, "common", "Blue Prince");
        return LooksLikeGameDir(guess) ? guess : null;
    }
}
