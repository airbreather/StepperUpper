using System.Runtime.InteropServices;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct MArrayPosition<T>
    {
        public MArrayPosition(T[] array) => this = new MArrayPosition<T>(array, 0);

        public MArrayPosition(MArrayPosition<T> pos, uint offset) => this = new MArrayPosition<T>(pos.Array, pos.Offset + offset);

        public MArrayPosition(T[] array, uint offset)
        {
            this.Array = array;
            this.Offset = offset;
        }

        public T this[uint idx]
        {
            get => this.Array[this.Offset + idx];
            set => this.Array[this.Offset + idx] = value;
        }

        public T[] Array { get; }

        public uint Offset { get; }

        public static MArrayPosition<T> operator +(MArrayPosition<T> start, uint offset) => new MArrayPosition<T>(start.Array, start.Offset + offset);
        public static MArrayPosition<T> operator -(MArrayPosition<T> start, uint offset) => new MArrayPosition<T>(start.Array, start.Offset - offset);

        public static MArrayPosition<T> operator +(MArrayPosition<T> start, MArrayPosition<T> offset) => new MArrayPosition<T>(start.Array, start.Offset + offset.Offset);
        public static MArrayPosition<T> operator -(MArrayPosition<T> start, MArrayPosition<T> offset) => new MArrayPosition<T>(start.Array, start.Offset - offset.Offset);
    }
}
