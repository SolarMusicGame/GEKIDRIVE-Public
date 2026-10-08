using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GekiDrive
{
    internal sealed class AnalysisRecord
    {
        internal int Key, Bpm, Platinum;
        internal bool Exists;
        internal string Designer;
    }

    internal static class AnalysisCacheStore
    {
        internal const int Format = 2;
        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static bool TryRead(string path, string fingerprint, ICollection<int> expected, out List<AnalysisRecord> records)
        {
            records = null;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 16 * 1024 * 1024) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 32) return false;
                byte[] checksum;
                using (var sha = SHA256.Create()) checksum = sha.ComputeHash(bytes, 0, bytes.Length - 32);
                for (int i = 0; i < 32; i++) if (checksum[i] != bytes[bytes.Length - 32 + i]) return false;
                using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != 0x4E414743 || reader.ReadInt32() != Format || reader.ReadString() != fingerprint) return false;
                    int count = reader.ReadInt32();
                    if (count != expected.Count || count < 0 || count > 100000) return false;
                    var remaining = new HashSet<int>(expected);
                    var result = new List<AnalysisRecord>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var record = new AnalysisRecord { Key = reader.ReadInt32(), Exists = reader.ReadBoolean(), Bpm = reader.ReadInt32(), Platinum = reader.ReadInt32(), Designer = reader.ReadString() };
                        if (!remaining.Remove(record.Key) || record.Designer.Length > 4096 || record.Bpm < 0 || record.Platinum < 0) return false;
                        result.Add(record);
                    }
                    if (remaining.Count != 0 || stream.Position != stream.Length) return false;
                    records = result;
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        internal static void Write(string path, string fingerprint, IList<AnalysisRecord> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporary))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(0x4E414743); writer.Write(Format); writer.Write(fingerprint); writer.Write(records.Count);
                    foreach (var record in records)
                    {
                        writer.Write(record.Key); writer.Write(record.Exists); writer.Write(record.Bpm); writer.Write(record.Platinum); writer.Write(record.Designer ?? "");
                    }
                }
                byte[] checksum;
                using (var sha = SHA256.Create()) checksum = sha.ComputeHash(File.ReadAllBytes(temporary));
                using (var stream = new FileStream(temporary, FileMode.Append)) stream.Write(checksum, 0, checksum.Length);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}


