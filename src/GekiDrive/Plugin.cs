using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GekiDrive
{
    [BepInPlugin("org.gekidrive.ongeki.public", "GEKIDRIVE PUBLIC NO SAVE", "0.5.1")]
    [BepInIncompatibility("org.nageki.ongeki")]
    [BepInProcess("mu3.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> PauseSelection, AutoNotes, AutoBells, AutoAvoid, Theoretical, CacheAnalysis, GpConfirmEnabled, FollowTrack, MaxOverDamage;
        internal static ConfigEntry<KeyCode> GpConfirmKey;
        internal static bool GameHooksReady, XmlOptimized;
        internal static BepInEx.Logging.ManualLogSource SharedLog;
        private ConfigEntry<KeyCode> autoKey, frameKey;
        private ConfigEntry<bool> showFps, frameControl;
        private ConfigEntry<int> targetFps;
        private Harmony harmony;
        private readonly XmlLoadingFix xmlFix = new XmlLoadingFix();
        private float elapsed, fps;
        private int frames, originalFps, originalVsync;
        private bool appliedFrames;
        private GUIStyle style;
        public const string SupportedHash = "0766056f0bb6e273417be31aa9f266d71589cd5d78abd5f022173d47f04f0a4a";

        private void Awake()
        {
            SharedLog = Logger;
            ModuleConfig.Bind(Config);
            VideoOutput.Initialize();
            showFps = Config.Bind("Display", "ShowFps", true, "顯示 FPS / Show FPS overlay.");
            frameControl = Config.Bind("Display", "EnableFrameControl", false, "覆寫 Unity 幀率與 VSync；尚需遊戲內驗證 / Override frame rate and VSync.");
            targetFps = Config.Bind("Display", "TargetFps", 60, new ConfigDescription("目標 FPS / Target FPS", new AcceptableValueRange<int>(30, 240)));
            PauseSelection = Config.Bind("MusicSelect", "PauseTimer", false, "僅在選歌 Select 狀態暫停系統計時器 / Pause system timer only during music selection.");
            AutoNotes = Config.Bind("AutoPlay", "Enabled", false, "啟用自動模式，拉桿由 FollowTrack 設定控制 / Enable auto mode; FollowTrack controls automatic lever position.");
            AutoBells = Config.Bind("AutoPlay", "CollectBells", true, "自動模式於鈴鐺到達時收集 / Collect bells at their scheduled time in auto mode.");
            AutoAvoid = Config.Bind("AutoPlay", "AvoidBullets", true, "自動模式閃避彈幕與長條雷射傷害 / Dodge bullets and beam damage in auto mode.");
            var optimizeXml = Config.Bind("Performance", "DisableXmlSerializerCompilation", true, "使用 XML interpreter，避開本機不存在的 Mono 編譯器 / Use XML interpreter instead of invoking an unavailable Mono compiler.");
            Theoretical = Config.Bind("AutoPlay", "TheoreticalJudgments", true, "自動模式每個音符採最佳判定與零時間誤差 / Best judgment and zero timing error for each auto-mode note.");
            FollowTrack = Config.Bind("AutoPlay", "FollowTrack", true, "自動模式讓角色跟隨軌道中心 / Follow track center in auto mode.");
            MaxOverDamage = Config.Bind("AutoPlay", "MaxOverDamage", true, "自動模式攻擊 Boss 時填滿原生傷害階梯 / Fill native boss damage ladder in auto mode.");
            CacheAnalysis = Config.Bind("Performance", "CacheChartAnalysis", true, "快取譜面標頭分析，譜面變動時重建 / Cache chart header analysis; rebuild on chart changes.");
            GpConfirmEnabled = Config.Bind("GP", "KeyboardConfirm", true, "GP 購入畫面使用 Enter 確認目前選項 / Confirm current GP purchase selection with Enter.");
            GpConfirmKey = Config.Bind("GP", "ConfirmKey", KeyCode.Return, "GP 確認鍵 / GP confirmation key.");
            autoKey = Config.Bind("AutoPlay", "ToggleKey", KeyCode.F8, "自動判定切換鍵 / Auto notes toggle key.");
            frameKey = Config.Bind("Display", "ToggleKey", KeyCode.F7, "幀率控制切換鍵 / Frame control toggle key.");
            originalFps = Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount;
            string path = Path.Combine(Path.Combine(Application.dataPath, "Managed"), "Assembly-CSharp.dll");
            string hash;
            try { hash = ComputeHash(path); }
            catch (Exception e) { Logger.LogError("Cannot fingerprint game; game patches disabled: " + e.Message); return; }
            Logger.LogInfo("Assembly-CSharp SHA256: " + hash);
            if (!string.Equals(hash, SupportedHash, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("PUBLIC GUARD FAILED: unsupported game fingerprint. Exit the game; no score protection is installed.");
                return;
            }
            try { PublicScoreGuard.Install(); }
            catch (Exception e) { Logger.LogError("PUBLIC GUARD FAILED: exit the game. " + e); return; }
            if (optimizeXml.Value) XmlOptimized = xmlFix.Apply(Logger);
            harmony = new Harmony("org.gekidrive.ongeki.public.features");
            try
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
                GameHooksReady = true;
                Optional("cabinet", Cabinet.Initialize);
                Optional("OBS", OverlayServer.Configure);
                Optional("LED", LedOutput.Configure);
                Optional("video", VideoOutput.Configure);
                Logger.LogInfo("Known local 1.52 binary recognized. MusicSelect and AutoPlay hooks installed (including Beam damage avoidance).");
            }
            catch (Exception e)
            {
                GameHooksReady = false;
                harmony.UnpatchSelf();
                Logger.LogError("Hooks disabled after patch failure: " + e);
            }
        }

        internal static string ComputeHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private void Update()
        {
            if (GameHooksReady)
            {
                if (Input.GetKeyDown(ModuleConfig.ReloadConfig.Value)) { Config.Reload(); ModuleConfig.Normalize(); Optional("OBS", OverlayServer.Configure); Optional("LED", LedOutput.Configure); Optional("video", VideoOutput.Configure); }
                Training.Update(); Cabinet.Update(); Telemetry.Update(); VideoOutput.Update();
            }
            if (Input.GetKeyDown(autoKey.Value))
            {
                if (GameHooksReady) { AutoNotes.Value = !AutoNotes.Value; Logger.LogInfo("Auto mode: " + AutoNotes.Value); }
                else Logger.LogWarning("Auto notes unavailable: game hooks not ready.");
            }
            if (Input.GetKeyDown(frameKey.Value))
            {
                frameControl.Value = !frameControl.Value;
                Logger.LogInfo("Frame control: " + frameControl.Value + ", target " + targetFps.Value);
            }
            if (frameControl.Value)
            {
                Application.targetFrameRate = targetFps.Value;
                QualitySettings.vSyncCount = 0;
                appliedFrames = true;
            }
            else if (appliedFrames) RestoreFrames();
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed >= 0.5f) { fps = frames / elapsed; elapsed = 0; frames = 0; }
        }

        private void OnGUI()
        {
            if (GameHooksReady) Telemetry.Draw();
            GpConfirm.Draw();
            bool auto = GameHooksReady && AutoNotes != null && AutoNotes.Value;
            if (style == null) { style = new GUIStyle(GUI.skin.box); style.fontSize = 24; style.normal.textColor = Color.white; }
            style.normal.textColor = PublicScoreGuard.Ready ? Color.white : Color.red;
            string text = PublicScoreGuard.Ready ? "GEKIDRIVE PUBLIC NO SAVE" : "PUBLIC GUARD FAILED - EXIT GAME";
            if (showFps.Value) text += "  " + fps.ToString("F1") + " FPS";
            if (auto) text += "  AUTO [" + autoKey.Value + "]";
            if (auto && Theoretical.Value) text += " THEORY";
            if (auto && FollowTrack.Value) text += " TRACK";
            if (auto && MaxOverDamage.Value) text += " MAX OD";
            if (auto && AutoBells.Value) text += " BELL";
            if (auto && AutoAvoid.Value) text += " DODGE";
            GUI.Box(new Rect(12, 12, auto ? 1150 : 720, 42), text, style);
        }

        private void RestoreFrames()
        {
            Application.targetFrameRate = originalFps;
            QualitySettings.vSyncCount = originalVsync;
            appliedFrames = false;
        }

        private void OnDestroy()
        {
            OverlayServer.Stop(); LedOutput.Stop(); ReplayModule.Finish(); PracticeAudio.Dispose(); VideoOutput.Dispose();
            GameHooksReady = false;
            if (harmony != null) harmony.UnpatchSelf();
            PublicScoreGuard.Dispose();
            MusicSelectTimer.Selecting = false;
            xmlFix.Restore();
            XmlOptimized = false;
            SharedLog = null;
            if (appliedFrames) RestoreFrames();
        }

        private void LateUpdate() { if (GameHooksReady) VideoOutput.LateUpdate(); }

        private void Optional(string name, Action action) { try { action(); } catch (Exception e) { Logger.LogError(name + " module unavailable: " + e.Message); } }
    }
}








