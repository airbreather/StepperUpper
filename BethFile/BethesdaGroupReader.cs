using System;

using static BethFile.B4S;

namespace BethFile
{
    public sealed class BethesdaGroupReader
    {
        private BethesdaGroupReaderState state;

        private uint pos;

        public BethesdaGroupReader(BethesdaGroup group)
        {
            this.Group = group;
        }

        public BethesdaGroup Group { get; }

        public BethesdaRecord CurrentRecord => new BethesdaRecord(this.EnsureInState(BethesdaGroupReaderState.Record));

        public BethesdaGroup CurrentSubgroup => new BethesdaGroup(this.EnsureInState(BethesdaGroupReaderState.Subgroup));

        public void NotifyDeletion()
        {
            switch (this.state)
            {
                case BethesdaGroupReaderState.Record:
                case BethesdaGroupReaderState.Subgroup:
                case BethesdaGroupReaderState.Deleted:
                    this.state = BethesdaGroupReaderState.Deleted;
                    break;

                default:
                    throw new InvalidOperationException("You can only delete while actively reading.");
            }
        }

        public unsafe BethesdaGroupReaderState Read()
        {
            switch (this.state)
            {
                case BethesdaGroupReaderState.Subgroup:
                    this.pos += UBitConverter.ToUInt32(this.Group.PayloadStart + this.pos, 4);
                    break;

                case BethesdaGroupReaderState.Record:
                    this.pos += UBitConverter.ToUInt32(this.Group.PayloadStart + this.pos, 4) + 24;
                    break;

                case BethesdaGroupReaderState.EndOfContent:
                    return this.state;
            }

            if (this.pos >= this.Group.DataSize)
            {
                return this.state = BethesdaGroupReaderState.EndOfContent;
            }

            if (UBitConverter.ToInt32(this.Group.PayloadStart + this.pos, 0) == GRUP)
            {
                this.state = BethesdaGroupReaderState.Subgroup;
            }
            else
            {
                this.state = BethesdaGroupReaderState.Record;
            }

            return this.state;
        }

        private UArrayPosition<byte> EnsureInState(BethesdaGroupReaderState state)
        {
            if (this.state != state)
            {
                throw new InvalidOperationException("You can only do that after a previous call to Read() returned " + state + ".  Last call actually returned " + this.state);
            }

            return this.Group.PayloadStart + this.pos;
        }
    }
}
