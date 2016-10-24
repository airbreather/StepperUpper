using System.Collections.Generic;

using static System.FormattableString;
using static BethFile.B4S;

namespace BethFile
{
    public struct BethesdaRecord
    {
        public BethesdaRecord(byte[] rawData)
        {
            this.Start = new UArrayPosition<byte>(rawData);
        }

        public BethesdaRecord(UArrayPosition<byte> rawData)
        {
            this.Start = rawData;
        }

        public UArrayPosition<byte> Start { get; }

        public UArraySegment<byte> RawData => new UArraySegment<byte>(this.Start, this.DataSize + 24);

        public UArrayPosition<byte> PayloadStart => this.Start + 24;

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.PayloadStart, this.DataSize);

        public B4S RecordType
        {
            get { return UBitConverter.ToUInt32(this.Start); }
            set { UBitConverter.SetUInt32(this.Start, value); }
        }

        public uint DataSize
        {
            get { return UBitConverter.ToUInt32(this.Start + 4); }
            set { UBitConverter.SetUInt32(this.Start + 4, value); }
        }

        public BethesdaRecordFlags Flags
        {
            get { return (BethesdaRecordFlags)UBitConverter.ToUInt32(this.Start + 8); }
            set { UBitConverter.SetUInt32(this.Start + 8, (uint)value); }
        }

        public uint Id
        {
            get { return UBitConverter.ToUInt32(this.Start + 12); }
            set { UBitConverter.SetUInt32(this.Start + 12, value); }
        }

        public uint Revision
        {
            get { return UBitConverter.ToUInt32(this.Start + 16); }
            set { UBitConverter.SetUInt32(this.Start + 16, value); }
        }

        public ushort Version
        {
            get { return UBitConverter.ToUInt16(this.Start + 20); }
            set { UBitConverter.SetUInt32(this.Start + 20, value); }
        }

        public ushort UNKNOWN_22
        {
            get { return UBitConverter.ToUInt16(this.Start + 22); }
            set { UBitConverter.SetUInt32(this.Start + 22, value); }
        }

        public IEnumerable<BethesdaField> Fields
        {
            get
            {
                UArraySegment<byte> payload = this.Payload;
                if (this.Flags.HasFlag(BethesdaRecordFlags.Compressed))
                {
                    payload = Zlib.Uncompress(payload);
                }

                return GetFields(payload);
            }
        }

        public static IEnumerable<BethesdaField> GetFields(UArraySegment<byte> payload)
        {
            uint pos = 0;
            uint? offsides = null;
            while (pos != payload.Count)
            {
                uint sz = offsides ?? UBitConverter.ToUInt16(payload, pos + 4);
                BethesdaField field = new BethesdaField(new UArraySegment<byte>(payload, pos, sz + 6u));
                yield return field;
                if (field.FieldType == XXXX)
                {
                    offsides = UBitConverter.ToUInt32(field.PayloadStart);
                }
                else
                {
                    offsides = null;
                }

                pos += sz + 6u;
            }
        }

        public override string ToString() => Invariant($"[{this.RecordType}:{this.Id:X8}]");
    }
}
