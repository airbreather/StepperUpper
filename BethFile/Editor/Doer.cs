using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using AirBreather;

using static BethFile.B4S;

namespace BethFile.Editor
{
    public static class Doer
    {
        public static readonly Encoding StandardEncoding = Encoding.GetEncoding(1252);

        public static HashSet<uint> GetOnams(Record root)
        {
            HashSet<uint> onams = new HashSet<uint>();
            var onamField = root.Fields.Single(f => f.FieldType == ONAM);
            var oldOnamData = onamField.Payload;
            for (int i = 0; i < oldOnamData.Length; i += 4)
            {
                uint onam = BitConverter.ToUInt32(oldOnamData, i);
                onams.Add(onam);
            }

            return onams;
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

                    MBitConverter.Set(dataField.Payload, 8, 3337248768);
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

                MBitConverter.Set(xespField.Payload, 0, (uint)0x14);
                MBitConverter.Set(xespField.Payload, 4, (uint)0x01);
                orig.CompressedFieldData = null;

                MergeInto(orig, root);
            }
        }

        public static void DeleteField(Record root, uint recordId, B4S fieldType)
        {
            var q = from record in FindRecords(root).AsParallel()
                    where record.Id == recordId
                    from field in record.Fields
                    where field.FieldType == fieldType
                    select (record, field);

            var (foundRecord, foundField) = q.First();
            foundRecord.Fields.Remove(foundField);
            foundRecord.CompressedFieldData = null;
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

        public static Record CreateEmptyRoot() => new Record
        {
            RecordType = TES4,
            Id = 0,
            Version = 43,
            Revision = 0,
            Fields =
            {
                new Field { FieldType = HEDR, Payload = "9a99d93f0000000000080000".HexStringToByteArrayUnchecked() },
                new Field { FieldType = CNAM, Payload = new byte[] { 0x00 } }
            }
        };

        public static IEnumerable<string> GetMasters(Record root) =>
            from field in root.Fields
            where field.FieldType == MAST
            select StandardEncoding.GetString(field.Payload, 0, field.Payload.Length - 1);

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
                }
            }
        }

        private static byte[] CompressDNAM(byte[] dnamPayload)
        {
            MArrayPosition<byte> pos = new MArrayPosition<byte>(dnamPayload);
            for (int i = 0; i < dnamPayload.Length; i += 8)
            {
                ulong val = MBitConverter.To<ulong>(dnamPayload, i);
                if (val == 0)
                {
                    continue;
                }

                MBitConverter.Set(pos, val);
                pos += 8;
            }

            Array.Resize(ref dnamPayload, pos.Offset);
            return dnamPayload;
        }

        public static uint CountItems(Record rec)
        {
            uint itemCount = 0;
            CountItems(rec, ref itemCount);
            return itemCount;
        }

        public static void MergeIntoConflictResolutionPatch(Record orig, Record conflictResolutionPatchRoot, string container)
        {
            Record origRoot = orig.Parent.Parent;
            while (origRoot.Parent != null)
            {
                origRoot = origRoot.Parent.Parent;
            }

            orig = new Record(orig)
            {
                Id = TranslateFormId(orig.Id, origRoot, conflictResolutionPatchRoot),
                Parent = orig.Parent
            };

            // TODO: process formids in fields.
            MergeInto(orig, conflictResolutionPatchRoot);
        }

        public static string GetMaster(byte index, Record root)
        {
            byte currMasterIndex = 0;
            foreach (var field in root.Fields)
            {
                if (field.FieldType != MAST)
                {
                    continue;
                }

                if (index == currMasterIndex)
                {
                    return StandardEncoding.GetString(field.Payload, 0, field.Payload.Length - 1);
                }

                ++currMasterIndex;
            }

            return null;
        }

        public static byte GetIndexOfMaster(Record root, string master, bool addIfMissing)
        {
            byte? finalMasterIndex = null;
            int currFieldIndex = -1;
            int nextMasterFieldIndex = 0;
            int currMasterIndex = 0;
            foreach (var field in root.Fields)
            {
                ++currFieldIndex;
                switch (field.FieldType)
                {
                    case _MAST:
                        break;

                    case _HEDR:
                    case _CNAM:
                    case _SNAM:
                        ++nextMasterFieldIndex;
                        goto default;

                    default:
                        continue;
                }

                string currMaster = StandardEncoding.GetString(field.Payload, 0, field.Payload.Length - 1);
                if (currMaster == master)
                {
                    finalMasterIndex = checked((byte)currMasterIndex);
                    break;
                }

                ++currMasterIndex;
                nextMasterFieldIndex = currFieldIndex + 2;
            }

            if (!finalMasterIndex.HasValue && addIfMissing)
            {
                byte[] encodedMaster = new byte[master.Length + 1];
                StandardEncoding.GetBytes(master, 0, master.Length, encodedMaster, 0);

                root.Fields.Insert(nextMasterFieldIndex, new Field { FieldType = DATA, Payload = new byte[1] });
                root.Fields.Insert(nextMasterFieldIndex, new Field { FieldType = MAST, Payload = encodedMaster });

                finalMasterIndex = checked((byte)currMasterIndex);
            }

            return finalMasterIndex ?? Byte.MaxValue;
        }

        public static byte GetMasterIndexForFormId(uint formId) => unchecked((byte)(formId >> 24));

        private static uint TranslateFormId(uint formId, Record fromRoot, Record toRoot) => TranslateFormId(formId, GetIndexOfMaster(toRoot, GetMaster(GetMasterIndexForFormId(formId), fromRoot), false));

        private static uint TranslateFormId(uint formId, byte newMasterIndex) => (formId & 0x00FFFFFF) | (unchecked((uint)newMasterIndex) << 24);

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
