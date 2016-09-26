using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct UArraySegment<T> : IReadOnlyList<T>
    {
        public UArraySegment(T[] array) : this(array, 0, unchecked((uint)array.LongLength))
        {
        }

        public UArraySegment(UArrayPosition<T> pos, uint count) : this(pos.Array, pos.Offset, count)
        {
        }
        
        public UArraySegment(UArraySegment<T> seg, uint offset, uint count) : this(seg.Array, seg.Offset + offset, count)
        {
        }

        public UArraySegment(T[] array, uint offset, uint count)
        {
            this.Array = array;
            this.Offset = offset;
            this.Count = count;
        }

        public T this[uint idx]
        {
            get { return this.Array[this.Offset + idx]; }
            set { this.Array[this.Offset + idx] = value; }
        }

        public T[] Array { get; }

        public uint Offset { get; }

        public uint Count { get; }

        public UArrayPosition<T> Pos => new UArrayPosition<T>(this.Array, this.Offset);

        int IReadOnlyCollection<T>.Count => checked((int)this.Count);

        T IReadOnlyList<T>.this[int index] => this[checked((uint)index)];

        public IEnumerator<T> GetEnumerator()
        {
            for (uint i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
