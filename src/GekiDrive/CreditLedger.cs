using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GekiDrive
{
    internal sealed class CreditLedger
    {
        private readonly string path;
        private HashSet<string> ids = new HashSet<string>();
        internal int Balance { get; private set; }
        internal CreditLedger(string path, int initial)
        {
            this.path = path;
            if (!File.Exists(path)) { Balance = Math.Max(0, Math.Min(9999, initial)); return; }
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44 || bytes.Length > 60 * 1024 * 1024) throw new InvalidDataException("Credit ledger size invalid.");
            using (var hash = SHA256.Create()) { byte[] digest = hash.ComputeHash(bytes, 0, bytes.Length - 32); for (int i = 0; i < 32; i++) if (digest[i] != bytes[bytes.Length - 32 + i]) throw new InvalidDataException("Credit ledger checksum mismatch."); }
            using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32))
            using (var r = new BinaryReader(stream))
            {
                if (r.ReadInt32() != 0x314C4347) throw new InvalidDataException("Unknown credit ledger format.");
                Balance = r.ReadInt32(); int count = r.ReadInt32();
                if (Balance < 0 || Balance > 9999 || count < 0 || count > 100000) throw new InvalidDataException("Credit ledger values invalid.");
                for (int i = 0; i < count; i++)
                {
                    int length = r.ReadInt32(); if (length < 1 || length > 512) throw new InvalidDataException("Credit transaction id size invalid.");
                    byte[] text = r.ReadBytes(length); if (text.Length != length) throw new EndOfStreamException();
                    string id = Encoding.UTF8.GetString(text); ValidateId(id); if (!ids.Add(id)) throw new InvalidDataException("Duplicate credit transaction id.");
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected credit ledger data.");
            }
        }
        internal bool Add(string id, int count)
        {
            ValidateId(id); if (count < 1 || count > 9999) throw new ArgumentOutOfRangeException("count");
            if (ids.Contains(id)) return false;
            if (Balance + count > 9999 || ids.Count >= 100000) throw new InvalidOperationException("Virtual credit / transaction limit reached.");
            var next = new HashSet<string>(ids); next.Add(id); Save(Balance + count, next); ids = next; Balance += count; return true;
        }
        internal bool Spend(int count)
        { if (count < 0 || count > Balance) return false; Save(Balance - count, ids); Balance -= count; return true; }
        private void Save(int balance, HashSet<string> transactions)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            {
                using (var w = new BinaryWriter(stream))
                {
                    w.Write(0x314C4347); w.Write(balance); w.Write(transactions.Count);
                    foreach (var id in transactions) { byte[] text = Encoding.UTF8.GetBytes(id); w.Write(text.Length); w.Write(text); }
                }
                payload = stream.ToArray();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
            using (var hash = SHA256.Create()) { stream.Write(payload, 0, payload.Length); byte[] digest = hash.ComputeHash(payload); stream.Write(digest, 0, digest.Length); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        private static void ValidateId(string id) { if (string.IsNullOrEmpty(id) || id.Length > 128) throw new InvalidDataException("Invalid credit id."); foreach (char ch in id) if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) throw new InvalidDataException("Invalid credit id characters."); }
    }
}
