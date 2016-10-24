using System;
using System.Collections.Generic;

using static System.FormattableString;
using static BethFile.B4S;

namespace BethFile.Editor
{
    public sealed class Record
    {
        public static readonly B4S DummyType = new B4S("eggs");

        private Lazy<List<Field>> fields = new Lazy<List<Field>>();

        public Record()
        {
        }

        public Record(Record copyFrom)
        {
            this.CopyHeadersFrom(copyFrom);
            this.CompressedFieldData = copyFrom.CompressedFieldData;

            Lazy<List<Field>> otherFields = copyFrom.fields;
            if (otherFields.IsValueCreated)
            {
                List<Field> myFields = new List<Field>(otherFields.Value);
                this.fields = new Lazy<List<Field>>(() => myFields);
                this.MakeFieldsNotLazy();
            }
            else
            {
                this.fields = new Lazy<List<Field>>(() => new List<Field>(otherFields.Value));
            }

            this.Subgroups.Capacity = copyFrom.Subgroups.Count;
            foreach (var subgroup in copyFrom.Subgroups)
            {
                this.Subgroups.Add(new Group(subgroup) { Parent = this });
            }
        }

        public Record(BethesdaFile copyFrom)
            : this(copyFrom.HeaderRecord)
        {
            this.Subgroups.Capacity = copyFrom.TopGroups.Length;
            foreach (var g in copyFrom.TopGroups)
            {
                this.Subgroups.Add(new Group(g) { Parent = this });
            }
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
                this.CompressedFieldData = copyFrom.Payload.ToArray();
                this.fields = new Lazy<List<Field>>(() => CopyFieldsFrom(BethesdaRecord.GetFields(Zlib.Uncompress(this.CompressedFieldData))));
            }
            else
            {
                List<Field> myFields = CopyFieldsFrom(copyFrom.Fields);
                this.fields = new Lazy<List<Field>>(() => myFields);
                this.MakeFieldsNotLazy();
            }
        }

        public Group Parent { get; set; }

        public B4S RecordType { get; set; } = DummyType;

        public BethesdaRecordFlags Flags { get; set; }

        public uint Id { get; set; }

        public uint Revision { get; set; }

        public ushort Version { get; set; }

        public ushort UNKNOWN_22 { get; set; }

        public byte[] CompressedFieldData { get; set; }

        public List<Field> Fields
        {
            get
            {
                List<Field> result;
                if (!this.fields.IsValueCreated)
                {
                    result = new List<Field>(this.fields.Value);
                    this.fields = new Lazy<List<Field>>(() => result);
                }

                return this.fields.Value;
            }
        }

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

        private static List<Field> CopyFieldsFrom(IEnumerable<BethesdaField> source)
        {
            List<Field> result = new List<Field>();

            uint? offsides = null;
            foreach (BethesdaField field in source)
            {
                if (field.FieldType == XXXX)
                {
                    offsides = UBitConverter.ToUInt32(field.PayloadStart);
                    continue;
                }

                byte[] payload = new byte[offsides ?? field.StoredSize];
                Buffer.BlockCopy(field.PayloadStart.Array, checked((int)field.PayloadStart.Offset), payload, 0, payload.Length);
                result.Add(new Field
                {
                    FieldType = field.FieldType,
                    Payload = payload
                });

                offsides = null;
            }

            return result;
        }

        private void MakeFieldsNotLazy()
        {
            if (!this.fields.IsValueCreated && this.fields.Value == null)
            {
                throw new Exception("This is not actually possible unless there's a bug in Lazy<T>.");
            }
        }
    }
}
