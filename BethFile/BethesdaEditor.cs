using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using static BethFile.B4S;

namespace BethFile
{
    public static class BethesdaEditor
    {
        public static void SortRecords(BethesdaGroup group)
        {
            byte[] newData = new byte[group.DataSize];
            List<BethesdaRecord> records = new List<BethesdaRecord>();
            BethesdaGroupReader reader = new BethesdaGroupReader(group);
            BethesdaGroupReaderState state = reader.Read();
            while (state != BethesdaGroupReaderState.EndOfContent)
            {
                if (state != BethesdaGroupReaderState.Record)
                {
                    throw new NotSupportedException("Only groups with strictly records are supported.");
                }

                records.Add(reader.CurrentRecord);
                state = reader.Read();
            }

            uint pos = 0;
            foreach (var record in records.OrderBy(rec => rec.Id))
            {
                UBuffer.BlockCopy(record.RawData, 0, newData, pos, record.RawData.Count);
                pos += record.RawData.Count;
            }

            UBuffer.BlockCopy(newData, 0, group.PayloadData, 0, group.DataSize);
        }

        public static void MoveSubgroupToBottom(BethesdaGroup group, BethesdaGroupType subgroupType, uint subgroupLabel)
        {
            BethesdaGroup subgroup = default(BethesdaGroup);
            BethesdaGroupReader reader = new BethesdaGroupReader(group);
            BethesdaGroupReaderState state = reader.Read();
            while (state != BethesdaGroupReaderState.EndOfContent)
            {
                if (state != BethesdaGroupReaderState.Subgroup)
                {
                    state = reader.Read();
                    continue;
                }

                subgroup = reader.CurrentSubgroup;
                if (subgroup.GroupType == subgroupType && subgroup.Label == subgroupLabel)
                {
                    break;
                }

                state = reader.Read();
            }

            if (state == BethesdaGroupReaderState.EndOfContent)
            {
                throw new InvalidOperationException("Subgroup not found.");
            }

            if (reader.Read() == BethesdaGroupReaderState.EndOfContent)
            {
                Debug.Fail("Subgroup is already at the end");
                return;
            }

            UArraySegment<byte> grpRawData = group.RawData;
            UArraySegment<byte> subRawData = subgroup.RawData;
            byte[] subgroupData = new byte[subRawData.Count];
            UBuffer.BlockCopy(subRawData, 0, subgroupData, 0, subRawData.Count);
            UBuffer.BlockCopy(grpRawData.Array, subRawData.Offset + subRawData.Count, grpRawData.Array, subRawData.Offset, grpRawData.Count - (subRawData.Offset - grpRawData.Offset) - subRawData.Count);
            UBuffer.BlockCopy(subgroupData, 0, grpRawData.Array, grpRawData.Offset + (grpRawData.Count - subRawData.Count), subRawData.Count);
        }

        public static BethesdaRecord RewriteRecord(BethesdaRecord record, IReadOnlyCollection<BethesdaField> fields)
        {
            uint fieldDataLength = 0;
            foreach (var field in fields)
            {
                fieldDataLength = fieldDataLength + field.RawData.Count;
            }

            byte[] recordRawData = new byte[fieldDataLength + 24];
            UBuffer.BlockCopy(record.RawData, 0, recordRawData, 0, 24);

            uint offset = 24;
            foreach (var field in fields)
            {
                var fieldRawData = field.RawData;
                UBuffer.BlockCopy(fieldRawData, 0, recordRawData, offset, fieldRawData.Count);
                offset += fieldRawData.Count;
            }

            record = new BethesdaRecord(recordRawData);
            record.DataSize = offset - 24;
            return record;
        }

