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

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.Start + 6, this.RawData.Count - 6);
    }
}
