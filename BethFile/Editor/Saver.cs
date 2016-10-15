using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zlib;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Saver
    {
        private static readonly byte[] FourBytes = new byte[4];

        public static BethesdaFile Save(Record root)
        {
            FinalizeHeader(root);
            byte[] rec = new byte[CalculateSize(root, true)];
            UArrayPosition<byte> pos = new UArrayPosition<byte>(rec);
            Saved saved = Write(root, ref pos);
            return new BethesdaFile(saved.Record, saved.Subgroups);
        }

        private static void FinalizeHeader(Record header) =>
            UBitConverter.SetUInt32(new UArrayPosition<byte>(header.Fields.Single(f => f.Type == HEDR).Payload, 4), Doer.CountItems(header) - 1);

        private static Saved Write(Record record, ref UArrayPosition<byte> pos)
        {
            Saved saved = new Saved();

            if (record.IsDummy)
            {
                goto groups;
            }

            BethesdaRecord rec = saved.Record = new BethesdaRecord(pos)
            {
                RecordType = record.RecordType,
                DataSize = CalculateSize(record, false) - 24,
                Flags = record.Flags,
                Id = record.Id,
                Revision = record.Revision,
                Version = record.Version,
                UNKNOWN_22 = record.UNKNOWN_22
            };

            pos += 24;
            byte[] payload = record.Flags.HasFlag(BethesdaRecordFlags.Compressed)
                ? GetCompressedPayload(record)
                : GetUncompressedPayload(record);
            uint payloadLength = unchecked((uint)payload.LongLength);
            if (payloadLength != 0)
            {
                UBuffer.BlockCopy(payload, 0, pos, 0, payloadLength);
            }

            pos += payloadLength;

            groups:
            saved.Subgroups = new BethesdaGroup[record.Subgroups.Count];
            for (int i = 0; i < saved.Subgroups.Length; i++)
            {
                saved.Subgroups[i] = Write(record.Subgroups[i], ref pos);
            }

            return saved;
        }

        private static BethesdaGroup Write(Group group, ref UArrayPosition<byte> pos)
        {
            UBitConverter.SetUInt32(pos, GRUP);

            BethesdaGroup grp = new BethesdaGroup(pos)
            {
                DataSize = CalculateSize(group) - 24,
                Label = group.Label,
                GroupType = group.GroupType,
                Stamp = group.Stamp,
                UNKNOWN_18 = group.UNKNOWN_18,
                Version = group.Version,
                UNKNOWN_22 = group.UNKNOWN_22
            };

            pos += 24;
            foreach (var rec in group.Records)
            {
                Write(rec, ref pos);
            }

            return grp;
        }

        private static uint CalculateSize(Record record, bool includeGroups)
        {
            uint val = 0;
            if (record.IsDummy)
            {
                goto groups;
            }

            val += 24;
            if (record.Flags.HasFlag(BethesdaRecordFlags.Compressed))
            {
                val += unchecked((uint)GetCompressedPayload(record).Length);
            }
            else
            {
                foreach (var field in record.Fields)
                {
                    uint fieldLength = unchecked((uint)(field.Payload.LongLength + 6));
                    val += fieldLength;
                    if (fieldLength > 65535)
                    {
                        val += 10;
                    }
                }
            }

            groups:
            if (includeGroups)
            {
                foreach (var grp in record.Subgroups)
                {
                    val += CalculateSize(grp);
                }
            }

            return val;
        }

        private static uint CalculateSize(Group group)
        {
            uint val = 24;
            foreach (var rec in group.Records)
            {
                val += CalculateSize(rec, true);
            }

            return val;
        }

        private static byte[] GetCompressedPayload(Record record)
        {
            if (record.OriginalCompressedFieldData != null)
            {
                return record.OriginalCompressedFieldData;
            }

            byte[] uncompressed = GetUncompressedPayload(record);
            uint uncompressedLength = unchecked((uint)uncompressed.LongLength);

            // xEdit uses default compression mode when it does the same.
            using (var result = new MemoryStream())
            {
                result.Write(FourBytes, 0, 4);
                using (var cmp = new ZlibStream(result, CompressionMode.Compress, leaveOpen: true))
                {
                    uint pos = 0;
                    byte[] buf = new byte[81920];
                    while (pos < uncompressedLength)
                    {
                        uint sz = Math.Min(uncompressedLength - pos, 81920);
                        UBuffer.BlockCopy(uncompressed, pos, buf, 0, sz);
                        cmp.Write(buf, 0, unchecked((int)sz));
                        pos += sz;
                    }
                }

                byte[] data = result.ToArray();
                UBitConverter.SetUInt32(data, 0, uncompressedLength);
                return record.OriginalCompressedFieldData = data;
            }
        }

        private static byte[] GetUncompressedPayload(Record record)
        {
            List<byte> result = new List<byte>();
            foreach (var field in record.Fields)
            {
                uint fieldLength = unchecked((uint)(field.Payload.LongLength));
                ushort storedFieldLength = unchecked((ushort)fieldLength);
                if (fieldLength > ushort.MaxValue)
                {
                    AddUInt32(result, XXXX);
                    AddUInt16(result, 4);
                    AddUInt32(result, fieldLength);
                    storedFieldLength = 0;
                }

                AddUInt32(result, field.Type);
                AddUInt16(result, storedFieldLength);
                result.AddRange(field.Payload);
            }

            return result.ToArray();
        }

        private static unsafe void AddUInt32(List<byte> lst, uint val)
        {
            byte* b = (byte*)&val;
            lst.Add(*(b++));
            lst.Add(*(b++));
            lst.Add(*(b++));
            lst.Add(*b);
        }

        private static unsafe void AddUInt16(List<byte> lst, ushort val)
        {
            byte* b = (byte*)&val;
            lst.Add(*(b++));
            lst.Add(*b);
        }

        private struct Saved
        {
            internal BethesdaRecord Record;

            internal BethesdaGroup[] Subgroups;
        }
    }
}
