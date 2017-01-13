using System.Runtime.InteropServices;

using AirBreather;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct BethesdaField
    {
        public BethesdaField(B4S fieldType, MArraySegment<byte> payload)
        {
            this.FieldType = fieldType;
            this.Payload = payload;
        }

        public B4S FieldType { get; }

        public MArraySegment<byte> Payload { get; }

        public override string ToString() => $"{this.FieldType}: ({this.Payload.ToArray().ByteArrayToHexString()})";
    }
}
