using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct MArraySegment<T> : IReadOnlyList<T>
    {
        public MArraySegment(T[] array) => this = new MArraySegment<T>(array, 0, checked((uint)array.LongLength));

        public MArraySegment(MArrayPosition<T> pos, uint count) => this = new MArraySegment<T>(pos.Array, pos.Offset, count);
        
        public MArraySegment(MArraySegment<T> seg, uint offset, uint count) => this = new MArraySegment<T>(seg.Array, seg.Offset + offset, count);

        public MArraySegment(T[] array, uint offset, uint count)
        {
            this.Array = array;
            this.Offset = offset;
            this.Count = count;
        }

        public T this[uint idx]
        {
            get => this.Array[this.Offset + idx];
            set => this.Array[this.Offset + idx] = value;
        }

        public T[] Array { get; }

        public uint Offset { get; }

        public uint Count { get; }

        public MArrayPosition<T> Pos => new MArrayPosition<T>(this.Array, this.Offset);

        int IReadOnlyCollection<T>.Count => checked((int)this.Count);

        T IReadOnlyList<T>.this[int index] => this[checked((uint)index)];

        public static implicit operator MArraySegment<T>(T[] array) => new MArraySegment<T>(array);

        public T[] ToArray()
        {
            if (typeof(T) != typeof(byte))
            {
                return Enumerable.ToArray(this);
            }

            T[] result = new T[this.Count];
            MArraySegment<byte> bthis = (MArraySegment<byte>)(object)this;
            MBuffer.BlockCopy(bthis, 0, (byte[])(object)result, 0, this.Count);
            return result;
        }

        public IEnumerator<T> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class Enumerator : IEnumerator<T>
        {
            private MArraySegment<T> seg;

            private uint curr;

            internal Enumerator(MArraySegment<T> seg) => this.seg = seg;

            public T Current => this.seg[this.curr - 1];

            object IEnumerator.Current => this.seg[this.curr - 1];

            void IDisposable.Dispose() { }

            public bool MoveNext() => this.curr <= this.seg.Count &&
                                      ++this.curr <= this.seg.Count;

            public void Reset() => this.curr = 0;
        }
    }
}
