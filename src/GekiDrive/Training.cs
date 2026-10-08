using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using MU3.Battle;
using MU3.Game;
using MU3.Notes;
using MU3.Sequence;
using MU3.Sound;
using MU3.Util;
using UnityEngine;

namespace GekiDrive
{
    internal static class Training
    {
        internal static GameEngine Engine;
        internal static NotesManager Notes;
        internal static SessionInfo Session;
        internal static Camera NotesCamera;
        internal static bool Active, Touched, Paused;
        internal static bool RetryFailed;
        internal static string ChartPath = "", Notice = "";
        internal static string RecoveryPath;
        internal static string LoadedChartHash = "";
        private static byte[] validChart;
        internal static bool Controlled { get { return Active && (ModuleConfig.Training.Value || ReplayModule.Playing); } }
        private static readonly Stopwatch wall = Stopwatch.StartNew();
        private static readonly PracticeClock clock = new PracticeClock();
        private static float appliedSpeed = 1;
        private static double request = -1;
        private static bool reload;
        private static readonly System.Reflection.FieldInfo controls = AccessTools.Field(typeof(NotesManager), "_noteControlList");
        internal static double Now { get { return wall.Elapsed.TotalMilliseconds; } }
        internal static double Position { get { return VideoOutput.Exporting ? VideoOutput.ExportPosition : clock.Position(Now); } }
        internal static void ResumeClock(double position) { clock.Start(position, Now, appliedSpeed); clock.SetPaused(Paused, Now); }
        internal static void RememberChart()
        {
            if (RecoveryPath != null) return;
            try
            {
                if (File.Exists(ChartPath) && new FileInfo(ChartPath).Length <= 32 * 1024 * 1024)
                {
                    validChart = File.ReadAllBytes(ChartPath);
                    using (var sha = System.Security.Cryptography.SHA256.Create()) LoadedChartHash = BitConverter.ToString(sha.ComputeHash(validChart)).Replace("-", "").ToLowerInvariant();
                }
                else { validChart = null; LoadedChartHash = Plugin.ComputeHash(ChartPath); }
            }
            catch (Exception e) { validChart = null; LoadedChartHash = ""; if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning("Chart recovery snapshot unavailable: " + e.Message); }
        }
        private static bool ReloadSafely()
        {
            string previousPath = ChartPath;
            byte[] previous = validChart;
            try { if (Notes.reloadScore(false)) return true; }
            catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning("Custom chart rejected: " + e.Message); }
            if (previous == null) throw new InvalidDataException("Chart rejected and no valid recovery snapshot exists.");
            string directory = Path.Combine(BepInEx.Paths.CachePath, "GekiDrive"); Directory.CreateDirectory(directory);
            RecoveryPath = Path.Combine(directory, "recovery-" + Guid.NewGuid().ToString("N") + ".ogkr");
            try
            {
                File.WriteAllBytes(RecoveryPath, previous);
                if (!Notes.reloadScore(false)) throw new InvalidDataException("Native parser rejected the last valid chart snapshot.");
                return false;
            }
            finally { try { File.Delete(RecoveryPath); } catch { } RecoveryPath = null; ChartPath = previousPath; validChart = previous; }
        }

        internal static void Start(float gap)
        {
            Active = true; Paused = false; RetryFailed = false;
            Touched |= ModuleConfig.Training.Value;
            ReplayModule.Begin();
            Touched |= ReplayModule.Playing;
            if (Controlled && PracticeAudio.StartOffset > 0)
            {
                foreach (var control in (NoteControlList)controls.GetValue(Notes)) if (control.frame < PracticeAudio.StartOffset * 0.06) control.isEnd = true;
            }
            appliedSpeed = Controlled ? ModuleConfig.Speed.Value : 1;
            clock.Start(PracticeAudio.StartOffset + gap, Now, appliedSpeed);
            Telemetry.Reset();
            VideoOutput.BeginSong();
        }

        internal static void Stop()
        {
            ReplayModule.Finish();
            VideoOutput.EndSong();
            Active = false; Paused = false; request = -1;
            PracticeAudio.Restore();
        }

