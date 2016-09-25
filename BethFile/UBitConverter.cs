using System;

using AirBreather;

namespace BethFile
{
    // much of this taken from:
    // https://github.com/dotnet/coreclr/blob/85391f2a227d99084656a7016a29bfb2664923b5/src/mscorlib/src/System/BitConverter.cs
    public static class UBitConverter
    {
        private static bool IsLittleEndian => BitConverter.IsLittleEndian;

        public static short ToInt16(UArraySegment<byte> value, uint startIndex) => ToInt16(value.Array, value.Offset + startIndex);
        public static ushort ToUInt16(UArraySegment<byte> value, uint startIndex) => ToUInt16(value.Array, value.Offset + startIndex);
        public static int ToInt32(UArraySegment<byte> value, uint startIndex) => ToInt32(value.Array, value.Offset + startIndex);
        public static uint ToUInt32(UArraySegment<byte> value, uint startIndex) => ToUInt32(value.Array, value.Offset + startIndex);
        public static long ToInt64(UArraySegment<byte> value, uint startIndex) => ToInt64(value.Array, value.Offset + startIndex);
        public static ulong ToUInt64(UArraySegment<byte> value, uint startIndex) => ToUInt64(value.Array, value.Offset + startIndex);

        public static uint ToUInt32(byte[] value, uint startIndex) => unchecked((uint)ToInt32(value, startIndex));

        public static unsafe int ToInt32(byte[] value, uint startIndex)
        {
            value.ValidateNotNull(nameof(value));
            startIndex.ValidateInRange(nameof(startIndex), 0, unchecked((uint)(value.Length - 3)));

            fixed( byte * pbyte = &value[startIndex]) {
                if( startIndex % 4 == 0) { // data is aligned 
                    return *((int *) pbyte);
                }
                else {
                    if( IsLittleEndian) { 
                        return (*pbyte) | (*(pbyte + 1) << 8)  | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24);
                    }
                    else {
                        return (*pbyte << 24) | (*(pbyte + 1) << 16)  | (*(pbyte + 2) << 8) | (*(pbyte + 3));                        
                    }
                }
            }
        }

        public static ushort ToUInt16(byte[] value, uint startIndex) => unchecked((ushort)ToInt16(value, startIndex));

        public static unsafe short ToInt16(byte[] value, uint startIndex)
        {
            value.ValidateNotNull(nameof(value));
            startIndex.ValidateInRange(nameof(startIndex), 0, unchecked((uint)(value.Length - 1)));

            fixed( byte * pbyte = &value[startIndex]) {
                if( startIndex % 2 == 0) { // data is aligned 
                    return *((short *) pbyte);
                }
                else {
                    if( IsLittleEndian) { 
                        return (short)((*pbyte) | (*(pbyte + 1) << 8)) ;
                    }
                    else {
                        return (short)((*pbyte << 8) | (*(pbyte + 1)));                        
                    }
                }
            }
        }

        public static ulong ToUInt64(byte[] value, uint startIndex) => unchecked((ulong)ToInt64(value, startIndex));

        public static unsafe long ToInt64(byte[] value, uint startIndex)
        {
            value.ValidateNotNull(nameof(value));
            startIndex.ValidateInRange(nameof(startIndex), 0, unchecked((uint)(value.Length - 7)));

            fixed ( byte * pbyte = &value[startIndex]) {
                if( startIndex % 8 == 0) { // data is aligned 
                    return *((long *) pbyte);
                }
                else {
                    if( IsLittleEndian) { 
                        int i1 = (*pbyte) | (*(pbyte + 1) << 8)  | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24);                        
                        int i2  = (*(pbyte+4)) | (*(pbyte + 5) << 8)  | (*(pbyte + 6) << 16) | (*(pbyte + 7) << 24);
                        return (uint)i1 | ((long)i2 << 32);
                    }
                    else {
                        int i1 = (*pbyte << 24) | (*(pbyte + 1) << 16)  | (*(pbyte + 2) << 8) | (*(pbyte + 3));                        
                        int i2  = (*(pbyte+4) << 24) | (*(pbyte + 5) << 16)  | (*(pbyte + 6) << 8) | (*(pbyte + 7));
                        return (uint)i2 | ((long)i1 << 32);
                    }
                }
            }     
        }
    }
}
