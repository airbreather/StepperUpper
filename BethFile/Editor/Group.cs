using System.Collections.Generic;
using System.Linq;

namespace BethFile.Editor
{
    public sealed class Group
    {
        public Group()
        {
        }

        public Group(Group copyFrom)
        {
            this.Type = copyFrom.Type;
            this.Label = copyFrom.Label;
            this.Stamp = copyFrom.Stamp;
            this.UNKNOWN_18 = copyFrom.UNKNOWN_18;
            this.Version = copyFrom.Version;
            this.UNKNOWN_22 = copyFrom.UNKNOWN_22;
            this.Records.AddRange(copyFrom.Records.Select(rec => new Record(rec)));
        }

        public BethesdaGroupType Type { get; set; }

        public uint Label { get; set; }

        public ushort Stamp { get; set; }

        public ushort UNKNOWN_18 { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public List<Record> Records { get; } = new List<Record>();

        public override string ToString()
        {
            switch (this.Type)
            {
                case BethesdaGroupType.Top:
                    return $"Top({(B4S)this.Label})";

                case BethesdaGroupType.WorldChildren:
                    return $"Children of [WRLD:{this.Label:X8}]";

                case BethesdaGroupType.InteriorCellBlock:
                    return $"Int block {this.Label}";

                case BethesdaGroupType.InteriorCellSubBlock:
                    return $"Int sub-block #{this.Label}";

                case BethesdaGroupType.ExteriorCellBlock:
                    return $"Ext block Y={unchecked((short)(this.Label >> 16))}, X={unchecked((short)(this.Label))}";

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return $"Ext sub-block Y={unchecked((short)(this.Label >> 16))}, X={unchecked((short)(this.Label))}";

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
