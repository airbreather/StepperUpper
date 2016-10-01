using System;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Parser
    {
        public static Record Parse(BethesdaFile file)
        {
            Record record = ParseRecord(file.HeaderRecord);
            Record d = null;
            record.Subgroups.AddRange(file.TopGroups.Select(ParseGroup));
            return record;
        }

        private static Record ParseRecord(BethesdaRecord record)
        {
            Record result = new Record
            {
                Type = record.Type,
                Flags = record.Flags,
                Id = record.Id,
                Revision = record.Revision,
                Version = record.Version,
                UNKNOWN_22 = record.UNKNOWN_22
            };

            if (record.Flags.HasFlag(BethesdaRecordFlags.Compressed))
            {
                result.OriginalCompressedFieldData = record.Payload.ToArray();
            }

            uint? offsides = null;
            foreach (BethesdaField field in record.Fields)
            {
                if (result.Type == XXXX)
                {
                    offsides = UBitConverter.ToUInt32(field.PayloadStart);
                    continue;
                }

                byte[] payload = new byte[offsides ?? field.StoredSize];
                Buffer.BlockCopy(field.PayloadStart.Array, checked((int)field.PayloadStart.Offset), payload, 0, payload.Length);
                result.Fields.Add(new Field
                {
                    Type = field.Type,
                    Payload = payload
                });

                offsides = null;
            }

            return result;
        }

        private static Group ParseGroup(BethesdaGroup grp)
        {
            Group result = new Group
            {
                Type = grp.GroupType,
                Label = grp.Label,
                Stamp = grp.Stamp,
                UNKNOWN_18 = grp.UNKNOWN_18,
                Version = grp.Version,
                UNKNOWN_22 = grp.UNKNOWN_22,
            };

            Record record = null;
            BethesdaGroupReader reader = new BethesdaGroupReader(grp);
            BethesdaGroupReaderState state;
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        result.Records.Add(record = ParseRecord(reader.CurrentRecord));
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        if (record == null)
                        {
                            result.Records.Add(record = new Record());
                        }

                        record.Subgroups.Add(ParseGroup(reader.CurrentSubgroup));
                        break;
                }
            }

            return result;
        }
    }
}
