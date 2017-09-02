using System.Linq;

using AirBreather;

using static System.FormattableString;

namespace BethFile.Editor
{
    public sealed class Field
    {
        public Field() { }

        public Field(Field copyFrom)
        {
            this.FieldType = copyFrom.FieldType;
            this.Payload = (byte[])copyFrom.Payload.Clone();
        }

        public Field(BethesdaField copyFrom)
        {
            this.FieldType = copyFrom.FieldType;
            this.Payload = copyFrom.Payload.ToArray();
        }

        public B4S FieldType { get; set; }

        public byte[] Payload { get; set; }

        public override string ToString() => Invariant($"{this.FieldType} ({this.Payload.Length} bytes) >> ({this.Payload.AsReadOnlySpan().ToHexString()})");
    }
}
