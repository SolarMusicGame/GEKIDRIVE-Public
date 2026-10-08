using HarmonyLib;
using MU3.Notes;

namespace GekiDrive
{
    [HarmonyPatch(typeof(NotesManager), "isPlayerInArea")]
    internal static class AutoFieldArea
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (AutoMode.Enabled && Plugin.Theoretical.Value) __result = true;
        }
    }

    // Normalize each scheduled note's judgment before the native effects/score calculation.
    // Do not replace the session result or display a synthetic final score.
    [HarmonyPatch(typeof(NotesManager), "setResultEffectAndScore", new System.Type[] {
        typeof(Judge), typeof(Timing), typeof(MU3.Battle.AttackNoteType), typeof(Lanes),
        typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3), typeof(float) })]
    internal static class AutoPerfectJudgment
    {
        [HarmonyPrefix]
        private static void Prefix(ref Judge judge, ref Timing timing, ref float frameDiff)
        {
            if (!AutoMode.Enabled || !Plugin.Theoretical.Value) return;
            judge = Judge.Perfect;
            timing = Timing.Just;
            frameDiff = 0f;
        }
    }
}

