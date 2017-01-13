using System;
using System.IO;
using System.Threading.Tasks;

namespace BethFile
{
    public sealed class BethesdaFileWriter
    {
        private readonly Stream stream;

        public BethesdaFileWriter(Stream stream) => this.stream = stream;

        public async Task WriteAsync(BethesdaFile file)
        {
            await this.WriteBytesAsync(file.HeaderRecord.RawData).ConfigureAwait(false);
            foreach (BethesdaGroup grp in file.TopGroups)
            {
                await this.WriteBytesAsync(grp.RawData).ConfigureAwait(false);
            }
        }

        private Task WriteBytesAsync(ArraySegment<byte> bytes) => this.stream.WriteAsync(bytes.Array, bytes.Offset, bytes.Count);
    }
}
