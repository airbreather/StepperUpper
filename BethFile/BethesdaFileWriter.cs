using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BethFile
{
    public sealed class BethesdaFileWriter
    {
        private readonly Stream stream;

        public BethesdaFileWriter(Stream stream)
        {
            this.stream = stream;
        }

        public async Task WriteAsync(BethesdaFile file)
        {
            await this.WriteBytesAsync(file.HeaderRecord.RawData).ConfigureAwait(false);
            foreach (BethesdaGroup grp in file.TopGroups)
            {
                await this.WriteBytesAsync(grp.RawData).ConfigureAwait(false);
            }
        }

        private async Task WriteBytesAsync(UArraySegment<byte> bytes)
        {
            if (bytes.Offset + bytes.Count < Int32.MaxValue)
            {
                await this.stream.WriteAsync(bytes.Array, unchecked((int)bytes.Offset), unchecked((int)bytes.Count)).ConfigureAwait(false);
            }
            else
            {
                // in for a penny, in for a pound... I guess...
                uint pos = 0;
                uint remaining = bytes.Count;
                byte[] sub = new byte[81920];
                while (remaining != 0)
                {
                    uint cnt = Math.Min(remaining, unchecked((uint)sub.Length));
                    UBuffer.BlockCopy(bytes.Array, pos, sub, 0, cnt);
                    pos += cnt;
                    remaining -= cnt;

                    await this.stream.WriteAsync(sub, 0, unchecked((int)cnt)).ConfigureAwait(false);
                }
            }
        }
    }
}
