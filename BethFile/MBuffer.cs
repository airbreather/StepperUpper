using System;

namespace BethFile
{
    public static class MBuffer
    {
        public static unsafe void BlockCopy(ArraySegment<byte> src, int srcOffset, ArraySegment<byte> dst, int dstOffset, int count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(byte[] src, int srcOffset, ArraySegment<byte> dst, int dstOffset, int count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(ArraySegment<byte> src, int srcOffset, byte[] dst, int dstOffset, int count) => BlockCopy(src.Array, src.Offset + srcOffset, dst, dstOffset, count);

        public static unsafe void BlockCopy(MArrayPosition<byte> src, int srcOffset, MArrayPosition<byte> dst, int dstOffset, int count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(byte[] src, int srcOffset, MArrayPosition<byte> dst, int dstOffset, int count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(MArrayPosition<byte> src, int srcOffset, byte[] dst, int dstOffset, int count) => BlockCopy(src.Array, src.Offset + srcOffset, dst, dstOffset, count);

        public static unsafe void BlockCopy(MArrayPosition<byte> src, int srcOffset, ArraySegment<byte> dst, int dstOffset, int count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(ArraySegment<byte> src, int srcOffset, MArrayPosition<byte> dst, int dstOffset, int count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);

        public static unsafe void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
        {
            if (count == 0)
            {
                return;
            }

// switch to true if debugging gets to be a pain...
#if false
            for (uint i = 0; i < count; i++)
            {
                uint dstIdx = dstOffset + i;
                uint srcIdx = srcOffset + i;

                dst[dstIdx] = src[srcIdx];
            }
#else
            fixed (void* srcptr = &src[srcOffset])
            fixed (void* dstptr = &dst[dstOffset])
            {
                System.Buffer.MemoryCopy(srcptr, dstptr, count, count);
            }
#endif
        }
    }
}
