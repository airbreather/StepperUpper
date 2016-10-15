using System;
using System.Collections.Generic;
using System.Linq;

using AirBreather;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Doer
    {
        public static IEnumerable<uint> GetOnams(Record root)
        {
            HashSet<uint> onams = new HashSet<uint>();
            var onamField = root.Fields.Single(f => f.Type == ONAM);
            var oldOnamData = onamField.Payload;
            for (int i = 0; i < oldOnamData.Length; i += 4)
            {
                uint onam = BitConverter.ToUInt32(oldOnamData, i);
                if (onams.Add(onam))
                {
                    yield return onam;
                }
            }
        }

        public static void FixOnams(Record root, IEnumerable<uint> extraOnams)
        {
            var onamField = root.Fields.Single(f => f.Type == ONAM);
            HashSet<uint> onams = GetOnams(root).ToHashSet();
            onams.IntersectWith(FindRecords(root).Select(r => r.Id));
            onams.UnionWith(extraOnams);

            uint[] onamsArray = new uint[onams.Count];
            onams.CopyTo(onamsArray);
            Array.Sort(onamsArray);

            onamField.Payload = new byte[unchecked((uint)(onamsArray.Length) * 4u)];

            uint idx = 0;
            foreach (uint onam in onamsArray)
            {
                UBitConverter.SetUInt32(onamField.Payload, idx, onam);
                idx += 4;
            }

            root.OriginalCompressedFieldData = null;
        }

        public static IEnumerable<Record> FindRecords(Record rec)
        {
            Stack<Record> stack = new Stack<Record>();
            stack.Push(rec);
            while (stack.Count != 0)
            {
                rec = stack.Pop();
                if (!rec.IsDummy)
                {
                    yield return rec;
                }

                foreach (var rec2 in rec.Subgroups.SelectMany(grp => grp.Records))
                {
                    stack.Push(rec2);
                }
            }
        }

        // used for debugging mainly
        public static Record Sort(Record root)
        {
            root = new Record(root);
            SortCore(root);
            return root;
        }

        private static void SortCore(Record rec)
        {
            foreach (var subgroup in rec.Subgroups)
            {
                SortCore(subgroup);
            }

            rec.Subgroups.Sort(GroupComparer.Instance);
            rec.Fields.Sort(FieldComparer.Instance);
            rec.OriginalCompressedFieldData = null;
        }

        private static void SortCore(Group grp)
        {
            foreach (var rec in grp.Records)
            {
                SortCore(rec);
            }

            grp.Records.Sort(RecordComparer.Instance);
        }

        public static void Optimize(Record root)
        {
            foreach (var rec in FindRecords(root))
            {
                switch (rec.RecordType)
                {
                    case _DOBJ:
                        foreach (var field in rec.Fields)
                        {
                            if (field.Type != DNAM)
                            {
                                continue;
                            }

                            field.Payload = CompressDNAM(field.Payload);
                            rec.OriginalCompressedFieldData = null;
                            break;
                        }

                        break;

                    case _WRLD:
                        List<Field> flds = rec.Fields;
                        for (int i = 0; i < flds.Count; i++)
                        {
                            switch (flds[i].Type)
                            {
                                case _OFST:
                                case _RNAM:
                                    flds.RemoveAt(i--);
                                    rec.OriginalCompressedFieldData = null;
                                    break;
                            }
                        }

                        break;

                    // not strictly necessary.
                    case _WEAP:
                        foreach (var field in rec.Fields)
                        {
                            if (field.Type != DNAM)
                            {
                                continue;
                            }

                            byte[] payload = field.Payload;
                            payload[12] &= unchecked((byte)(~0x40));
                            payload[41] &= unchecked((byte)(~0x01));

                            rec.OriginalCompressedFieldData = null;
                        }

                        break;

                    // not strictly necessary either, it seems.
                    case _REFR:
                        foreach (var field in rec.Fields)
                        {
                            if (field.Type != XLOC)
                            {
                                continue;
                            }

                            if (field.Payload[0] == 0)
                            {
                                field.Payload[0] = 1;
                            }

                            rec.OriginalCompressedFieldData = null;
                        }

                        break;
                }
            }
        }

        private static byte[] CompressDNAM(byte[] dnamPayload)
        {
            UArrayPosition<byte> pos = new UArrayPosition<byte>(dnamPayload);
            for (uint i = 0; i < dnamPayload.Length; i += 8)
            {
                ulong val = UBitConverter.ToUInt64(dnamPayload, i);
                if (val == 0)
                {
                    continue;
                }

                UBitConverter.SetUInt64(pos, val);
                pos += 8;
            }

            Array.Resize(ref dnamPayload, checked((int)pos.Offset));
            return dnamPayload;
        }

        public static uint CountItems(Record rec)
        {
            uint itemCount = 0;
            CountItems(rec, ref itemCount);
            return itemCount;
        }

        public static uint CountItems(Group grp)
        {
            uint itemCount = 0;
            CountItems(grp, ref itemCount);
            return itemCount;
        }

        private static void CountItems(Group grp, ref uint i)
        {
            ++i;
            foreach (var subRec in grp.Records)
            {
                CountItems(subRec, ref i);
            }
        }

        private static void CountItems(Record rec, ref uint i)
        {
            if (!rec.IsDummy)
            {
                ++i;
            }

            foreach (var grp in rec.Subgroups)
            {
                CountItems(grp, ref i);
            }
        }
    }
}
