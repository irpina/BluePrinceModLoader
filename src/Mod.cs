using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Il2CppHutongGames.PlayMaker.Actions;

[assembly: MelonInfo(typeof(KeepDevObjects.Mod), "Keep Dev Objects", "1.1.0", "datamine")]
[assembly: MelonGame(null, null)]

namespace KeepDevObjects
{
    // Neutralizes FurySdkDestroyIfCheatsNotEnabled.OnEnter for chosen objects so the
    // dev/cheat GameObjects survive scene load instead of self-destructing.
    //
    // IMPORTANT: keeping EVERY Fury-destroyed object crashes boot, because some of them
    // are meant to be destroyed as part of normal startup. So this defaults to a filter
    // and only keeps objects whose name matches KeepFilter. It also logs each distinct
    // owner name once, so you can see the real object names and tune the list.
    public class Mod : MelonMod
    {
        // true  = keep only objects whose name matches KeepFilter (safe, recommended)
        // false = keep everything (WILL crash boot on this build; diagnostic use only)
        private const bool Filtered = true;

        // Case-insensitive substring match against the owning GameObject's name.
        private static readonly string[] KeepFilter =
        {
            "Cheaters Failz",
            "Back Doors",
            "publisher",
            "Floorplan Selection",
        };

        // Log each distinct owner name we encounter exactly once.
        private const bool LogSeenNames = true;
        private static readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        public override void OnInitializeMelon()
        {
            var target = AccessTools.Method(
                typeof(FurySdkDestroyIfCheatsNotEnabled),
                nameof(FurySdkDestroyIfCheatsNotEnabled.OnEnter));

            if (target == null)
            {
                LoggerInstance.Error("Could not find FurySdkDestroyIfCheatsNotEnabled.OnEnter — nothing patched.");
                return;
            }

            HarmonyInstance.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Mod), nameof(SkipDestroy))));

            LoggerInstance.Msg($"Patched FurySdkDestroyIfCheatsNotEnabled.OnEnter (filtered={Filtered}).");
        }

        // Return false to skip the original OnEnter (object survives).
        // Return true to run the original (object self-destructs as normal).
        private static bool SkipDestroy(FurySdkDestroyIfCheatsNotEnabled __instance)
        {
            string name = null;
            try { name = __instance.Fsm?.GameObject?.name; }
            catch { /* interop hiccup — treat as unknown */ }

            if (LogSeenNames && !string.IsNullOrEmpty(name) && _seen.Add(name))
                Melon<Mod>.Logger.Msg($"[fury-destroy] owner: '{name}'");

            if (!Filtered)
                return false; // keep everything (diagnostic only — crashes boot here)

            if (!string.IsNullOrEmpty(name))
            {
                foreach (var frag in KeepFilter)
                {
                    if (name.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return false; // keep this one
                }
            }

            return true; // let it self-destruct as the game intends
        }
    }
}
