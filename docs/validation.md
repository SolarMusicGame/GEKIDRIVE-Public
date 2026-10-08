# Public 0.5.1 驗證狀態

本記錄描述公開版，不代表所有 ongeki 1.52 安裝皆相容。遊戲指紋以 README 列出的 SHA256 為準。

| 檢查 | 狀態 |
| --- | --- |
| net35 / CLR 2.0 建置 | 通過，0 警告／錯誤 |
| 既有自動、GP、快取、XML 與場景方法簽章 | 靜態檢查通過 |
| 新增練習、判定、輸入重放、CRI 與顯示依賴 | 47 個 Harmony 目標／注入欄位參數檢查通過 |
| 公開版六個保存／提交攔截入口與獨立插件 ID | Verify-Public 靜態檢查通過 |
| 公開版 prefix 的無条件拒絕、封包完成與一般查詢放行 | PublicGuardTests 在相同簽章的測試物件上通過，不測試 Mono Harmony detour |
| 時鐘速度／暫停連續性、重放往返／損毀／片段起點 | ModuleTests 通過 |
| LED 封包／CRC、credit 原子持久化／重複交易 | ModuleTests 通過，未連接硬體 |
| HTTP 頁面／快照、WebSocket 遙測／close、來源限制、加點轉交、重啟 | ServerTests 本機實際連線通過 |
| 遊戲內變速、重試、OGKR 重載、重放、鏡頭／顯示／PNG 輸出 | 未實測 |
| 手動／F8／Training 歌曲不更新成績與獎勵 | 已檢查攔截程式與原生路徑，未遊戲內實測 |
| 續玩／登出不建立 UpsertUserAll HTTP request，重新登入舊成績不變 | 已檢查封包路徑，未遊戲／伺服器實測 |
| FFmpeg MP4 編碼、LINE Pay 訂單驗證、真實控制器 | 未測試；訂單驗證／控制器韌體未包含 |

發布前或收到實機回報後，請補上實際版本、插件組合、測試流程與伺服器結果，不把靜態檢查寫成已實機驗證。測試時至少涵蓋：手動一首、F8 一首、Training 一首、續玩與登出；確認 banner／READY 日誌、結算顯示、沒有 UpsertUserAllApi 請求，再次登入後成績不變。
