using BepInEx.Configuration;
using UnityEngine;

namespace GekiDrive
{
    internal static class ModuleConfig
    {
        internal static ConfigEntry<bool> Training, Loop, TimingBar, LeverAssist, Obs, RecordReplay, ReplayPlayback, FreeCam, HideHud, ExportFrames;
        internal static ConfigEntry<bool> LockGp, FreePlay, VirtualCredits, SerialLed, Landscape, SecondDisplay, PaymentBridge;
        internal static ConfigEntry<float> Speed, SegmentStart, SegmentEnd, PreRoll, TimingRange, CameraMoveSpeed;
        internal static ConfigEntry<int> GpValue, CreditValue, ObsPort, Baud, OutputWidth, OutputHeight, DisplayIndex, ExportWidth, ExportHeight, ExportFps, RetryButton, MaxReplayEvents, CoinUnits;
        internal static ConfigEntry<string> CustomChart, ReplayPath, ReplayDirectory, ComPort, WindowMode, MainCrop, LeftCrop, RightCrop, SecondaryCrop, CameraName, ExportDirectory, PaymentToken;
        internal static ConfigEntry<KeyCode> ToggleTraining, Retry, Rewind, MarkStart, MarkEnd, ReloadChart, Pause, Slower, Faster, ReloadConfig, LoadReplay, ToggleCamera, AddCoin;

