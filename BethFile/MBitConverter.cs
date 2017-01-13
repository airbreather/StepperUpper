using System.Runtime.CompilerServices;

namespace BethFile
{
    public static class MBitConverter
    {
        public static T To<T>(MArrayPosition<byte> value) => To<T>(value.Array, value.Offset);
        public static T To<T>(MArraySegment<byte> value, uint startIndex) => To<T>(value.Array, value.Offset + startIndex);
        public static T To<T>(byte[] array, uint startIndex) => Unsafe.As<byte, T>(ref array[startIndex]);

        public static void Set<T>(MArrayPosition<byte> pos, T val) => Set(pos.Array, pos.Offset, val);
        public static void Set<T>(byte[] arr, uint startIndex, T val) => Unsafe.As<byte, T>(ref arr[startIndex]) = val;
    }
}
