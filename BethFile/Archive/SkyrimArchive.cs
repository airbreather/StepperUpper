using System;
using System.Collections;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

using AirBreather;

namespace BethFile.Archive
{
    public sealed class SkyrimArchive
    {
        private static readonly Encoding PathStringEncoding = Encoding.GetEncoding(1252);

        public static IObservable<ExtractedFile> ExtractAll(Stream archiveStream) => Observable.Create<ExtractedFile>(async (obs, cancellationToken) =>
        {
            if (!archiveStream.ValidateNotNull(nameof(archiveStream)).CanSeek)
            {
                throw new ArgumentException("Must be seekable.", nameof(archiveStream));
            }

            // TODO: System.Buffers
            var metaBuf = new byte[257];
            await archiveStream.LoopedReadAsync(metaBuf, 0, 36, cancellationToken).ConfigureAwait(false);

            // "BSA\0", ASCII-encoded
            const uint Magic = 4281154;
            if (BitConverter.ToUInt32(metaBuf, 0) != Magic)
            {
                throw new NotSupportedException("I don't know how to read this stream as a BSA file.");
            }

            var version = BitConverter.ToUInt32(metaBuf, 4);
            if (version != 104 && version != 103)
            {
                throw new NotSupportedException("I don't know how to read BSA files for games other than Skyrim / Oblivion.");
            }

            var flags = (ArchiveFlags)BitConverter.ToInt32(metaBuf, 12);

            if (flags.HasFlag(ArchiveFlags.IsXbox360))
            {
                throw new NotSupportedException("I didn't feel like writing code to extract BSA files meant for the Xbox 360.");
            }

            if (!flags.HasFlag(ArchiveFlags.IncludeDirectoryNames) ||
                !flags.HasFlag(ArchiveFlags.IncludeFileNames))
            {
                throw new NotSupportedException("I don't know how to create the output files if the BSA file doesn't even give us name / path information.");
            }

            var embeddedFileNames = flags.HasFlag(ArchiveFlags.EmbedFileNames);

            var numberOfFolders = checked((int)BitConverter.ToUInt32(metaBuf, 16));
            var numberOfFiles = checked((int)BitConverter.ToUInt32(metaBuf, 20));

            var folders = new(int fileCount, string folder)[numberOfFolders];
            for (int i = 0; i < folders.Length; i++)
            {
                await archiveStream.LoopedReadAsync(metaBuf, 0, 16, cancellationToken).ConfigureAwait(false);
                folders[i].fileCount = checked((int)BitConverter.ToUInt32(metaBuf, 8));
            }

            var compressedOnes = new BitArray(numberOfFiles, flags.HasFlag(ArchiveFlags.CompressedByDefault));
            var files = new(int size, int dataOffset)[numberOfFiles];
            var currFileIndex = 0;
            for (int i = 0; i < folders.Length; i++)
            {
                var fileCount = folders[i].fileCount;
                await archiveStream.ReadAsync(metaBuf, 0, 1, cancellationToken).ConfigureAwait(false);
                await archiveStream.LoopedReadAsync(metaBuf, 1, metaBuf[0], cancellationToken).ConfigureAwait(false);
                var folderPath = PathStringEncoding.GetString(metaBuf, 1, metaBuf[0] - 1);
                folders[i].folder = folderPath;
                for (int j = 0; j < fileCount; j++)
                {
                    await archiveStream.LoopedReadAsync(metaBuf, 0, 16, cancellationToken).ConfigureAwait(false);
                    files[currFileIndex].size = checked((int)BitConverter.ToUInt32(metaBuf, 8));
                    files[currFileIndex].dataOffset = checked((int)BitConverter.ToUInt32(metaBuf, 12));

                    if ((files[currFileIndex].size & (1 << 30)) != 0)
                    {
                        compressedOnes[currFileIndex] = !compressedOnes[currFileIndex];
                        files[currFileIndex].size &= (1 << 30) - 1;
                    }

                    ++currFileIndex;
                }
            }

            var decoded = new char[1];
            var fileNameBuilder = new StringBuilder();
            currFileIndex = 0;
            foreach (var (fileCount, folder) in folders)
            {
                for (int i = 0; i < fileCount; i++)
                {
                    bool isCompressed = compressedOnes[currFileIndex];
                    var (dataLength, dataOffset) = files[currFileIndex++];
                    while (true)
                    {
                        // read one filename character at a time until we hit the NUL terminator.
                        // TODO: optimize, perhaps?  it's complicated, though, since hypothetically,
                        // any reasonably wide buffer could potentially hold the *entire* contents
                        // of this file AND parts of the next file, so the buffer would have to be
                        // very... "lively"?..., probably with really fun helper methods.
                        await archiveStream.ReadAsync(metaBuf, 0, 1, cancellationToken).ConfigureAwait(false);
                        if (metaBuf[0] == 0)
                        {
                            break;
                        }

                        // Windows-1252 is a one-byte encoding, so our one-char buffer is always big
                        // enough to hold the decoded chars.
                        PathStringEncoding.GetChars(metaBuf, 0, 1, decoded, 0);
                        fileNameBuilder.Append(decoded);
                    }

                    var currentFileName = fileNameBuilder.ToString();
                    fileNameBuilder.Clear();

                    // remember our position in the file name block...
                    var prevPosition = archiveStream.Position;

                    // jump over to the raw file data...
                    archiveStream.Position = dataOffset;

                    // I said *raw* file data...
                    if (embeddedFileNames)
                    {
                        await archiveStream.ReadAsync(metaBuf, 0, 1, cancellationToken).ConfigureAwait(false);
                        archiveStream.Seek(metaBuf[0], SeekOrigin.Current);
                    }

                    // read it into memory...
                    // TODO: System.Buffers
                    var buf = new byte[dataLength];
                    await archiveStream.LoopedReadAsync(buf, 0, buf.Length, cancellationToken).ConfigureAwait(false);

                    // jump right back to where we left off to handle the next file...
                    archiveStream.Position = prevPosition;

                    // and leave it for the caller to deal with however they want.
                    var fullFilePath = Path.Combine(folder, currentFileName);
                    var bufTask = isCompressed
                        ? Task.Run(() =>
                        {
                            try
                            {
                                return Zlib.Uncompress(buf);
                            }
                            catch /* when (currentFileName == "placeholder.txt") */
                            {
                                // HACK: this happens a few times in HighResTexturePack02.bsa, always
                                // with files named "placeholder.txt" that wind up being 4 bytes long.
                                return Array.Empty<byte>();
                            }
                        })
                        : Task.FromResult(buf);

                    obs.OnNext(new ExtractedFile(fullFilePath, bufTask));
                }
            }
        });

        [Flags]
        private enum ArchiveFlags
        {
            None                  = 0b000000000,
            IncludeDirectoryNames = 0b000000001,
            IncludeFileNames      = 0b000000010,
            CompressedByDefault   = 0b000000100,
            IsXbox360             = 0b001000000,
            EmbedFileNames        = 0b100000000,
        }
    }
}
