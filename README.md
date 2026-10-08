# GEKIDRIVE Public 0.5.1 — No Score Upload

供 ongeki 1.52 本機練習使用的 BepInEx 5 插件。**公開版固定不寫入歌曲成績，並阻擋 UpsertUserAll 帳號保存提交**；手動、自動與練習模式都適用，config 無法解除。

畫面仍顯示原生結算，不會把分數改成 0。這是與作者自用正式版分開的公開分支，DLL、插件 ID、cfg 和發布包均不同。新增功能與完整遊戲／伺服器流程仍待實機驗證。

Public practice build for ongeki 1.52. Song-result persistence and native UpsertUserAll submissions are always blocked. Results remain visible on screen. This also prevents other account changes carried by UpsertUserAll from being saved; it is not a complete network disconnection tool.

## 快速開始

1. 下載發布附件 `GEKIDRIVE-Public-0.5.1.zip`，關閉遊戲。
2. 移除舊 `GekiDrive.dll`、`Nageki.dll`、`GekiDrive.AutoZero.dll` 與重複公開版 DLL。
3. 將 `GekiDrive.Public.dll` 放進 `package/BepInEx/plugins/GekiDrive.Public/`。
4. 用原有方式啟動，確認日誌出現 `PUBLIC SCORE GUARD READY`，画面顯示 `GEKIDRIVE PUBLIC NO SAVE`。
5. 設定檔自動建立於 `BepInEx/config/org.gekidrive.ongeki.public.cfg`；修改後按 F6 重新讀取。

若看到 `PUBLIC GUARD FAILED` 或插件未載入，請退出遊戲；此時不能宣稱已禁止成績保存。公開版必須單獨載入，自用版和公開版不相容。

- [完整安裝、設定與操作教學](docs/guide.md)
- [不上傳成績的確切範圍](docs/no-score-policy.md)
- [完整預設 config 範例](examples/org.gekidrive.ongeki.public.example.cfg)
- [進階模組與限制](docs/modules-0.5.md)
- [發布說明](docs/release-0.5.1-public.md)
- [驗證狀態](docs/validation.md)

## 功能

| 模組 | 功能 | 預設 |
| --- | --- | --- |
| 公開版保護 | 不寫歌曲結果／play log，阻擋 UpsertUserAll | 固定啟用，無 config 開關 |
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

GP 固定值可在 `[GP] LockValue=true`、`LockedValue=900` 設定。所有一般功能與快捷鍵皆在 cfg；公開版保存規則固定在程式中。

## 相容性與狀態

目前僅對以下環境完成建置和靜態相容性檢查：ongeki 1.52、Unity 5.6.4、CLR 2.0、64-bit BepInEx 5.4.23.2，遊戲 Assembly-CSharp SHA256：

```text
0766056f0bb6e273417be31aa9f266d71589cd5d78abd5f022173d47f04f0a4a
```

未知指紋不安裝遊戲補丁。公開版保護獨立於其他功能安裝，但仍需遊戲／伺服器實測。

已完成 net35 建置、原生方法與參數靜態檢查，並通過時鐘、重放、CRC、credit 與實際本機 OBS HTTP/WebSocket 測試。尚未實測完整登入、遊玩、續玩、登出與伺服器無提交流程，也未驗證真實 LED／第二螢幕設備。

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