using System;
using System.IO.Ports;
using System.Threading;
using HarmonyLib;
using MU3.Mecha;

namespace GekiDrive
{
    internal static class LedOutput
    {
        private static readonly object sync = new object();
        private static readonly byte[] colors = new byte[201];
        private static bool dirty;
        private static volatile bool running;
        private static Thread thread;
        private static SerialPort port;
        internal static void Capture(byte index, LedColor color)
        {
            Capture(index, color.getR(), color.getG(), color.getB());
        }
        internal static void Capture(byte index, byte red, byte green, byte blue)
        {
            if (!ModuleConfig.SerialLed.Value || index >= 67) return;
            lock (sync) { colors[index * 3] = red; colors[index * 3 + 1] = green; colors[index * 3 + 2] = blue; dirty = true; }
        }
        internal static void Configure()
        {
            Stop();
            if (!ModuleConfig.SerialLed.Value) return;
            string name = ModuleConfig.ComPort.Value; int baud = ModuleConfig.Baud.Value;
            running = true;
            thread = new Thread(delegate()
            {
                try
                {
                    port = new SerialPort(name, baud) { WriteTimeout = 200 }; port.Open();
                    ushort sequence = 0;
                    while (running)
                    {
                        byte[] copy = null;
                        lock (sync) { if (dirty) { copy = (byte[])colors.Clone(); dirty = false; } }
                        if (copy != null) { byte[] packet = WireCodec.LedFrame(sequence++, copy); port.Write(packet, 0, packet.Length); }
                        Thread.Sleep(33);
                    }
                }
                catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("LED serial output stopped: " + e.Message); }
                finally { running = false; if (port != null) { port.Dispose(); port = null; } }
            }) { IsBackground = true, Name = "GEKIDRIVE LEDs" };
            thread.Start();
        }
        internal static void Stop() { running = false; if (thread != null) { thread.Join(500); thread = null; } }
    }
    [HarmonyPatch(typeof(Bd15093_6IF), "_setLedColor", new Type[] { typeof(byte), typeof(LedColor) })]
    internal static class LedCapture
    {
        [HarmonyPrefix] private static void Prefix(byte __0, LedColor __1) { LedOutput.Capture(__0, __1); }
    }
    [HarmonyPatch(typeof(Bd15093_6IF), "setLedColor", new Type[] { typeof(byte), typeof(LedColor) })]
    internal static class LedEmulate
    {
        [HarmonyPrefix] private static void Prefix(byte __0, LedColor __1) { LedOutput.Capture(__0, __1); }
    }
    [HarmonyPatch(typeof(Bd15093_6IF), "setLedColor", new Type[] { typeof(byte), typeof(UnityEngine.Color) })]
    internal static class LedEmulateColor
    {
        [HarmonyPrefix] private static void Prefix(byte __0, UnityEngine.Color __1)
        { LedOutput.Capture(__0, (byte)UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(__1.r * 255), 0, 255), (byte)UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(__1.g * 255), 0, 255), (byte)UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(__1.b * 255), 0, 255)); }
    }
}
