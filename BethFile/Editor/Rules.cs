using System;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Rules
    {
        public static bool IsIdentical(this Record curr, Record master)
        {
            if (master == null ||
                curr.Id != master.Id ||
                curr.Flags != master.Flags ||
                curr.Fields.Count != master.Fields.Count ||
                curr.RecordType != master.RecordType)
            {
                return false;
            }

            foreach (var tup in curr.Fields.OrderBy(f => f, FieldComparer.Instance).Zip(master.Fields.OrderBy(f => f, FieldComparer.Instance), Tuple.Create))
            {
                var f1 = tup.Item1;
                var f2 = tup.Item2;
                if (f1.FieldType != f2.FieldType ||
                    !FieldsAreIdentical(curr.RecordType, f1, f2))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FieldsAreIdentical(B4S recordType, Field f1, Field f2)
        {
            // clear out unused bytes before doing the check.
            switch (f1.FieldType)
            {
                case _CTDA:
                    if (recordType != IDLE)
                    {
                        goto default;
                    }

                    f1 = new Field(f1);
                    f2 = new Field(f2);
                    UBitConverter.SetUInt32(f1.Payload, 0, f1.Payload[0]);
                    UBitConverter.SetUInt32(f2.Payload, 0, f2.Payload[0]);

                    UBitConverter.SetUInt16(f1.Payload, 10, 0);
                    UBitConverter.SetUInt16(f2.Payload, 10, 0);

                    // TODO: ugh, [IDLE:00013344]'s field is apparently allowed
                    // to differ non-trivially.
                    goto default;

                case _ENAM:
                    if (f1.Payload.LongLength != f2.Payload.LongLength)
                    {
                        return false;
                    }

                    if (recordType != IDLE)
                    {
                        goto default;
                    }

                    // ENAM is apparently case-insensitive, according to xEdit.
                    f1 = new Field(f1);
                    f2 = new Field(f2);

                    for (long i = 0; i < f1.Payload.LongLength; i++)
                    {
                        f1.Payload[i] = unchecked((byte)(f1.Payload[i] & 0xDF));
                        f2.Payload[i] = unchecked((byte)(f1.Payload[i] & 0xDF));
                    }

                    goto default;

                default:
                    return f1.Payload.SequenceEqual(f2.Payload);
            }
        }
    }
}
