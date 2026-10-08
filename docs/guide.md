# 完整使用教學：GEKIDRIVE Public 0.5.1

> **AI 生成・不承諾維護**：本教學及本專案程式皆由 AI 生成與整理，按現況分享。發布者不承諾維護、修復、更新、相容性或技術支援，Issue／PR 可能不處理。使用前請自行評估、備份與驗證。完整聲明見 [AI-NOTICE.md](../AI-NOTICE.md)。

## 1. 準備環境

本模組不是遊戲本體。需要既有 ongeki 1.52、Unity 5.6.4、64-bit BepInEx 5.4.23.2 與能正常啟動的環境；不包含遊戲 DLL、歌曲或譜面。
只有 README 列出的 Assembly-CSharp SHA256 已完成相容性檢查。資料夾名稱含 1.50.00 不代表实际 DLL 版本是 1.50。
第一次測試先保留原來的插件備份，其他模組可能與 Harmony hook 衝突。公開版不保證帳號進度保存，請先用練習帳號測試。

## 2. 安裝、更新、移除

1. 關閉遊戲，下載 `GEKIDRIVE-Public-0.5.1.zip`，解壓縮。
2. 找到 `mu3.exe` 所在的 `package` 目錄；確認同層已有 `BepInEx`。
3. 建立 `BepInEx/plugins/GekiDrive.Public/`，把 `GekiDrive.Public.dll` 放進去。
4. 移除 plugins 中的 `GekiDrive.dll`、`Nageki.dll`、`GekiDrive.AutoZero.dll` 與重複公開版 DLL。勿和其他版本混用。
5. 以原本可用的啟動方式開遊戲，先檢查 `BepInEx/LogOutput.log` 確認公開版已載入且沒有初始化錯誤，再開始測試。
6. 第一次啟動會生成 `BepInEx/config/org.gekidrive.ongeki.public.cfg`。不必手動建立空 cfg。

更新時關閉遊戲、替換公開版 DLL，保留公開版 cfg。移除時關閉遊戲、移走 DLL；cfg 與輸出檔可以自行保留。
`PUBLIC GUARD FAILED` 或沒看到公開版載入資訊時請退出遊戲，並查看日誌中的初始化錯誤。

## 3. config 與快捷鍵

使用文字編輯器打開公開版 cfg。每一項附有用途、範圍与預設值；布林值用 `true` / `false`，秒與速度可用小數，Windows 路徑不用加引號。
修改後保存檔案，在遊戲內按 F6 重新讀取；OBS、序列埠與顯示設定會重建。重放／影片輸出的起始設定在下一次開始歌曲或重試生效。
範例 `org.gekidrive.ongeki.public.example.cfg` 是完整預設參考。已有 cfg 請逐項合併，以免覆蓋個人設定。

| 預設按鍵 | 功能 | config 項目 |
| --- | --- | --- |
| F4 | 自由鏡頭開關 | Keys.ToggleFreeCamera |
| F5 | 下一次開始歌曲載入重放開關 | Keys.ToggleReplayPlayback |
| F6 | 重新讀取 cfg | Keys.ReloadConfig |
| F7 | 幀率控制開關 | Display.ToggleKey |
| F8 | 自動遊玩開關 | AutoPlay.ToggleKey |
| F9 | 練習模式開關 | Keys.ToggleTraining |
| F10 | 從頭重試 | Keys.Retry |
| F11 | OGKR 重載並重試片段 | Keys.ReloadChart |
| Home / End | 標記片段起點／終點 | Keys.MarkSegmentStart / MarkSegmentEnd |
| Backspace | 回到片段起點含 pre-roll | Keys.RewindSegment |
| P | 練習暫停／恢復 | Keys.PracticePause |
| - / = | 減速／加速 0.05x | Keys.Slower / Faster |
| Insert | 虛擬投幣 | Keys.AddVirtualCoin |
| 左右鍵、Enter | GP 方案選取／確認 | GP.KeyboardConfirm / ConfirmKey |

設定中的按鍵名稱使用 Unity KeyCode，例如 `F10`、`Backspace`、`Return`。原生遊戲仍會收到這些鍵，請避免和自己的輸入映射衝突。

## 4. 自動遊玩、幀率與 GP

F8 或 `[AutoPlay] Enabled=true` 啟用自動。FollowTrack 跟隨軌道中心；TheoreticalJudgments 送入最佳判定；CollectBells 收鈴鐺；AvoidBullets 攔截彈幕與長條雷射傷害；MaxOverDamage 填滿原生 Boss 傷害階梯。
上述子項只有主開關啟用時介入。關閉不會撤銷本局已得分數與傷害。公開版遊玩結果不會保留到帳號。

預設不鎖 60 FPS。要鎖 60：`[Display] EnableFrameControl=true`、`TargetFps=60`，按 F6；F7 可切換。支援 30–240 的目標值，實際 FPS 仍受硬體與遊戲負載影響，高 FPS 對遊戲同步需自行實測。
GP 畫面先用左右鍵選方案，再 Enter。方案 unavailable 代表原生流程不允許。要固定本機 GP：

