using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

using AirBreather;
using AirBreather.IO;
using AirBreather.Text;

using static System.FormattableString;

namespace StepperUpper
{
    // sequential so that our ToString() is guaranteed to be correct.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Md5Checksum : IEquatable<Md5Checksum>
    {
        internal ulong X0;
        internal ulong X1;

        internal Md5Checksum(string s)
            : this(s.HexStringToByteArrayChecked())
        {
        }

        internal Md5Checksum(byte[] buf)
        {
            buf.ValidateNotNull(nameof(buf));
            if (buf.Length != 16)
            {
                throw new ArgumentException("Must contain exactly 16 bytes.", nameof(buf));
            }

            this.X0 = BitConverter.ToUInt64(buf, 0);
            this.X1 = BitConverter.ToUInt64(buf, 8);
        }

        public bool Equals(Md5Checksum other) => this.X0 == other.X0 && this.X1 == other.X1;

        public override bool Equals(object obj) => obj is Md5Checksum && this.Equals((Md5Checksum)obj);

        // MD5 hashes are effectively random bits, so simple XOR is fine.
        public override int GetHashCode() => (this.X0 ^ this.X1).GetHashCode();

        public unsafe override string ToString()
        {
            // Sure we COULD allocate a byte[] and use the safe version, but this is more fun.
            // Technically, this SHOULD also be more efficient, but that's not relevant.
            fixed (Md5Checksum* ptr = &this)
            {
                return StringUtility.BytesToHexStringUnsafe((byte*)ptr, 16);
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct FileWithChecksum
    {
        internal Md5Checksum Checksum;

        internal string Path;

        internal FileWithChecksum(string path, Md5Checksum checksum)
        {
            this.Path = path;
            this.Checksum = checksum;
        }
    }

    internal class CachedMd5
    {
        internal static IObservable<FileWithChecksum> Calculate(IObservable<FileInfo> files) =>
            files
                .Where(file => !file.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase))
                .Select(file => Task.Run(async () =>
                {
                    FileInfo cachedChecksum = new FileInfo(file.FullName + ".md5");
                    if (cachedChecksum.Exists && cachedChecksum.LastWriteTimeUtc >= file.LastWriteTimeUtc)
                    {
                        byte[] bytes = new byte[32];
                        using (FileStream cachedStream = AsyncFile.OpenReadSequential(cachedChecksum.FullName))
                        {
                            await cachedStream.LoopedReadAsync(bytes, 0, 32).ConfigureAwait(false);
                        }

                        string checksumString = EncodingEx.UTF8NoBOM.GetString(bytes);
                        return new FileWithChecksum(file.FullName, new Md5Checksum(checksumString));
                    }

                    string path = file.FullName;
                    byte[] hash;
                    using (FileStream fs = AsyncFile.OpenReadSequential(path))
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] buf = new byte[81920];
                        int cnt;
                        while ((cnt = await fs.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) != 0)
                        {
                            md5.TransformBlock(buf, 0, cnt, null, 0);
                        }

                        md5.TransformFinalBlock(buf, 0, 0);

                        hash = md5.Hash;
                    }

                    Md5Checksum checksum = new Md5Checksum(hash);
                    using (FileStream cachedStream = AsyncFile.CreateSequential(cachedChecksum.FullName))
                    {
                        byte[] buf = EncodingEx.UTF8NoBOM.GetBytes(Invariant($"{checksum.ToString()} *{file.Name}{Environment.NewLine}"));
                        await cachedStream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    }

                    return new FileWithChecksum(path, checksum);
                }))
                .Merge();
    }
}
