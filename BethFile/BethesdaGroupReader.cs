using System;

using static BethFile.B4S;

namespace BethFile
{
    public sealed class BethesdaGroupReader
    {
        private readonly UArraySegment<byte> outer;

        private BethesdaGroupReaderState state;

        private UArraySegment<byte> inner;

        // group header eats 24 bytes.
        private uint pos = 24;

        public BethesdaGroupReader(BethesdaGroup group)
        {
            this.outer = group.RawData;
        }

        public BethesdaRecord CurrentRecord => new BethesdaRecord(this.EnsureInState(BethesdaGroupReaderState.Record));

        public BethesdaGroup CurrentSubgroup => new BethesdaGroup(this.EnsureInState(BethesdaGroupReaderState.Subgroup));

        public unsafe BethesdaGroupReaderState Read()
        {
            switch (this.state)
            {
                case BethesdaGroupReaderState.Record:
                case BethesdaGroupReaderState.Subgroup:
                    this.pos += this.inner.Count;
                    break;

                case BethesdaGroupReaderState.EndOfContent:
                    return this.state;
            }

            if (this.pos == this.outer.Count)
            {
                this.inner = default(UArraySegment<byte>);
                return this.state = BethesdaGroupReaderState.EndOfContent;
            }

            uint dataSize = UBitConverter.ToUInt32(this.outer, this.pos + 4);

            if (UBitConverter.ToInt32(this.outer, this.pos) == GRUP)
            {
                this.state = BethesdaGroupReaderState.Subgroup;
            }
            else
            {
                dataSize += 24;
                this.state = BethesdaGroupReaderState.Record;
            }

            this.inner = new UArraySegment<byte>(this.outer, this.pos, dataSize);

            return this.state;
        }

        private UArraySegment<byte> EnsureInState(BethesdaGroupReaderState state)
        {
            if (this.state != state)
            {
                throw new InvalidOperationException("You can only do that after a previous call to Read() returned " + state + ".  Last call actually returned " + this.state);
            }

            return this.inner;
        }
    }
}
