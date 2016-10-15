using System.Runtime.InteropServices;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct UArrayPosition<T>
    {
        public UArrayPosition(T[] array) : this(array, 0)
        {
        }

        public UArrayPosition(UArrayPosition<T> pos, uint offset) : this(pos.Array, pos.Offset + offset)
        {
        }

        public UArrayPosition(T[] array, uint offset)
        {
            this.Array = array;
            this.Offset = offset;
        }

        public T this[uint idx]
        {
            get { return this.Array[this.Offset + idx]; }
            set { this.Array[this.Offset + idx] = value; }
        }

        public T[] Array { get; }

        public uint Offset { get; }

        public static UArrayPosition<T> operator +(UArrayPosition<T> start, uint offset) => new UArrayPosition<T>(start.Array, start.Offset + offset);
        public static UArrayPosition<T> operator -(UArrayPosition<T> start, uint offset) => new UArrayPosition<T>(start.Array, start.Offset - offset);

        public static UArrayPosition<T> operator +(UArrayPosition<T> start, UArrayPosition<T> offset) => new UArrayPosition<T>(start.Array, start.Offset + offset.Offset);
        public static UArrayPosition<T> operator -(UArrayPosition<T> start, UArrayPosition<T> offset) => new UArrayPosition<T>(start.Array, start.Offset - offset.Offset);
    }
}
