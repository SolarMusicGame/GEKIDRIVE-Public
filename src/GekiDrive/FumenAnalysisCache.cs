using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using MU3.Data;
using MU3.DataStudio;
using MusicData = MU3.Data.MusicData;

namespace GekiDrive
{
    internal sealed class CacheContext
    {
        internal string Fingerprint, Path;
        internal HashSet<int> Keys;
        internal bool Hit, Migrate;
        internal Stopwatch Clock;
    }

    [HarmonyPatch(typeof(DataManager), "makeFumenAnalysisDataList")]
    internal static class FumenAnalysisCache
    {
        [HarmonyPrefix]
        private static bool Prefix(DataManager __instance, ref Dictionary<int, FumenAnalysisData> ____fumenAnalysisData, out CacheContext __state)
        {
            __state = null;
            if (!Plugin.CacheAnalysis.Value) return true;
            try
            {
                var context = new CacheContext { Keys = new HashSet<int>(), Clock = Stopwatch.StartNew(), Path = System.IO.Path.Combine(System.IO.Path.Combine(Paths.CachePath, "GekiDrive"), "fumen-analysis-v2.bin") };
                using (var buffer = new MemoryStream())
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8))
                {
                    writer.Write(Plugin.SupportedHash);
                    var music = new List<MusicData>(__instance.allMusicData);
                    music.Sort(delegate(MusicData a, MusicData b) { return a.id.CompareTo(b.id); });
                    foreach (var item in music)
                    {
                        writer.Write(item.id); writer.Write(item.isLunatic);
                        for (int difficulty = 0; difficulty < 5; difficulty++)
                        {
                            context.Keys.Add(item.id * 100 + DataManager.diffID((FumenDifficulty)difficulty));
                            if ((item.isLunatic && difficulty != 4) || (!item.isLunatic && difficulty == 4)) continue;
                            string path = __instance.getOgkrPath(item.id, (FumenDifficulty)difficulty);
                            if (string.IsNullOrEmpty(path)) { writer.Write(""); continue; }
                            var file = new FileInfo(path);
                            writer.Write(file.FullName); writer.Write(file.Exists);
                            if (file.Exists) { writer.Write(file.Length); writer.Write(file.LastWriteTimeUtc.Ticks); }
                        }
                    }
                    writer.Flush();
                    context.Fingerprint = AnalysisCacheStore.Hash(buffer.ToArray());
                }
                __state = context;
                List<AnalysisRecord> records;
                string source = context.Path;
                if (!File.Exists(source))
                {
                    string legacy = System.IO.Path.Combine(System.IO.Path.Combine(Paths.CachePath, "Nageki"), "fumen-analysis-v2.bin");
                    if (File.Exists(legacy)) source = legacy;
                }
                if (!AnalysisCacheStore.TryRead(source, context.Fingerprint, context.Keys, out records)) return true;
                context.Migrate = !string.Equals(source, context.Path, StringComparison.OrdinalIgnoreCase);
                var restored = new Dictionary<int, FumenAnalysisData>(records.Count);
                foreach (var record in records)
                    restored.Add(record.Key, new FumenAnalysisData { isExist = record.Exists, bpm = record.Bpm, platinumScoreMax = record.Platinum, notesDesignerName = record.Designer });
                ____fumenAnalysisData = restored;
                context.Hit = true;
                return false;
            }
            catch (Exception e)
            {
                __state = null;
                Plugin.SharedLog.LogWarning("Chart cache skipped; using original loader: " + e.Message);
                return true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Dictionary<int, FumenAnalysisData> ____fumenAnalysisData, CacheContext __state)
        {
            if (__state == null) return;
            __state.Clock.Stop();
            Plugin.SharedLog.LogInfo("Chart header analysis: " + __state.Clock.ElapsedMilliseconds + " ms, cache " + (__state.Hit ? "HIT" : "MISS"));
            if ((__state.Hit && !__state.Migrate) || ____fumenAnalysisData == null || ____fumenAnalysisData.Count != __state.Keys.Count) return;
            try
            {
                var records = new List<AnalysisRecord>(____fumenAnalysisData.Count);
                foreach (var entry in ____fumenAnalysisData)
                {
                    if (!__state.Keys.Contains(entry.Key) || entry.Value == null) return;
                    records.Add(new AnalysisRecord { Key = entry.Key, Exists = entry.Value.isExist, Bpm = entry.Value.bpm, Platinum = entry.Value.platinumScoreMax, Designer = entry.Value.notesDesignerName });
                }
                AnalysisCacheStore.Write(__state.Path, __state.Fingerprint, records);
                Plugin.SharedLog.LogInfo("Chart analysis cache saved/migrated: " + records.Count + " records. Next unchanged startup can reuse it.");
            }
            catch (Exception e) { Plugin.SharedLog.LogWarning("Chart cache write skipped: " + e.Message); }
        }
    }
}



