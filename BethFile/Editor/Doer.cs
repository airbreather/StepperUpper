using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // used for deleting the actual things
        public static void Delete(Record root, IEnumerable<ObjectIdentifier> toDeleteList)
        {
            var payload = root.Fields.Single(x => x.Type == HEDR).Payload;
            uint cnt = UBitConverter.ToUInt32(payload, 4);

            // TODO: we don't technically need to sort the whole thing.  just
            // enough to make sure that deleting multiple fields within a record
            // with the same type don't screw up.
            foreach (var id in toDeleteList.OrderByDescending(x => x))
            {
                object[] res = id.Resolve(root);
                object parent = res[res.Length - 2];
                object toDelete = res[res.Length - 1];
                Record parentRecord = parent as Record;
                if (parentRecord == null)
                {
                    goto parentNotRecord;
                }

                Field delField = toDelete as Field;
                if (delField == null)
                {
                    goto childNotField;
                }

                if (!parentRecord.Fields.Remove(delField))
                {
                    throw new Exception("Could not delete...");
                }

                parentRecord.OriginalCompressedFieldData = null;

                continue;

                childNotField:
                if (!parentRecord.Subgroups.Remove((Group)toDelete))
                {
                    throw new Exception("Could not delete...");
                }

                cnt -= CountItems((Group)toDelete);
                continue;

                parentNotRecord:
                Group parentGroup = (Group)parent;
                Group grp = toDelete as Group;
                if (grp == null)
                {
                    goto childNotSubgroup;
                }

                foreach (var dummyRecord in parentGroup.Records)
                {
                    if (dummyRecord.Subgroups.Remove(grp))
                    {
                        goto deletedSuccessfully;
                    }
                }

                throw new Exception("Could not delete...");

                deletedSuccessfully:
                cnt -= CountItems(grp);

                continue;

                childNotSubgroup:
                if (!parentGroup.Records.Remove((Record)toDelete))
                {
                    throw new Exception("Could not delete...");
                }

                cnt -= CountItems((Record)toDelete);
            }

            UBitConverter.SetUInt32(payload, 4, cnt);
        }

        // used for calculating the set of things to delete.
        public static ObjectIdentifier[] GetItemsToDelete(Record oldRoot, Record newRoot)
        {
            HashSet<ObjectIdentifier> oldData = null;
            List<ObjectIdentifier> newData = null;
            Parallel.Invoke(
                () => oldData = GetObjects(oldRoot).ToHashSet(),
                () => newData = GetObjects(newRoot));

            oldData.ExceptWith(newData);
            foreach (var id in oldData.ToList())
            {
                for (var par = id.Pop(); !par.IsDefault; par = par.Pop())
                {
                    if (oldData.Contains(par))
                    {
                        oldData.Remove(id);
                        break;
                    }
                }
            }

            ObjectIdentifier[] results = new ObjectIdentifier[oldData.Count];
            oldData.CopyTo(results);
            return results;
        }

        private static List<ObjectIdentifier> GetObjects(Record rec)
        {
            var vis = new ObjectVisitor();
            vis.Visit(rec);
            return vis.Data;
        }

        public static Record Sort(Record root)
        {
            root = new Record(root);
            SortCore(root);
            return root;
        }

        private static void SortCore(Record rec)
        {
            rec.Subgroups.ForEach(SortCore);
            rec.Subgroups.Sort(GroupComparer.Instance);
            rec.Fields.Sort(FieldComparer.Instance);
            rec.OriginalCompressedFieldData = null;
        }

        private static void SortCore(Group grp)
        {
            grp.Records.ForEach(SortCore);
            grp.Records.Sort(RecordComparer.Instance);
        }

        public static void UDR(Record root, Record[] origRoot, HashSet<uint> udrs)
        {
            bool did;
            do
            {
                did = UDRCore(root, root, origRoot, udrs);
            } while (did);
        }

        private static bool UDRCore(Record rec, Record currRoot, Record[] origRoot, HashSet<uint> udrs)
        {
            bool result = false;
            if (!rec.IsDummy && udrs.Contains(rec.Id) && rec.Flags.HasFlag(BethesdaRecordFlags.Deleted))
            {
                var origRecord = origRoot.Select(r => ResolveOrig(r, rec.Id)).First(x => x != null);
                Record orig = (Record)origRecord.Item2[origRecord.Item2.Length - 1];

                rec.CopyHeadersFrom(orig);
                rec.Revision = 0;
                rec.Flags |= BethesdaRecordFlags.InitiallyDisabled;

                rec.Fields.Clear();
                rec.Fields.AddRange(orig.Fields.Select(f => new Field(f)));

                if (rec.RecordType == ACHR || rec.RecordType == ACRE || orig.Flags.HasFlag(BethesdaRecordFlags.PersistentReference))
                {
                    rec.Flags |= BethesdaRecordFlags.PersistentReference;
                    object[] reRes = origRecord.Item1[origRecord.Item1.Length - 2].Resolve(currRoot);
                    Group parent = (Group)reRes[reRes.Length - 2];
                    if (parent.GroupType != BethesdaGroupType.CellPersistentChildren)
                    {
                        parent.Records.Remove(rec);
                        reRes.OfType<Record>()
                             .First(r => r.RecordType == B4S.CELL)
                             .Subgroups
                             .Single(grp => grp.GroupType == BethesdaGroupType.CellChildren)
                             .Records
                             .SelectMany(r => r.Subgroups)
                             .Single(g => g.GroupType == BethesdaGroupType.CellPersistentChildren)
                             .Records
                             .Add(rec);

                        result = true;
                    }
                }

                if (!rec.Flags.HasFlag(BethesdaRecordFlags.PersistentReference))
                {
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
                }

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
                rec.OriginalCompressedFieldData = null;
            }

            foreach (var grp in rec.Subgroups)
            {
                foreach (var rec2 in grp.Records)
                {
                    if (UDRCore(rec2, currRoot, origRoot, udrs))
                    {
                        return true;
                    }
                }
            }

            return result;
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

        private static readonly Dictionary<Record, List<ObjectIdentifier>> VisitCache = new Dictionary<Record, List<ObjectIdentifier>>();

        public static Tuple<ObjectIdentifier[], object[]> ResolveOrig(Record origRoot, uint recordId)
        {
            // TODO: talk about suboptimal...
            List<ObjectIdentifier> visitedData;
            if (!VisitCache.TryGetValue(origRoot, out visitedData))
            {
                var vis = new ObjectVisitor();
                vis.Visit(origRoot);
                visitedData = VisitCache[origRoot] = vis.Data;
            }

            foreach (var id in visitedData)
            {
                if (id.Data[id.Data.Length - 1] != recordId)
                {
                    continue;
                }

                object[] res = id.Resolve(origRoot);
                Record c = res[res.Length - 1] as Record;
                if (c?.Id != recordId)
                {
                    continue;
                }

                ObjectIdentifier[] result = new ObjectIdentifier[res.Length];
                ObjectIdentifier curr = ObjectIdentifier.Root;
                for (int i = 0; i < result.Length; i++)
                {
                    Group grp = res[i] as Group;
                    curr = result[i] = grp == null
                        ? curr.Push((Record)res[i])
                        : curr.Push(grp);
                }

                return Tuple.Create(result, res);
            }

            return null;
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
