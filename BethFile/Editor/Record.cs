using System.Collections.Generic;
using System.Linq;

namespace BethFile.Editor
{
    public sealed class Record
    {
        public static readonly B4S DummyType = new B4S("FUCK");

        public Record()
        {
        }

        public Record(Record copyFrom)
        {
            this.Type = copyFrom.Type;
            this.Flags = copyFrom.Flags;
            this.Id = copyFrom.Id;
            this.Revision = copyFrom.Revision;
            this.Version = copyFrom.Version;
            this.UNKNOWN_22 = copyFrom.UNKNOWN_22;
            this.OriginalCompressedFieldData = (byte[])copyFrom.OriginalCompressedFieldData?.Clone();
            this.Fields.AddRange(copyFrom.Fields.Select(f => new Field(f)));
            this.Subgroups.AddRange(copyFrom.Subgroups.Select(g => new Group(g)));
        }

        public B4S Type { get; set; } = DummyType;

        public BethesdaRecordFlags Flags { get; set; }

        public uint Id { get; set; }

        public uint Revision { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public byte[] OriginalCompressedFieldData { get; set; }

        public List<Field> Fields { get; } = new List<Field>();

        public List<Group> Subgroups { get; } = new List<Group>();

        public bool IsDummy => this.Type == DummyType;

        public override string ToString() => $"[{this.Type}:{this.Id:X8}]";
    }
}
