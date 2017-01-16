using System;

using static System.FormattableString;

namespace BethFile
{
    public struct BethesdaGroup
    {
        public BethesdaGroup(byte[] rawData) => this.Start = new MArrayPosition<byte>(rawData);

        public BethesdaGroup(MArrayPosition<byte> start) => this.Start = start;

        public MArrayPosition<byte> Start { get; }

        public ArraySegment<byte> RawData => new ArraySegment<byte>(this.Start.Array, this.Start.Offset, checked((int)this.DataSize + 24));

        public ArraySegment<byte> PayloadData => new ArraySegment<byte>(this.Start.Array, this.Start.Offset + 24, checked((int)this.DataSize));

        public MArrayPosition<byte> PayloadStart => this.Start + 24;

        public B4S RecordType => this.Label;

        public uint DataSize
        {
            get => MBitConverter.To<uint>(this.Start + 4) - 24;
            set => MBitConverter.Set(this.Start + 4, value + 24);
        }

        public uint Label
        {
            get => MBitConverter.To<uint>(this.Start + 8);
            set => MBitConverter.Set(this.Start + 8, value);
        }

        public BethesdaGroupType GroupType
        {
            get => MBitConverter.To<BethesdaGroupType>(this.Start + 12);
            set => MBitConverter.Set(this.Start + 12, value);
        }

        public ushort Stamp
        {
            get => MBitConverter.To<ushort>(this.Start + 16);
            set => MBitConverter.Set(this.Start + 16, value);
        }

        public ushort UNKNOWN_18
        {
            get => MBitConverter.To<ushort>(this.Start + 18);
            set => MBitConverter.Set(this.Start + 18, value);
        }

        public ushort Version
        {
            get => MBitConverter.To<ushort>(this.Start + 20);
            set => MBitConverter.Set(this.Start + 20, value);
        }

        public ushort UNKNOWN_22
        {
            get => MBitConverter.To<ushort>(this.Start + 22);
            set => MBitConverter.Set(this.Start + 22, value);
        }

        public override string ToString()
        {
            switch (this.GroupType)
            {
                case BethesdaGroupType.Top:
                    return Invariant($"Top({this.RecordType})");

                case BethesdaGroupType.WorldChildren:
                    return Invariant($"Children of [WRLD:{this.Label:X8}]");

                case BethesdaGroupType.InteriorCellBlock:
                    return Invariant($"Int block {this.Label}");

                case BethesdaGroupType.InteriorCellSubBlock:
                    return Invariant($"Int sub-block #{this.Label}");

                case BethesdaGroupType.ExteriorCellBlock:
                    return Invariant($"Ext block Y={MBitConverter.To<ushort>(this.Start + 8)}, X={MBitConverter.To<ushort>(this.Start + 10)}");

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return Invariant($"Ext sub-block Y={MBitConverter.To<ushort>(this.Start + 8)}, X={MBitConverter.To<ushort>(this.Start + 10)}");

                case BethesdaGroupType.CellChildren:
                    return Invariant($"Children of [CELL:{this.Label:X8}]");

                case BethesdaGroupType.TopicChildren:
                    return Invariant($"Children of [DIAL:{this.Label:X8}]");

                case BethesdaGroupType.CellPersistentChildren:
                    return Invariant($"Persistent children of [CELL:{this.Label:X8}]");

                case BethesdaGroupType.CellTemporaryChildren:
                    return Invariant($"Temporary children of [CELL:{this.Label:X8}]");

                ////case BethesdaGroupType.CellVisibleDistantChildren:
                default:
                    return Invariant($"Visible distant children of [CELL:{this.Label:X8}]");
            }
        }
    }
}
