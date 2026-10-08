using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace GekiDrive
{
    internal static class OverlayServer
    {
        private static TcpListener listener;
        private static Thread acceptThread;
        private static int generation, clientCount;
        private static readonly object sync = new object();
        private static readonly List<TcpClient> clients = new List<TcpClient>();
        private static byte[] html;
        internal static void Configure()
        {
            Stop();
            if (!ModuleConfig.Obs.Value) return;
            try
            {
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GekiDrive.overlay.html"))
                using (var reader = new StreamReader(resource)) html = Encoding.UTF8.GetBytes(reader.ReadToEnd());
                listener = new TcpListener(IPAddress.Loopback, ModuleConfig.ObsPort.Value); listener.Start(8);
                var local = listener; int epoch = generation;
                acceptThread = new Thread(delegate()
                {
                    while (epoch == generation)
                    {
                        TcpClient client;
                        try { client = local.AcceptTcpClient(); } catch { break; }
                        if (Interlocked.Increment(ref clientCount) > 8) { client.Close(); Interlocked.Decrement(ref clientCount); continue; }
                        lock (sync)
                        {
                            if (epoch != generation) { client.Close(); Interlocked.Decrement(ref clientCount); break; }
                            clients.Add(client);
                        }
                        ThreadPool.QueueUserWorkItem(delegate { Serve(client, epoch); });
                    }
                }) { IsBackground = true, Name = "GEKIDRIVE OBS" }; acceptThread.Start();
                if (Plugin.SharedLog != null) Plugin.SharedLog.LogInfo("OBS overlay: http://127.0.0.1:" + ModuleConfig.ObsPort.Value + "/ (WebSocket /ws)");
            }
            catch (Exception e) { Stop(); if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("OBS server could not start: " + e.Message); }
        }
        internal static void Stop()
        {
            Interlocked.Increment(ref generation);
            if (listener != null) { listener.Stop(); listener = null; }
            lock (sync) { foreach (var client in clients) client.Close(); }
            if (acceptThread != null) { acceptThread.Join(300); acceptThread = null; }
        }
        private static void Serve(TcpClient client, int epoch)
        {
            try
            {
                client.NoDelay = true; client.ReceiveTimeout = 1500; client.SendTimeout = 1500;
                using (NetworkStream stream = client.GetStream())
                {
                    string header = Header(stream); string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] first = lines[0].Split(' '); if (first.Length != 3) return;
                    var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lines.Length; i++) { int split = lines[i].IndexOf(':'); if (split > 0) fields[lines[i].Substring(0, split)] = lines[i].Substring(split + 1).Trim(); }
                    string path = first[1].Split('?')[0];
                    string origin;
                    if (fields.TryGetValue("Origin", out origin) && !LocalOrigin(origin)) { Response(stream, "403 Forbidden", "text/plain", Encoding.UTF8.GetBytes("Local origin required")); return; }
                    if (first[0] == "GET" && path == "/ws")
                    {
                        string key, version, upgrade;
                        if (!fields.TryGetValue("Sec-WebSocket-Key", out key) || !fields.TryGetValue("Sec-WebSocket-Version", out version) || version != "13" || !fields.TryGetValue("Upgrade", out upgrade) || !string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase)) return;
                        Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + WireCodec.WebSocketAccept(key) + "\r\n\r\n"));
                        while (epoch == generation)
                        {
                            Write(stream, WireCodec.WebSocketFrame(Encoding.UTF8.GetBytes(Telemetry.Json), 1));
                            if (stream.DataAvailable && !ReadControl(stream)) break;
                            Thread.Sleep(100);
                        }
                    }
                    else if (first[0] == "GET" && path == "/") Response(stream, "200 OK", "text/html; charset=utf-8", html);
                    else if (first[0] == "GET" && path == "/snapshot") Response(stream, "200 OK", "application/json", Encoding.UTF8.GetBytes(Telemetry.Json));
                    else if (first[0] == "POST" && path == "/credit")
                    {
                        string lengthText, auth; int length;
                        if (!fields.TryGetValue("Content-Length", out lengthText) || !int.TryParse(lengthText, out length) || length < 1 || length > 4096 || !fields.TryGetValue("Authorization", out auth) || !auth.StartsWith("Bearer ", StringComparison.Ordinal)) { Response(stream, "400 Bad Request", "text/plain", Encoding.ASCII.GetBytes("Invalid request")); return; }
                        byte[] body = new byte[length]; Read(stream, body, length);
                        string id = "", value = "";
                        foreach (string pair in Encoding.UTF8.GetString(body).Split('&')) { int split = pair.IndexOf('='); if (split < 0) continue; string field = pair.Substring(0, split); string content = Uri.UnescapeDataString(pair.Substring(split + 1).Replace("+", " ")); if (field == "transactionId") id = content; if (field == "credits") value = content; }
                        int count;
                        bool accepted = int.TryParse(value, out count) && Cabinet.QueueSignal(auth.Substring(7), id, count);
                        Response(stream, accepted ? "202 Accepted" : "403 Forbidden", "application/json", Encoding.ASCII.GetBytes(accepted ? "{\"queued\":true}" : "{\"queued\":false}"));
                    }
                    else Response(stream, "404 Not Found", "text/plain", Encoding.ASCII.GetBytes("Not found"));
                }
            }
            catch (Exception) { /* Client disconnect / timeout never interrupts the game thread. */ }
            finally { client.Close(); lock (sync) clients.Remove(client); Interlocked.Decrement(ref clientCount); }
        }
        private static bool LocalOrigin(string origin) { Uri uri; return Uri.TryCreate(origin, UriKind.Absolute, out uri) && (uri.Host == "127.0.0.1" || uri.Host == "localhost" || uri.Host == "[::1]"); }
        private static string Header(Stream stream)
        {
            var bytes = new List<byte>();
            while (bytes.Count < 8192)
            {
                int value = stream.ReadByte(); if (value < 0) throw new EndOfStreamException(); bytes.Add((byte)value);
                int n = bytes.Count; if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10) return Encoding.ASCII.GetString(bytes.ToArray());
            }
            throw new InvalidDataException("HTTP header limit.");
        }
        private static bool ReadControl(Stream stream)
        {
            int a = stream.ReadByte(), b = stream.ReadByte(); if (a < 0 || b < 0 || (a & 128) == 0 || (b & 128) == 0) return false;
            int opcode = a & 15, length = b & 127;
            if (length == 126) { int high = stream.ReadByte(), low = stream.ReadByte(); if (high < 0 || low < 0) return false; length = (high << 8) | low; }
            if (length > 4096 || length == 127 || (opcode >= 8 && length > 125)) return false;
            byte[] mask = new byte[4], payload = new byte[length]; Read(stream, mask, 4); Read(stream, payload, length);
            for (int i = 0; i < length; i++) payload[i] ^= mask[i % 4];
            if (opcode == 8) { Write(stream, WireCodec.WebSocketFrame(payload, 8)); return false; }
            if (opcode == 9) Write(stream, WireCodec.WebSocketFrame(payload, 10));
            return true;
        }
        private static void Read(Stream stream, byte[] bytes, int size) { int offset = 0; while (offset < size) { int read = stream.Read(bytes, offset, size - offset); if (read == 0) throw new EndOfStreamException(); offset += read; } }
        private static void Write(Stream stream, byte[] bytes) { stream.Write(bytes, 0, bytes.Length); }
        private static void Response(Stream stream, string status, string type, byte[] content)
        { Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n\r\n")); Write(stream, content); }
    }
}
