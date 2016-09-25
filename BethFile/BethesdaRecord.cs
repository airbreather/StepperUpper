using System.Collections.Generic;
using System.IO;
using Ionic.Zlib;

namespace BethFile
{
    public struct BethesdaRecord
    {
        public BethesdaRecord(byte[] rawData)
        {
            this.RawData = new UArraySegment<byte>(rawData);
        }

        public BethesdaRecord(UArraySegment<byte> rawData)
        {
            this.RawData = rawData;
        }

        public UArraySegment<byte> RawData { get; }

        public UArraySegment<byte> Payload => new UArraySegment<byte>(this.RawData, 24, this.RawData.Count - 24);

        public B4S Type => UBitConverter.ToUInt32(this.RawData, 0);

        public uint DataSize => this.RawData.Count - 24;

        public BethesdaRecordFlags Flags => (BethesdaRecordFlags)UBitConverter.ToUInt32(this.RawData, 8);

        public uint Id => UBitConverter.ToUInt32(this.RawData, 12);

        public uint Revision => UBitConverter.ToUInt32(this.RawData, 16);

        public ushort Version => UBitConverter.ToUInt16(this.RawData, 20);

        public ushort UNKNOWN_22 => UBitConverter.ToUInt16(this.RawData, 22);

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
                while (pos != payload.Count)
                {
                    ushort sz = UBitConverter.ToUInt16(payload, pos + 4);
                    yield return new BethesdaField(new UArraySegment<byte>(payload, pos, sz + 6u));
                    pos += sz + 6u;
                }
            }
        }
    }
}
