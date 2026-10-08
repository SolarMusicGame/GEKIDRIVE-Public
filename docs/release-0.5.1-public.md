# GEKIDRIVE Public v0.5.1 — No Score Upload

公開版供 ongeki 1.52 本機練習使用，與自用正式版分開。

## 固定保存規則

- 手動、自動與練習歌曲都不執行原生結果寫入／play log 加入。
- 續玩／登出的 UpsertUserAll 保存封包不建立或提交；config 沒有解除開關。
- 畫面仍顯示本次原生結算，不是結算全零版。
- UpsertUserAll 包含其他帳號資料，因此選項／卡牌／進度等變更也可能不保存。
- 其他登入／查詢／GP log／機台統計通訊仍可能執行，這不是全網路斷線工具。

## 附件與操作

- `GEKIDRIVE-Public-0.5.1.zip`：僅公開 DLL、完整預設 cfg 範例、README／完整教學與編碼工具。
- `GEKIDRIVE-Public-0.5.1-source.zip`：公開版原始碼、建置工具與測試。
- `SHA256SUMS.txt`：附件 SHA256。

將 `GekiDrive.Public.dll` 放入 `BepInEx/plugins/GekiDrive.Public/`，移除自用版、全零實驗版與重複 DLL。啟動後確認 `PUBLIC SCORE GUARD READY` 日誌與 `GEKIDRIVE PUBLIC NO SAVE` banner。
cfg 為 `BepInEx/config/org.gekidrive.ongeki.public.cfg`；F6 重新讀取。完整步驟見 `docs/guide.md`。

保留自動遊玩、片段練習與 CRI 變速、判定／拉桿 HUD、OBS、OGKR 重載、.orp 重放、鏡頭／影格輸出、視窗／顯示與本機機台工具。預設自動、練習、OBS、設備與匯出關閉，重放錄製開啟。

## 驗證與限制

net35 建置與原生方法／參數靜態檢查通過；純模組及實際 localhost HTTP／WebSocket 測試通過。新增模組與完整遊戲／伺服器不上傳流程尚未實機驗證，詳見 `docs/validation.md`。
未知指紋或插件未載入時不具保護；若顯示 `PUBLIC GUARD FAILED` 請退出遊戲。
重試／重放不是全部引擎狀態快照；橫屏為裁切合成；影片為另行編碼的無音訊影格序列；LINE Pay 訂單驗證與硬體韌體未包含。

不包含遊戲 DLL、歌曲、譜面、個人設定、日誌、第三方參考原始碼或自用正式版 DLL。
