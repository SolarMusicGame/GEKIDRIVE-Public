# GEKIDRIVE Public 0.5.1 進階模組說明

適用本機已核對指紋的 ongeki 1.52；這是公開不上傳成績分支，不包含結算全 0 的實驗補丁。完整初次安裝見 [guide.md](guide.md)，固定保存規則見 [no-score-policy.md](no-score-policy.md)。

## 安裝與 config

關閉遊戲，把 `GekiDrive.Public.dll` 放入 `package/BepInEx/plugins/GekiDrive.Public/`，移除舊 `Nageki.dll` 或 `GekiDrive.AutoZero.dll`，同一時間只保留一份本模組。
設定沿用 `package/BepInEx/config/org.gekidrive.ongeki.public.cfg`。首次啟動會自動補上新設定；範例 cfg 包含公開版預設項目，請合併設定，保留已有 AutoPlay/Performance 等段落，不要整份覆蓋舊 cfg。
設定、快捷鍵、路徑、解析度、COM、裁切位置均可修改；遊戲內 F6 重新讀取 cfg，OBS、COM 與顯示設定隨之重建。重放與影格輸出的起始設定在下一次開始歌曲／重試才生效。

GP 範例：

```ini
[GP]
LockValue = true
LockedValue = 900
```

數值可自行改為 0–999999。停用鎖定後沿用遊戲目前的 GP，不會還原啟用前的數值。

## 練習與判定

