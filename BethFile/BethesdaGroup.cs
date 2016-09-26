namespace BethFile
{
    public struct BethesdaGroup
    {
        public BethesdaGroup(byte[] rawData)
        {
            this.RawData = new UArraySegment<byte>(rawData);
        }

        public BethesdaGroup(UArraySegment<byte> rawData)
        {
            this.RawData = rawData;
        }

        public UArraySegment<byte> RawData { get; }

        public UArraySegment<byte> PayloadData => new UArraySegment<byte>(this.RawData, 24, this.RawData.Count - 24);

        public B4S RecordType => UBitConverter.ToUInt32(this.PayloadData, 0);

        public uint PayloadSize => UBitConverter.ToUInt32(this.RawData, 4) - 24;

        public uint Label => UBitConverter.ToUInt32(this.RawData, 8);

        public BethesdaGroupType GroupType => (BethesdaGroupType)UBitConverter.ToInt32(this.RawData, 12);

        public bool IsDefault => this.RawData.Array == null;

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
                    return $"Ext block Y={UBitConverter.ToInt16(this.RawData, 8)}, X={UBitConverter.ToInt16(this.RawData, 10)}";

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return $"Ext sub-block Y={UBitConverter.ToInt16(this.RawData, 8)}, X={UBitConverter.ToInt16(this.RawData, 10)}";

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
