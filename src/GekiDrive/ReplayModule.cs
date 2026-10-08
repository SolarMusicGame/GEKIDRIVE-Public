using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx;
using HarmonyLib;
using MU3.Notes;

namespace GekiDrive
{
    internal static class ReplayModule
    {
        internal static bool Playing { get; private set; }
        internal static bool Prepared { get { return pending != null; } }
        internal static int PreparedStart { get { return pending == null ? 0 : (int)pending.StartMsec; } }
        internal static bool AutoCaptured { get { return playingTape != null && playingTape.Auto; } }
        private static ReplayTape recording, playingTape, pending;
        private static readonly List<ReplayEvent> input = new List<ReplayEvent>();
        private static int cursor, hold, trigger, flick;
        private static readonly int[] presses = new int[10];
        private static float baseFader, filteredFader, lastFrame = -1, lastSample = -1, lastEvent;
        private static bool sampling;
        private static readonly Action<NotesManager, int> SetSeed = (Action<NotesManager, int>)Delegate.CreateDelegate(typeof(Action<NotesManager, int>), AccessTools.PropertySetter(typeof(NotesManager), "RandomShellSeedBase"));
        internal static bool Injecting { get { return Playing && !sampling; } }
        internal static void Prepare()
        {
            pending = null;
            if (!ModuleConfig.ReplayPlayback.Value || string.IsNullOrEmpty(ModuleConfig.ReplayPath.Value)) return;
            try
            {
                var tape = ReplayCodec.Read(ModuleConfig.ReplayPath.Value);
                if (Training.Session.musicData == null || string.IsNullOrEmpty(Training.LoadedChartHash) || tape.MusicId != Training.Session.musicData.id || tape.Difficulty != (int)Training.Session.musicLevel || tape.GameHash != Plugin.SupportedHash || tape.ChartHash != Training.LoadedChartHash)
                    throw new InvalidDataException("Replay song/difficulty/game/chart fingerprint mismatch.");
                pending = tape;
                if (tape.StartMsec >= Training.Notes.getEndPlayMsec()) { pending = null; throw new InvalidDataException("Replay start is past the chart end."); }
                SetSeed(Training.Notes, tape.Seed);
            }
            catch (Exception e) { Training.Notice = "Replay rejected: " + e.Message; if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning(Training.Notice); }
        }
        internal static void Begin()
        {
            Finish(); playingTape = pending; pending = null;
            Playing = playingTape != null;
            cursor = hold = trigger = flick = 0; Array.Clear(presses, 0, presses.Length); lastFrame = lastSample = -1; lastEvent = 0;
            input.Clear();
            ReplaySkills.Reset();
            if (Playing)
            {
                foreach (var e in playingTape.Events) if (e.Kind == 1) input.Add(e);
                while (cursor < input.Count && input[cursor].Time < PracticeAudio.StartOffset)
                { var e = input[cursor++]; hold = e.Data & 1023; baseFader = e.A; filteredFader = e.B; }
                Training.Notice = "Replay playing; session results will not be saved.";
                return;
            }
            if (!ModuleConfig.RecordReplay.Value || Training.Session.musicData == null) return;
            try { if (string.IsNullOrEmpty(Training.LoadedChartHash)) throw new InvalidDataException("Loaded chart fingerprint unavailable."); recording = new ReplayTape { MusicId = Training.Session.musicData.id, Difficulty = (int)Training.Session.musicLevel, Seed = Training.Notes.RandomShellSeedBase, Auto = AutoMode.Enabled, StartMsec = PracticeAudio.StartOffset, ChartHash = Training.LoadedChartHash, GameHash = Plugin.SupportedHash }; }
            catch (Exception e) { recording = null; if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning("Replay record unavailable: " + e.Message); }
        }
        internal static void Event(byte kind, float time, float a, float b, float c, int data)
        {
            if (recording == null) return;
            if (recording.Events.Count >= ModuleConfig.MaxReplayEvents.Value) { Training.Notice = "Replay event limit reached; recording saved early."; Finish(); return; }
            // Native events and post-update samples can share a frame; clamp rounding drift.
            time = Math.Max(lastEvent, Math.Max(0, time)); lastEvent = time;
            recording.Events.Add(new ReplayEvent { Kind = kind, Time = time, A = a, B = b, C = c, Data = data });
        }
        internal static void Sample(GameDeviceManager device)
        {
            if (recording == null) return;
            float time = (float)Training.Position;
            if (time <= lastSample) return;
            lastSample = time;
            sampling = true;
            try
            {
                int mask = 0;
                for (int key = 0; key < 10; key++)
                {
                    if (device.isButtonHold((GameDeviceManager.GKey)key)) mask |= 1 << key;
                    if (device.isButton((GameDeviceManager.GKey)key)) mask |= 1 << (key + 10);
                }
                if (device.isFlick(false, 0)) mask |= 1 << 20;
                if (device.isFlick(true, 0)) mask |= 1 << 21;
                recording.Auto |= AutoMode.Enabled;
                Event(1, time, device.getBaseFader(0), device.getFader(0), Training.Notes.fieldState.playerJudge, mask);
            }
            finally { sampling = false; }
        }
        internal static void Advance()
        {
            float frame = Training.Notes.getCurrentFrame();
            if (frame == lastFrame) return;
            lastFrame = frame; trigger = flick = 0; Array.Clear(presses, 0, presses.Length);
            float time = Training.Notes.getCurrentMsec();
            while (cursor < input.Count && input[cursor].Time <= time + 0.01f)
            {
                var e = input[cursor++]; baseFader = e.A; filteredFader = e.B;
                hold = e.Data & 1023; trigger |= (e.Data >> 10) & 1023;
                flick |= (e.Data >> 20) & 3;
                for (int key = 0; key < 10; key++) if ((e.Data & (1 << (key + 10))) != 0) presses[key]++;
            }
        }
        internal static float Fader(bool filtered) { Advance(); return filtered ? filteredFader : baseFader; }
        internal static bool Key(int key, bool pressed) { Advance(); return key >= 0 && key < 10 && ((pressed ? trigger : hold) & (1 << key)) != 0; }
        internal static bool Flick(bool side) { Advance(); return (flick & (side ? 2 : 1)) != 0; }
        internal static bool TriggerInSet(GameDeviceManager.GKeySet set, bool consume)
        {
            Advance();
            for (int key = 0; key < 10; key++) if (presses[key] > 0 && Training.Engine.gameDeviceManager.isGKey((GameDeviceManager.GKey)key, set)) { if (consume) presses[key]--; return true; }
            return false;
        }
        internal static void Finish()
        {
            Playing = false; playingTape = null;
            if (recording == null) return;
            var tape = recording; recording = null;
            string directory = string.IsNullOrEmpty(ModuleConfig.ReplayDirectory.Value) ? Path.Combine(Paths.BepInExRootPath, "GEKIDRIVE/replays") : ModuleConfig.ReplayDirectory.Value;
            string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + tape.MusicId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".orp");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Directory.CreateDirectory(directory); ReplayCodec.Write(path, tape); if (Plugin.SharedLog != null) Plugin.SharedLog.LogInfo("Replay saved: " + path); }
                catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Replay save failed: " + e.Message); }
            });
        }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "getBaseFader")]
    internal static class ReplayBaseFader
    {
        [HarmonyPrefix] private static bool Prefix(ref float __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.Fader(false); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "getFader")]
    internal static class ReplayFader
    {
        [HarmonyPrefix] private static bool Prefix(ref float __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.Fader(true); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "isButtonHold", new Type[] { typeof(GameDeviceManager.GKey) })]
    internal static class ReplayHold
    {
        [HarmonyPrefix] private static bool Prefix(GameDeviceManager.GKey __0, ref bool __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.Key((int)__0, false); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "isButton", new Type[] { typeof(GameDeviceManager.GKey) })]
    internal static class ReplayPress
    {
        [HarmonyPrefix] private static bool Prefix(GameDeviceManager.GKey __0, ref bool __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.Key((int)__0, true); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "update")]
    internal static class ReplayInputCapture
    {
        [HarmonyPostfix] private static void Postfix(GameDeviceManager __instance) { if (Training.Active) ReplayModule.Sample(__instance); }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "isFlick")]
    internal static class ReplayFlick
    {
        [HarmonyPrefix] private static bool Prefix(bool __0, ref bool __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.Flick(__0); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "isButtonTrigNum")]
    internal static class ReplayTriggerCount
    {
        [HarmonyPrefix] private static bool Prefix(GameDeviceManager.GKeySet __0, ref bool __result) { if (!ReplayModule.Injecting) return true; __result = ReplayModule.TriggerInSet(__0, false); return false; }
    }
    [HarmonyPatch(typeof(GameDeviceManager), "decButtonTrigNum")]
    internal static class ReplayConsumeTrigger
    {
        [HarmonyPrefix] private static bool Prefix(GameDeviceManager.GKeySet __0) { if (!ReplayModule.Injecting) return true; ReplayModule.TriggerInSet(__0, true); return false; }
    }
    [HarmonyPatch(typeof(ShellNoteCore), "drawModel")]
    internal static class ReplayBullet
    {
        [HarmonyPostfix] private static void Postfix(ShellNoteCore __instance)
        {
            if (Training.Active && __instance.isDraw && ((int)Training.Notes.getCurrentFrame() % 3) == 0)
                ReplayModule.Event(3, Training.Notes.getCurrentMsec(), __instance.pos.x, __instance.pos.y, __instance.pos.z, __instance.GetHashCode());
        }
    }
}
