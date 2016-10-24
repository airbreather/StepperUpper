using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

using AirBreather;
using AirBreather.IO;

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

        public static bool operator ==(Md5Checksum first, Md5Checksum second) => first.Equals(second);
        public static bool operator !=(Md5Checksum first, Md5Checksum second) => !first.Equals(second);

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

    internal static class ConcurrentMd5
    {
        internal static IObservable<FileWithChecksum> Calculate(IObservable<FileInfo> files) =>
            files
                .Select(file => Task.Run(async () =>
                {
                    string path = file.FullName;
                    byte[] hash;
                    using (FileStream fs = AsyncFile.OpenReadSequential(path))
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] buf = new byte[AsyncFile.FullCopyBufferSize];
                        int cnt;
                        while ((cnt = await fs.ReadAsync(buf, 0, AsyncFile.FullCopyBufferSize).ConfigureAwait(false)) != 0)
                        {
                            md5.TransformBlock(buf, 0, cnt, null, 0);
                        }

                        md5.TransformFinalBlock(buf, 0, 0);

                        hash = md5.Hash;
                    }

                    Md5Checksum checksum = new Md5Checksum(hash);
                    return new FileWithChecksum(path, checksum);
                }))
                .Merge();
    }
}
