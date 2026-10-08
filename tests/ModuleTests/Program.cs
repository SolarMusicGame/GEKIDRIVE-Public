using System;
using System.IO;
using System.Text;
using GekiDrive;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
void Near(double a, double b, string message) { Check(Math.Abs(a - b) < 0.001, message); }
void Reject(Action action, string message) { try { action(); } catch { return; } throw new Exception(message); }
var clock = new PracticeClock();
clock.Start(32000, 1000, 0.5); Near(clock.Position(3000), 33000, "Half-speed clock");
clock.SetSpeed(2, 3000); Near(clock.Position(4000), 35000, "Rate change continuity");
clock.SetPaused(true, 4000); Near(clock.Position(9000), 35000, "Paused clock");
clock.SetPaused(false, 9000); Near(clock.Position(10000), 37000, "Resume continuity");
Near(PracticeClock.Clamp(double.NaN), 1, "Reject NaN rate"); Near(PracticeClock.Clamp(99), 2, "Rate upper bound");
Check(WireCodec.WebSocketAccept("dGhlIHNhbXBsZSBub25jZQ==") == "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", "RFC 6455 handshake vector");
Reject(() => WireCodec.WebSocketAccept("invalid"), "Malformed handshake key accepted");
foreach (int size in new[] { 0, 125, 126, 65535 }) { var frame = WireCodec.WebSocketFrame(new byte[size], 1); Check(frame[0] == 129 && frame.Length == size + (size < 126 ? 2 : 4), "WebSocket length boundary"); }
Reject(() => WireCodec.WebSocketFrame(new byte[65536], 1), "Oversized frame accepted");
Check(WireCodec.Crc16(Encoding.ASCII.GetBytes("123456789"), 9) == 0x4B37, "CRC16/MODBUS vector");
byte[] rgb = new byte[201]; for (int i = 0; i < rgb.Length; i++) rgb[i] = (byte)i;
var led = WireCodec.LedFrame(513, rgb); Check(led.Length == 210 && led[4] == 1 && led[5] == 2 && led[6] == 67 && led[207] == 200, "LED packet layout");
Check((ushort)(led[208] | led[209] << 8) == WireCodec.Crc16(led, 208), "LED packet CRC");
Reject(() => WireCodec.LedFrame(0, new byte[200]), "Wrong LED count accepted");
string directory = Path.Combine(Path.GetTempPath(), "GekiDrive-ModuleTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
try
{
    var tape = new ReplayTape { MusicId = 123, Difficulty = 4, Seed = 88, Auto = true, StartMsec = 32000, ChartHash = "chart", GameHash = "game" };
    for (int i = 0; i < 1000; i++) tape.Events.Add(new ReplayEvent { Kind = (byte)(1 + i % 4), Time = i * 16.666f, A = i / 100f, B = -i, C = 0.1f, Data = i });
    string path = Path.Combine(directory, "roundtrip.orp"); ReplayCodec.Write(path, tape);
    var copy = ReplayCodec.Read(path); Check(copy.Events.Count == 1000 && copy.Seed == 88 && copy.Auto && copy.StartMsec == 32000 && copy.Events[999].Data == 999 && copy.ChartHash == "chart", "ORP round trip");
    tape.StartMsec = float.NaN; Reject(() => ReplayCodec.Write(Path.Combine(directory, "bad-start.orp"), tape), "Non-finite ORP start accepted"); tape.StartMsec = 32000;
    byte[] bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(Path.Combine(directory, "corrupt.orp"), bytes);
    Reject(() => ReplayCodec.Read(Path.Combine(directory, "corrupt.orp")), "Corrupted ORP accepted");
    File.WriteAllBytes(Path.Combine(directory, "truncated.orp"), bytes[..20]); Reject(() => ReplayCodec.Read(Path.Combine(directory, "truncated.orp")), "Truncated ORP accepted");
    tape.Events.Add(new ReplayEvent { Kind = 1, Time = 0 }); Reject(() => ReplayCodec.Write(Path.Combine(directory, "unsorted.orp"), tape), "Rewound ORP timeline accepted");
    tape.Events.RemoveAt(tape.Events.Count - 1); tape.Events.Add(new ReplayEvent { Kind = 1, Time = float.NaN }); Reject(() => ReplayCodec.Write(Path.Combine(directory, "nan.orp"), tape), "Non-finite ORP accepted");
    string ledgerPath = Path.Combine(directory, "credits.bin"); var ledger = new CreditLedger(ledgerPath, 3);
    Check(ledger.Add("order-1", 2) && ledger.Balance == 5, "Credit add"); Check(!ledger.Add("order-1", 2) && ledger.Balance == 5, "Credit duplicate");
    Check(ledger.Spend(3) && ledger.Balance == 2 && !ledger.Spend(3), "Credit spend/insufficient");
    ledger = new CreditLedger(ledgerPath, 99); Check(ledger.Balance == 2 && !ledger.Add("order-1", 2), "Credit balance and id persist across restart");
    Reject(() => ledger.Add("order\n2", 1), "Invalid credit id accepted"); Reject(() => ledger.Add("order-2", 9999), "Credit overflow accepted");
    bytes = File.ReadAllBytes(ledgerPath); bytes[^1] ^= 1; File.WriteAllBytes(ledgerPath, bytes); Reject(() => new CreditLedger(ledgerPath, 0), "Corrupted credit ledger accepted");
    Console.WriteLine("PASS: clock speed/pause continuity, RFC WebSocket handshake/lengths, LED CRC/layout, ORP roundtrip/corruption/truncation/timeline, credit idempotency/persistence/overflow.");
}
finally { Directory.Delete(directory, true); }
