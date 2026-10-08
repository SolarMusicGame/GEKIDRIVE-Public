# GEKIDRIVE Public v0.5.1

> **AI 生成・不承諾維護**：本專案程式碼、README、教學、測試與發布說明皆由 AI 生成與整理。按現況分享，不承諾持續維護、錯誤修復、更新、相容性、技術支援或 Issue／PR 處理。功能與實際效果不作保證，使用前請自行評估、備份與驗證。
>
> **AI-generated, provided as-is, unmaintained.** No ongoing maintenance, support, bug fixes, updates or compatibility guarantees are promised.

本版本供 ongeki 1.52 本機練習與當次畫面展示。遊玩結果不會保留到帳號，其他帳號變更也可能不保留。

## 下載與操作

- `GEKIDRIVE-Public-0.5.1.zip`：公開 DLL、完整預設 cfg、README、完整教學與編碼工具。
- `GEKIDRIVE-Public-0.5.1-source.zip`：原始碼、建置工具、測試與文件。
- `SHA256SUMS.txt`：附件 SHA256 校驗。

關閉遊戲，將 `GekiDrive.Public.dll` 放入 `BepInEx/plugins/GekiDrive.Public/`，移除舊版或重複 DLL。啟動後先確認公開版已載入，且日誌沒有初始化錯誤。
cfg 為 `BepInEx/config/org.gekidrive.ongeki.public.cfg`；F6 重新讀取。完整步驟見 `docs/guide.md`，AI 與維護聲明見 `AI-NOTICE.md`。

包含自動遊玩、片段練習與 CRI 變速、判定／拉桿 HUD、OBS、OGKR 重載、.orp 重放、鏡頭／影格輸出、視窗／顯示與本機機台工具。預設自動、練習、OBS、設備與匯出關閉，重放錄製開啟。

## 驗證與限制

net35 建置、原生方法／參數靜態檢查與隔離測試通過；實際 localhost HTTP／WebSocket 測試通過。完整遊戲、伺服器與硬體流程尚未實機驗證，詳見 `docs/validation.md`。
若插件未載入、遊戲指紋不符或顯示 `PUBLIC GUARD FAILED`，請退出遊戲並檢查日誌。
重試／重放不是全部引擎狀態快照；橫屏為裁切合成；影片為另行編碼的無音訊影格序列；LINE Pay 訂單驗證與硬體韌體未包含。

本次補充 AI／維護聲明與公開文件，插件 DLL 與功能未變更。下載附件內的文件已同步更新。
發布附件不含遊戲 DLL、歌曲、譜面、個人設定、日誌或第三方參考原始碼。