        internal static void Request(double milliseconds, bool reloadChart)
        {
            if (!Active || !ModuleConfig.Training.Value) { Notice = "Enable training (F9) during a song first."; return; }
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            if (Session.isTutorial || Session.isEventMode) { Notice = "Retry is disabled for tutorial/event play."; return; }
            request = Math.Max(0, Math.Min(milliseconds, Notes.getEndPlayMsec() - 1000));
            reload = reloadChart;
        }

        internal static bool Restart()
        {
            if (request < 0 || Notes == null || Engine == null) return false;
            double target = request; request = -1;
            Touched = true;
            ReplayModule.Finish();
            try
            {
                Notes.stopPlay();
                Singleton<GameSound>.instance.gameBGM.stop();
                Notes.reset();
                bool recovered = reload && !ReloadSafely();
                target = Math.Max(0, Math.Min(target, Notes.getEndPlayMsec() - 1000));
                Notes.clearNotes();
                // Do not instantly create all expired objects when starting at a later segment.
                var list = (NoteControlList)controls.GetValue(Notes);
                foreach (var control in list) if (control.frame < target * 0.06) control.isEnd = true;
                Engine.counters.reset();
                Engine.skillManager.init(Session);
                Engine.enemyManager.initialize();
                Engine.enemyManager.battleStart();
                PracticeAudio.RequestOffset = (int)target;
                Singleton<GameSound>.instance.gameBGM.playMusic(Session.musicData, (int)target);
                Notes.startPlay(0);
                Notice = (recovered ? "Chart rejected; restored last valid chart. Restarted at " : "Restarted at ") + (target / 1000).ToString("F2") + " s";
                return true;
            }
            catch (Exception e)
            {
                Notice = "Retry failed: " + e.Message;
                if (Plugin.SharedLog != null) Plugin.SharedLog.LogError(Notice);
                // Keep the partial run non-persistent and stop the clock instead of hiding a parse failure.
                Active = false;
                RetryFailed = true;
                return false;
            }
            finally { PracticeAudio.RequestOffset = -1; reload = false; }
        }

