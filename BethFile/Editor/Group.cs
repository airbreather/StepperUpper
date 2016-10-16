using System;
using System.Collections.Generic;
using System.Linq;

using static System.FormattableString;
using static BethFile.B4S;

namespace BethFile.Editor
{
    public sealed class Group
    {
        public Group()
        {
        }

        public Group(Group copyFrom)
        {
            this.CopyHeadersFrom(copyFrom);
            this.Records.AddRange(copyFrom.Records.Select(rec => new Record(rec)));
        }

        public Group(BethesdaGroup copyFrom)
        {
            this.GroupType = copyFrom.GroupType;
            this.Label = copyFrom.Label;
            this.Stamp = copyFrom.Stamp;
            this.UNKNOWN_18 = copyFrom.UNKNOWN_18;
            this.Version = copyFrom.Version;
            this.UNKNOWN_22 = copyFrom.UNKNOWN_22;

            Record record = null;
            Record dummyRecord = null;
            BethesdaGroupReader reader = new BethesdaGroupReader(copyFrom);
            BethesdaGroupReaderState state;
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        this.Records.Add(record = new Record(reader.CurrentRecord) { Parent = this });
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        var currSubgroup = reader.CurrentSubgroup;
                        switch (currSubgroup.GroupType)
                        {
                            case BethesdaGroupType.CellChildren:
                            case BethesdaGroupType.CellPersistentChildren:
                            case BethesdaGroupType.CellTemporaryChildren:
                            case BethesdaGroupType.CellVisibleDistantChildren:
                            case BethesdaGroupType.WorldChildren:
                            case BethesdaGroupType.TopicChildren:
                                break;

                            default:
                                record = null;
                                break;
                        }

                        if (record == null)
                        {
                            if (dummyRecord == null)
                            {
                                this.Records.Add(dummyRecord = new Record { Parent = this });
                            }

                            record = dummyRecord;
                        }

                        record.Subgroups.Add(new Group(currSubgroup) { Parent = record });
                        break;
                }
            }
        }

        public Record Parent { get; set; }

        public BethesdaGroupType GroupType { get; set; }

        public uint Label { get; set; }

        public ushort Stamp { get; set; }

        public ushort UNKNOWN_18 { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public List<Record> Records { get; } = new List<Record>();

        public void CopyHeadersFrom(Group copyFrom)
        {
            this.GroupType = copyFrom.GroupType;
            this.Label = copyFrom.Label;
            this.Stamp = copyFrom.Stamp;
            this.UNKNOWN_18 = copyFrom.UNKNOWN_18;
            this.Version = copyFrom.Version;
            this.UNKNOWN_22 = copyFrom.UNKNOWN_22;
        }

        public override string ToString()
        {
            switch (this.GroupType)
            {
                case BethesdaGroupType.Top:
                    return Invariant($"Top({(B4S)this.Label})");

                case BethesdaGroupType.WorldChildren:
                    return Invariant($"Children of [WRLD:{this.Label:X8}]");

                case BethesdaGroupType.InteriorCellBlock:
                    return Invariant($"Int block {this.Label}");

                case BethesdaGroupType.InteriorCellSubBlock:
                    return Invariant($"Int sub-block #{this.Label}");

                case BethesdaGroupType.ExteriorCellBlock:
                    return Invariant($"Ext block Y={unchecked((short)(this.Label >> 16))}, X={unchecked((short)(this.Label))}");

                case BethesdaGroupType.ExteriorCellSubBlock:
                    return Invariant($"Ext sub-block Y={unchecked((short)(this.Label >> 16))}, X={unchecked((short)(this.Label))}");

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