        internal static void Bind(ConfigFile c)
        {
            Training = c.Bind("Training", "Enabled", false, "練習模式；練習／重放的當局不写入成績與獎勵。");
            Speed = Range(c, "Training", "PlaybackSpeed", 1f, 0.5f, 2f, "CRI DSP 保留音高變速，非數學無損；改速即同步譜面時鐘。");
            SegmentStart = Range(c, "Training", "SegmentStartSeconds", 0f, 0f, 3600f, "片段起點（秒）。");
            SegmentEnd = Range(c, "Training", "SegmentEndSeconds", 0f, 0f, 3600f, "片段終點；0 不設終點。");
            PreRoll = Range(c, "Training", "PreRollSeconds", 2f, 0f, 10f, "回到片段前的預備時間。");
            Loop = c.Bind("Training", "LoopSegment", false, "到終點自動重試片段。");
            RetryButton = Range(c, "Training", "CabinetRetryButton", -1, -1, 9, "實體按鍵編號（GameDeviceManager.GKey）；-1 關閉。設置後該鍵仍有原本功能。");
            CustomChart = c.Bind("Training", "CustomChartPath", "", "目前選中歌曲使用的自製 OGKR 絕對路徑；空白使用原曲。音訊沿用原曲。");
            ToggleTraining = c.Bind("Keys", "ToggleTraining", KeyCode.F9, "切換練習模式；本局曾練習即不保存。");
            Retry = c.Bind("Keys", "Retry", KeyCode.F10, "從歌曲開頭重試。");
            Rewind = c.Bind("Keys", "RewindSegment", KeyCode.Backspace, "回到片段起點含預備時間。");
            MarkStart = c.Bind("Keys", "MarkSegmentStart", KeyCode.Home, "以目前歌曲時間設片段起點。");
            MarkEnd = c.Bind("Keys", "MarkSegmentEnd", KeyCode.End, "設片段終點。");
            ReloadChart = c.Bind("Keys", "ReloadChart", KeyCode.F11, "重新讀取目前 OGKR 並重試片段。");
            Pause = c.Bind("Keys", "PracticePause", KeyCode.P, "練習暫停／恢復。");
            Slower = c.Bind("Keys", "Slower", KeyCode.Minus, "減速 0.05x。");
            Faster = c.Bind("Keys", "Faster", KeyCode.Equals, "加速 0.05x。");
            ReloadConfig = c.Bind("Keys", "ReloadConfig", KeyCode.F6, "重新讀取 config，重設 OBS／序列埠／顯示設定。");
            TimingBar = c.Bind("Visualizer", "HitTimingBar", true, "底部顯示每一擊 Early/Late 毫秒值；漏擊不顯示成時間偏差。");
            TimingRange = Range(c, "Visualizer", "TimingRangeMs", 100f, 10f, 300f, "判定條左右各顯示的毫秒範圍。");
            LeverAssist = c.Bind("Visualizer", "LeverTracking", true, "顯示實際拉桿、軌道中心、安全區間及未來 2 秒的中心路徑；中心僅是參考，非唯一解。");
            Obs = c.Bind("OBS", "Enabled", false, "localhost HTTP / WebSocket 伺服器，OBS Browser Source 使用 http://127.0.0.1:port/。");
            ObsPort = Range(c, "OBS", "Port", 8765, 1024, 65535, "HTTP 與 WebSocket 共用埠，綁定 loopback。");
            RecordReplay = c.Bind("Replay", "RecordEverySong", true, "自動輸出壓縮 .orp（按鍵、拉桿、判定、彈幕位置與技能事件）。");
            ReplayPlayback = c.Bind("Replay", "PlaybackEnabled", false, "下一首載入 ReplayPath；歌曲、難度與譜面 SHA256 必須匹配。");
            ReplayPath = c.Bind("Replay", "ReplayPath", "", ".orp 絕對路徑。");
            ReplayDirectory = c.Bind("Replay", "Directory", "", "空白存於 BepInEx/GEKIDRIVE/replays。");
            MaxReplayEvents = Range(c, "Replay", "MaxEvents", 1000000, 1000, 2000000, "單局事件上限，超過即停止錄製並告警。");
            LoadReplay = c.Bind("Keys", "ToggleReplayPlayback", KeyCode.F5, "切換 PlaybackEnabled；載入發生在下一次開始／重試。");
            FreeCam = c.Bind("Replay", "FreeCamera", false, "練習／重放時自由鏡頭；WASD/QE 移動，右鍵拖曳旋轉。");
            CameraMoveSpeed = Range(c, "Replay", "CameraMoveSpeed", 5f, 0.1f, 100f, "自由鏡頭移動速度。");
            CameraName = c.Bind("Replay", "CameraName", "", "空白使用原生 NotesManager 鏡頭；亦可指定場景 Camera 名稱。");
            ToggleCamera = c.Bind("Keys", "ToggleFreeCamera", KeyCode.F4, "切換自由鏡頭。");
            HideHud = c.Bind("Replay", "HideBattleHud", false, "練習／重放時隱藏 BattleUI；不隱藏譜面。");
            ExportFrames = c.Bind("Export", "Enabled", false, "練習／重放輸出無 BattleUI 的 PNG 序列，搭配 encode-video.ps1 產生 MP4（需 FFmpeg）。");
            ExportWidth = Range(c, "Export", "Width", 1920, 320, 7680, "輸出寬度。");
            ExportHeight = Range(c, "Export", "Height", 1080, 240, 7680, "輸出高度。");
            ExportFps = Range(c, "Export", "Fps", 60, 10, 120, "離線影格步長；重放／練習時鐘每輸出一張才前進。");
            ExportDirectory = c.Bind("Export", "Directory", "", "空白存於 BepInEx/GEKIDRIVE/exports。");
            LockGp = c.Bind("GP", "LockValue", false, "固定私人機台的 GP 數值。");
            GpValue = Range(c, "GP", "LockedValue", 900, 0, 999999, "GP 固定值。");
            FreePlay = c.Bind("Cabinet", "FreePlay", false, "私人機台遊戲費用免費模式；不向 AMDaemon 扣 credit。");
            VirtualCredits = c.Bind("Cabinet", "VirtualCredits", false, "本機虛擬 CREDIT 計數器；與原生 AMDaemon credit 分開。");
            CreditValue = Range(c, "Cabinet", "InitialCredits", 3, 0, 9999, "虛擬計數器初值（啟動時）。");
            CoinUnits = Range(c, "Cabinet", "CreditsPerPulse", 1, 1, 99, "虛擬投幣鍵或原生投幣通知每次增加的 CREDIT。");
            AddCoin = c.Bind("Keys", "AddVirtualCoin", KeyCode.Insert, "VirtualCredits 開啟時增加一個投幣 pulse。");
            PaymentBridge = c.Bind("Cabinet", "PaymentSignalBridge", false, "接受本機已驗證支付端 POST /credit 信號；需 OBS.Enabled 與非空 token。本模組不驗證 LINE Pay 訂單。");
            PaymentToken = c.Bind("Cabinet", "PaymentSignalToken", "", "POST Bearer token；請自行設定至少 24 字元，空白／過短拒絕接收信號。");
            SerialLed = c.Bind("LED", "SerialEnabled", false, "原生燈色轉發為 GD1 二進位序列協定，詳見 docs/led-protocol.md。");
            ComPort = c.Bind("LED", "Port", "COM3", "Arduino / Teensy 序列埠。");
            Baud = Range(c, "LED", "BaudRate", 115200, 9600, 1000000, "序列埠速率。");
            WindowMode = c.Bind("Video", "WindowMode", "Native", "Native / Windowed / Borderless。");
            OutputWidth = Range(c, "Video", "Width", 1920, 640, 7680, "視窗／主顯示寬度。");
            OutputHeight = Range(c, "Video", "Height", 1080, 480, 7680, "視窗／主顯示高度。");
            Landscape = c.Bind("Video", "LandscapeLayout", false, "將原生 portrait 相機輸出重組為中間譜面、兩側原生 UI 裁切區與即時資料；區域可在 config 校正。");
            MainCrop = c.Bind("Video", "TrackCrop", "0,0,1,0.72", "來源 UV x,y,w,h（左下原點），中央譜面裁切。");
            LeftCrop = c.Bind("Video", "LeftPanelCrop", "0,0.72,0.5,0.28", "左側角色／資料裁切。");
            RightCrop = c.Bind("Video", "RightPanelCrop", "0.5,0.72,0.5,0.28", "右側資料／按鍵提示裁切。");
            SecondDisplay = c.Bind("Video", "SecondDisplay", false, "將指定來源區域獨立呈現於另一顯示器。");
            DisplayIndex = Range(c, "Video", "SecondDisplayIndex", 1, 1, 7, "Unity Display 索引（主顯示為 0）。");
            SecondaryCrop = c.Bind("Video", "SecondDisplayCrop", "0,0.72,1,0.28", "第二顯示器來源 UV 區域（Boss／卡牌）。");
            Normalize();
        }

        internal static void Normalize()
        {
            Speed.Value = (float)PracticeClock.Clamp(Speed.Value);
            if (float.IsNaN(SegmentStart.Value) || float.IsInfinity(SegmentStart.Value)) SegmentStart.Value = 0;
            if (float.IsNaN(SegmentEnd.Value) || float.IsInfinity(SegmentEnd.Value)) SegmentEnd.Value = 0;
            if (float.IsNaN(PreRoll.Value) || float.IsInfinity(PreRoll.Value)) PreRoll.Value = 2;
            if (float.IsNaN(TimingRange.Value) || float.IsInfinity(TimingRange.Value)) TimingRange.Value = 100;
            if (float.IsNaN(CameraMoveSpeed.Value) || float.IsInfinity(CameraMoveSpeed.Value)) CameraMoveSpeed.Value = 5;
            if (WindowMode.Value != "Native" && WindowMode.Value != "Windowed" && WindowMode.Value != "Borderless") WindowMode.Value = "Native";
        }

        private static ConfigEntry<T> Range<T>(ConfigFile c, string section, string key, T value, T min, T max, string description) where T : System.IComparable
        { return c.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<T>(min, max))); }
    }
}
