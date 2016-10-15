﻿using System;
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
            UBitConverter.SetUInt32(new UArrayPosition<byte>(header.Fields.Single(f => f.FieldType == HEDR).Payload, 4), Doer.CountItems(header) - 1);

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
            UBuffer.BlockCopy(payload, 0, pos, 0, payloadLength);

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
            uint val = record.IsDummy
                ? 0
                : checked(24 + (record.Flags.HasFlag(BethesdaRecordFlags.Compressed)
                    ? unchecked((uint)GetCompressedPayload(record).LongLength)
                    : GetUncompressedPayloadSize(record)));

            if (includeGroups)
            {
                foreach (var grp in record.Subgroups)
                {
                    val = checked(val + CalculateSize(grp));
                }
            }

            return val;
        }

        private static uint CalculateSize(Group group)
        {
            uint val = 24;
            foreach (var rec in group.Records)
            {
                val = checked(val + CalculateSize(rec, true));
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
            byte[] result = new byte[GetUncompressedPayloadSize(record)];
            UArrayPosition<byte> pos = new UArrayPosition<byte>(result);
            foreach (var field in record.Fields)
            {
                uint fieldLength = unchecked((uint)(field.Payload.LongLength));
                ushort storedFieldLength = unchecked((ushort)fieldLength);
                if (fieldLength > ushort.MaxValue)
                {
                    UBitConverter.SetUInt32(pos, XXXX);
                    pos += 4;

                    UBitConverter.SetUInt16(pos, 4);
                    pos += 2;

                    UBitConverter.SetUInt32(pos, fieldLength);
                    pos += 4;
                    storedFieldLength = 0;
                }

                UBitConverter.SetUInt32(pos, field.FieldType);
                pos += 4;

                UBitConverter.SetUInt16(pos, storedFieldLength);
                pos += 2;

                UBuffer.BlockCopy(field.Payload, 0, pos, 0, fieldLength);

                pos += fieldLength;
            }

            return result;
        }

        private static uint GetUncompressedPayloadSize(Record record)
        {
            uint payloadSize = 0;
            foreach (var field in record.Fields)
            {
                uint fieldLength = checked((uint)(field.Payload.LongLength));
                if (fieldLength > ushort.MaxValue)
                {
                    payloadSize = checked(payloadSize + 10);
                }

                payloadSize = checked(payloadSize + 6 + fieldLength);
            }

            return payloadSize;
        }

        private struct Saved
        {
            internal BethesdaRecord Record;

            internal BethesdaGroup[] Subgroups;
        }
    }
}