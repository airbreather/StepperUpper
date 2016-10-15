using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using AirBreather;

using static BethFile.B4S;

namespace BethFile
{
    public sealed class BethesdaFileReader
    {
        private readonly Stream stream;

        private readonly byte[] header = new byte[24];

        public BethesdaFileReader(Stream stream)
        {
            this.stream = stream;
        }

        public async Task<BethesdaFile> ReadFileAsync()
        {
            if (!await this.ReadHeaderAsync().ConfigureAwait(false))
            {
                throw new EndOfStreamException();
            }

            byte[] headerBytes = await this.ReadDataAsync().ConfigureAwait(false);
            BethesdaRecord headerRecord = new BethesdaRecord(headerBytes);

            List<BethesdaGroup> topGroups = new List<BethesdaGroup>();
            while (await this.ReadHeaderAsync().ConfigureAwait(false))
            {
                byte[] groupData = await this.ReadDataAsync().ConfigureAwait(false);
                topGroups.Add(new BethesdaGroup(groupData));
            }

            return new BethesdaFile(headerRecord, topGroups.ToArray());
        }

        private async Task<byte[]> ReadDataAsync()
        {
            uint dataLength = BitConverter.ToUInt32(this.header, 4);
            if (BitConverter.ToUInt32(this.header, 0) == GRUP)
            {
                dataLength -= 24;
            }

            byte[] rawData = new byte[dataLength + 24];
            Buffer.BlockCopy(this.header, 0, rawData, 0, 24);
            uint pos = 24;
            uint remaining = dataLength;
            byte[] sub = new byte[81920];
            while (remaining != 0)
            {
                uint cnt = unchecked((uint)(await this.stream.ReadAsync(sub, 0, (int)Math.Min(remaining, sub.Length)).ConfigureAwait(false)));
                if (cnt == 0)
                {
                    throw new EndOfStreamException();
                }

                UBuffer.BlockCopy(sub, 0, rawData, pos, cnt);
                pos += cnt;
                remaining -= cnt;
            }

            return rawData;
        }

        private async Task<bool> ReadHeaderAsync()
        {
            switch (await this.stream.LoopedReadAsync(header, 0, 24).ConfigureAwait(false))
            {
                case 0:
                    return false;

                case 24:
                    return true;

                default:
                    throw new EndOfStreamException();
            }
        }
    }
}
