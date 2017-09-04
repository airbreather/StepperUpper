namespace BethFile
{
    internal static class ArchiveHash
    {
        internal static unsafe ulong GenHash(MArrayPosition<byte> str, bool isFolder)
        {
            fixed (byte* ptr = &str.Array[str.Offset])
            {
                return GenHash(ptr, isFolder);
            }
        }

        internal static unsafe ulong GenHash(byte* sPath, bool isFolder)
        {
            const byte UpperA = (byte)'A';
            const byte UpperZ = (byte)'Z';
            const byte ForwardSlash = (byte)'/';
            const byte Backlash = (byte)'\\';
            const byte Dot = (byte)'.';
            const byte LowerMask = 0x20;

            uint hash = 0;
            uint hash2 = 0;

            // copy into a local array so we don't modify the data that was passed
            byte* s = stackalloc byte[256];

            // This is a pointer to the file extension, otherwise NULL
            byte* pExt = null;

            // Length of the filename or folder path
            byte iLen = 255;
            for (byte i = 0; i < 255; i++)
            {
                byte b = sPath[i];
                if (b < UpperA)
                {
                    switch (b)
                    {
                        case 0:
                            iLen = i;
                            s[i] = 0;
                            goto done;

                        case ForwardSlash:
                            b = Backlash;
                            break;

                        case Dot:
                            if (isFolder)
                            {
                                pExt = s + i;
                            }

                            break;
                    }
                }
                else if (b <= UpperZ)
                {
                    b |= LowerMask;
                }

                s[i] = b;
            }

            done:

            // pointer to the end of the filename or folder path
            byte* pEnd = s + iLen;

            // Hash 1
            // If this is a file with an extension
            if (pExt != null)
            {
                for (byte* x = pExt; x < pEnd; x++)
                {
                    hash = unchecked((hash * 0x1003F) + *x);
                }

                // From here on, iLen and pEnd must NOT include the file extension.
                iLen = unchecked((byte)(pExt - s));
                pEnd = pExt;
            }

            for (byte* x = s + 1; x < (pEnd - 2); x++)
            {
                hash2 = unchecked((hash2 * 0x1003F) + *x);
            }

            hash = unchecked(hash + hash2);

            // Hash 2
            hash2 =
                s[iLen - 1] |
                ((iLen > 2) ? ((uint)s[iLen - 2]) << 8 : 0) |
                (((uint)iLen) << 16) |
                (((uint)s[0]) << 24);

            if (pExt != null)
            {
                // load these 4 bytes as a long integer
                switch (*(uint*)pExt)
                {
                    // 2E 6B 66 00 == .kf\0
                    case 0x00666B2E:
                        hash2 |= 0x80;
                        break;

                    // .nif
                    case 0x66696E2E:
                        hash2 |= 0x8000;
                        break;

                    // .dds
                    case 0x7364642E:
                        hash2 |= 0x8080;
                        break;

                    // .wav
                    case 0x7661772E:
                        hash2 |= 0x80000000;
                        break;
                }
            }

            return unchecked((((ulong)hash) << 32) | hash2);
        }
    }
}
