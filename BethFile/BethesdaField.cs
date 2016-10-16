using System.Runtime.InteropServices;

using AirBreather;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct BethesdaField
    {
        public BethesdaField(UArraySegment<byte> rawData)
        {
            this.RawData = rawData;
        }

        public B4S FieldType => UBitConverter.ToUInt32(this.RawData, 0);

        public UArraySegment<byte> RawData { get; }

        public UArrayPosition<byte> Start => this.RawData.Pos;

        public UArrayPosition<byte> PayloadStart => this.Start + 6;

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.PayloadStart, this.RawData.Count - 6);

        public ushort StoredSize
        {
            get { return UBitConverter.ToUInt16(this.Start + 4); }
            set { UBitConverter.SetUInt16(this.Start + 4, value); }
        }

        public override string ToString() => $"{this.FieldType}: ({this.Payload.ToArray().ByteArrayToHexString()}";
    }
}