`[Training] Enabled = true` 或 F9 啟用。F10 從頭重試；Home / End 設定片段起終點；Backspace 回到起點減去 `PreRollSeconds`，`LoopSegment=true` 在終點自動重試。P 暫停／恢復，`-` / `=` 每次調整 0.05x。
`PlaybackSpeed` 範圍 0.5–2.0。音訊使用獨立 CRI voice pool 與 time-stretch DSP，譜面使用相同速度的時鐘，目標是保留音高；時間伸縮會改變波形，並非數學上的音訊無損。若 CRI 初始化拋出錯誤，會退回 1x 並記錄日誌。原生音訊庫仍可能無聲地拒絕配置，必須實際聽歌確認。
CRI 原理參考：[官方 time-stretch 文件](https://game.criware.jp/manual/unity_plugin_en/latest/contents/atom4u_practice_0004_x2speed_playback.html)。

畫面提供 Retry / Segment / Reload Chart 虛擬按鈕；`CabinetRetryButton` 可指定原生 GKey 0–9 的實體重試鍵，-1 關閉。該鍵仍有原本遊戲功能。
重試重新初始化譜面、計分、技能與敵人，再從指定音訊時間開始；不是保存所有引擎物件的任意時間倒帶。起點以前的物件會略過，因此跨越起點的長條／雷射不保證完整還原；請增加 pre-roll，把起點設在長條開始前。
公開版所有當局保留畫面上的結算，但固定攔截原生結果寫入、play log 與 UpsertUserAll 保存；手動遊玩也適用，與 Training 開關無關。啟用過練習的歌曲會持續使用練習時鐘至結束；要關閉請在歌曲結束後切換。教學／Event 不允許即時重試，Party 不處理重試；本工具供本機單人練習使用。

`[Visualizer] HitTimingBar` 顯示最近 4 秒擊打的 Early/Late ms，取原生 frameDiff，原生 60-frame 時間單位換算成 ms，並非提升底層判定解析度。MISS 不當作時間偏差。
`LeverTracking` 的畫面 HUD 顯示拉桿實際位置、中心偏差、安全區及未來兩秒中心路徑。這是疊加參考條，不是重寫原生軌道 mesh；中心也不是所有譜面的唯一最佳路線。

## Custom Chart Live Loader

`[Training] CustomChartPath = D:\Charts\test.ogkr`，選一首使用相符音訊的歌曲，啟用練習。修改 OGKR 後按 F11，使用原生 parser 重載並重試片段，免重啟遊戲。
只替換目前歌曲譜面，不新增選歌資料、封面或音訊。成功載入會保存最後有效譜面的記憶體快照（上限 32 MB）；重載失敗會用暫存 OGKR 恢復上一份有效譜面。無快照或恢复也失敗時停止本次練習並記錄錯誤，請重新選曲。首次載入本身無效的 OGKR 不保證可恢復，請先保存可用版本。

## OBS

`[OBS] Enabled = true`、`Port = 8765`。OBS 新增 Browser Source，URL `http://127.0.0.1:8765/`，建議 900×220，背景透明。
`/ws` 每秒約 10 次送出 JSON，`/snapshot` 提供單次快照。包含曲名／ID／難度、原生 Technical Score、Combo、Critical Break／Break／Hit／Miss、最近時間偏差、拉桿資訊、practice/replay 狀態。
僅綁定本機 loopback；改埠後同步改 OBS URL。遊戲外程式可以讀取此介面，不需要讀取遊戲記憶體。

## .orp 重放與影片

`[Replay] RecordEverySong = true` 預設逐曲記錄，路徑空白時保存於 `BepInEx/GEKIDRIVE/replays/`。檔案使用 ORP2 版本標頭、gzip + SHA256，保存遊戲／譜面指紋、歌曲／難度、片段起點、彈幕隨機 seed、自動模式、按鍵 edge/hold/flick、拉桿、判定、彈幕位置與技能觸發事件；有事件數與檔案大小限制。片段錄製的檔案從紀錄起點播放。壓縮／存檔在背景執行，請等日誌顯示保存完成再關閉遊戲。
輸入依原生 GameDeviceManager 更新節奏取樣，並非每個硬體訊號的獨立毫秒封包。彈幕位置／技能事件作為記錄，重放時仍由原生引擎重算，不直接注入紀錄的傷害。
播放方式：設定 `ReplayPath`，`PlaybackEnabled=true` 或 F5，選相同歌曲與難度，再開始；檔案、遊戲、譜面指紋不符會拒絕。卡牌組合、技能狀態與其他原生選項目前未完整保存，請維持錄製時相同配置；這不是保證結算逐位元一致的重放系統。
練習／重放可用 F4 自由鏡頭：WASD/QE 移動、右鍵拖曳旋轉；CameraName 可指定鏡頭。`HideBattleHud=true` 隱藏 BattleUI。

`[Export] Enabled=true` 後下次練習／重放開始輸出 PNG 序列，設定 Width / Height / Fps / Directory。引擎處理一個指定時間點後才輸出影格並前進時鐘，避免磁碟速度直接決定播放步長。輸出時歌曲音訊靜音，儲存於 `BepInEx/GEKIDRIVE/exports/<時間>/`。這是離線影格輸出，速度可能很慢，磁碟用量也很大。匯出 FPS 在開始時固定，過程中改 cfg FPS 不影響當次檔案。

```powershell
.\encode-video.ps1 -FramesDirectory 'D:\Game\package\BepInEx\GEKIDRIVE\exports\20261008-123456-000' -FFmpeg 'D:\Tools\ffmpeg.exe'
```

影片編碼需要自行提供 FFmpeg；本機目前未找到 FFmpeg，所以尚未測試 MP4 編碼。輸出為無音訊 MP4；沒有包含遊戲音訊擷取／變速混音。自由鏡頭與 PNG 輸出需實際遊戲測試。

## 視窗、橫屏、第二顯示器

`[Video] WindowMode = Native / Windowed / Borderless`，Width/Height 設輸出尺寸。
`LandscapeLayout=true` 將原生 1080×1920 畫面渲染到 texture，重組為中間軌道與兩側原生 UI 裁切畫面。這是畫面合成，不是所有選單／UI 的原生橫屏重排；選曲與非戰鬥畫面保留完整 portrait 畫面並等比例留黑邊。各 Crop 是左下原點的 `x,y,w,h` UV，範圍 0–1，可依实际畫面校正，預設裁切尚未由實機驗證。
`SecondDisplay=true`、`SecondDisplayIndex=1`，SecondDisplayCrop 控制第二螢幕的 Boss／卡牌來源區域。輸出來自主畫面的裁切，不是建立全新的獨立 Boss 相機。需 Unity 偵測到第二個 Display；螢幕佈局、原生相機與 Canvas 相容性待測。

## 私人機台 credit / 支付信號 / LED

`[Cabinet] FreePlay=true` 繞過本機 Credit 遊戲費用扣款；不改 Test Mode 原生 FreePlay 標誌，畫面未必顯示原生 FREE PLAY 字樣。
`VirtualCredits=true` 使用獨立本機 CREDIT ledger。InitialCredits 僅在 ledger 尚不存在時作初值；Insert 或原生投幣通知增加 CreditsPerPulse。餘額與交易 ID 原子保存於 `BepInEx/GEKIDRIVE/credit-ledger.bin`，存檔失敗不加點，重複交易不加第二次。

支付入口需要 OBS.Enabled、VirtualCredits 與 PaymentSignalBridge 都為 true，PaymentSignalToken 至少 24 字元。
你自己的支付端必須先驗證訂單，再向 `http://127.0.0.1:8765/credit` POST：

```text
Authorization: Bearer <config 中的 token>
Content-Type: application/x-www-form-urlencoded

transactionId=order-123&credits=1
```

ID 僅允許英數、`-`、`_`，長度 1–128，credits 1–9999。HTTP 202 表示已排入加點佇列，不代表存檔完成；日誌可核對處理結果。本模組沒有 LINE Pay 訂單查詢、簽章驗證、退款或 QR 生成；完整金流串接仍需你的支付端服務資料。

`[LED] SerialEnabled=true`，設定 Port / BaudRate。轉發原生 67 個 RGB LED 位址更新，批次發送 GD1；見 [led-protocol.md](led-protocol.md)。尚未連接真實控制器測試，控制器韌體／腳位／燈條排列必須依你的設備實作。

## 驗證狀態與實機檢查

已完成 net35 編譯（0 警告／錯誤）、本機遊戲 DLL 的補丁與 CRI API 靜態檢查；純模組測試涵蓋時鐘、重放損毀／邊界、CRC、credit 持久化與重複交易。本機實際 HTTP/WebSocket 測試涵蓋頁面、快照、來源限制、關閉握手、加點入口轉交與重啟。
尚未啟動遊戲實測這些新增模組，也沒有硬體或 LINE Pay 端可驗證。請先單獨測試練習 1x 重試 → 0.5x/2x 音訊與譜面同步 → 片段／重載 → 同曲重放，確認結算不保存，再分別開 OBS、橫屏、匯出與設備功能。若失敗，提供該次完整 LogOutput.log、相關 cfg 段落與操作順序。
