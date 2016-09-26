using System.Collections.Generic;
using System.IO;

using Ionic.Zlib;

using static BethFile.B4S;

namespace BethFile
{
    public struct BethesdaRecord
    {
        public BethesdaRecord(byte[] rawData)
        {
            this.Start = new UArrayPosition<byte>(rawData);
        }

        public BethesdaRecord(UArrayPosition<byte> rawData)
        {
            this.Start = rawData;
        }

        public UArrayPosition<byte> Start { get; }

        public UArraySegment<byte> RawData => new UArraySegment<byte>(this.Start, this.DataSize + 24);

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.Start + 24, this.DataSize);

        public B4S Type => UBitConverter.ToUInt32(this.Start, 0);

        public uint DataSize => UBitConverter.ToUInt32(this.Start, 4);

        public BethesdaRecordFlags Flags
        {
            get { return (BethesdaRecordFlags)UBitConverter.ToUInt32(this.Start, 8); }
            set { UBitConverter.Set(this.Start + 8, (uint)value); }
        }

        public uint Id => UBitConverter.ToUInt32(this.Start, 12);

        public uint Revision => UBitConverter.ToUInt32(this.Start, 16);

        public ushort Version => UBitConverter.ToUInt16(this.Start, 20);

        public ushort UNKNOWN_22 => UBitConverter.ToUInt16(this.Start, 22);

        public IEnumerable<BethesdaField> Fields
        {
            get
            {
                UArraySegment<byte> payload = this.Payload;
                if (this.Flags.HasFlag(BethesdaRecordFlags.Compressed))
                {
                    uint decompSize = UBitConverter.ToUInt32(payload, 0);
                    byte[] payloadArray = new byte[decompSize];
                    using (MemoryStream sourceStream = new MemoryStream(this.Payload.Array, false))
                    {
                        sourceStream.Position = this.Payload.Offset + 4;
                        byte[] buf = new byte[81920];
                        uint soFar = 0;
                        using (ZlibStream decompressStream = new ZlibStream(sourceStream, CompressionMode.Decompress))
                        {
                            int cnt;
                            while ((cnt = decompressStream.Read(buf, 0, buf.Length)) != 0)
                            {
                                UBuffer.BlockCopy(buf, 0, payloadArray, soFar, unchecked((uint)cnt));
                                soFar += unchecked((uint)cnt);
                            }
                        }
                    }

                    payload = new UArraySegment<byte>(payloadArray);
                }

                uint pos = 0;
                uint? offsides = null;
                while (pos != payload.Count)
                {
                    uint sz = offsides ?? UBitConverter.ToUInt16(payload, pos + 4);
                    BethesdaField field = new BethesdaField(new UArraySegment<byte>(payload, pos, sz + 6u));
                    yield return field;
                    if (field.Type == XXXX)
                    {
                        offsides = UBitConverter.ToUInt32(field.Payload, 0);
                    }
                    else
                    {
                        offsides = null;
                    }

                    pos += sz + 6u;
                }
            }
        }
    }
}
