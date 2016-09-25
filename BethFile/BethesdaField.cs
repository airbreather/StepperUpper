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
    }
}
