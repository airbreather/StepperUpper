using System;
using System.Collections.Generic;
using System.Linq;

using static System.FormattableString;
using static BethFile.B4S;

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
            this.CopyHeadersFrom(copyFrom);
            this.OriginalCompressedFieldData = (byte[])copyFrom.OriginalCompressedFieldData?.Clone();
            this.Fields.AddRange(copyFrom.Fields.Select(f => new Field(f)));
            this.Subgroups.AddRange(copyFrom.Subgroups.Select(g => new Group(g) { Parent = this }));
        }

        public Record(BethesdaFile copyFrom)
            : this(copyFrom.HeaderRecord)
        {
            this.Subgroups.AddRange(copyFrom.TopGroups.Select(g => new Group(g) { Parent = this }));
        }

        public Record(BethesdaRecord copyFrom)
        {
            this.RecordType = copyFrom.RecordType;
            this.Flags = copyFrom.Flags;
            this.Id = copyFrom.Id;
            this.Revision = copyFrom.Revision;
            this.Version = copyFrom.Version;
            this.UNKNOWN_22 = copyFrom.UNKNOWN_22;
            if (copyFrom.Flags.HasFlag(BethesdaRecordFlags.Compressed))
            {
                this.OriginalCompressedFieldData = copyFrom.Payload.ToArray();
            }

            uint? offsides = null;
            foreach (BethesdaField field in copyFrom.Fields)
            {
                if (field.FieldType == XXXX)
                {
                    offsides = UBitConverter.ToUInt32(field.PayloadStart);
                    continue;
                }

                byte[] payload = new byte[offsides ?? field.StoredSize];
                Buffer.BlockCopy(field.PayloadStart.Array, checked((int)field.PayloadStart.Offset), payload, 0, payload.Length);
                this.Fields.Add(new Field
                {
                    FieldType = field.FieldType,
                    Payload = payload
                });

                offsides = null;
            }
        }

        public Group Parent { get; set; }

        public B4S RecordType { get; set; } = DummyType;

        public BethesdaRecordFlags Flags { get; set; }

        public uint Id { get; set; }

        public uint Revision { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public byte[] OriginalCompressedFieldData { get; set; }

        public List<Field> Fields { get; } = new List<Field>();

        public List<Group> Subgroups { get; } = new List<Group>();

        public bool IsDummy => this.RecordType == DummyType;

        public void CopyHeadersFrom(Record rec)
        {
            this.RecordType = rec.RecordType;
            this.Flags = rec.Flags;
            this.Id = rec.Id;
            this.Revision = rec.Revision;
            this.Version = rec.Version;
            this.UNKNOWN_22 = rec.UNKNOWN_22;
        }

        public override string ToString() => Invariant($"[{this.RecordType}:{this.Id:X8}]");
    }
}
