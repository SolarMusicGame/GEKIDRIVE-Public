using System;
using HarmonyLib;
using MU3.Notes;

namespace GekiDrive
{
    internal static class AutoMode
    {
        internal static bool Enabled { get { return Plugin.GameHooksReady && Plugin.AutoNotes != null && (ReplayModule.Playing ? ReplayModule.AutoCaptured : Plugin.AutoNotes.Value); } }
    }

    [HarmonyPatch(typeof(NotesManager), "isAutoPlay")]
    internal static class AutoPlay
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (AutoMode.Enabled) __result = true;
        }
    }

    [HarmonyPatch(typeof(BellNoteCoreBase), "checkBellHit")]
    internal static class AutoBell
    {
        // Cache an open delegate once; no per-note reflection or object allocations.
        private static readonly Func<BellNoteCoreBase, NotesManager> Manager =
            (Func<BellNoteCoreBase, NotesManager>)Delegate.CreateDelegate(typeof(Func<BellNoteCoreBase, NotesManager>),
                AccessTools.PropertyGetter(typeof(BellNoteCoreBase), "ntMgr"));

        [HarmonyPrefix]
        private static bool Prefix(BellNoteCoreBase __instance, ref BellNoteCoreBase.Result __result)
        {
            if (!AutoMode.Enabled || !Plugin.AutoBells.Value) return true;
            if (__instance.isJudged) { __result = __instance.result; return false; }
            var manager = Manager(__instance);
            if (manager == null) return true;
            __result = manager.getCurrentFrame() >= __instance.param.frame
                ? BellNoteCoreBase.Result.Get : BellNoteCoreBase.Result.None;
            return false;
        }
    }

    [HarmonyPatch(typeof(ShellNoteCore), "checkHit")]
    internal static class AutoDodge
    {
        private static readonly Func<ShellNoteCore, NotesManager> Manager =
            (Func<ShellNoteCore, NotesManager>)Delegate.CreateDelegate(typeof(Func<ShellNoteCore, NotesManager>),
                AccessTools.PropertyGetter(typeof(ShellNoteCore), "ntMgr"));

        [HarmonyPrefix]
        private static bool Prefix(ShellNoteCore __instance, ref ShellNoteCore.Result __result)
        {
            if (!AutoMode.Enabled || !Plugin.AutoAvoid.Value) return true;
            if (__instance.isJudged) { __result = __instance.result; return false; }
            var manager = Manager(__instance);
            if (manager == null) return true;
            __result = manager.getCurrentFrame() >= __instance.param.frameHit
                ? ShellNoteCore.Result.Dodge : ShellNoteCore.Result.None;
            return false;
        }
    }

    // BeamNoteCore checks continuous/swept collision independently of ShellNoteCore.
    // Keep its lifecycle, animation and sound, but reject the resulting Beam hit
    // before native hit counters, skills, damage effects and HP loss are applied.
    [HarmonyPatch(typeof(NotesManager), "damagePlayer", new Type[] { typeof(Damage), typeof(int) })]
    internal static class AutoBeamDodge
    {
        [HarmonyPrefix]
        private static bool Prefix(Damage type)
        {
            return type != Damage.Beam || !AutoMode.Enabled || !Plugin.AutoAvoid.Value;
        }
    }
}

