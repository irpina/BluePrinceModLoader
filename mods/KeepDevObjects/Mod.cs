using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppHutongGames.PlayMaker.Actions;

[assembly: MelonInfo(typeof(KeepDevObjects.Mod), "Keep Dev Objects", "1.2.0", "datamine")]
[assembly: MelonGame(null, null)]

namespace KeepDevObjects
{
    // 1) Stops the built-in dev "Cheaters Failz (Back Doors)" cheat tree from self-destructing
    //    on scene load (Harmony prefix on FurySdkDestroyIfCheatsNotEnabled.OnEnter, filtered).
    // 2) Auto-shows the cheat overlay so the menu appears just from having this mod enabled -
    //    no mouse button 4, no UnityExplorer needed. It activates only the overlay's disabled
    //    parent objects; pop-up menus (Floorplan Selection, Item add) stay closed until you
    //    press their hotkey.
    public class Mod : MelonMod
    {
        private const bool Filtered = true;
        private const bool AutoShowCheatMenu = true;

        private static readonly string[] KeepFilter =
        {
            "Cheaters Failz", "Back Doors", "publisher", "Floorplan Selection",
        };

        // Only the path that reveals the on-screen hotkey overlay: shortcuts -> cheat UI
        // (which carry Cheat Text, CAM controls and the R/T/L/I/F + freecam key handlers).
        // Deliberately NOT "cinematic cheats" (HUD-off / camera-lower) or "CheatsButtons"
        // (button panel) - activating those locks character movement.
        private static readonly string[] OverlayObjects =
        {
            "PL publisher mode", "shortcuts", "cheat UI",
        };

        private const bool LogSeenNames = true;
        private static readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        private int _frame;
        private GameObject _cheatRoot;
        private bool _announced;

        public override void OnInitializeMelon()
        {
            var target = AccessTools.Method(
                typeof(FurySdkDestroyIfCheatsNotEnabled),
                nameof(FurySdkDestroyIfCheatsNotEnabled.OnEnter));

            if (target == null)
            {
                LoggerInstance.Error("Could not find FurySdkDestroyIfCheatsNotEnabled.OnEnter - nothing patched.");
                return;
            }

            HarmonyInstance.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Mod), nameof(SkipDestroy))));

            LoggerInstance.Msg($"Patched FurySdkDestroyIfCheatsNotEnabled.OnEnter (filtered={Filtered}, autoShow={AutoShowCheatMenu}).");
        }

        public override void OnUpdate()
        {
            if (!AutoShowCheatMenu) return;
            if (++_frame < 30) return;   // ~twice a second
            _frame = 0;
            try { EnsureCheatOverlay(); }
            catch { _cheatRoot = null; } // scene change / destroyed -> re-find next tick
        }

        private void EnsureCheatOverlay()
        {
            if (_cheatRoot == null || !RootAlive())
            {
                _cheatRoot = FindByName("Cheaters Failz (Back Doors)");
                if (_cheatRoot == null) return; // not in scene yet
                if (!_announced) { LoggerInstance.Msg("Cheat menu found - auto-showing overlay."); _announced = true; }
            }

            foreach (var comp in _cheatRoot.GetComponentsInChildren(Il2CppType.Of<Transform>(), true))
            {
                var t = comp.TryCast<Transform>();
                if (t == null) continue;
                foreach (var name in OverlayObjects)
                {
                    if (t.name == name && !t.gameObject.activeSelf)
                        t.gameObject.SetActive(true);
                }
            }
        }

        private bool RootAlive()
        {
            try { _ = _cheatRoot.name; return true; }
            catch { return false; }
        }

        private static GameObject FindByName(string name)
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameObject>()))
            {
                var g = o.TryCast<GameObject>();
                if (g != null && g.name == name) return g;
            }
            return null;
        }

        private static bool SkipDestroy(FurySdkDestroyIfCheatsNotEnabled __instance)
        {
            string name = null;
            try { name = __instance.Fsm?.GameObject?.name; }
            catch { }

            if (LogSeenNames && !string.IsNullOrEmpty(name) && _seen.Add(name))
                Melon<Mod>.Logger.Msg($"[fury-destroy] owner: '{name}'");

            if (!Filtered) return false;

            if (!string.IsNullOrEmpty(name))
                foreach (var frag in KeepFilter)
                    if (name.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return false; // keep this one

            return true; // let it self-destruct as the game intends
        }
    }
}
