using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using AirBreather;

namespace BethFile.Editor
{
    public sealed class ObjectComparer : EqualityComparer<object>
    {
        public static readonly ObjectComparer Instance = new ObjectComparer();

        private ObjectComparer() { }

        public override bool Equals(object x, object y)
        {
            switch (x)
            {
                case Field f1:
                    return y is Field f2 && FieldComparer.Instance.Equals(f1, f2);

                case Record r1:
                    return y is Record r2 && RecordComparer.Instance.Equals(r1, r2);

                case Group g1:
                    return y is Group g2 && GroupComparer.Instance.Equals(g1, g2);

                default:
                    throw new NotSupportedException("Unrecognized type.");
            }
        }

        public override int GetHashCode(object obj)
        {
            int hc = HashCode.Seed;
            switch (obj)
            {
                case Field f1:
                    return hc.HashWith(0).HashWith(FieldComparer.Instance.GetHashCode(f1));

                case Record r1:
                    return hc.HashWith(1).HashWith(RecordComparer.Instance.GetHashCode(r1));

                case Group g1:
                    return hc.HashWith(2).HashWith(GroupComparer.Instance.GetHashCode(g1));

                default:
                    throw new NotSupportedException("Unrecognized type.");

            }
        }
    }

    public sealed class RecordComparer : Comparer<Record>, IEqualityComparer<Record>
    {
        public static readonly RecordComparer Instance = new RecordComparer();

        private RecordComparer() { }

        public override int Compare(Record x, Record y) => x.Id.CompareTo(y.Id);

        public bool Equals(Record x, Record y) => Compare(x, y) == 0;

        public int GetHashCode(Record x) => unchecked((int)x.Id);
    }

    public sealed class GroupComparer : Comparer<Group>, IEqualityComparer<Group>
    {
        public static readonly GroupComparer Instance = new GroupComparer();

        private GroupComparer() { }

        public override int Compare(Group x, Group y)
        {
            int cmp = x.GroupType.CompareTo(y.GroupType);
            return cmp == 0
                ? x.Label.CompareTo(y.Label)
                : cmp;
        }

        public bool Equals(Group x, Group y) => Compare(x, y) == 0;

        public int GetHashCode(Group x) => HashCode.Seed.HashWith(x.GroupType).HashWith(x.Label);
    }

    public sealed class FieldComparer : Comparer<Field>, IEqualityComparer<Field>
    {
        public static readonly FieldComparer Instance = new FieldComparer();

        private FieldComparer() { }

        public override int Compare(Field x, Field y)
        {
            int cmp = ((uint)x.FieldType).CompareTo(y.FieldType);
            if (cmp != 0)
            {
                return cmp;
            }

            byte[] xdata = x.Payload;
            byte[] ydata = y.Payload;

            long xlen = xdata.LongLength;
            long ylen = ydata.LongLength;

            long cnt = Math.Min(xlen, ylen);
            cmp = memcmp(xdata, ydata, cnt);
            return cmp == 0
                ? xlen.CompareTo(ylen)
                : cmp;
        }

        public bool Equals(Field x, Field y) => x.FieldType == y.FieldType && x.Payload.LongLength == y.Payload.LongLength && memcmp(x.Payload, y.Payload, x.Payload.LongLength) == 0;

        public int GetHashCode(Field x) => HashCode.Seed.HashWith(x.FieldType).HashWith(x.Payload.LongLength);

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long cnt);
    }

}
