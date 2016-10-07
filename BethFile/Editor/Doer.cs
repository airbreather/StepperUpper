using System;
using System.Collections.Generic;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Doer
    {
        public static void DeleteRecords(Record parent, HashSet<uint> ids)
        {
            DeleteCore(parent, ids);
        }

        public static void UDR(Record rec, Dictionary<uint, Record> origMapping)
        {
            // TODO: I feel like something ONAM-related can / should be done here... but I can't
            // figure out exactly what.  Let's at least get rid of the useless zeroes.
            var onamField = rec.Fields.Find(x => x.Type == ONAM);

            List<uint> vals = new List<uint>();
            for (uint i = 0; i < onamField.Payload.Length; i += 4)
            {
                uint val = UBitConverter.ToUInt32(onamField.Payload, i);

                // this ContainsKey isn't what we need...
                if (val != 0 /*&& !origMapping.ContainsKey(val)*/)
                {
                    vals.Add(val);
                }
            }

            if (vals.Count != onamField.Payload.Length / 4)
            {
                onamField.Payload = new byte[vals.Count * 4];

                UArrayPosition<byte> pos = new UArrayPosition<byte>(onamField.Payload);
                foreach (uint val in vals)
                {
                    UBitConverter.SetUInt32(pos, val);
                    pos += 4;
                }
            }

            UDRCore(rec, origMapping);
        }

        private static void UDRCore(Record rec, Dictionary<uint, Record> origMapping)
        {
            Record orig;
            if (!rec.IsDummy && origMapping.TryGetValue(rec.Id, out orig))
            {
                rec.Flags = (rec.Flags & ~BethesdaRecordFlags.Deleted) | BethesdaRecordFlags.InitiallyDisabled;

                rec.Fields.Clear();
                rec.Fields.AddRange(orig.Fields.Select(f => new Field(f)));
                Field dataField = rec.Fields.Find(f => f.Type == DATA);
                if (dataField == null)
                {
                    rec.Fields.Add(dataField = new Field
                    {
                        Type = DATA,
                        Payload = new byte[24]
                    });
                }

                UBitConverter.SetUInt32(dataField.Payload, 8, 3337248768);

                Field xespField = rec.Fields.Find(f => f.Type == XESP);
                if (xespField == null)
                {
                    rec.Fields.Add(xespField = new Field
                    {
                        Type = XESP,
                        Payload = new byte[8]
                    });
                }

                UBitConverter.SetUInt32(xespField.Payload, 0, 0x14);
                UBitConverter.SetUInt32(xespField.Payload, 4, 0x01);
            }

            foreach (var rec2 in rec.Subgroups.SelectMany(grp => grp.Records))
            {
                UDRCore(rec2, origMapping);
            }
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

        public static void Optimize(Record rec)
        {
            foreach (var rec2 in FindRecords(rec))
            {
                switch (rec2.Type)
                {
                    case _DOBJ:
                        foreach (var field in rec2.Fields)
                        {
                            if (field.Type != DNAM)
                            {
                                continue;
                            }

                            UArrayPosition<byte> pos = new UArrayPosition<byte>(field.Payload);
                            for (uint i = 0; i < field.Payload.Length; i += 8)
                            {
                                ulong val = UBitConverter.ToUInt64(field.Payload, i);
                                if (val == 0)
                                {
                                    continue;
                                }

                                UBitConverter.SetUInt64(pos, val);
                                pos += 8;
                            }

                            byte[] payload = field.Payload;
                            Array.Resize(ref payload, checked((int)pos.Offset));
                            field.Payload = payload;
                        }

                        break;

                    case _WRLD:
                        List<Field> flds = rec2.Fields;
                        for (int i = 0; i < flds.Count; i++)
                        {
                            switch (flds[i].Type)
                            {
                                case _OFST:
                                case _RNAM:
                                    flds.RemoveAt(i--);
                                    break;
                            }
                        }

                        break;
                }
            }
        }

        internal static void Sort(Group grp)
        {
            // this isn't quite perfect yet... need to order groups too somehow.
            grp.Records.Sort((r1, r2) => r1.Id.CompareTo(r2.Id));
        }

        private static bool DeleteCore(Record rec, HashSet<uint> ids)
        {
            if (!rec.IsDummy && ids.Contains(rec.Id))
            {
                return true;
            }

            List<Group> grps = rec.Subgroups;
            for (int i = 0; i < grps.Count; i++)
            {
                Group grp = grps[i];
                bool deleted = false;
                List<Record> subs = grp.Records;
                for (int j = 0; j < subs.Count; j++)
                {
                    if (!DeleteCore(subs[j], ids))
                    {
                        continue;
                    }

                    subs.RemoveAt(j--);
                    deleted = true;
                }

                if (deleted)
                {
                    Sort(grp);
                }

                if (subs.Count == 0 ||
                    subs.Count == 1 && subs[0].IsDummy && subs[0].Subgroups.Count == 0)
                {
                    grps.RemoveAt(i--);
                }
            }

            return false;
        }
    }
}
