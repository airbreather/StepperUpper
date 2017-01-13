using System;
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

        public ArraySegment<byte> RawData => new ArraySegment<byte>(this.Start.Array, this.Start.Offset, checked((int)(this.DataSize + 24)));

        public MArrayPosition<byte> PayloadStart => this.Start + 24;

        public ArraySegment<byte> Payload => new ArraySegment<byte>(this.Start.Array, this.Start.Offset + 24, checked((int)this.DataSize));

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
                ArraySegment<byte> payload = this.Payload;
                if (this.Flags.HasFlag(BethesdaRecordFlags.Compressed))
                {
                    payload = new ArraySegment<byte>(Zlib.Uncompress(payload));
                }

                return GetFields(payload);
            }
        }

        public static IEnumerable<BethesdaField> GetFields(ArraySegment<byte> payload)
        {
            int pos = 0;
            int? offsides = null;
            while (pos != payload.Count)
            {
                B4S typ = MBitConverter.To<B4S>(payload, pos);
                if (typ == XXXX)
                {
                    Debug.Assert(MBitConverter.To<ushort>(payload, pos + 4) == 4, "XXXX has a special meaning for parsing.");
                    offsides = checked((int)MBitConverter.To<uint>(payload.Array, payload.Offset + pos + 6));
                    pos += 10;
                    continue;
                }

                int sz = offsides ?? MBitConverter.To<ushort>(payload, pos + 4);
                yield return new BethesdaField(typ, new ArraySegment<byte>(payload.Array, payload.Offset + pos + 6, sz));

                offsides = null;
                pos += sz + 6;
            }
        }

        public override string ToString() => Invariant($"[{this.RecordType}:{this.Id:X8}]");
    }
}
