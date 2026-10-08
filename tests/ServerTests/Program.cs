using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GekiDrive;

var reserve = new TcpListener(IPAddress.Loopback, 0);
reserve.Start(); ModuleConfig.ObsPort.Value = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
ModuleConfig.Obs.Value = true;
OverlayServer.Configure();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + ModuleConfig.ObsPort.Value) };
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
try
{
    Check((await http.GetStringAsync("/", timeout.Token)).Contains("WebSocket"), "Embedded OBS page missing");
    Check(await http.GetStringAsync("/snapshot", timeout.Token) == Telemetry.Json, "Snapshot mismatch");
    using var foreign = new HttpRequestMessage(HttpMethod.Get, "/snapshot"); foreign.Headers.Add("Origin", "https://example.com");
    using var denied = await http.SendAsync(foreign, timeout.Token); Check(denied.StatusCode == HttpStatusCode.Forbidden, "Foreign origin accepted");
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + ModuleConfig.ObsPort.Value + "/ws"), timeout.Token);
    var buffer = new byte[4096];
    for (int i = 0; i < 2; i++)
    {
        var received = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        Check(received.MessageType == WebSocketMessageType.Text && received.EndOfMessage && Encoding.UTF8.GetString(buffer, 0, received.Count) == Telemetry.Json, "WebSocket telemetry mismatch");
    }
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token);
    Check(ws.State == WebSocketState.Closed, "Close handshake failed");
    async Task<HttpStatusCode> Credit(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/credit");
        request.Headers.Add("Authorization", "Bearer " + token);
        request.Content = new StringContent("transactionId=test-001&credits=2", Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await http.SendAsync(request, timeout.Token); return response.StatusCode;
    }
    Check(await Credit("wrong") == HttpStatusCode.Forbidden, "Invalid credit token accepted");
    Check(await Credit(Cabinet.Token) == HttpStatusCode.Accepted, "Credit signal not forwarded");
    Check(Cabinet.LastId == "test-001" && Cabinet.LastCount == 2, "Credit form decoded incorrectly");
    OverlayServer.Stop(); OverlayServer.Configure();
    Check(await http.GetStringAsync("/snapshot", timeout.Token) == Telemetry.Json, "Restart failed");
    Console.WriteLine("PASS: real HTTP server, embedded page, snapshot, origin restriction, ClientWebSocket telemetry/close, authenticated credit forwarding and restart.");
}
finally { OverlayServer.Stop(); }

namespace GekiDrive
{
    // Only game/config dependencies are faked; network server and frame codec are production sources.
    internal sealed class Entry<T> { internal T Value; }
    internal static class ModuleConfig { internal static Entry<bool> Obs = new(); internal static Entry<int> ObsPort = new(); }
    internal static class Telemetry { internal static string Json = "{\"title\":\"測試曲\",\"combo\":123}"; }
    internal sealed class Log { internal void LogInfo(string text) { } internal void LogError(string text) { throw new Exception(text); } }
    internal static class Plugin { internal static Log SharedLog = new(); }
    internal static class Cabinet
    {
        internal const string Token = "integration-test-token-0123456789";
        internal static string LastId; internal static int LastCount;
        internal static bool QueueSignal(string token, string id, int count) { if (token != Token) return false; LastId = id; LastCount = count; return true; }
    }
}
