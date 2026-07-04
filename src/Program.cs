using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BPModManager;

/// <summary>
/// Blue Prince Mod Manager.
/// No args  -> WinForms GUI.
/// With args -> CLI:  status | enable &lt;id&gt; | disable &lt;id&gt; | install-melonloader   [--game &lt;path&gt;]
/// </summary>
public static class Program
{
    static void Log(string s) => Console.WriteLine(s);

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    public static int Main(string[] args)
    {
        var managerRoot = ResolveManagerRoot();
        var savedPathFile = Path.Combine(managerRoot, "game-path.txt");

        // --game override
        string? gameDir = null;
        var argList = args.ToList();
        var gi = argList.IndexOf("--game");
        if (gi >= 0 && gi + 1 < argList.Count) { gameDir = argList[gi + 1]; argList.RemoveRange(gi, 2); }

        gameDir ??= File.Exists(savedPathFile) ? File.ReadAllText(savedPathFile).Trim() : null;
        if (gameDir != null && !GameLocator.LooksLikeGameDir(gameDir)) gameDir = null;
        gameDir ??= GameLocator.FindGameDir();

        var lib = new ModLibrary(managerRoot);
        var backupDir = Path.Combine(managerRoot, "SaveBackups");
        var settingsPath = Path.Combine(managerRoot, "manager-settings.json");
        var settings = ManagerSettings.Load(settingsPath);

        if (argList.Count > 0)
        {
            AttachConsole(-1); // WinExe -> write to the parent terminal for CLI use
            return RunCommand(argList, gameDir, lib, backupDir).GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(managerRoot, lib, gameDir, savedPathFile, backupDir, settings, settingsPath));
        return 0;
    }

    /// <summary>Published: Library\ is next to the exe. During `dotnet run` walk up to find it.</summary>
    static string ResolveManagerRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Library")))
                return dir.FullName;
        return AppContext.BaseDirectory;
    }

    // ---------- scriptable commands ----------
    static async Task<int> RunCommand(List<string> argList, string? gameDir, ModLibrary lib, string backupDir)
    {
        var cmd = argList[0].ToLowerInvariant();

        switch (cmd)
        {
            case "backup":
                SaveManager.BackupNow(backupDir, Log);
                return 0;
            case "backups":
                foreach (var b in SaveManager.ListBackups(backupDir))
                    Log($"{b.When:yyyy-MM-dd HH:mm}  {b.Size / 1024,6} KB  {b.Name}");
                return 0;
            case "restore":
                if (argList.Count < 2) { Log("usage: restore <backup.zip|name>"); return 2; }
                var target = File.Exists(argList[1]) ? argList[1]
                    : SaveManager.ListBackups(backupDir).FirstOrDefault(b => b.Name.Equals(argList[1], StringComparison.OrdinalIgnoreCase))?.Path;
                if (target == null) { Log("Backup not found: " + argList[1]); return 2; }
                SaveManager.Restore(target, backupDir, Log);
                return 0;
            case "savedir":
                Log(SaveManager.FindSaveDir() ?? "save folder not found");
                return 0;
            case "backup-mods":
                lib.BackupLibrary(backupDir, Log);
                return 0;
            case "restore-mods":
                if (argList.Count < 2) { Log("usage: restore-mods <backup.zip|name>"); return 2; }
                var mt = File.Exists(argList[1]) ? argList[1]
                    : ModLibrary.ListModBackups(backupDir).FirstOrDefault(b => b.Name.Equals(argList[1], StringComparison.OrdinalIgnoreCase))?.Path;
                if (mt == null) { Log("Mod backup not found: " + argList[1]); return 2; }
                lib.RestoreLibrary(mt, backupDir, Log);
                return 0;
        }

        if (gameDir == null && cmd != "status" && cmd != "diagnose")
        { Log("Blue Prince install not found. Use --game <path>."); return 2; }

        switch (cmd)
        {
            case "status":
                Log($"game:        {gameDir ?? "NOT FOUND"}");
                if (gameDir != null)
                    Log($"melonloader: {MelonLoaderInstaller.InstalledVersion(gameDir) ?? "not installed"}");
                foreach (var m in lib.Scan())
                    Log($"mod: {m.Id}  v{m.Version}  {(lib.IsEnabled(m) ? "ENABLED" : "disabled")}");
                return 0;

            case "diagnose":
                var cap = LinkDiagnostics.Evaluate(lib.LibraryDir, gameDir ?? lib.LibraryDir);
                Log($"admin={cap.IsAdmin}  developerMode={cap.DeveloperMode}  symlink={cap.SymlinkAllowed}  sameDrive={cap.SameDrive}");
                Log($"best link method: {cap.Best}");
                Log(cap.Summary);
                if (cap.Advice != null) Log("advice: " + cap.Advice);
                return 0;

            case "install-melonloader":
                await MelonLoaderInstaller.InstallAsync(gameDir!, Log);
                return 0;

            case "import-existing":  // adopt mods already in the game's Mods folder
                Log($"Imported {lib.AdoptFromGame(gameDir!, Log)} existing mod(s).");
                return 0;

            case "add":              // add a .dll / folder the user already has
                if (argList.Count < 2) { Log("usage: add <path-to-dll-or-folder>"); return 2; }
                var added = lib.ImportPath(argList[1], Log);
                Log($"Added {added.Count} mod(s).");
                return 0;

            case "enable":
            case "disable":
                if (argList.Count < 2) { Log($"usage: {cmd} <modId>"); return 2; }
                var mod = lib.Scan().FirstOrDefault(m =>
                    string.Equals(m.Id, argList[1], StringComparison.OrdinalIgnoreCase));
                if (mod == null) { Log($"No mod '{argList[1]}' in Library."); return 2; }
                try
                {
                    if (cmd == "enable") await lib.EnableAsync(mod, gameDir!, Log);
                    else lib.Disable(mod, Log);
                }
                catch (Exception e) { Log("FAILED: " + e.Message); return 1; }
                return 0;

            default:
                Log("commands: status | diagnose | enable <id> | disable <id> | add <path> | import-existing |");
                Log("          install-melonloader | backup | backups | restore <name> | savedir |");
                Log("          backup-mods | restore-mods <name>    [--game <path>]");
                return 2;
        }
    }
}
