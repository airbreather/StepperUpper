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

        public B4S RecordType => UBitConverter.ToUInt32(this.PayloadStart);

        public uint DataSize
        {
            get { return UBitConverter.ToUInt32(this.Start + 4) - 24; }
            set { UBitConverter.SetUInt32(this.Start + 4, value + 24); }
        }

        public uint Label => UBitConverter.ToUInt32(this.Start + 8);

        public BethesdaGroupType GroupType => (BethesdaGroupType)UBitConverter.ToInt32(this.Start + 12);

        public override string ToString()
        {
            switch (this.GroupType)
            {
                case BethesdaGroupType.Top:
                    return $"Top({this.RecordType})";

                case BethesdaGroupType.WorldChildren:
                    return $"Children of [WLRD:{this.Label:X8}]";

                case BethesdaGroupType.InteriorCellBlock:
                    return $"Int block {this.Label}";

                case BethesdaGroupType.InteriorCellSubBlock:
                    return $"Int sub-block #{this.Label}";

                case BethesdaGroupType.ExteriorCellBlock:
                    return $"Ext block Y={UBitConverter.ToInt16(this.Start + 8)}, X={UBitConverter.ToInt16(this.Start + 10)}";

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return $"Ext sub-block Y={UBitConverter.ToInt16(this.Start + 8)}, X={UBitConverter.ToInt16(this.Start + 10)}";

                case BethesdaGroupType.CellChildren:
                    return $"Children of [CELL:{this.Label:X8}]";

                case BethesdaGroupType.TopicChildren:
                    return $"Children of [DIAL:{this.Label:X8}]";

                case BethesdaGroupType.CellPersistentChildren:
                    return $"Persistent children of [CELL:{this.Label:X8}]";

                case BethesdaGroupType.CellTemporaryChildren:
                    return $"Temporary children of [CELL:{this.Label:X8}]";

                ////case BethesdaGroupType.CellVisibleDistantChildren:
                default:
                    return $"Visible distant children of [CELL:{this.Label:X8}]";
            }
        }
    }
}
