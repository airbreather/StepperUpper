using System;
using System.Runtime.InteropServices;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct BethesdaField
    {
        public BethesdaField(UArraySegment<byte> rawData)
        {
            this.RawData = rawData;
        }

        public B4S Type => UBitConverter.ToUInt32(this.RawData, 0);

        public UArraySegment<byte> RawData { get; }

        public UArrayPosition<byte> Start => this.RawData.Pos;

        public UArrayPosition<byte> PayloadStart => this.Start + 6;

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.PayloadStart, this.RawData.Count - 6);

        public override string ToString() => $"{this.Type}: ({Convert.ToBase64String(this.Payload.Array, (int)this.Payload.Offset, (int)this.Payload.Count)}";
    }
}
