# 開發與發布

本專案內容由 AI 生成與整理，發布者不承諾維護、回覆或合併 Issue／PR；以下流程供自行開發與驗證參考，不代表提供技術支援。詳見 [AI-NOTICE.md](AI-NOTICE.md)。

此版本供本機練習。修改功能前，請先確認相容性並保留既有測試。
補丁目標、插件 ID 與 cfg 路徑的改動必須同步更新教學和驗證工具。

## 本機檢查

以自己的 package 路徑執行 build.ps1、Verify-Build.ps1、Verify-Modules.ps1、Verify-Public.ps1。執行 CacheTests、ModuleTests、ServerTests 與 Windows net48 的 PublicGuardTests；最後一項驗證實際 prefix 邏輯，不測試遊戲 Mono detour。網路測試只使用 localhost 暫時埠，檔案測試只使用測試暫存目錄。
建置需要遊戲 DLL，但禁止將它們提交至 Git。GameFiles、references、artifacts、bin、obj 都應保持忽略。

## 問題與修改說明

描述重現步驟、原本／修改後行為、相關遊戲指紋和測試結果。遊戲內或硬體未驗證時必須明確列出。
不要分享個人 cfg、支付 token、access code、遊戲資源或完整伺服器帳號資料。

## 發布

1. 確認目前提交是公開版，README／版本號／cfg 範例一致。
2. 用自己的遊戲 DLL 完成上述建置與檢查，核對 zip 僅含目前公開版本。
3. 執行 `tools/package.ps1 -GameDir <package 路徑>` 生成公開版二進位包。
4. 只從已追蹤檔案建立原始碼包，附 SHA256 校驗與 `docs/release-0.5.1-public.md` 發布說明。
5. 正式發布前確認 GitHub 登入帳號和 repo 擁有者，發布包不包含其他版本。

完整執行指令見 [教學](docs/guide.md)。尚未實機測試的版本應保留明確測試狀態，不能把編譯成功等同於完整端到端驗證。
