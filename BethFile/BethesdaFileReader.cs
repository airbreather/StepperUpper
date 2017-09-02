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

        public BethesdaFileReader(Stream stream) => this.stream = stream;

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
            int dataLength = checked((int)BitConverter.ToUInt32(this.header, 4));
            if (MBitConverter.To<uint>(this.header, 0) == GRUP)
            {
                dataLength -= 24;
            }

            byte[] rawData = new byte[dataLength + 24];
            MBuffer.BlockCopy(this.header, 0, rawData, 0, 24);
            await this.stream.LoopedReadAsync(rawData, 24, dataLength).ConfigureAwait(false);
            return rawData;
        }

        private async Task<bool> ReadHeaderAsync()
        {
            switch (await this.stream.LoopedReadAsync(this.header, 0, 24).ConfigureAwait(false))
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
