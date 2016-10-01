using System;
using System.Collections.Generic;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Saver
    {
        public static BethesdaFile Save(Record record)
        {
            FinalizeHeader(record);
            byte[] rec = new byte[CalculateSize(record, true)];
            UArrayPosition<byte> pos = new UArrayPosition<byte>(rec);
            Saved saved = Write(record, ref pos);
            return new BethesdaFile(saved.Record, saved.Subgroups);
        }

        private static void FinalizeHeader(Record header)
        {
            uint itemCount = CountItems(header) - 1;
            var hedr = header.Fields.Single(f => f.Type == HEDR);
            UBitConverter.SetUInt32(new UArrayPosition<byte>(hedr.Payload) + 4, itemCount);

            // assume deletes ONLY.
            HashSet<uint> recs = new HashSet<uint>();
            AddRecordIds(header, recs);

            List<uint> onams = new List<uint>();
            var idx = header.Fields.FindIndex(f => f.Type == ONAM);
            byte[] payload = header.Fields[idx].Payload;

            uint i;
            for (i = 0; i < payload.Length; i += 4)
            {
                var x = UBitConverter.ToUInt32(payload, i);
                if (recs.Remove(x))
                {
                    onams.Add(x);
                }
            }

            payload = new byte[i];
            i = 0;
            foreach (var val in onams)
            {
                UBitConverter.SetUInt32(payload, i, val);
                i += 4;
            }

            header.Fields[idx] = new Field
            {
                Type = ONAM,
                Payload = payload
            };
        }

        private static void AddRecordIds(Record rec, HashSet<uint> ids)
        {
            if (rec.Type != Record.DummyType)
            {
                ids.Add(rec.Id);
            }

            foreach (var rec2 in rec.Subgroups.SelectMany(grp => grp.Records))
            {
                AddRecordIds(rec2, ids);
            }
        }

        private static uint CountItems(Record rec)
        {
            uint i = 0;
            if (rec.Type != Record.DummyType)
            {
                ++i;
            }

            foreach (var grp in rec.Subgroups)
            {
                ++i;
                foreach (var subRec in grp.Records)
                {
                    i += CountItems(subRec);
                }
            }

            return i;
        }

        private static Saved Write(Record record, ref UArrayPosition<byte> pos)
        {
            Saved saved = new Saved();

            if (record.Type == Record.DummyType)
            {
                goto groups;
            }

            saved.HasRecord = true;
            BethesdaRecord rec = saved.Record = new BethesdaRecord(pos)
            {
                Type = record.Type,
                DataSize = CalculateSize(record, false) - 24,
                Flags = record.Flags,
                Id = record.Id,
                Revision = record.Revision,
                Version = record.Version,
                UNKNOWN_22 = record.UNKNOWN_22
            };

            pos += 24;
            if (record.Flags.HasFlag(BethesdaRecordFlags.Compressed))
            {
                byte[] compressedPayload = GetCompressedPayload(record);
                uint compressedPayloadLength = unchecked((uint)compressedPayload.LongLength);
                UBuffer.BlockCopy(compressedPayload, 0, pos, 0, compressedPayloadLength);
                pos += compressedPayloadLength;
            }
            else
            {
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

                    UBitConverter.SetUInt32(pos, field.Type);
                    pos += 4;

                    UBitConverter.SetUInt16(pos, storedFieldLength);
                    pos += 2;

                    if (fieldLength != 0)
                    {
                        UBuffer.BlockCopy(field.Payload, 0, pos, 0, fieldLength + 6);
                    }

                    pos += fieldLength;
                }
            }

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
                GroupType = group.Type,
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
            if (record.Type == Record.DummyType)
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

            throw new NotImplementedException("TODO: this.");
        }

        private sealed class Saved
        {
            internal bool HasRecord;

            internal BethesdaRecord Record;

            internal BethesdaGroup[] Subgroups;
        }
    }
}
