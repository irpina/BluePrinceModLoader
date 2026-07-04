using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace BPModManager;

public sealed class MainForm : Form
{
    readonly string _managerRoot;
    readonly string _savedPathFile;
    readonly ModLibrary _lib;
    readonly string _backupDir;
    readonly ManagerSettings _settings;
    readonly string _settingsPath;
    string? _gameDir;

    // controls
    readonly TextBox _gamePathBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    readonly Label _mlStatus = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    readonly Button _mlInstall = new() { Text = "Install MelonLoader", AutoSize = true };
    readonly Label _linkBanner = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    readonly Button _linkFix = new() { Text = "Fix…", AutoSize = true, Visible = false };
    readonly ListView _modList = new()
    {
        Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true,
        FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, MultiSelect = false
    };
    readonly TextBox _log = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(24, 24, 28),
        ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9f)
    };
    readonly Button _launch = new() { Text = "Launch Blue Prince", AutoSize = true };
    readonly Button _refresh = new() { Text = "Refresh", AutoSize = true };
    readonly Button _addMods = new() { Text = "Add mods…", AutoSize = true };
    readonly Button _importGame = new() { Text = "Import mods from game folder", AutoSize = true };
    readonly Button _backupSaves = new() { Text = "Back up saves", AutoSize = true };
    readonly Button _restoreSaves = new() { Text = "Restore saves…", AutoSize = true };
    readonly Button _backupModsBtn = new() { Text = "Back up mods", AutoSize = true };
    readonly CheckBox _autoBackup = new() { Text = "Back up saves before launching", AutoSize = true };

    bool _suppressCheckEvents;

    public MainForm(string managerRoot, ModLibrary lib, string? gameDir, string savedPathFile,
                    string backupDir, ManagerSettings settings, string settingsPath)
    {
        _managerRoot = managerRoot;
        _lib = lib;
        _gameDir = gameDir;
        _savedPathFile = savedPathFile;
        _backupDir = backupDir;
        _settings = settings;
        _settingsPath = settingsPath;
        _autoBackup.Checked = settings.AutoBackupBeforeLaunch;

        Text = "Blue Prince Mod Manager";
        MinimumSize = new Size(760, 560);
        Size = new Size(820, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        WireEvents();
        RefreshAll();
    }

    void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // game path
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // ml + link banner
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));// mod list
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));// log
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // backups
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // buttons

        // --- game path row ---
        var gameRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameRow.Controls.Add(new Label { Text = "Game:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 6, 0) }, 0, 0);
        gameRow.Controls.Add(_gamePathBox, 1, 0);
        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) => BrowseForGame();
        gameRow.Controls.Add(browse, 2, 0);
        root.Controls.Add(gameRow, 0, 0);

        // --- melonloader + link banner row ---
        var infoRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        infoRow.Controls.Add(_mlStatus, 0, 0);
        infoRow.Controls.Add(_mlInstall, 1, 0);
        var bannerPanel = new Panel { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(10, 2, 0, 2) };
        bannerPanel.Controls.Add(_linkFix);
        _linkFix.Dock = DockStyle.Right;
        bannerPanel.Controls.Add(_linkBanner);
        infoRow.Controls.Add(bannerPanel, 2, 0);
        root.Controls.Add(infoRow, 0, 1);

        // --- mod list ---
        _modList.Columns.Add("Mod", 240);
        _modList.Columns.Add("Version", 70);
        _modList.Columns.Add("Status", 100);
        _modList.Columns.Add("Description", 320);
        var ctx = new ContextMenuStrip();
        var removeItem = new ToolStripMenuItem("Remove from library");
        removeItem.Click += (_, _) => RemoveSelected();
        ctx.Items.Add(removeItem);
        _modList.ContextMenuStrip = ctx;
        root.Controls.Add(_modList, 0, 2);

        // --- log ---
        var logGroup = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
        logGroup.Controls.Add(_log);
        root.Controls.Add(logGroup, 0, 3);

        // --- backups (saves + mods) ---
        var backupGroup = new GroupBox { Text = "Backups", Dock = DockStyle.Fill, AutoSize = true };
        var backupRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Padding = new Padding(6, 2, 6, 4) };
        backupRow.Controls.Add(_backupSaves);
        backupRow.Controls.Add(_restoreSaves);
        backupRow.Controls.Add(_backupModsBtn);
        _autoBackup.Margin = new Padding(20, 8, 0, 0);
        backupRow.Controls.Add(_autoBackup);
        backupGroup.Controls.Add(backupRow);
        root.Controls.Add(backupGroup, 0, 4);

        // --- buttons ---
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        btnRow.Controls.Add(_launch);
        btnRow.Controls.Add(_refresh);
        btnRow.Controls.Add(_importGame);
        btnRow.Controls.Add(_addMods);
        root.Controls.Add(btnRow, 0, 5);

        Controls.Add(root);
    }

    void WireEvents()
    {
        _mlInstall.Click += async (_, _) => await InstallMelonLoader();
        _launch.Click += (_, _) => LaunchGame();
        _refresh.Click += (_, _) => RefreshAll();
        _linkFix.Click += (_, _) => ShowLinkFix();
        _addMods.Click += (_, _) => AddModsFromDisk();
        _importGame.Click += (_, _) => ImportFromGameFolder();
        _backupSaves.Click += (_, _) => BackupSaves();
        _restoreSaves.Click += (_, _) => RestoreSaves();
        _backupModsBtn.Click += (_, _) => BackupMods();
        _autoBackup.CheckedChanged += (_, _) =>
        {
            _settings.AutoBackupBeforeLaunch = _autoBackup.Checked;
            _settings.Save(_settingsPath);
        };
        _modList.ItemChecked += ModList_ItemChecked;
    }

    // ---------------- data / refresh ----------------

    void RefreshAll()
    {
        _gamePathBox.Text = _gameDir ?? "(not found - use Browse)";

        _importGame.Enabled = _gameDir != null;

        if (_gameDir == null)
        {
            _mlStatus.Text = "MelonLoader: game not located";
            _mlInstall.Enabled = false;
            _launch.Enabled = false;
            _linkBanner.Text = "";
            _linkFix.Visible = false;
        }
        else
        {
            var ml = MelonLoaderInstaller.InstalledVersion(_gameDir);
            _mlStatus.Text = ml != null ? $"MelonLoader: v{ml}" : "MelonLoader: NOT installed";
            _mlStatus.ForeColor = ml != null ? Color.ForestGreen : Color.Firebrick;
            _mlInstall.Enabled = true;
            _mlInstall.Text = ml != null ? "Reinstall MelonLoader" : "Install MelonLoader";
            _launch.Enabled = true;

            var cap = LinkDiagnostics.Evaluate(_lib.LibraryDir, _gameDir);
            _linkBanner.Text = cap.Summary;
            _linkBanner.ForeColor = cap.Best switch
            {
                LinkMethod.Symlink => Color.ForestGreen,
                LinkMethod.Hardlink => Color.DarkGoldenrod,
                _ => Color.Firebrick
            };
            _linkFix.Visible = cap.Best != LinkMethod.Symlink;

            int unmanaged = _lib.ListUnmanagedGameMods(_gameDir).Count;
            _importGame.Text = unmanaged > 0
                ? $"Import {unmanaged} mod(s) from game folder"
                : "Import mods from game folder";
            _importGame.Enabled = unmanaged > 0;
        }

        PopulateMods();
    }

    void PopulateMods()
    {
        // Detach the check handler while rebuilding so removing checked items can't
        // re-enter our logic, and guard with the flag too.
        _suppressCheckEvents = true;
        _modList.ItemChecked -= ModList_ItemChecked;
        _modList.BeginUpdate();
        try
        {
            _modList.Items.Clear();
            foreach (var mod in _lib.Scan())
            {
                bool on = _lib.IsEnabled(mod);
                var item = new ListViewItem(mod.Name) { Tag = mod, Checked = on };
                item.SubItems.Add(mod.Version);
                item.SubItems.Add(on ? "ENABLED" : (_lib.NeedsDownload(mod) ? "not downloaded" : "disabled"));
                item.SubItems.Add(mod.Description);
                _modList.Items.Add(item);
            }
        }
        finally
        {
            _modList.EndUpdate();
            _modList.ItemChecked += ModList_ItemChecked;
            _suppressCheckEvents = false;
        }
    }

    // ---------------- actions ----------------

    async void ModList_ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_suppressCheckEvents) return;
        if (e.Item.Tag is not ModInfo mod) return;

        if (_gameDir == null) { DeferRefresh(); return; }

        try
        {
            if (e.Item.Checked && !_lib.IsEnabled(mod))
            {
                if (MelonLoaderInstaller.InstalledVersion(_gameDir) == null)
                    LogLine("Note: MelonLoader isn't installed yet - the mod won't load until you install it.");
                _modList.Enabled = false;
                await _lib.EnableAsync(mod, _gameDir, LogLine); // downloads first if it's a remote mod
            }
            else if (!e.Item.Checked && _lib.IsEnabled(mod))
            {
                _lib.Disable(mod, LogLine);
            }
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Could not change mod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _modList.Enabled = true; }

        // Never rebuild the ListView from inside its own ItemChecked notification -
        // a checkboxed ListView throws in WmReflectNotify if Items are cleared there.
        DeferRefresh();
    }

    void DeferRefresh()
    {
        if (IsHandleCreated) BeginInvoke(new Action(RefreshAll));
        else RefreshAll();
    }

    void RemoveSelected()
    {
        if (_modList.SelectedItems.Count == 0 || _modList.SelectedItems[0].Tag is not ModInfo mod) return;
        if (MessageBox.Show(
                $"Remove '{mod.Name}' from the library?\r\nThis disables it and deletes its library folder.",
                "Remove mod", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try { _lib.RemoveFromLibrary(mod, LogLine); }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); }
        RefreshAll();
    }

    async Task InstallMelonLoader()
    {
        if (_gameDir == null) return;
        _mlInstall.Enabled = false;
        try
        {
            await MelonLoaderInstaller.InstallAsync(_gameDir, LogLine);
        }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); }
        finally { _mlInstall.Enabled = true; RefreshAll(); }
    }

    void LaunchGame()
    {
        try
        {
            if (_settings.AutoBackupBeforeLaunch)
            {
                try
                {
                    SaveManager.BackupNow(_backupDir, LogLine, "prelaunch");
                    SaveManager.Prune(_backupDir, _settings.MaxBackups, LogLine);
                }
                catch (Exception ex) { LogLine("Auto-backup skipped: " + ex.Message); }
            }
            Process.Start(new ProcessStartInfo("steam://run/" + GameLocator.AppId) { UseShellExecute = true });
            LogLine("Launching Blue Prince via Steam…");
        }
        catch (Exception ex) { LogLine("ERROR launching: " + ex.Message); }
    }

    void BackupSaves()
    {
        // Manual backups are always kept - never pruned.
        try { SaveManager.BackupNow(_backupDir, LogLine); }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); MessageBox.Show(ex.Message, "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void BackupMods()
    {
        try { _lib.BackupLibrary(_backupDir, LogLine); }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); MessageBox.Show(ex.Message, "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void RestoreSaves()
    {
        var backups = SaveManager.ListBackups(_backupDir);
        if (backups.Count == 0)
        {
            MessageBox.Show("No save backups found yet.", "Restore saves", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new OpenFileDialog
        {
            Title = "Pick a save backup to restore",
            InitialDirectory = _backupDir,
            Filter = "Save backups (BluePrince_save*.zip)|BluePrince_save*.zip|All zips (*.zip)|*.zip"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (IsGameRunning())
        {
            MessageBox.Show("Close Blue Prince before restoring saves (it may overwrite them on exit).",
                "Game is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(
                $"Restore '{Path.GetFileName(dlg.FileName)}' over your current save?\r\n" +
                "Your current save is backed up first (…_prerestore_….zip).",
                "Restore saves", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;
        try { SaveManager.Restore(dlg.FileName, _backupDir, LogLine); }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); MessageBox.Show(ex.Message, "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    static bool IsGameRunning()
    {
        try { return Process.GetProcessesByName("BLUE PRINCE").Length > 0; }
        catch { return false; }
    }

    void BrowseForGame()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select the folder containing BLUE PRINCE.exe" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (GameLocator.LooksLikeGameDir(dlg.SelectedPath))
        {
            _gameDir = dlg.SelectedPath;
            File.WriteAllText(_savedPathFile, _gameDir);
            LogLine("Game folder set: " + _gameDir);
            RefreshAll();
        }
        else
        {
            MessageBox.Show($"{GameLocator.ExeName} was not found in that folder.",
                "Not the game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void AddModsFromDisk()
    {
        // let the user pick one or more .dll files they already have
        using var dlg = new OpenFileDialog
        {
            Title = "Select mod .dll file(s) to add to the library",
            Filter = "Mod assemblies (*.dll)|*.dll|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int n = 0;
            foreach (var f in dlg.FileNames)
                n += _lib.ImportPath(f, LogLine).Count;
            LogLine(n > 0 ? $"Added {n} mod(s) to the library (disabled - tick to enable)." : "Nothing added.");
        }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); }
        RefreshAll();
    }

    void ImportFromGameFolder()
    {
        if (_gameDir == null) return;
        var unmanaged = _lib.ListUnmanagedGameMods(_gameDir);
        if (unmanaged.Count == 0)
        {
            MessageBox.Show("No unmanaged mods found in the game's Mods folder.",
                "Nothing to import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var names = string.Join("\r\n  ", unmanaged.Select(Path.GetFileName));
        var msg =
            $"Found {unmanaged.Count} mod(s) already in the game's Mods folder:\r\n\r\n  {names}\r\n\r\n" +
            "Import them into the manager? Each file is moved into the library and linked back " +
            "into Mods, so the game still loads it and you can now enable/disable it here.";
        if (MessageBox.Show(msg, "Import existing mods", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        try
        {
            int n = _lib.AdoptFromGame(_gameDir, LogLine);
            LogLine($"Imported {n} existing mod(s).");
        }
        catch (Exception ex) { LogLine("ERROR: " + ex.Message); }
        RefreshAll();
    }

    void ShowLinkFix()
    {
        if (_gameDir == null) return;
        var cap = LinkDiagnostics.Evaluate(_lib.LibraryDir, _gameDir);

        var msg =
            cap.Summary + "\r\n\r\n" + (cap.Advice ?? "") + "\r\n\r\n" +
            "Choose how to enable real symlinks:\r\n\r\n" +
            "  YES  - open Windows Developer Mode settings (recommended, one-time)\r\n" +
            "  NO   - restart this manager as administrator now\r\n" +
            "  CANCEL - keep using " + cap.Best.ToString().ToLowerInvariant();

        var r = MessageBox.Show(msg, "Enable symlinks", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (r == DialogResult.Yes)
        {
            LinkDiagnostics.OpenDeveloperModeSettings();
            LogLine("Opened Developer Mode settings. Turn it on, then click Refresh.");
        }
        else if (r == DialogResult.No)
        {
            var args = _gameDir != null ? new[] { "--game", _gameDir } : Array.Empty<string>();
            if (LinkDiagnostics.RelaunchAsAdmin(args)) Application.Exit();
            else LogLine("Elevation was cancelled.");
        }
    }

    void LogLine(string s)
    {
        if (InvokeRequired) { BeginInvoke(() => LogLine(s)); return; }
        _log.AppendText(s + Environment.NewLine);
    }
}
