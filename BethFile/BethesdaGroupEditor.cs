using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BethFile
{
    public static class BethesdaGroupEditor
    {
        public static void SortRecords(BethesdaGroup group)
        {
            byte[] newData = new byte[group.PayloadSize];
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

            UBuffer.BlockCopy(newData, 0, group.PayloadData, 0, group.PayloadData.Count);
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

            byte[] subgroupData = new byte[subgroup.RawData.Count];
            UBuffer.BlockCopy(subgroup.RawData, 0, subgroupData, 0, subgroup.RawData.Count);
            UBuffer.BlockCopy(group.RawData.Array, subgroup.RawData.Offset + subgroup.RawData.Count, group.RawData.Array, subgroup.RawData.Offset, group.RawData.Count - (subgroup.RawData.Offset - group.RawData.Offset) - subgroup.RawData.Count);
            UBuffer.BlockCopy(subgroupData, 0, group.RawData.Array, group.RawData.Offset + (group.RawData.Count - subgroup.RawData.Count), subgroup.RawData.Count);
        }
    }
}
