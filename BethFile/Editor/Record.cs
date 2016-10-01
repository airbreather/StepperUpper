using System.Collections.Generic;

namespace BethFile.Editor
{
    public sealed class Record
    {
        public static readonly B4S DummyType = new B4S("FUCK");

        public B4S Type { get; set; } = DummyType;

        public BethesdaRecordFlags Flags { get; set; }

        public uint Id { get; set; }

        public uint Revision { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public byte[] OriginalCompressedFieldData { get; set; }

        public List<Field> Fields { get; } = new List<Field>();

        public List<Group> Subgroups { get; } = new List<Group>();

        public override string ToString() => $"[{this.Type}:{this.Id:X8}]";
    }
}