        internal static void Update()
        {
            if (Input.GetKeyDown(ModuleConfig.ToggleTraining.Value))
            {
                ModuleConfig.Training.Value = !ModuleConfig.Training.Value;
                if (Active && ModuleConfig.Training.Value)
                {
                    Touched = true;
                    clock.Start(Notes.getCurrentMsec(), Now, ModuleConfig.Speed.Value);
                    Request(Notes.getCurrentMsec(), false);
                }
                else if (Active && Touched && !ReplayModule.Playing)
                {
                    // Stay controlled through this song to keep the existing audio/notes timeline continuous.
                    ModuleConfig.Training.Value = true;
                    Notice = "Training stays active until this song ends; disable it after the result.";
                }
            }
            if (Input.GetKeyDown(ModuleConfig.LoadReplay.Value)) ModuleConfig.ReplayPlayback.Value = !ModuleConfig.ReplayPlayback.Value;
            if (Input.GetKeyDown(ModuleConfig.ToggleCamera.Value)) ModuleConfig.FreeCam.Value = !ModuleConfig.FreeCam.Value;
            if (!Active || Notes == null) return;
            if (Touched && !ReplayModule.Playing && !ModuleConfig.Training.Value)
            { ModuleConfig.Training.Value = true; Notice = "Disable training after this song ends to preserve the timeline."; }
            if (Controlled)
            {
                Touched = true;
                if (!PracticeAudio.Ready) ModuleConfig.Speed.Value = 1;
                if (Input.GetKeyDown(ModuleConfig.Pause.Value))
                {
                    Paused = !Paused; clock.SetPaused(Paused, Now); Notes.setPause(Paused); PracticeAudio.SetPaused(Paused);
                }
                if (Input.GetKeyDown(ModuleConfig.Slower.Value)) ModuleConfig.Speed.Value = Mathf.Max(0.5f, ModuleConfig.Speed.Value - 0.05f);
                if (Input.GetKeyDown(ModuleConfig.Faster.Value)) ModuleConfig.Speed.Value = Mathf.Min(2, ModuleConfig.Speed.Value + 0.05f);
                if (Math.Abs(appliedSpeed - ModuleConfig.Speed.Value) > 0.001)
                { clock.SetSpeed(ModuleConfig.Speed.Value, Now); appliedSpeed = ModuleConfig.Speed.Value; PracticeAudio.Apply(); }
            }
            if (!ModuleConfig.Training.Value) return;
            if (Input.GetKeyDown(ModuleConfig.Retry.Value) || (ModuleConfig.RetryButton.Value >= 0 && Engine.gameDeviceManager.isButton((GameDeviceManager.GKey)ModuleConfig.RetryButton.Value))) Request(0, false);
            if (Input.GetKeyDown(ModuleConfig.Rewind.Value)) Request(Math.Max(0, ModuleConfig.SegmentStart.Value - ModuleConfig.PreRoll.Value) * 1000, false);
            if (Input.GetKeyDown(ModuleConfig.ReloadChart.Value)) Request(Math.Max(0, ModuleConfig.SegmentStart.Value - ModuleConfig.PreRoll.Value) * 1000, true);
            if (Input.GetKeyDown(ModuleConfig.MarkStart.Value)) ModuleConfig.SegmentStart.Value = Notes.getCurrentMsec() / 1000;
            if (Input.GetKeyDown(ModuleConfig.MarkEnd.Value)) ModuleConfig.SegmentEnd.Value = Notes.getCurrentMsec() / 1000;
            if (ModuleConfig.Loop.Value && ModuleConfig.SegmentEnd.Value > ModuleConfig.SegmentStart.Value && Notes.getCurrentMsec() >= ModuleConfig.SegmentEnd.Value * 1000)
                Request(Math.Max(0, ModuleConfig.SegmentStart.Value - ModuleConfig.PreRoll.Value) * 1000, false);
        }
    }

    internal static class PracticeAudio
    {
        internal static bool Starting;
        internal static int StartOffset, RequestOffset = -1;
        internal static CriAtomExPlayer Player;
        internal static bool Ready;
        private static CriAtomExStandardVoicePool pool;
        internal static void Configure(CriAtomExPlayer player)
        {
            Player = player;
            try
            {
                if (pool == null) { pool = new CriAtomExStandardVoicePool(4, 2, 192000, true, 0x47444); pool.AttachDspTimeStretch(); }
                player.SetVoicePoolIdentifier(pool.identifier);
                player.SetPitch(0);
                player.SetDspTimeStretchRatio(1 / ModuleConfig.Speed.Value);
                Ready = true;
            }
            catch (Exception e)
            {
                ModuleConfig.Speed.Value = 1;
                Ready = false;
                if (pool != null) { try { pool.Dispose(); } catch { } pool = null; }
                player.SetVoicePoolIdentifier(0);
                Training.Notice = "Time-stretch unavailable; using 1x: " + e.Message;
                if (Plugin.SharedLog != null) Plugin.SharedLog.LogError(Training.Notice);
            }
        }
        internal static void Apply()
        {
            if (Player == null || !Ready) return;
            Player.SetDspTimeStretchRatio(1 / ModuleConfig.Speed.Value); Player.UpdateAll();
        }
        internal static void SetPaused(bool paused) { if (Player != null) Player.Pause(paused); }
        internal static void Restore() { if (Player != null) { if (Ready) Player.SetDspTimeStretchRatio(1); Player.SetVolume(1); Player.UpdateAll(); Player = null; } Ready = false; }
        internal static void Dispose() { Restore(); if (pool != null) { pool.Dispose(); pool = null; } }
    }

    [HarmonyPatch(typeof(GameEngine), "initialize")]
    internal static class TrainingSession
    {
        [HarmonyPostfix]
        private static void Postfix(GameEngine __instance, SessionInfo sessionInfo)
        { Training.Engine = __instance; Training.Session = sessionInfo; Training.Touched = false; }
    }
    [HarmonyPatch(typeof(NotesManager), "initialize")]
    internal static class TrainingNotes
    {
        [HarmonyPostfix]
        private static void Postfix(NotesManager __instance, Camera ____camera) { Training.Notes = __instance; Training.NotesCamera = ____camera; }
    }
    [HarmonyPatch(typeof(NotesManager), "startPlay")]
    internal static class TrainingStart
    {
        [HarmonyPostfix]
        private static void Postfix(float msecStartGap) { Training.Start(msecStartGap); }
    }
    [HarmonyPatch(typeof(NotesManager), "stopPlay")]
    internal static class TrainingStop
    {
        [HarmonyPostfix]
        private static void Postfix() { Training.Stop(); }
    }
    [HarmonyPatch(typeof(NotesManager), "progressFrameAndFrameReal")]
    internal static class TrainingTime
    {
        [HarmonyPrefix]
        private static bool Prefix(ref float ____frame, ref float ____frameReal)
        {
            if (!Training.Controlled) return true;
            ____frame = ____frameReal = (float)(Training.Position * 0.06);
            return false;
        }
    }
    [HarmonyPatch(typeof(PlayMusic), "Execute_Play")]
    internal static class TrainingRetry
    {
        private static readonly Func<PlayMusic, bool> Party = (Func<PlayMusic, bool>)Delegate.CreateDelegate(typeof(Func<PlayMusic, bool>), AccessTools.Method(typeof(PlayMusic), "isPartyPlay"));
        [HarmonyPrefix]
        private static bool Prefix(PlayMusic __instance, ref bool ____isForceEndBattle)
        {
            if (Party(__instance)) return true;
            bool restarted = Training.Restart();
            if (Training.RetryFailed) { Training.RetryFailed = false; ____isForceEndBattle = true; return true; }
            if (!restarted) return true;
            ____isForceEndBattle = false;
            return false;
        }
    }
    [HarmonyPatch(typeof(GameEngine), "applyResultToUserData")]
    internal static class TrainingSaveGuard
    {
        [HarmonyPrefix]
        private static bool Prefix() { return false; }
    }
    [HarmonyPatch(typeof(NotesManager), "getOgkrPath")]
    internal static class CustomChartPath
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            if (Training.RecoveryPath != null) __result = Training.RecoveryPath;
            else if (ModuleConfig.Training.Value && !string.IsNullOrEmpty(ModuleConfig.CustomChart.Value))
            {
                string path = ModuleConfig.CustomChart.Value;
                if (Path.IsPathRooted(path) && File.Exists(path) && string.Equals(Path.GetExtension(path), ".ogkr", StringComparison.OrdinalIgnoreCase)) __result = path;
                else Training.Notice = "Custom chart must be an existing absolute .ogkr path; using original chart.";
            }
            Training.ChartPath = __result;
        }
    }
    [HarmonyPatch(typeof(NotesManager), "loadScore")]
    internal static class ChartRecoverySnapshot
    {
        [HarmonyPostfix]
        private static void Postfix(bool __result) { if (__result) Training.RememberChart(); }
    }
    [HarmonyPatch(typeof(NotesManager), "update")]
    internal static class ExportFrameReady
    {
        [HarmonyPostfix]
        private static void Postfix() { VideoOutput.MarkFrameReady(); }
    }
    [HarmonyPatch(typeof(SoundManager), "playMusic")]
    internal static class PracticeMusic
    {
        [HarmonyPrefix]
        private static void Prefix(ref int __1)
        {
            ReplayModule.Prepare();
            PracticeAudio.Starting = ModuleConfig.Training.Value || ReplayModule.Prepared;
            if (PracticeAudio.RequestOffset >= 0) __1 = PracticeAudio.RequestOffset;
            else if (ReplayModule.Prepared) __1 = ReplayModule.PreparedStart;
            PracticeAudio.StartOffset = Math.Max(0, __1);
        }
        [HarmonyFinalizer]
        private static void Finalizer() { PracticeAudio.Starting = false; }
    }
    [HarmonyPatch(typeof(SoundPlayer), "play")]
    internal static class PracticeMusicPlayer
    {
        [HarmonyPrefix]
        private static void Prefix(CriAtomExPlayer ____player) { if (PracticeAudio.Starting) PracticeAudio.Configure(____player); }
    }
}
