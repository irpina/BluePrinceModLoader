# Blue Prince Dev Menus

Blue Prince ships with the developers' own internal cheat/debug menu still in the game
files. The game deletes it the moment a scene loads. **KeepDevObjects** is a tiny
MelonLoader mod that stops that one deletion, so the menu stays alive and you can use it.

Single-player only. No online, no anti-cheat, so this is safe to run.

> ⚠️ **Spoilers + save safety.** The dev menu lets you spawn any room and item and set
> progress flags, so it spoils content. **Back up your save folder before using it** —
> cheats can permanently change a save. Using a separate profile is a good idea.

---

## Requirements

- **Blue Prince** (Steam, Windows).
- **[MelonLoader](https://github.com/LavaGang/MelonLoader/releases)** installed on the
  game. Run the auto-installer, point it at `BLUE PRINCE.exe`, install, then launch the
  game once so it finishes setup and quit.

## Install

1. Close the game.
2. Download **`KeepDevObjects.dll`** from this repo.
3. Drop it into the game's `Mods` folder:
   ```
   ...\steamapps\common\Blue Prince\Mods\KeepDevObjects.dll
   ```
4. Launch the game.

**Confirm it loaded** — open `...\Blue Prince\MelonLoader\Latest.log` and look for:
```
[Keep_Dev_Objects] Patched FurySdkDestroyIfCheatsNotEnabled.OnEnter (filtered=True).
```

## Turn the menu on

Get into an actual run (not the main menu), then:

**Press your mouse's side button (mouse button 4 / the "forward" thumb button).**

You'll see **`CF ACTIVATED`** on screen. The cheat system is now live.

> No 5-button mouse? See [Troubleshooting](#troubleshooting).

## Controls

These are the game's own dev hotkeys, taken from its built-in on-screen overlay.

**Cheat actions**

| Key | Does |
|-----|------|
| `F` | **Floorplan Selection** — pick and spawn any room in the game |
| `I` | **Item Selection** — give yourself any item |
| `R` | Redraw the current floorplan options |
| `T` | Turn / rotate floorplans |
| `L` | Luck Up |

**Free camera / cinematic**

| Key | Does |
|-----|------|
| `0` | Toggle free cam |
| `Space` | Toggle HUD |
| `1` / `2` | FOV down / up |
| `3` / `4` | Move speed down / up |
| `5` / `6` | Camera tilt down / up |
| `8` / `9` | Lower / raise camera |
| `M` | Camera Recorder (keyframe camera-path tool) |

`F` opens a grid with a button for every room in the game (including the secret ones);
click to spawn it. `I` opens a grid with every item; click to add it to your inventory.
There is also a typed command console for resources (codes like `GEM`, `GOLD`, `KEY`,
`STEP`, `LUCK`, `DAY`), but the `F` / `I` menus cover most of it.

## Troubleshooting

**"CF ACTIVATED" never shows / no side mouse button.** The activation is hard-wired to
mouse button 4, which not every mouse has. Either remap a spare button to "mouse button
4 / forward" in your mouse software, or use [UnityExplorer](https://github.com/yukieiji/UnityExplorer)
(another MelonLoader mod): browse to `__SYSTEM > Cheaters Failz (Back Doors)` and toggle
its child UI objects active by hand.

**Hotkeys do nothing after activating.** Make sure you're in a run, not a menu, and that
"CF ACTIVATED" appeared. If the on-screen overlay isn't up, try the publisher-mode toggle
(press `P` then `L`).

**Game crashes on launch.** Make sure you only added `KeepDevObjects.dll` and that
MelonLoader launches cleanly on its own first.

**Undo it.** Delete `KeepDevObjects.dll` from `Mods`. The dev menu goes back to
self-deleting and the game is exactly as shipped.

## How it works

The menu is a GameObject named `Cheaters Failz (Back Doors)`. It carries a PlayMaker
action, `FurySdkDestroyIfCheatsNotEnabled`, whose `OnEnter` destroys the object on load
in retail builds (the "if cheats not enabled" check is a compile-time constant that was
optimized away, so there's no in-game toggle). The mod puts a Harmony prefix on that
`OnEnter` and skips it for this object, so it survives scene load. Nothing else about the
game is touched. By default the mod is filtered to keep only the cheat menu and let every
other dev object self-destruct as normal (keeping all of them crashes boot).

## Building from source

Source is in [`src/`](src). It's a standard MelonLoader IL2CPP mod targeting `net6`.
The `.csproj` references MelonLoader, Harmony, Il2CppInterop and the game's interop
assemblies straight from your Blue Prince install (edit `GameDir` if yours differs), then:

```
dotnet build -c Release
```

## Disclaimer

Not affiliated with or endorsed by the developers or publisher. This uses content the
developers left in the shipped game for their own testing. Provided as-is; use on your
own single-player copy at your own risk. Back up your saves.