        public static unsafe BethesdaRecord OptimizeDOBJ(BethesdaRecord record)
        {
            if (record.Type != DOBJ)
            {
                throw new ArgumentException("Must be a DOBJ.", nameof(record));
            }

            BethesdaField field = record.Fields.Single();
            UArraySegment<byte> rawData = field.RawData;
            if (rawData.Count != field.StoredSize)
            {
                throw new NotSupportedException("DOBJ fields with XXXX lengths are not supported right now.");
            }

            ushort realCount = 0;
            byte[] realDataBuffer = new byte[rawData.Count];
            for (uint i = 6; i < rawData.Count; i += 8)
            {
                if (rawData[i + 0] == 0 &&
                    rawData[i + 2] == 0 &&
                    rawData[i + 3] == 0 &&
                    rawData[i + 4] == 0 &&
                    rawData[i + 5] == 0 &&
                    rawData[i + 6] == 0 &&
                    rawData[i + 7] == 0)
                {
                    continue;
                }

                UBuffer.BlockCopy(rawData, i, realDataBuffer, unchecked((uint)(realCount + 6)), 8);
                realCount += 8;
            }

            UArrayPosition<byte> pos = new UArrayPosition<byte>(realDataBuffer);
            UBitConverter.SetUInt32(pos, field.Type);
            UBitConverter.SetUInt16(pos + 4, realCount);
            field = new BethesdaField(new UArraySegment<byte>(realDataBuffer, 0, unchecked((uint)(realCount + 6))));
            return RewriteRecord(record, new[] { field });
        }

        public static BethesdaRecord UndeleteAndDisableReference(BethesdaRecord record, BethesdaRecord template)
        {
            if (record.Type != REFR)
            {
                throw new ArgumentException("Must be a REFR.", nameof(record));
            }

            byte[] rawData = new byte[template.Payload.Count + 68];
            UBuffer.BlockCopy(record.RawData, 0, rawData, 0, 24);
            record = new BethesdaRecord(rawData);
            record.DataSize = template.DataSize;
            UBuffer.BlockCopy(template.Payload, 0, record.Payload, 0, template.Payload.Count);

            record.Flags = (record.Flags & ~BethesdaRecordFlags.Deleted) | BethesdaRecordFlags.InitiallyDisabled;

            int handled = 0;
            foreach (var field in record.Fields)
            {
                switch (field.Type)
                {
                    case _DATA:
                        if ((handled & 2) != 0)
                        {
                            throw new InvalidDataException("DATA shows up twice.");
                        }

                        handled |= 2;
                        UDR_DATA(field);
                        break;

                    case _XESP:
                        if ((handled & 1) != 0)
                        {
                            throw new InvalidDataException("XESP shows up twice.");
                        }

                        UDR_XESP(field);
                        handled |= 1;
                        break;
                }

                if (handled == 3)
                {
                    break;
                }
            }

            if ((handled & 2) == 0)
            {
                BethesdaField field = new BethesdaField(new UArraySegment<byte>(rawData, record.DataSize + 24, 30));
                record.DataSize += 30;
                UBitConverter.SetUInt32(field.Start, DATA);
                UBitConverter.SetUInt16(field.Start + 4, 24);
                UDR_DATA(field);
            }

            if ((handled & 1) == 0)
            {
                BethesdaField field = new BethesdaField(new UArraySegment<byte>(rawData, record.DataSize + 24, 14));
                record.DataSize += 14;
                UBitConverter.SetUInt32(field.Start, XESP);
                UBitConverter.SetUInt16(field.Start + 4, 8);
                UDR_XESP(field);
            }

            return record;
        }

        private static void UDR_XESP(BethesdaField field)
        {
            UBitConverter.SetUInt32(field.PayloadStart, 0x14);
            UBitConverter.SetUInt32(field.PayloadStart + 4, 0x01);
        }

        private static void UDR_DATA(BethesdaField field)
        {
            ////System.BitConverter.ToUInt32(System.BitConverter.GetBytes((float)-30000), 0)
            UBitConverter.SetUInt32(field.PayloadStart + 8, 3337248768);
        }
    }
}
