namespace BethFile
{
    public static class UBuffer
    {
        public static unsafe void BlockCopy(UArraySegment<byte> src, uint srcOffset, UArraySegment<byte> dst, uint dstOffset, uint count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(byte[] src, uint srcOffset, UArraySegment<byte> dst, uint dstOffset, uint count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(UArraySegment<byte> src, uint srcOffset, byte[] dst, uint dstOffset, uint count) => BlockCopy(src.Array, src.Offset + srcOffset, dst, dstOffset, count);

        public static unsafe void BlockCopy(UArrayPosition<byte> src, uint srcOffset, UArrayPosition<byte> dst, uint dstOffset, uint count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(byte[] src, uint srcOffset, UArrayPosition<byte> dst, uint dstOffset, uint count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(UArrayPosition<byte> src, uint srcOffset, byte[] dst, uint dstOffset, uint count) => BlockCopy(src.Array, src.Offset + srcOffset, dst, dstOffset, count);

        public static unsafe void BlockCopy(UArrayPosition<byte> src, uint srcOffset, UArraySegment<byte> dst, uint dstOffset, uint count) => BlockCopy(src.Array, src.Offset + srcOffset, dst.Array, dst.Offset + dstOffset, count);
        public static unsafe void BlockCopy(UArraySegment<byte> src, uint srcOffset, UArrayPosition<byte> dst, uint dstOffset, uint count) => BlockCopy(src, srcOffset, dst.Array, dst.Offset + dstOffset, count);

        public static unsafe void BlockCopy(byte[] src, uint srcOffset, byte[] dst, uint dstOffset, uint count)
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
