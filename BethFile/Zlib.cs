using System;
using System.IO;

using AirBreather.IO;

using Ionic.Zlib;

namespace BethFile
{
    internal static class Zlib
    {
        internal static byte[] Uncompress(UArraySegment<byte> data)
        {
            uint dataLength = UBitConverter.ToUInt32(data, 0);
            uint remaining = dataLength;

            // TODO: System.Buffers
            byte[] payloadArray = new byte[dataLength];
            using (var ms = new MemoryStream(data.Array, checked((int)(data.Offset + 4)), checked((int)(data.Count - 4)), false))
            using (var def = new ZlibStream(ms, CompressionMode.Decompress, leaveOpen: true))
            {
                byte[] buf2 = new byte[AsyncFile.FullCopyBufferSize];

                int cnt;
                while (remaining != 0 &&
                       (cnt = def.Read(buf2, 0, unchecked((int)Math.Min(remaining, buf2.Length)))) != 0)
                {
                    UBuffer.BlockCopy(buf2, 0, payloadArray, dataLength - remaining, unchecked((uint)cnt));
                    remaining -= unchecked((uint)cnt);
                }
            }

            return payloadArray;
        }

        internal static byte[] Compress(UArraySegment<byte> data)
        {
            using (var ms = new MemoryStream())
            {
                // TODO: System.Buffers
                byte[] buf = new byte[AsyncFile.FullCopyBufferSize];
                uint cnt = data.Count;
                UBitConverter.SetUInt32(buf, 0, cnt);
                ms.Write(buf, 0, 4);

                // xEdit uses the default level (6) when it does the same.
                using (var cmp = new ZlibStream(ms, CompressionMode.Compress, leaveOpen: true))
                {
                    uint pos = 0;
                    while (pos < cnt)
                    {
                        uint sz = Math.Min(cnt - pos, AsyncFile.FullCopyBufferSize);
                        UBuffer.BlockCopy(data, pos, buf, 0, sz);
                        cmp.Write(buf, 0, unchecked((int)sz));
                        pos += sz;
                    }
                }

                return ms.ToArray();
            }
        }
    }
}
