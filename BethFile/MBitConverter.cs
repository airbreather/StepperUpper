using System;
using System.Runtime.CompilerServices;

namespace BethFile
{
    public static class MBitConverter
    {
        public static T To<T>(MArrayPosition<byte> value) => To<T>(value.Array, value.Offset);
        public static T To<T>(ArraySegment<byte> value, int startIndex) => To<T>(value.Array, value.Offset + startIndex);
        public static T To<T>(byte[] array, int startIndex) => Unsafe.ReadUnaligned<T>(ref array[startIndex]);

        public static void Set<T>(MArrayPosition<byte> pos, T val) => Set(pos.Array, pos.Offset, val);
        public static void Set<T>(byte[] arr, int startIndex, T val) => Unsafe.WriteUnaligned(ref arr[startIndex], val);
    }
}
