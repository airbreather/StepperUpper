using System.Collections.Generic;
using System.Diagnostics;

using static System.FormattableString;
using static BethFile.B4S;

namespace BethFile
{
    public struct BethesdaRecord
    {
        public BethesdaRecord(byte[] rawData) => this.Start = new MArrayPosition<byte>(rawData);

        public BethesdaRecord(MArrayPosition<byte> rawData) => this.Start = rawData;

        public MArrayPosition<byte> Start { get; }

        public MArraySegment<byte> RawData => new MArraySegment<byte>(this.Start, this.DataSize + 24);

        public MArrayPosition<byte> PayloadStart => this.Start + 24;

        public MArraySegment<byte> Payload => new MArraySegment<byte>(this.PayloadStart, this.DataSize);

        public B4S RecordType
        {
            get => MBitConverter.To<B4S>(this.Start);
            set => MBitConverter.Set(this.Start, value);
        }

        public uint DataSize
        {
            get => MBitConverter.To<uint>(this.Start + 4);
            set => MBitConverter.Set(this.Start + 4, value);
        }

        public BethesdaRecordFlags Flags
        {
            get => MBitConverter.To<BethesdaRecordFlags>(this.Start + 8);
            set => MBitConverter.Set(this.Start + 8, value);
        }

        public uint Id
        {
            get => MBitConverter.To<uint>(this.Start + 12);
            set => MBitConverter.Set(this.Start + 12, value);
        }

        public uint Revision
        {
            get => MBitConverter.To<uint>(this.Start + 16);
            set => MBitConverter.Set(this.Start + 16, value);
        }

        public ushort Version
        {
            get => MBitConverter.To<ushort>(this.Start + 20);
            set => MBitConverter.Set(this.Start + 20, value);
        }

        public ushort UNKNOWN_22
        {
            get => MBitConverter.To<ushort>(this.Start + 22);
            set => MBitConverter.Set(this.Start + 22, value);
        }

        public IEnumerable<BethesdaField> Fields
        {
            get
            {
                MArraySegment<byte> payload = this.Payload;
                if (this.Flags.HasFlag(BethesdaRecordFlags.Compressed))
                {
                    payload = Zlib.Uncompress(payload);
                }

                return GetFields(payload);
            }
        }

        public static IEnumerable<BethesdaField> GetFields(MArraySegment<byte> payload)
        {
            uint pos = 0;
            uint? offsides = null;
            while (pos != payload.Count)
            {
                B4S typ = MBitConverter.To<B4S>(payload, pos);
                if (typ == XXXX)
                {
                    Debug.Assert(MBitConverter.To<ushort>(payload, pos + 4) == 4, "XXXX has a special meaning for parsing.");
                    offsides = MBitConverter.To<uint>(payload, pos + 6);
                    pos += 10u;
                    continue;
                }

                uint sz = offsides ?? MBitConverter.To<ushort>(payload, pos + 4);
                yield return new BethesdaField(typ, new MArraySegment<byte>(payload, pos + 6, sz));

                offsides = null;
                pos += sz + 6u;
            }
        }

        public override string ToString() => Invariant($"[{this.RecordType}:{this.Id:X8}]");
    }
}
