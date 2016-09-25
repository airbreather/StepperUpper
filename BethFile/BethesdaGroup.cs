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

        public uint PayloadSize => UBitConverter.ToUInt32(this.RawData, 4) - 24;

        public uint Label => UBitConverter.ToUInt32(this.RawData, 8);

        public BethesdaGroupType GroupType => (BethesdaGroupType)UBitConverter.ToInt32(this.RawData, 12);

        public bool IsDefault => this.RawData.Array == null;

#if false
        public override string ToString()
        {
            byte[] data = BitConverter.GetBytes(this.LabelData);
            switch (this.GroupType)
            {
                case BethesdaGroupType.Top:
                    return $"Record of type {Encoding.ASCII.GetString(data, 0, 4)}";

                case BethesdaGroupType.WorldChildren:
                    return $"World children of WLRD:{this.LabelData.ToString("X")}";

                case BethesdaGroupType.InteriorCellBlock:
                    return $"Interior cell block #{this.LabelData}";

                case BethesdaGroupType.InteriorCellSubBlock:
                    return $"Interior cell sub-block #{this.LabelData}";

                case BethesdaGroupType.ExteriorCellBlock:
                    return $"Exterior cell block at Y={BitConverter.ToInt16(data, 0)}, X={BitConverter.ToInt16(data, 2)}";

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return $"Exterior cell sub-block at Y={BitConverter.ToInt16(data, 0)}, X={BitConverter.ToInt16(data, 2)}";

                case BethesdaGroupType.CellChildren:
                    return $"Cell children of CELL:{this.LabelData.ToString("X")}";

                case BethesdaGroupType.TopicChildren:
                    return $"Topic children of DIAL:{this.LabelData.ToString("X")}";

                case BethesdaGroupType.CellPersistentChildren:
                    return $"Cell persistent children of CELL:{this.LabelData.ToString("X")}";

                case BethesdaGroupType.CellTemporaryChildren:
                    return $"Cell temporary children of CELL:{this.LabelData.ToString("X")}";

                ////case BethesdaGroupType.CellVisibleDistantChildren:
                default:
                    return $"Cell visible distant children of CELL:{this.LabelData.ToString("X")}";
            }
        }
#endif
    }
}
