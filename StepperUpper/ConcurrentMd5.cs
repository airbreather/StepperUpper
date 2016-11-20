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

        internal Md5Checksum(string s) => this = new Md5Checksum(s?.HexStringToByteArrayChecked());

        internal Md5Checksum(byte[] buf)
        {
            if (buf == null)
            {
                this = default(Md5Checksum);
                return;
            }

            if (buf.Length != 16)
            {
                throw new ArgumentException("Must contain exactly 16 bytes.", nameof(buf));
            }

            this.X0 = BitConverter.ToUInt64(buf, 0);
            this.X1 = BitConverter.ToUInt64(buf, 8);
        }

        public static bool operator ==(Md5Checksum first, Md5Checksum second) => first.Equals(second);
        public static bool operator !=(Md5Checksum first, Md5Checksum second) => !first.Equals(second);

        public bool Equals(Md5Checksum other) => this.X0 == other.X0 & this.X1 == other.X1;

        public override bool Equals(object obj) => obj is Md5Checksum other && this.Equals(other);

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

    // sequential so that our ToString() is guaranteed to be correct.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Sha512Checksum : IEquatable<Sha512Checksum>
    {
        internal ulong X0;
        internal ulong X1;
        internal ulong X2;
        internal ulong X3;
        internal ulong X4;
        internal ulong X5;
        internal ulong X6;
        internal ulong X7;

        internal Sha512Checksum(string s) => this = new Sha512Checksum(s?.HexStringToByteArrayChecked());

        internal Sha512Checksum(byte[] buf)
        {
            if (buf == null)
            {
                this = default(Sha512Checksum);
                return;
            }

            if (buf.Length != 64)
            {
                throw new ArgumentException("Must contain exactly 64 bytes.", nameof(buf));
            }

            this.X0 = BitConverter.ToUInt64(buf, 0x00);
            this.X1 = BitConverter.ToUInt64(buf, 0x08);
            this.X2 = BitConverter.ToUInt64(buf, 0x10);
            this.X3 = BitConverter.ToUInt64(buf, 0x18);
            this.X4 = BitConverter.ToUInt64(buf, 0x20);
            this.X5 = BitConverter.ToUInt64(buf, 0x28);
            this.X6 = BitConverter.ToUInt64(buf, 0x30);
            this.X7 = BitConverter.ToUInt64(buf, 0x38);
        }

        public static bool operator ==(Sha512Checksum first, Sha512Checksum second) => first.Equals(second);
        public static bool operator !=(Sha512Checksum first, Sha512Checksum second) => !first.Equals(second);

        public bool Equals(Sha512Checksum other) =>
            this.X0 == other.X0 &
            this.X1 == other.X1 &
            this.X2 == other.X2 &
            this.X3 == other.X3 &
            this.X4 == other.X4 &
            this.X5 == other.X5 &
            this.X6 == other.X6 &
            this.X7 == other.X7;

        public override bool Equals(object obj) => obj is Sha512Checksum other && this.Equals(other);

        // SHA512 hashes are effectively random bits, so simple XOR is fine.
        public override int GetHashCode() => (this.X0 ^ this.X1 ^ this.X2 ^ this.X3 ^ this.X4 ^ this.X5 ^ this.X6 ^ this.X7).GetHashCode();

        public unsafe override string ToString()
        {
            // Sure we COULD allocate a byte[] and use the safe version, but this is more fun.
            // Technically, this SHOULD also be more efficient, but that's not relevant.
            fixed (Sha512Checksum* ptr = &this)
            {
                return StringUtility.BytesToHexStringUnsafe((byte*)ptr, 64);
            }
        }
    }

    [Flags]
    internal enum Hashes
    {
        None   = 0b00,
        Md5    = 0b01,
        Sha512 = 0b10
    }

    internal static class ConcurrentHashCheck
    {
        internal static IObservable<(FileInfo file, Md5Checksum md5Checksum, Sha512Checksum sha512Checksum)> Calculate(IObservable<(FileInfo file, Hashes hashesToCalculate)> files) =>
            files
                .Select(tup => Task.Run(async () =>
                {
                    var (file, hashesToCalculate) = tup;
                    string path = file.FullName;
                    byte[] md5Hash, sha512Hash;
                    using (FileStream fs = AsyncFile.OpenReadSequential(path))
                    using (MD5 md5 = hashesToCalculate.HasFlag(Hashes.Md5) ? MD5.Create() : null)
                    using (SHA512 sha512 = hashesToCalculate.HasFlag(Hashes.Sha512) ? SHA512.Create() : null)
                    {
                        byte[] buf = new byte[AsyncFile.FullCopyBufferSize];
                        int cnt;
                        while ((cnt = await fs.ReadAsync(buf, 0, AsyncFile.FullCopyBufferSize).ConfigureAwait(false)) != 0)
                        {
                            md5?.TransformBlock(buf, 0, cnt, null, 0);
                            sha512?.TransformBlock(buf, 0, cnt, null, 0);
                        }

                        md5?.TransformFinalBlock(buf, 0, 0);
                        sha512?.TransformFinalBlock(buf, 0, 0);

                        md5Hash = md5?.Hash;
                        sha512Hash = sha512?.Hash;
                    }

                    Md5Checksum md5Checksum = new Md5Checksum(md5Hash);
                    Sha512Checksum sha512Checksum = new Sha512Checksum(sha512Hash);

                    return (file, md5Checksum, sha512Checksum);
                }))
                .Merge();
    }
}
