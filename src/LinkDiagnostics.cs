using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace BPModManager;

public enum LinkMethod { Symlink, Hardlink, Copy }

public sealed record LinkCapability(
    LinkMethod Best,
    bool SymlinkAllowed,
    bool DeveloperMode,
    bool IsAdmin,
    bool SameDrive,
    string Summary,
    string? Advice);

/// <summary>
/// Works out how a mod file will actually be placed in the game folder (symlink vs
/// hardlink vs copy) and why, so the UI can warn and offer to fix it.
/// </summary>
public static class LinkDiagnostics
{
    public static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public static bool DeveloperModeEnabled()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            return (k?.GetValue("AllowDevelopmentWithoutDevLicense") as int?) == 1;
        }
        catch { return false; }
    }

    /// <summary>Ground truth: actually try to create a symlink in a temp dir.</summary>
    public static bool CanCreateSymlink()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bpmm_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var real = Path.Combine(dir, "real.txt");
            File.WriteAllText(real, "x");
            var link = Path.Combine(dir, "link.txt");
            File.CreateSymbolicLink(link, real);
            return File.Exists(link);
        }
        catch { return false; }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    public static bool SameDrive(string a, string b) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)),
                      Path.GetPathRoot(Path.GetFullPath(b)),
                      StringComparison.OrdinalIgnoreCase);

    public static LinkCapability Evaluate(string libraryDir, string gameDir)
    {
        bool admin = IsAdmin();
        bool dev = DeveloperModeEnabled();
        bool sym = CanCreateSymlink();
        bool same = SameDrive(libraryDir, gameDir);

        if (sym)
            return new(LinkMethod.Symlink, true, dev, admin, same,
                "Symlinks available - mods link cleanly (edits in the library apply instantly).",
                null);

        if (same)
            return new(LinkMethod.Hardlink, false, dev, admin, same,
                "Using hard links (library and game are on the same drive). This is fine.",
                "For symlinks (needed if you move the game to another drive), enable Developer Mode or run as admin.");

        return new(LinkMethod.Copy, false, dev, admin, same,
            "Mods will be COPIED - library and game are on different drives and symlinks aren't permitted.",
            "Enable Windows Developer Mode, or run the manager as administrator, to use real symlinks across drives.");
    }

    /// <summary>Relaunch this exe elevated with the same args. Returns false if the user cancels UAC.</summary>
    public static bool RelaunchAsAdmin(IEnumerable<string> args)
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return false;
        var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try { Process.Start(psi); return true; }
        catch { return false; } // Win32Exception when UAC declined
    }

    public static void OpenDeveloperModeSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:developers") { UseShellExecute = true }); }
        catch { }
    }
}
