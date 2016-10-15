using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using AirBreather;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public struct ObjectIdentifier : IEquatable<ObjectIdentifier>, IComparable<ObjectIdentifier>
    {
        ////private readonly ImmutableArray<uint> data;

        private ObjectIdentifier(ImmutableArray<uint> data)
        {
            this.Data = data;
        }

        public bool IsDefault => this.Data.Length != 0;
        ////{
        ////    get
        ////    {
        ////        var self = this;
        ////        return self.data == null || self.data.Length == 0;
        ////    }
        ////}

        public static ObjectIdentifier Root => new ObjectIdentifier(ImmutableArray<uint>.Empty);

        // this used to be ImmutableList<uint>.
        internal ImmutableArray<uint> Data { get; }
        ////{
        ////    get
        ////    {
        ////        var self = this;
        ////        return self.data.IsDefault
        ////            ? ImmutableArray<uint>.Empty
        ////            : self.data;
        ////    }
        ////}

        public ObjectIdentifier Push(Record rec) => new ObjectIdentifier(this.Data.Add(rec.RecordType).Add(rec.Id));

        public ObjectIdentifier Push(Group grp) => new ObjectIdentifier(this.Data.Add(unchecked((uint)grp.GroupType)).Add(grp.Label));

        public ObjectIdentifier Push(Field fld, uint idx) => new ObjectIdentifier(this.Data.Add(fld.Type).Add(idx));

        public ObjectIdentifier Pop()
        {
            var self = this;
            return new ObjectIdentifier(self.Data.RemoveRange(self.Data.Length - 2, 2));
        }

        public object[] Resolve(Record root)
        {
            var self = this;

            List<object> path = new List<object>(self.Data.Length / 2);

            object obj = root;
            for (int i = 0; i < self.Data.Length; i += 2)
            {
                path.Add(obj = Resolve(obj, self.Data[i], self.Data[i + 1]));
            }

            return path.ToArray();
        }

        private static object Resolve(object obj, uint type, uint label)
        {
            Record rec = obj as Record;
            if (rec == null)
            {
                goto notRecord;
            }

            if (type == TES4 && label == 0 && rec.RecordType == TES4 && rec.Id == 0)
            {
                return obj;
            }

            uint i = 0;
            foreach (var fld in rec.Fields)
            {
                if (fld.Type == type && ++i == label)
                {
                    return fld;
                }
            }

            foreach (var subgroup in rec.Subgroups)
            {
                if (unchecked((uint)subgroup.GroupType) == type &&
                    subgroup.Label == label)
                {
                    return subgroup;
                }
            }

            return null;

            notRecord:
            Group grp = (Group)obj;
            foreach (var record in grp.Records)
            {
                if (record.IsDummy)
                {
                    return Resolve(record, type, label);
                }

                if (record.RecordType == type && record.Id == label)
                {
                    return record;
                }
            }

            return null;
        }

        public bool Equals(ObjectIdentifier other)
        {
            var self = this;
            if (self.Data.Length != other.Data.Length)
            {
                return false;
            }

            var enumerator1 = self.Data.GetEnumerator();
            var enumerator2 = other.Data.GetEnumerator();
            while (enumerator1.MoveNext())
            {
                enumerator2.MoveNext();
                if (enumerator1.Current != enumerator2.Current)
                {
                    return false;
                }
            }

            return true;
        }

        public int CompareTo(ObjectIdentifier other)
        {
            var enumerator1 = this.Data.GetEnumerator();
            var enumerator2 = other.Data.GetEnumerator();
            while (enumerator1.MoveNext())
            {
                // second ended before first or mismatch.
                if (!enumerator2.MoveNext())
                {
                    return 1;
                }

                int cmp = enumerator1.Current.CompareTo(enumerator2.Current);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            // first ended before second or mismatch.
            return enumerator2.MoveNext() ? -1 : 0;
        }

        public override int GetHashCode()
        {
            var self = this;
            if (self.Data.Length < 2)
            {
                return 0;
            }

            if (self.Data.Length < 4)
            {
                return HashCode.Seed
                               .HashWith(self.Data[self.Data.Length - 2])
                               .HashWith(self.Data[self.Data.Length - 1]);
            }

            return HashCode.Seed
                           .HashWith(self.Data[self.Data.Length - 4])
                           .HashWith(self.Data[self.Data.Length - 3])
                           .HashWith(self.Data[self.Data.Length - 2])
                           .HashWith(self.Data[self.Data.Length - 1]);
        }

        public override string ToString()
        {
            var self = this;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < self.Data.Length; i += 2)
            {
                sb.Append('(');
                sb.Append(self.Data[i + 0].ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(' ');
                sb.Append(self.Data[i + 1].ToString(CultureInfo.InvariantCulture));
                sb.Append(')');
                sb.Append('.');
            }

            if (sb.Length != 0)
            {
                --sb.Length;
            }

            return sb.ToString();
        }
    }
}
