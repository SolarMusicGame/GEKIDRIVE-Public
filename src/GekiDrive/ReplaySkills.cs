using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MU3.Skill;

namespace GekiDrive
{
    [HarmonyPatch]
    internal static class ReplaySkills
    {
        private static readonly Dictionary<SkillStatus, int> counts = new Dictionary<SkillStatus, int>();
        internal static void Reset()
        {
            counts.Clear();
            if (Training.Engine == null) return;
            foreach (var card in Training.Engine.skillManager.cardStatusList)
                foreach (var skill in card.skillStatusList) counts[skill] = skill.count;
        }
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "calcTimingNote", "calcTimingBell", "calcTimingBullet", "calcTimingEvent" }) yield return AccessTools.Method(typeof(SkillManager), name);
        }
        [HarmonyPostfix]
        private static void Postfix(SkillManager __instance)
        {
            if (!Training.Active) return;
            foreach (var card in __instance.cardStatusList)
                for (int i = 0; i < card.skillStatusList.Count; i++)
                {
                    var skill = card.skillStatusList[i]; int previous;
                    counts.TryGetValue(skill, out previous); counts[skill] = skill.count;
                    if (skill.count > previous) ReplayModule.Event(4, Training.Notes.getCurrentMsec(), (int)card.deckPos, i, skill.count - previous, card.skillID);
                }
        }
    }
}
