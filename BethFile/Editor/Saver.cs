using System;
using System.Collections.Generic;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Saver
    {
        public static BethesdaFile Save(Record root)
        {
            FinalizeHeader(root);
            byte[] rec = new byte[CalculateSize(root, true)];
            MArrayPosition<byte> pos = new MArrayPosition<byte>(rec);
            var (record, subgroups) = Write(root, ref pos);
            return new BethesdaFile(record, subgroups);
        }

        private static void FinalizeHeader(Record root)
        {
            var onamField = root.Fields.SingleOrDefault(f => f.FieldType == ONAM);
            if (onamField == null)
            {
                goto afterOnam;
            }

            HashSet<uint> prevOnams = Doer.GetOnams(root);
            List<uint> currOnams = new List<uint>(prevOnams.Count);
            foreach (Record rec in Doer.FindRecords(root))
            {
                if (prevOnams.Remove(rec.Id) && (!rec.Flags.HasFlag(BethesdaRecordFlags.InitiallyDisabled) || !rec.Flags.HasFlag(BethesdaRecordFlags.PersistentReference)))
                {
                    currOnams.Add(rec.Id);
                }
            }

            uint[] onamsArray = currOnams.ToArray();
            Array.Sort(onamsArray);

            onamField.Payload = new byte[unchecked((uint)(onamsArray.Length) * 4u)];

            MArrayPosition<byte> pos = new MArrayPosition<byte>(onamField.Payload);
            foreach (uint onam in onamsArray)
            {
                MBitConverter.Set(pos, onam);
                pos += 4;
            }

            root.CompressedFieldData = null;

            afterOnam:
            MBitConverter.Set(new MArrayPosition<byte>(root.Fields.Single(f => f.FieldType == HEDR).Payload, 4), Doer.CountItems(root) - 1);
        }

        private static (BethesdaRecord record, BethesdaGroup[] subgroups) Write(Record record, ref MArrayPosition<byte> pos)
        {
            var result = default((BethesdaRecord record, BethesdaGroup[] subgroups));

            if (record.IsDummy)
            {
                goto groups;
            }

            result.record = new BethesdaRecord(pos)
            {
                RecordType = record.RecordType,
                DataSize = unchecked((uint)(CalculateSize(record, false) - 24)),
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
            int payloadLength = payload.Length;
            MBuffer.BlockCopy(payload, 0, pos, 0, payloadLength);

            pos += payloadLength;

            groups:
            result.subgroups = new BethesdaGroup[record.Subgroups.Count];
            for (int i = 0; i < result.subgroups.Length; i++)
            {
                result.subgroups[i] = Write(record.Subgroups[i], ref pos);
            }

            return result;
        }

        private static BethesdaGroup Write(Group group, ref MArrayPosition<byte> pos)
        {
            MBitConverter.Set(pos, GRUP);

            BethesdaGroup grp = new BethesdaGroup(pos)
            {
                DataSize = unchecked((uint)(CalculateSize(group) - 24)),
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

        private static int CalculateSize(Record record, bool includeGroups)
        {
            int val = record.IsDummy
                ? 0
                : 24 + (record.Flags.HasFlag(BethesdaRecordFlags.Compressed)
                    ? GetCompressedPayload(record).Length
                    : GetUncompressedPayloadSize(record));

            if (includeGroups)
            {
                foreach (var grp in record.Subgroups)
                {
                    val += CalculateSize(grp);
                }
            }

            return val;
        }

        private static int CalculateSize(Group group)
        {
            int val = 24;
            foreach (var rec in group.Records)
            {
                val += CalculateSize(rec, true);
            }

            return val;
        }

        private static byte[] GetCompressedPayload(Record record) =>
            record.CompressedFieldData ??
                (record.CompressedFieldData = Zlib.Compress(new ArraySegment<byte>(GetUncompressedPayload(record))).ToArray());

        private static byte[] GetUncompressedPayload(Record record)
        {
            byte[] result = new byte[GetUncompressedPayloadSize(record)];
            MArrayPosition<byte> pos = new MArrayPosition<byte>(result);
            foreach (var field in record.Fields)
            {
                int fieldLength = field.Payload.Length;
                ushort storedFieldLength = unchecked((ushort)fieldLength);
                if (fieldLength > UInt16.MaxValue)
                {
                    MBitConverter.Set(pos, XXXX);
                    pos += 4;

                    MBitConverter.Set(pos, (ushort)4);
                    pos += 2;

                    MBitConverter.Set(pos, fieldLength);
                    pos += 4;
                    storedFieldLength = 0;
                }

                MBitConverter.Set(pos, field.FieldType);
                pos += 4;

                MBitConverter.Set(pos, storedFieldLength);
                pos += 2;

                MBuffer.BlockCopy(field.Payload, 0, pos, 0, fieldLength);
                pos += fieldLength;
            }

            return result;
        }

        private static int GetUncompressedPayloadSize(Record record)
        {
            int payloadSize = 0;
            foreach (var field in record.Fields)
            {
                int fieldLength = field.Payload.Length;
                if (fieldLength > UInt16.MaxValue)
                {
                    payloadSize = payloadSize + 10;
                }

                payloadSize = payloadSize + 6 + fieldLength;
            }

            return payloadSize;
        }
    }
}
