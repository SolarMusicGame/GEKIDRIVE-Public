# GD1 LED serial 協定

開啟 `[LED] SerialEnabled` 並設定 Port（如 COM3）／BaudRate（預設 115200），8 data bits、no parity、1 stop bit。模組最多約每 33ms 傳送一次變更後的完整 67-LED RGB snapshot；沒有變化不傳送。序列埠故障會記錄錯誤並停止 worker，排除故障後按 F6 重新啟動。

| byte offset | 長度 | 值 |
| --- | --- | --- |
| 0 | 3 | ASCII `GD1`：47 44 31 hex |
| 3 | 1 | type = 01 |
| 4 | 2 | sequence，uint16 little endian，溢位回 0 |
| 6 | 1 | LED count = 67 |
| 7 | 201 | 位址 0–66 依序各 R、G、B，uint8 |
| 208 | 2 | CRC16/MODBUS，對 bytes 0–207 計算，little endian |

每個完整封包固定 210 bytes。CRC 初值 FFFF、反向 polynomial A001，無最後 XOR；標準測試 `123456789` 得 4B37。
Arduino／Teensy 接收端應先搜尋 `GD1` 標頭，驗證 type/count 與 CRC，成功才更新全部 LED；損毀時重新尋找標頭，不要用壞封包的 RGB。Sequence 可用於偵測漏包，不是確認回覆。模組不等待 ACK。
位址是原生 LED board 順序，實體燈條的左／右排列、RGB 色序與腳位需要控制器端映射。初始化尚未收到的位址為黑色。這份協定不假定 Arduino 型號或燈條晶片；尚未提供或驗證特定硬體韌體。
