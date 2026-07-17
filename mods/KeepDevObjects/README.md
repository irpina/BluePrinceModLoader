# Keep Dev Objects (Cheat Menu)

Enables Blue Prince's built-in developer cheat menu. The retail build runs a script that
deletes the menu (`Cheaters Failz (Back Doors)`) every time a scene loads; this mod
intercepts that deletion and then switches the on-screen overlay on, so the menu appears
just from having the mod installed — no key combo needed.

## Just want the mod? (drop-in)

You don't need the mod loader for this. If you already have **MelonLoader** on Blue Prince:

1. Download **[`KeepDevObjects.dll`](KeepDevObjects.dll)** from this folder.
2. Drop it into `…\steamapps\common\Blue Prince\Mods\`.
3. Launch the game and get into a run — the cheat overlay appears on its own.

(No MelonLoader yet? The [mod loader](../../) installs it for you and manages this mod.)

## Controls

On-screen hotkeys (from the game's own overlay):

| Key | Action | | Key | Action |
|-----|--------|-|-----|--------|
| `F` | Floorplan Selection (spawn any room) | | `0` | Free cam |
| `I` | Item Selection (give any item) | | `1`/`2` | FOV down/up |
| `R` | Redraw floorplans | | `3`/`4` | Move speed down/up |
| `L` | Luck Up | | `5`/`6` | Camera tilt |
| | | | `M` | Camera recorder |

Mod hotkey (added by KeepDevObjects itself, not the game):

| Key | Action |
|-----|--------|
| `F8` | Toggle the forced overlay off/on. The mod normally keeps the hotkey overlay visible at all times; press `F8` to hide it (and its key handlers), press again to bring it back. |

## Notes

- Single-player only, no anti-cheat. It can spawn items and set progress flags, so it
  **spoils content and can permanently change a save** — back up your save first (or use a
  throwaway profile).
- To remove it, delete `KeepDevObjects.dll` from `Mods\`. The dev menu goes back to
  self-deleting and the game is exactly as shipped.

## How it works

The menu is a GameObject named `Cheaters Failz (Back Doors)` carrying a PlayMaker action,
`FurySdkDestroyIfCheatsNotEnabled`, whose `OnEnter` calls `Object.Destroy()` on it — and
Blue Prince reloads scenes often (every new day), so that fires repeatedly. The mod puts a
Harmony prefix on that `OnEnter` and skips it for this object, so the menu survives each
reload. It then activates the overlay's disabled parent objects (`PL publisher mode`,
`shortcuts`, `cheat UI`) so the hotkey bar shows without locking movement. Nothing else
about the game is changed.

Source: [`Mod.cs`](Mod.cs) · built as a MelonLoader IL2CPP mod (`net6`).
