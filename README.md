# Blue Prince Mod Loader

A small, portable mod manager for **Blue Prince**. It installs MelonLoader for you,
enables/disables mods without copying files (it links them into the game), fetches mods and
their dependencies from the web, and can back up your saves. It ships with the **Developer
Cheat Menu** mod built in.

Single-player game, no anti-cheat, so this is safe to run. Cheats can permanently change a
save, so keep the save-backup feature on if you use them.

---

## For users

1. Download the latest release, unzip anywhere (keep `BPModManager.exe` and the `Library`
   folder together).
2. Run `BPModManager.exe`. It auto-detects your Blue Prince install via Steam.
3. Click **Install MelonLoader** (one time).
4. Tick a mod to enable it. **Keep Dev Objects (Cheat Menu)** is included; **UnityExplorer**
   downloads itself (and its dependency) when you enable it.
5. Click **Launch Blue Prince**.

Nothing is added to the game except MelonLoader and links to your enabled mods, and
disabling removes exactly those.

## Features

- **MelonLoader install** — one click; pulls the official release and sets it up.
- **Link-based enable/disable** — a mod's files are symlinked (or hard-linked, or copied)
  into the game's `Mods`/`UserLibs`, tracked so removal is exact. A banner tells you which
  method is in use and offers to enable symlinks (Developer Mode / admin) when useful.
- **Fetch mods + dependencies** — a mod can declare a GitHub release as its source; the
  manager downloads it and links its `Mods\` and `UserLibs\` contents. UnityExplorer works
  this way.
- **Import what you already have** — add loose `.dll`s, or adopt mods already in the game's
  `Mods` folder.
- **Save & mod backups** — back up the save folder (with restore + optional auto-backup
  before launch) and the whole mod library.

## The built-in cheat menu

**Keep Dev Objects** un-hides Blue Prince's own developer cheat menu (`Cheaters Failz`),
which the retail build deletes on load, and auto-shows the overlay so it appears just from
having the mod enabled. In-game hotkeys (from the game's own overlay):

| Key | Action | | Key | Action |
|-----|--------|-|-----|--------|
| `F` | Floorplan Selection (spawn any room) | | `0` | Free cam |
| `I` | Item Selection (give any item) | | `1`/`2` | FOV down/up |
| `R` | Redraw floorplans | | `3`/`4` | Move speed down/up |
| `L` | Luck Up | | `5`/`6` | Camera tilt |
| | | | `M` | Camera recorder |

See [`mods/KeepDevObjects`](mods/KeepDevObjects) for the mod's source and details.

## Command line

The manager is also scriptable (any argument runs headless; no argument opens the GUI):

```
BPModManager status | diagnose
BPModManager enable <id> | disable <id> | add <path> | import-existing
BPModManager install-melonloader
BPModManager backup | backups | restore <name> | savedir
BPModManager backup-mods | restore-mods <name>
                                            [--game "C:\path\to\Blue Prince"]
```

## Adding a mod

Create `Library\<Id>\` with the mod's files and a `modinfo.json`, either bundled:

```json
{ "id": "MyMod", "name": "My Mod", "version": "1.0", "description": "…",
  "files": { "Mods": [ "MyMod.dll" ], "UserLibs": [] } }
```

…or fetched from a GitHub release (the zip should contain `Mods\`/`UserLibs\`):

```json
{ "id": "UnityExplorer", "name": "UnityExplorer", "description": "…",
  "source": { "githubRepo": "yukieiji/UnityExplorer",
              "assetName": "UnityExplorer.MelonLoader.IL2CPP.CoreCLR.zip" } }
```

## Building

```
cd src
dotnet build -c Release
dotnet publish -c Release        # self-contained single-file win-x64 exe
```
Targets .NET 10. The built-in mod's source is under `mods/KeepDevObjects` (a MelonLoader
IL2CPP mod, `net6`).

## Disclaimer

Not affiliated with or endorsed by the developers or publisher. Uses content the developers
left in the shipped game. Provided as-is; use on your own single-player copy at your own
risk, and back up your saves.
