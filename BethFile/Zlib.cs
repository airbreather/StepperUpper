using System;
using System.IO;

using Ionic.Zlib;

namespace BethFile
{
    internal static class Zlib
    {
        internal static byte[] Uncompress(byte[] data) => Uncompress(new ArraySegment<byte>(data));
        internal static byte[] Uncompress(ArraySegment<byte> data)
        {
            int dataLength = checked((int)MBitConverter.To<uint>(data, 0));
            using (var ms = new MemoryStream(data.Array, data.Offset + 4, data.Count - 4))
            using (var def = new ZlibStream(ms, CompressionMode.Decompress, leaveOpen: true))
            using (var res = new MemoryStream(dataLength))
            {
                def.CopyTo(res);
                return res.GetBuffer();
            }
        }

        internal static ArraySegment<byte> Compress(ArraySegment<byte> data)
        {
            // xEdit uses the default level (6) when it does the same.
            using (var res = new MemoryStream())
            {
                int cnt = data.Count;

                // why do I have to do something like this to avoid allocating? ...
                unchecked
                {
                    res.WriteByte((byte)(cnt >> 00));
                    res.WriteByte((byte)(cnt >> 08));
                    res.WriteByte((byte)(cnt >> 16));
                    res.WriteByte((byte)(cnt >> 24));
                }

                using (var def = new ZlibStream(res, CompressionMode.Compress, leaveOpen: true))
                {
                    def.Write(data.Array, data.Offset, data.Count);
                }

                return res.TryGetBuffer(out var buffer) ? buffer : new ArraySegment<byte>(res.ToArray());
            }
        }
    }
}
