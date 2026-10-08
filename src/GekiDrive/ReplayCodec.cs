using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace GekiDrive
{
    internal struct ReplayEvent
    {
        internal byte Kind;
        internal float Time, A, B, C;
        internal int Data;
    }
    internal sealed class ReplayTape
    {
        internal int MusicId, Difficulty, Seed;
        internal bool Auto;
        internal float StartMsec;
        internal string ChartHash = "", GameHash = "";
        internal List<ReplayEvent> Events = new List<ReplayEvent>();
    }
    internal static class ReplayCodec
    {
        private const int Magic = 0x3250524F, Limit = 2000000;
        internal static void Write(string path, ReplayTape tape)
        {
            if (tape.Events.Count > Limit) throw new InvalidDataException("Replay event limit exceeded.");
            ValidateStart(tape.StartMsec);
            byte[] payload;
            using (var memory = new MemoryStream())
            {
                using (var zip = new GZipStream(memory, CompressionMode.Compress))
                using (var w = new BinaryWriter(zip))
                {
                    w.Write(tape.MusicId); w.Write(tape.Difficulty); w.Write(tape.Seed); w.Write(tape.Auto);
                    w.Write(tape.StartMsec);
                    Text(w, tape.ChartHash); Text(w, tape.GameHash); w.Write(tape.Events.Count);
                    float previous = -1;
                    foreach (var e in tape.Events)
                    {
                        Validate(e, previous); previous = e.Time;
                        w.Write(e.Kind); w.Write(e.Time); w.Write(e.A); w.Write(e.B); w.Write(e.C); w.Write(e.Data);
                    }
                }
                payload = memory.ToArray();
            }
            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
            using (var w = new BinaryWriter(stream))
            using (var hash = SHA256.Create())
            { w.Write(Magic); w.Write(payload.Length); w.Write(hash.ComputeHash(payload)); w.Write(payload); }
            File.Move(temp, path);
        }
        internal static ReplayTape Read(string path)
        {
            byte[] payload, digest;
            using (var stream = File.OpenRead(path))
            using (var r = new BinaryReader(stream))
            {
                if (r.ReadInt32() != Magic) throw new InvalidDataException("Unknown ORP format.");
                int length = r.ReadInt32();
                if (length < 0 || length > 64 * 1024 * 1024 || stream.Length != length + 40L) throw new InvalidDataException("Invalid ORP size.");
                digest = r.ReadBytes(32); payload = r.ReadBytes(length);
                using (var hash = SHA256.Create())
                {
                    var actual = hash.ComputeHash(payload); int difference = 0;
                    for (int i = 0; i < 32; i++) difference |= actual[i] ^ digest[i];
                    if (difference != 0) throw new InvalidDataException("ORP checksum mismatch.");
                }
            }
            using (var memory = new MemoryStream(payload))
            using (var zip = new GZipStream(memory, CompressionMode.Decompress))
            using (var r = new BinaryReader(zip))
            {
                var tape = new ReplayTape { MusicId = r.ReadInt32(), Difficulty = r.ReadInt32(), Seed = r.ReadInt32(), Auto = r.ReadBoolean() };
                tape.StartMsec = r.ReadSingle(); ValidateStart(tape.StartMsec);
                tape.ChartHash = Text(r); tape.GameHash = Text(r);
                int count = r.ReadInt32();
                if (count < 0 || count > Limit) throw new InvalidDataException("Invalid ORP event count.");
                float previous = -1;
                for (int i = 0; i < count; i++)
                {
                    var e = new ReplayEvent { Kind = r.ReadByte(), Time = r.ReadSingle(), A = r.ReadSingle(), B = r.ReadSingle(), C = r.ReadSingle(), Data = r.ReadInt32() };
                    Validate(e, previous); previous = e.Time; tape.Events.Add(e);
                }
                if (zip.ReadByte() != -1) throw new InvalidDataException("Unexpected ORP trailing data.");
                return tape;
            }
        }
        private static void Text(BinaryWriter w, string text) { byte[] bytes = Encoding.UTF8.GetBytes(text); if (bytes.Length > 4096) throw new InvalidDataException("ORP text limit."); w.Write(bytes.Length); w.Write(bytes); }
        private static string Text(BinaryReader r) { int size = r.ReadInt32(); if (size < 0 || size > 4096) throw new InvalidDataException("ORP text limit."); var bytes = r.ReadBytes(size); if (bytes.Length != size) throw new EndOfStreamException(); return Encoding.UTF8.GetString(bytes); }
        private static void Validate(ReplayEvent e, float previous)
        {
            if (e.Kind < 1 || e.Kind > 4 || !Finite(e.Time) || e.Time < 0 || e.Time < previous || e.Time > 3600000 || !Finite(e.A) || !Finite(e.B) || !Finite(e.C)) throw new InvalidDataException("Invalid ORP event.");
        }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        private static void ValidateStart(float value) { if (!Finite(value) || value < 0 || value > 3600000) throw new InvalidDataException("Invalid ORP start position."); }
    }
}