```ini
[GP]
KeyboardConfirm = true
ConfirmKey = Return
LockValue = true
LockedValue = 900
```

LockedValue 範圍 0–999999，可自行改。解除鎖定不會還原啟用前的 GP。這是本機遊戲數值設定，不是伺服器帳號加點功能。

## 5. 深度練習與自製譜面

先設定 `[Training] Enabled=true`、`PlaybackSpeed=1`，進一首歌曲。F10 測試從頭重試，再用 Home / End 標记片段；Backspace 回到起點。`PreRollSeconds=2` 表示先回到起點前兩秒；`LoopSegment=true` 啟用循環。
重試重新初始化譜面／計分／技能／敵人，不是還原所有物件的時間快照。跨越起點的長條／雷射可能被略過，請把起點放在它們出現前。教學／Event 不允許重試，Party 重試不支援，請使用本機單人模式。
先確認 1x 正常，再以 - / = 或 PlaybackSpeed 設定 0.5–2x，聽音訊並檢查譜面同步。音高保留使用 CRI time-stretch；不是波形完全無損，原生 DSP 支援仍待實機測試。P 可以暫停。已啟用過練習的當局會維持練習時鐘至歌曲結束，請在曲後關閉。
底部 HitTimingBar 顯示 Early/Late ms，換算自原生 60-frame 時間單位；LeverTracking 顯示實際拉桿、中心、安全區及未來中心軌跡。兩項在 `[Visualizer]` 設定。

Custom chart：把 `[Training] CustomChartPath` 設為现有 `.ogkr` 絕對路徑，選相符音訊的歌曲。編輯檔案後按 F11，使用原生 parser 重載並重試片段；音訊、歌曲 ID、封面仍沿用所選原曲。
重載失敗嘗試恢復最後有效快照，沒有有效快照或恢復失敗則結束本次練習並記錄錯誤。首次載入無效譜面不保證可恢復，先保留可用版本。還有畫面上的 Retry / Segment / Reload Chart 按鈕可用。

## 6. OBS overlay

設定 `[OBS] Enabled=true`、`Port=8765`，F6 重新讀取。OBS 新增「瀏覽器」來源，URL 填 `http://127.0.0.1:8765/`，尺寸可從 900×220 開始調整，背景透明。
`/snapshot` 回傳當前 JSON，WebSocket `/ws` 約每秒 10 次輸出曲名／難度、原生 Technical Score、Combo、Critical Break／Break／Hit／Miss、練習／重放狀態與判定資訊。
服務僅接受本機 loopback。畫面沒資料時，先以瀏覽器開相同 URL；開不起來檢查 OBS.Enabled、日誌的埠占用錯誤，或改 Port 後同步改 OBS URL。

## 7. 重放、自由鏡頭、影片

`[Replay] RecordEverySong=true` 預設逐曲錄製。預設目錄 `BepInEx/GEKIDRIVE/replays/`，日誌顯示 `Replay saved:` 後才是完成保存。`.orp` 是本模組 ORP2 格式，不保證其他工具相容。
播放：設定 ReplayPath 絕對路徑、PlaybackEnabled=true（或 F5），選相同曲目与難度再開始。片段檔案從紀錄起點開始；遊戲與譜面指紋不符會拒絕。保持原本卡牌、技能與原生選項，記錄不是所有引擎狀態的快照，不保證分數完全一致。
F4 在練習／重放中切換自由鏡頭：WASD/QE 移動、右鍵拖曳旋轉。CameraName 空白使用 NotesManager 鏡頭；`HideBattleHud=true` 隱藏 BattleUI。

影片：開啟 `[Export] Enabled=true`，設定 Width / Height / Fps，下次練習或重放開始輸出 PNG。預設目錄 `BepInEx/GEKIDRIVE/exports/<時間>/`，包含 `capture.txt` 及 `frame000000.png` 等檔案。
這是離線輸出，歌曲音訊會靜音，磁碟容量與輸出耗時都可能很大。要做直式展示可設定 Width=1080、Height=1920；一般橫式影片可用 1920×1080，但鏡頭構圖也需自己調整。
自行準備 FFmpeg 後，以 PowerShell 執行發布包中的工具：

```powershell
.\encode-video.ps1 -FramesDirectory 'D:\Game\package\BepInEx\GEKIDRIVE\exports\20261008-123456-000' -FFmpeg 'D:\Tools\ffmpeg.exe'
```

輸出是無音訊 `chart.mp4`，不自動覆蓋已存在的檔案。用 `-Output 'D:\Videos\demo.mp4'` 指定其他檔名；没有包含遊戲音訊擷取或變速混音。MP4 編碼與遊戲內擷取尚未實測。

## 8. 視窗與顯示器

