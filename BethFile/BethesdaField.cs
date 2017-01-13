using System;
using System.Linq;
using System.Runtime.InteropServices;

using AirBreather;

namespace BethFile
{
    [StructLayout(LayoutKind.Auto)]
    public struct BethesdaField
    {
        public BethesdaField(B4S fieldType, ArraySegment<byte> payload)
        {
            this.FieldType = fieldType;
            this.Payload = payload;
        }

        public B4S FieldType { get; }

        public ArraySegment<byte> Payload { get; }

        public override string ToString() => $"{this.FieldType}: ({this.Payload.ToArray().ByteArrayToHexString()})";
    }
}
