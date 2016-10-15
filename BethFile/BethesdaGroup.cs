using static System.FormattableString;

namespace BethFile
{
    public struct BethesdaGroup
    {
        public BethesdaGroup(byte[] rawData)
        {
            this.Start = new UArrayPosition<byte>(rawData);
        }

        public BethesdaGroup(UArrayPosition<byte> start)
        {
            this.Start = start;
        }

        public UArrayPosition<byte> Start { get; }

        public UArraySegment<byte> RawData => new UArraySegment<byte>(this.Start, this.DataSize + 24);

        public UArraySegment<byte> PayloadData => new UArraySegment<byte>(this.PayloadStart, this.DataSize);

        public UArrayPosition<byte> PayloadStart => this.Start + 24;

        public B4S RecordType
        {
            get { return UBitConverter.ToUInt32(this.PayloadStart); }
            set { UBitConverter.SetUInt32(this.PayloadStart, value); }
        }

        public uint DataSize
        {
            get { return UBitConverter.ToUInt32(this.Start + 4) - 24; }
            set { UBitConverter.SetUInt32(this.Start + 4, value + 24); }
        }

        public uint Label
        {
            get { return UBitConverter.ToUInt32(this.Start + 8); }
            set { UBitConverter.SetUInt32(this.Start + 8, value); }
        }

        public BethesdaGroupType GroupType
        {
            get { return (BethesdaGroupType)UBitConverter.ToInt32(this.Start + 12); }
            set { UBitConverter.SetUInt32(this.Start + 12, (uint)value); }
        }

        public ushort Stamp
        {
            get { return UBitConverter.ToUInt16(this.Start + 16); }
            set { UBitConverter.SetUInt16(this.Start + 16, value); }
        }

        public ushort UNKNOWN_18
        {
            get { return UBitConverter.ToUInt16(this.Start + 18); }
            set { UBitConverter.SetUInt16(this.Start + 18, value); }
        }

        public ushort Version
        {
            get { return UBitConverter.ToUInt16(this.Start + 20); }
            set { UBitConverter.SetUInt16(this.Start + 20, value); }
        }

        public ushort UNKNOWN_22
        {
            get { return UBitConverter.ToUInt16(this.Start + 22); }
            set { UBitConverter.SetUInt16(this.Start + 22, value); }
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
                    return Invariant($"Ext block Y={UBitConverter.ToInt16(this.Start + 8)}, X={UBitConverter.ToInt16(this.Start + 10)}");

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return Invariant($"Ext sub-block Y={UBitConverter.ToInt16(this.Start + 8)}, X={UBitConverter.ToInt16(this.Start + 10)}");

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
