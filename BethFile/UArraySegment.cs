using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public T[] ToArray()
        {
            if (typeof(T) != typeof(byte))
            {
                return Enumerable.ToArray(this);
            }

            T[] result = new T[this.Count];
            UArraySegment<byte> bthis = (UArraySegment<byte>)(object)this;
            UBuffer.BlockCopy(bthis, 0, (byte[])(object)result, 0, this.Count);
            return result;
        }

        public IEnumerator<T> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class Enumerator : IEnumerator<T>
        {
            private UArraySegment<T> seg;

            private uint curr;

            internal Enumerator(UArraySegment<T> seg)
            {
                this.seg = seg;
            }

            public T Current => this.seg[this.curr - 1];

            object IEnumerator.Current => this.seg[this.curr - 1];

            void IDisposable.Dispose()
            {
            }

            public bool MoveNext() => this.curr <= this.seg.Count &&
                                      ++this.curr <= this.seg.Count;

            public void Reset() => this.curr = 0;
        }
    }
}