`[Video] WindowMode` 可為 Native、Windowed、Borderless，Width/Height 控制視窗尺寸。LandscapeLayout=true 將原生直式畫面重組為中央譜面與兩側 UI 裁切區；不是全遊戲的原生橫屏 UI 重排。
TrackCrop、LeftPanelCrop、RightPanelCrop 使用 `x,y,w,h` 的 0–1 UV，左下為原點。預設位置需看实際畫面校正；非戰鬥畫面保留完整直式畫面並留黑邊。
第二螢幕開啟 SecondDisplay=true、SecondDisplayIndex=1，以 SecondDisplayCrop 調整 Boss／卡牌區域；這是主畫面裁切，不是新 Boss 場景。需要 Unity 偵測到第二個 display，無設備時先保持關閉。

## 9. 私人機台與進階輸出

`[Cabinet] FreePlay` 繞過本機遊戲費用扣款，不改原生 Test Mode FreePlay 標誌。VirtualCredits 使用獨立本機 ledger，InitialCredits 只在 ledger 尚未存在時作初值。Insert 或原生投幣通知增加 CreditsPerPulse。
餘額／交易 ID 保存於 `BepInEx/GEKIDRIVE/credit-ledger.bin`。重複 ID 不重複加點，存檔失敗不加點。這些不是伺服器餘額功能。
支付入口需 OBS.Enabled、VirtualCredits、PaymentSignalBridge 全開，PaymentSignalToken 至少 24 字元。你的支付端先驗證訂單，再以 Bearer token POST `/credit`，body 為 `transactionId=order-123&credits=1`。HTTP 202 只表示排入佇列；完整示例與限制見 [進階設定](modules-0.5.md)。不要公開自己的 token。
本模組沒有 LINE Pay 訂單驗證／退款／QR 生成。LED 以 SerialEnabled 開啟，Port／BaudRate 指定設備，控制器解析 [GD1 協定](led-protocol.md)；尚未提供特定硬體韌體。

## 10. 常見問題與回報

| 現象 | 檢查方式 |
| --- | --- |
| 沒有公開版 banner | 查看 LogOutput.log 是否載入 Public DLL、有沒有重複／衝突插件，請先退出遊戲 |
| PUBLIC GUARD FAILED | 查看遊戲指紋與完整例外；不支援的 DLL 不安裝 hook |
| F6 設定無效 | 確認編輯的是 org.gekidrive.ongeki.public.cfg |
| 新成績／卡牌進度不保留 | 公開版供本機練習，遊玩結果與其他帳號變更可能不保留，這是使用限制 |
| 首次載入仍慢 | 第一次建立譜面標頭快取，下次再觀察 cache HIT；不保證所有開機流程加速 |
| GP 仍不能購入 | 檢查可用 credit／方案狀態，附完整畫面和 GP dialog 日誌 |
| 重放遭拒絕 | 確認同一遊戲指紋、譜面內容、歌曲 ID、難度與有效檔案 |
| 變速不正常 | 先退回 1x，附 CRI 例外與操作順序；其他音訊補丁可能衝突 |
| 第二螢幕／LED 不動 | 確認設備／Display index／COM、日誌及韌體協定，F6 重啟模組 |

問題回報附版本、完整 LogOutput.log、相關 cfg、重現步驟與其他插件列表；先移除 cfg 中的支付 token／個人資訊。不需要分享遊戲 DLL、音訊、譜面或帳號 access code。

## 11. 從原始碼建置

需要 .NET SDK；純模組測試目標 net10.0，插件目標 net35。自行提供遊戲 package 內的 Assembly-CSharp、Assembly-CSharp-firstpass、UnityEngine、mscorlib、BepInEx、0Harmony 等 DLL。

```powershell
.\build.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Build.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Modules.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Public.ps1 -GameDir 'D:\YourGame\package'
dotnet run --project tests\CacheTests\CacheTests.csproj -c Release
dotnet run --project tests\ModuleTests\ModuleTests.csproj -c Release
dotnet run --project tests\ServerTests\ServerTests.csproj -c Release
dotnet build tests\PublicGuardTests\PublicGuardTests.csproj -c Release '-p:GameDir=D:\YourGame\package'
.\tests\PublicGuardTests\bin\Release\net48\PublicGuardTests.exe
.\tools\package.ps1 -GameDir 'D:\YourGame\package'
```

也可設定環境變數 GEKIDRIVE_GAME_DIR；插件未指定時使用 repo 內 GameFiles/package，該目錄不納入 Git。輸出 DLL 與 zip 在 artifacts，不納入原始碼提交。
PublicGuardTests 在 Windows .NET Framework 4.8 上呼叫實際公開版 prefix 邏輯，使用相同簽章的測試物件，驗證拒絕／封包完成／一般查詢放行；不執行 Harmony detour。遊戲附帶 Harmony 使用 Mono，相同 DLL 在 .NET 10／Windows CLR 的 detour 測試不相容，不能以此取代遊戲內安裝測試。
建置／測試通過不代表遊戲內與伺服器端已驗證；發布狀態見 [validation.md](validation.md)。
