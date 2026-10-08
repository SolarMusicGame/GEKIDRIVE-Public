using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GekiDrive
{
    internal static class WireCodec
    {
        internal static string WebSocketAccept(string key)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(key); } catch { throw new InvalidDataException("Invalid WebSocket key."); }
            if (bytes.Length != 16) throw new InvalidDataException("Invalid WebSocket key length.");
            using (var hash = SHA1.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
        internal static byte[] WebSocketFrame(byte[] payload, byte opcode)
        {
            if (payload.Length > 65535) throw new InvalidDataException("WebSocket frame too large.");
            using (var stream = new MemoryStream())
            {
                stream.WriteByte((byte)(0x80 | opcode));
                if (payload.Length < 126) stream.WriteByte((byte)payload.Length);
                else { stream.WriteByte(126); stream.WriteByte((byte)(payload.Length >> 8)); stream.WriteByte((byte)payload.Length); }
                stream.Write(payload, 0, payload.Length); return stream.ToArray();
            }
        }
        internal static byte[] LedFrame(ushort sequence, byte[] rgb)
        {
            if (rgb.Length != 67 * 3) throw new InvalidDataException("GD1 requires 67 RGB pixels.");
            byte[] bytes = new byte[210]; bytes[0] = 71; bytes[1] = 68; bytes[2] = 49; bytes[3] = 1;
            bytes[4] = (byte)sequence; bytes[5] = (byte)(sequence >> 8); bytes[6] = 67;
            Buffer.BlockCopy(rgb, 0, bytes, 7, rgb.Length);
            ushort crc = Crc16(bytes, 208); bytes[208] = (byte)crc; bytes[209] = (byte)(crc >> 8); return bytes;
        }
        internal static ushort Crc16(byte[] bytes, int count)
        {
            ushort value = 0xFFFF;
            for (int i = 0; i < count; i++) { value ^= bytes[i]; for (int bit = 0; bit < 8; bit++) value = (ushort)((value >> 1) ^ ((value & 1) != 0 ? 0xA001 : 0)); }
            return value;
        }
    }
}
