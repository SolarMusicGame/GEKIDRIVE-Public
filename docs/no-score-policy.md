# 公開版不上傳成績的規則

公開版插件識別為 `org.gekidrive.ongeki.public`，DLL 為 `GekiDrive.Public.dll`，畫面固定顯示 `GEKIDRIVE PUBLIC NO SAVE`。
無論手動遊玩、F8 自動、Training 開關或 config 值，皆採相同規則，沒有解除成績攔截的設定。

## 攔截範圍

1. `GameEngine.applyResultToUserData` 不執行：歌曲結果不由這條路徑寫入玩家成績、rating、獎勵與遊玩進度。
2. `UserLocal.addPlayLog` 不執行：不加入歌曲遊玩紀錄。
3. `PacketUpsertUserAll.create` 不執行：續玩／登出不建立這種帳號保存請求；使用原生「沒有待送內容」的回傳路徑。
4. 該封包的 `proc` 直接結束；泛用 `Packet.create`／`proc` 也攔截 `UpsertUserAll`，避免透過基底型別的替代入口提交。

這些保護使用獨立 Harmony ID，先於其他功能安裝。其他模組安裝失敗不會移除已安裝的公開版成績保護。
沒有將結算畫面改為 0，也沒有對伺服器提交假的成功回應；被攔截的使用者保存請求不會建立 HTTP client。

## 玩家會看到什麼

- 遊戲畫面與結算仍顯示本次原生計算的分數、Combo、判定與 OVER DAMAGE。
- 下一次登入不保留這次歌曲成績或依賴 `UpsertUserAll` 保存的帳號變更。
- `UpsertUserAll` 也是完整使用者資料保存介面。因此選項、卡牌／進度等其他帳號變更也可能不保存。請使用練習帳號，勿期待只禁止分數卻保留所有其他進度。
- `.orp` 與 OBS 資料屬本機輸出，不是玩家成績提交。
- 登入／查詢、登出協定、GP log 與機台統計等其他請求仍可能執行；本模組不是全網路斷線工具，也不修改伺服器。

## 啟動檢查與限制

日誌必須出現 `PUBLIC SCORE GUARD READY`，畫面必須顯示 `GEKIDRIVE PUBLIC NO SAVE`。
若顯示 `PUBLIC GUARD FAILED`、BepInEx 沒有載入插件，或偵測到不支援的遊戲指紋，請退出遊戲：這些情況無法宣稱有成績保護。移除模組後遊戲恢復原生保存行為。
已驗證的遊戲 SHA256：`0766056f0bb6e273417be31aa9f266d71589cd5d78abd5f022173d47f04f0a4a`。

靜態檢查已確認上述六個方法、參數、封包狀態及不受 config 影響的攔截邏輯。尚未在實際遊戲／伺服器驗證登入 → 手動／自動遊玩 → 續玩 → 登出完整流程。
其他補丁若另行上傳資料、改動上述流程或移除本模組的 Harmony patch，不在此保護範圍內。實測時應比對伺服器端沒有 `UpsertUserAllApi` 請求，並確認重新登入後舊成績保持不變。
