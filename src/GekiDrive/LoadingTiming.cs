using System.Diagnostics;
using HarmonyLib;

namespace GekiDrive
{
    [HarmonyPatch(typeof(MU3.Data.DataManager), "initializeDevelop")]
    internal static class LoadingTiming
    {
        [HarmonyPrefix]
        private static void Prefix(out Stopwatch __state) { __state = Stopwatch.StartNew(); }

        [HarmonyPostfix]
        private static void Postfix(Stopwatch __state)
        {
            __state.Stop();
            if (Plugin.SharedLog != null)
                Plugin.SharedLog.LogInfo("Data initialization: " + __state.ElapsedMilliseconds + " ms (XML interpreter optimization: " + Plugin.XmlOptimized + ")");
        }
    }
}

