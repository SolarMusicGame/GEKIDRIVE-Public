using HarmonyLib;
using MU3.Notes;

namespace GekiDrive
{
    [HarmonyPatch(typeof(NotesManager), "updateFader")]
    internal static class AutoTrack
    {
        [HarmonyPostfix]
        private static void Postfix(NotesManager.FieldState[] ____fieldStateList, int ____currentFieldState, ref float ____frameFieldOff)
        {
            if (!AutoMode.Enabled || !Plugin.FollowTrack.Value || ____fieldStateList == null || ____currentFieldState < 0 || ____currentFieldState >= ____fieldStateList.Length) return;
            var state = ____fieldStateList[____currentFieldState];
            float center = state.area.posInC;
            if (float.IsNaN(center) || float.IsInfinity(center)) return;
            state.fader = center;
            state.faderFilt = center;
            state.playerJudge = center;
            state.playerDraw = center;
            ____fieldStateList[____currentFieldState] = state;
            ____frameFieldOff = 0f;
        }
    }

    [HarmonyPatch(typeof(MU3.Battle.EnemyManager), "attackPlayer")]
    internal static class AutoMaxOverDamage
    {
        private static readonly System.Func<MU3.Battle.EnemyManager, NotesManager.WaveDetailData> CurrentWave =
            (System.Func<MU3.Battle.EnemyManager, NotesManager.WaveDetailData>)System.Delegate.CreateDelegate(typeof(System.Func<MU3.Battle.EnemyManager, NotesManager.WaveDetailData>),
                AccessTools.Method(typeof(MU3.Battle.EnemyManager), "getWDD"));

        [HarmonyPrefix]
        private static void Prefix(MU3.Battle.EnemyManager __instance, ref int damage, MU3.Battle.EnemyManager.EnemyLifeList ____lifeList)
        {
            if (!AutoMode.Enabled || !Plugin.MaxOverDamage.Value || damage <= 0 || ____lifeList == null) return;
            var wave = CurrentWave(__instance);
            if (wave == null || wave.wave < 0 || wave.wave >= ____lifeList.Count) return;
            var enemy = ____lifeList[wave.wave];
            if (!enemy.isBoss || enemy.lifeTotal == null || enemy.lifeTotal.Count == 0) return;
            // Supply enough attack damage to fill the native boss-life ladder.
            // Native battle effects, rewards and overdamage computation still execute.
            int maximumLife = enemy.lifeTotal[enemy.lifeTotal.Count - 1];
            long remaining = (long)maximumLife - enemy.damage;
            damage = remaining <= 0 ? 0 : (int)System.Math.Min(int.MaxValue, remaining);
        }
    }
}



