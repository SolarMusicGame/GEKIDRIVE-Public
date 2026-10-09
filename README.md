# GEKIDRIVE Public 0.5.1

> **AI 生成・不承諾維護**：本專案的程式碼、README、教學、測試與發布說明皆由 AI 生成與整理。發布者僅分享現有成果，不承諾持續維護、錯誤修復、版本更新、相容性或技術支援；Issue／PR 可能不會回覆或處理。功能正確性與實際效果不作保證，使用前請自行評估、備份與驗證。
>
> **AI-generated, provided as-is, unmaintained.** No ongoing maintenance, support, bug fixes, updates or compatibility guarantees are promised. See [AI 與維護聲明](AI-NOTICE.md).

供 ongeki 1.52 本機練習使用的 BepInEx 5 插件。遊玩結果只供當次畫面展示，不會保留到帳號；其他帳號變更也可能不保留。

畫面顯示當次原生結算。新增功能與完整遊戲流程仍待實機驗證。

Local practice build for ongeki 1.52. Results are shown for the current session and are not retained on the account. Other account changes may also not be retained.

## 快速開始

1. 下載發布附件 `GEKIDRIVE-Public-0.5.1.zip`，關閉遊戲。
2. 移除舊 `GekiDrive.Public.dll` 與重複公開版 DLL。
3. 將 `GekiDrive.Public.dll` 放進 `package/BepInEx/plugins/GekiDrive.Public/`。
4. 用原有方式啟動，確認公開版插件載入，日誌沒有初始化錯誤。
5. 設定檔自動建立於 `BepInEx/config/org.gekidrive.ongeki.public.cfg`；修改後按 F6 重新讀取。

若看到 `PUBLIC GUARD FAILED` 或插件未載入，請退出遊戲並檢查日誌。公開版必須單獨載入，勿和其他版本混用。

- [完整安裝、設定與操作教學](docs/guide.md)
- [公開版使用限制](docs/public-edition.md)
- [完整預設 config 範例](examples/org.gekidrive.ongeki.public.example.cfg)
- [進階模組與限制](docs/modules-0.5.md)
- [發布說明](docs/release-0.5.1-public.md)
- [驗證狀態](docs/validation.md)

## 功能

| 模組 | 功能 | 預設 |
| --- | --- | --- |
| 自動遊玩 | F8，最佳判定、軌道跟隨、鈴鐺、彈幕／雷射防護、Boss OVER DAMAGE | 主開關關閉 |
| 練習 | 重試、片段循環、0.5–2x 保留音高變速、暫停、OGKR 重載 | 關閉 |
| 判定／拉桿 HUD | Early/Late ms、安全區、中心偏差與未來中心軌跡 | 開啟 |
| OBS | 本機 HTTP／WebSocket 的歌曲、Technical Score、Combo／判定資料 | 關閉 |
| 重放 | 本機 .orp 記錄／输入重放、自由鏡頭 | 錄製開啟，播放關閉 |
| 影片 | 無 BattleUI 的 PNG 序列，可另用 FFmpeg 轉無音訊 MP4 | 關閉 |
| 顯示 | FPS、幀率控制、無邊框、橫屏裁切合成、第二顯示器裁切 | FPS 開，其餘關閉 |
| GP／機台 | GP 鍵盤確認、GP 固定值、Free Play、虛擬 credit | 鍵盤確認開，其餘關閉 |
| 輸出 | token 驗證本機支付信號入口、67 RGB LED 序列轉發 | 關閉 |
| 載入 | 譜面標頭快取、XML interpreter 修正 | 開啟 |

GP 固定值可在 `[GP] LockValue=true`、`LockedValue=900` 設定。一般功能與快捷鍵皆在 cfg。

## 相容性與狀態

目前僅對以下環境完成建置和靜態相容性檢查：ongeki 1.52、Unity 5.6.4、CLR 2.0、64-bit BepInEx 5.4.23.2，遊戲 Assembly-CSharp SHA256：

```text
0766056f0bb6e273417be31aa9f266d71589cd5d78abd5f022173d47f04f0a4a
```

未知指紋不安裝遊戲補丁，完整遊戲行為仍需實測。

已完成 net35 建置、原生方法與參數靜態檢查，並通過時鐘、重放、CRC、credit 與實際本機 OBS HTTP/WebSocket 測試。尚未實測完整登入、遊玩、續玩與登出流程，也未驗證真實 LED／第二螢幕設備。

橫屏是畫面裁切合成；重試不是所有引擎狀態的快照；重放需同曲／同譜面／相同卡牌選項，不保證結算完全一致。支付入口不包含 LINE Pay 訂單驗證、退款或 QR 生成。完整限制在教學中列出。

## 建置

需要 .NET SDK 與自行提供的遊戲／BepInEx DLL，插件輸出為 net35，純模組測試使用 net10.0。

```powershell
.\build.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Build.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Modules.ps1 -GameDir 'D:\YourGame\package'
.\tools\Verify-Public.ps1 -GameDir 'D:\YourGame\package'
.\tools\package.ps1 -GameDir 'D:\YourGame\package'
```

可改用 `GEKIDRIVE_GAME_DIR` 環境變數。遊戲 DLL、譜面、音訊、使用者設定、日誌、參考專案與建置產物不放入原始碼倉庫。

## 問題回報與貢獻

請附模組版本、完整 LogOutput.log、相关 cfg、其他插件與重現步驟。移除支付 token／帳號 access code 等個人資料，勿提交遊戲檔案。開發與發布流程見 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 參考

[AquaMai](https://github.com/MuNET-OSS/AquaMai) 與 [AppleChu](https://github.com/MuNET-OSS/AppleChu) 提供模組分離、設定與版本相容性設計參考。本專案為針對本機 ongeki DLL 的獨立實作，沒有使用其他遊戲補丁位址。發布包不附帶上述參考專案或其原始碼。
## 作者的話
沒有手台測視過加油
6767
