using AirBreather;

using static System.FormattableString;

namespace BethFile.Editor
{
    public sealed class Field
    {
        public Field()
        {
        }

        public Field(Field copyFrom)
        {
            this.Type = copyFrom.Type;
            this.Payload = (byte[])copyFrom.Payload.Clone();
        }

        public Field(BethesdaField copyFrom)
        {
            this.Type = copyFrom.Type;
            this.Payload = copyFrom.Payload.ToArray();
        }

        public B4S Type { get; set; }

        public byte[] Payload { get; set; }

        public override string ToString() => Invariant($"{this.Type} ({this.Payload.Length} bytes) >> ({this.Payload.ByteArrayToHexString()})");
    }
}
