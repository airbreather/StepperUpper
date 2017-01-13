using System;

using static BethFile.B4S;

namespace BethFile
{
    public sealed class BethesdaGroupReader
    {
        private BethesdaGroupReaderState state;

        private uint pos;

        public BethesdaGroupReader(BethesdaGroup group) => this.Group = group;

        public BethesdaGroup Group { get; }

        public BethesdaRecord CurrentRecord => new BethesdaRecord(this.EnsureInState(BethesdaGroupReaderState.Record));

        public BethesdaGroup CurrentSubgroup => new BethesdaGroup(this.EnsureInState(BethesdaGroupReaderState.Subgroup));

        public BethesdaGroupReaderState Read()
        {
            switch (this.state)
            {
                case BethesdaGroupReaderState.Subgroup:
                    this.pos += MBitConverter.To<uint>(this.Group.PayloadStart + this.pos + 4);
                    break;

                case BethesdaGroupReaderState.Record:
                    this.pos += MBitConverter.To<uint>(this.Group.PayloadStart + this.pos + 4) + 24;
                    break;

                case BethesdaGroupReaderState.EndOfContent:
                    return this.state;
            }

            if (this.pos >= this.Group.DataSize)
            {
                return this.state = BethesdaGroupReaderState.EndOfContent;
            }

            if (MBitConverter.To<B4S>(this.Group.PayloadStart + this.pos) == GRUP)
            {
                this.state = BethesdaGroupReaderState.Subgroup;
            }
            else
            {
                this.state = BethesdaGroupReaderState.Record;
            }

            return this.state;
        }

        private MArrayPosition<byte> EnsureInState(BethesdaGroupReaderState state) =>
            this.state == state
                ? this.Group.PayloadStart + this.pos
                : throw new InvalidOperationException("You can only do that after a previous call to Read() returned " + state + ".  Last call actually returned " + this.state);
    }
}
