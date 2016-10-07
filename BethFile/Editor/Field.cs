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

        public B4S Type { get; set; }

        public byte[] Payload { get; set; }

        public override string ToString() => $"{this.Type} >> ({this.Payload.Length} bytes)";
    }
}
