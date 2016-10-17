using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Doer
    {
        public static IEnumerable<uint> GetOnams(Record root)
        {
            HashSet<uint> onams = new HashSet<uint>();
            var onamField = root.Fields.Single(f => f.FieldType == ONAM);
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

        // not working right now.
        public static void GetITMs(Record root, Merged orig)
        {
            var q = from currRecord in FindRecords(root).AsParallel()
                    where currRecord.IsIdentical(orig.FindRecord(currRecord.Id))
                    select currRecord;
            var itms = q.OrderBy(r => r.RecordType).ThenBy(r => r.Id).ToArray();
        }

        public static void PerformDeletes(Record root, HashSet<uint> ids)
        {
            Stack<Group> stack = new Stack<Group>();
            foreach (var grp in root.Subgroups)
            {
                stack.Push(grp);
            }

            while (stack.Count != 0)
            {
                Group grp = stack.Pop();

                for (int i = 0; i < grp.Records.Count; i++)
                {
                    Record rec = grp.Records[i];

                    if (rec.IsDummy || !ids.Remove(rec.Id))
                    {
                        goto done;
                    }

                    grp.Records.RemoveAt(i--);

                    done:
                    foreach (var grp2 in rec.Subgroups)
                    {
                        stack.Push(grp2);
                    }
                }

                while (grp?.Records.Count == 0)
                {
                    Record par = grp.Parent;
                    par.Subgroups.Remove(grp);
                    if (!par.IsDummy || par.Subgroups.Count != 0)
                    {
                        break;
                    }

                    grp = par.Parent;
                    grp.Records.Remove(par);
                }
            }
        }

        // assumes all IDs were deleted by PerformDeletes.
        public static void PerformUDRs(Record root, Merged master, IEnumerable<uint> ids)
        {
            foreach (uint id in ids)
            {
                Record orig = master.FindRecord(id);
                orig = new Record(orig) { Parent = orig.Parent };

                orig.Flags |= BethesdaRecordFlags.InitiallyDisabled;
                if (orig.RecordType == ACHR || orig.RecordType == ACRE)
                {
                    orig.Flags |= BethesdaRecordFlags.PersistentReference;
                }

                if (orig.Flags.HasFlag(BethesdaRecordFlags.PersistentReference) && orig.Parent.GroupType != BethesdaGroupType.CellPersistentChildren)
                {
                    Record wrldParent = null;

                    // persistent children of the HIGHEST cell in the world
                    while (orig.Parent != null)
                    {
                        Record rrrr = orig.Parent.Parent;
                        if (rrrr.RecordType == WRLD)
                        {
                            wrldParent = rrrr;
                        }

                        orig.Parent = rrrr.Parent;
                    }

                    orig.Parent = wrldParent.Subgroups
                                            .Single(g => g.GroupType == BethesdaGroupType.WorldChildren)
                                            .Records
                                            .Single(r => r.RecordType == CELL)
                                            .Subgroups
                                            .Single(g => g.GroupType == BethesdaGroupType.CellChildren)
                                            .Records
                                            .Single()
                                            .Subgroups
                                            .Single(g => g.GroupType == BethesdaGroupType.CellPersistentChildren);
                }

                bool isPersistent = orig.Flags.HasFlag(BethesdaRecordFlags.PersistentReference);
                if (!isPersistent)
                {
                    Field dataField = orig.Fields.Find(f => f.FieldType == DATA);
                    if (dataField == null)
                    {
                        orig.Fields.Add(dataField = new Field
                        {
                            FieldType = DATA,
                            Payload = new byte[24]
                        });
                    }

                    UBitConverter.SetUInt32(dataField.Payload, 8, 3337248768);
                }

                Field xespField = orig.Fields.Find(f => f.FieldType == XESP);
                if (xespField == null)
                {
                    orig.Fields.Add(xespField = new Field
                    {
                        FieldType = XESP,
                        Payload = new byte[8]
                    });
                }

                UBitConverter.SetUInt32(xespField.Payload, 0, 0x14);
                UBitConverter.SetUInt32(xespField.Payload, 4, 0x01);
                orig.CompressedFieldData = null;

                MergeInto(orig, root);
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

                foreach (var subgroup in rec.Subgroups)
                {
                    foreach (var rec2 in subgroup.Records)
                    {
                        stack.Push(rec2);
                    }
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
            rec.CompressedFieldData = null;
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
                if (rec.Flags.HasFlag(BethesdaRecordFlags.Deleted))
                {
                    rec.Fields.Clear();
                    rec.CompressedFieldData = null;
                }

                switch (rec.RecordType)
                {
                    case _DOBJ:
                        foreach (var field in rec.Fields)
                        {
                            if (field.FieldType != DNAM)
                            {
                                continue;
                            }

                            field.Payload = CompressDNAM(field.Payload);
                            rec.CompressedFieldData = null;
                            break;
                        }

                        break;

                    case _WRLD:
                        List<Field> flds = rec.Fields;
                        for (int i = 0; i < flds.Count; i++)
                        {
                            switch (flds[i].FieldType)
                            {
                                case _OFST:
                                case _RNAM:
                                    flds.RemoveAt(i--);
                                    rec.CompressedFieldData = null;
                                    break;
                            }
                        }

                        break;

                    // not strictly necessary.
                    case _WEAP:
                        foreach (var field in rec.Fields)
                        {
                            if (field.FieldType != DNAM)
                            {
                                continue;
                            }

                            byte[] payload = field.Payload;
                            payload[12] &= unchecked((byte)(~0x40));
                            payload[41] &= unchecked((byte)(~0x01));

                            rec.CompressedFieldData = null;
                        }

                        break;

                    // not strictly necessary either, it seems.
                    case _REFR:
                        foreach (var field in rec.Fields)
                        {
                            if (field.FieldType != XLOC || field.Payload[0] != 0)
                            {
                                continue;
                            }

                            field.Payload[0] = 1;
                            rec.CompressedFieldData = null;
                        }

                        break;

                    case _LVLN:
                        if (0 <= rec.Fields.FindIndex(f => f.FieldType == LLCT))
                        {
                            continue;
                        }

                        // TODO: this is what xEdit does, but it doesn't feel right...
                        rec.Fields.Add(new Field
                        {
                            FieldType = LLCT,
                            Payload = new[] { (byte)1 },
                        });

                        rec.Fields.Add(new Field
                        {
                            FieldType = LVLO,
                            Payload = new byte[12],
                        });

                        rec.CompressedFieldData = null;
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

        public static IEnumerable<object> Iterate(Record root)
        {
            bool first = true;
            Stack<object> results = new Stack<object>();
            results.Push(root);
            while (results.Count != 0)
            {
                object curr = results.Pop();
                if (first)
                {
                    first = false;
                }
                else
                {
                    yield return curr;
                }

                Record rec = curr as Record;
                if (rec == null)
                {
                    goto currIsSubgroup;
                }

                foreach (Group subgroup in rec.Subgroups.AsEnumerable().Reverse())
                {
                    results.Push(subgroup);
                }

                continue;

                currIsSubgroup:
                Group grp = (Group)curr;
                foreach (Record subrecord in grp.Records.AsEnumerable().Reverse())
                {
                    if (subrecord.IsDummy)
                    {
                        foreach (Group subgroup in subrecord.Subgroups.AsEnumerable().Reverse())
                        {
                            results.Push(subgroup);
                        }

                        continue;
                    }

                    results.Push(subrecord);
                }
            }
        }

        private static void MergeInto(Record orig, Record root)
        {
            Stack<Group> parentGroups = new Stack<Group>();
            Stack<Record> parentRecords = new Stack<Record>();

            Group origParent = orig.Parent;
            while (origParent != null)
            {
                parentGroups.Push(origParent);
                parentRecords.Push(origParent.Parent);
                origParent = origParent.Parent.Parent;
            }

            parentRecords.Pop();
            Record recordParent = root;
            while (parentGroups.Count != 1)
            {
                Group origParentGroup = parentGroups.Pop();
                Group parentGroup = recordParent.Subgroups.FirstOrDefault(g => g.GroupType == origParentGroup.GroupType && g.Label == origParentGroup.Label);
                if (parentGroup == null)
                {
                    recordParent.Subgroups.Add(parentGroup = new Group { Parent = recordParent });
                    parentGroup.CopyHeadersFrom(origParentGroup);
                }

                Record origSubrecord = parentRecords.Pop();
                recordParent = parentGroup.Records.FirstOrDefault(r => r.Id == origSubrecord.Id);
                if (recordParent == null)
                {
                    parentGroup.Records.Add(recordParent = new Record { Parent = parentGroup });
                    recordParent.CopyHeadersFrom(origSubrecord);
                }
            }

            if (parentRecords.Count != 0)
            {
                Debug.Fail("Didn't expect this at all.");
                parentRecords.Clear();
            }

            Group finalOrigParent = parentGroups.Pop();
            Group finalParent = recordParent.Subgroups.FirstOrDefault(g => g.GroupType == finalOrigParent.GroupType && g.Label == finalOrigParent.Label);

            if (finalParent == null)
            {
                recordParent.Subgroups.Add(finalParent = new Group { Parent = recordParent });
                finalParent.CopyHeadersFrom(finalOrigParent);
            }

            finalParent.Records.Add(orig);
        }
    }
}
