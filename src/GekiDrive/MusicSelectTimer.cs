using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MU3;

namespace GekiDrive
{
    // Scope to Select state; confirmation, matching, logout and dialogs retain their timers.
    internal static class MusicSelectTimer
    {
        internal static bool Selecting;
        public static float FilterDelta(float delta, SystemUI.Timer timer)
        {
            if (!Selecting || Plugin.PauseSelection == null || !Plugin.PauseSelection.Value) return delta;
            var ui = SystemUI.instance;
            return ui != null && ReferenceEquals(timer, ui.systemTimer) ? 0f : delta;
        }
    }

    [HarmonyPatch(typeof(Scene_32_PrePlayMusic_MusicSelect), "Enter_Select")]
    internal static class EnterSelect
    {
        [HarmonyPostfix] private static void Postfix() { MusicSelectTimer.Selecting = true; }
    }

    [HarmonyPatch(typeof(Scene_32_PrePlayMusic_MusicSelect), "Leave_Select")]
    internal static class LeaveSelect
    {
        [HarmonyPrefix] private static void Prefix() { MusicSelectTimer.Selecting = false; }
    }

    [HarmonyPatch(typeof(Scene_32_PrePlayMusic_MusicSelect), "OnDestroy")]
    internal static class DestroySelect
    {
        [HarmonyPrefix] private static void Prefix() { MusicSelectTimer.Selecting = false; }
    }

    [HarmonyPatch(typeof(SystemUI.Timer), "execute")]
    internal static class TimerExecute
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var output = new List<CodeInstruction>();
            var deltaGetter = AccessTools.PropertyGetter(typeof(UnityEngine.Time), "deltaTime");
            var filter = AccessTools.Method(typeof(MusicSelectTimer), "FilterDelta");
            int matches = 0;
            foreach (var instruction in source)
            {
                output.Add(instruction);
                if (instruction.Calls(deltaGetter))
                {
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(new CodeInstruction(OpCodes.Call, filter));
                    matches++;
                }
            }
            if (matches != 1) throw new InvalidOperationException("Timer signature changed: expected exactly one deltaTime call, found " + matches);
            return output;
        }
    }
}

