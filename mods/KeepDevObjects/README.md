# Blue Prince — Enabling the Developer Cheat Menu

Blue Prince ships with the developers' own internal cheat/debug menu still in the
files. The game normally deletes it the instant a scene loads. This little mod
(`KeepDevObjects`) just stops that deletion, so the menu stays alive and you can use it.

Single-player only, no anti-cheat, so this is safe to run. That said:

- **Back up your save first.** Cheats can permanently change a save (spawn items, set
  flags, mark trophies). Copy your save folder somewhere before messing around.
- Consider using a throwaway/second profile if you just want to poke at it.

---

## What you need

1. **MelonLoader** installed on Blue Prince (IL2CPP, the auto-installer handles it).
   - Get it from the official MelonLoader releases, point it at `BLUE PRINCE.exe`, install.
   - Launch the game once so MelonLoader finishes setting itself up, then quit.
2. **`KeepDevObjects.dll`** (the mod file, shared alongside this doc).

---

## Install

1. Close the game.
2. Drop `KeepDevObjects.dll` into the game's `Mods` folder:
   ```
   ...\steamapps\common\Blue Prince\Mods\KeepDevObjects.dll
   ```
3. Launch the game.

**Check it loaded:** open `...\Blue Prince\MelonLoader\Latest.log` and look for:
```
[Keep_Dev_Objects] Patched FurySdkDestroyIfCheatsNotEnabled.OnEnter (filtered=True).
```
If that line is there, you're good.

---

## Turning the menu on in-game

Once you're in an actual run (not the main menu):

**Press your mouse's side button (mouse button 4 / the "forward" thumb button).**

You should see **`CF ACTIVATED`** pop up. That's it — the cheat system is now live.

> No 5-button mouse? See Troubleshooting at the bottom.

---

## Controls

These are the game's own dev hotkeys (straight from its built-in on-screen overlay):

**Cheat actions**
| Key | Does |
|-----|------|
| `F` | **Floorplan Selection** — pick and spawn ANY room in the game |
| `I` | **Item Selection** — give yourself ANY item |
| `R` | Redraw the current floorplan options |
| `T` | Turn / rotate floorplans |
| `L` | Luck Up |

**Free camera / cinematic**
| Key | Does |
|-----|------|
| `0` | Toggle free cam |
| `Space` | Toggle HUD on/off |
| `1` / `2` | FOV down / up |
| `3` / `4` | Move speed down / up |
| `5` / `6` | Camera tilt down / up |
| `8` / `9` | Lower / raise camera |
| `M` | Camera Recorder (keyframe camera-path tool) |

**The two big menus:**
- `F` opens a grid with a button for every room in the game (including the secret ones).
  Click one to force it as your next draft / spawn it.
- `I` opens a grid with every item (keys, tools, trinkets, special items). Click to
  add it to your inventory.

There is also a typed command console for resources (type codes like `GEM`, `GOLD`,
`KEY`, `STEP`, `LUCK`, `DAY`) but the `F`/`I` menus cover most of what people want.

---

## Troubleshooting

**"CF ACTIVATED" never shows / no side mouse button:**
The activation is hard-wired to mouse button 4, which not every mouse has. Options:
- Remap a spare mouse button to "mouse button 4/forward" in your mouse software, or
- Use **UnityExplorer** (another MelonLoader mod). With it installed, open the object
  browser, find `__SYSTEM > Cheaters Failz (Back Doors)`, and toggle its child UI
  objects active manually. More fiddly, but works with any mouse.

**Hotkeys do nothing after activating:**
Make sure you're in a run, not a menu, and that "CF ACTIVATED" actually appeared.
Some of the dev tools also expect the on-screen overlay to be up — if you don't see it,
try the publisher-mode toggle (press `P` then `L`).

**Game crashes on launch after adding the mod:**
Make sure you only added `KeepDevObjects.dll` and not any other loose files, and that
MelonLoader itself launches cleanly without the mod first.

**Want to undo it:**
Delete `KeepDevObjects.dll` from the `Mods` folder. The dev menu goes back to
self-deleting and the game is exactly as shipped.

---

*How it works, for the curious: the menu is a GameObject called `Cheaters Failz
(Back Doors)`. It carries a PlayMaker action, `FurySdkDestroyIfCheatsNotEnabled`, that
destroys the object on load in retail builds. The mod hooks that action's `OnEnter` and
skips it for this object, so it survives. Nothing else about the game is changed.*
