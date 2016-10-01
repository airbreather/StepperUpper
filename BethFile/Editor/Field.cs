namespace BethFile.Editor
{
    public sealed class Field
    {
        public B4S Type { get; set; }

        public byte[] Payload { get; set; }

        public override string ToString() => $"{this.Type} >> ({this.Payload.Length} bytes)";
    }
}
