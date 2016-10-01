using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AirBreather;

using static BethFile.B4S;

namespace BethFile
{
    public sealed class BethesdaFileCleaner : BethesdaFileRewriter
    {
        private readonly List<bool> safeList = new List<bool>();

        private readonly HashSet<uint> itms;

        private readonly HashSet<uint> udrs;

        private readonly uint[] onams;

        private readonly uint thingCount;

        private readonly Dictionary<uint, BethesdaRecord> udrMapping = new Dictionary<uint, BethesdaRecord>();

        public BethesdaFileCleaner(IEnumerable<BethesdaFile> bases, IEnumerable<uint> itms, IEnumerable<uint> udrs, IEnumerable<uint> onams, uint thingCount)
        {
            this.itms = itms.ToHashSet();
            this.udrs = udrs.ToHashSet();
            this.onams = onams.ToArray();
            this.thingCount = thingCount;
            foreach (var record in bases.SelectMany(rec => Helpers.ExtractRecords(rec, this.udrs)))
            {
                // overwrite anything already there.
                udrMapping[record.Id] = record;
            }
        }

        protected override RewriteAction ShouldWriteGroupPre(ref BethesdaGroup group)
        {
            this.safeList.Add(false);
            return base.ShouldWriteGroupPre(ref group);
        }

        protected override RewriteAction ShouldWriteRecord(ref BethesdaRecord record)
        {
            if (this.itms.Contains(record.Id))
            {
                return RewriteAction.DoNotWrite;
            }

            for (int i = 0; i < this.safeList.Count; i++)
            {
                this.safeList[i] = true;
            }

            switch (record.Type)
            {
                case _TES4:
                    List<BethesdaField> fields = new List<BethesdaField>();
                    foreach (var field in record.Fields)
                    {
                        BethesdaField newField = field;
                        switch (newField.Type)
                        {
                            case _HEDR:
                                UBitConverter.SetUInt32(field.PayloadStart + 4, this.thingCount);
                                break;

                            case _ONAM:
                                byte[] newFieldRawData = new byte[this.onams.Length * 4 + 6];
                                UArrayPosition<byte> pos = new UArrayPosition<byte>(newFieldRawData);

                                UBitConverter.SetUInt32(pos, ONAM);
                                pos += 4;

                                UBitConverter.SetUInt16(pos, checked((ushort)(this.onams.Length * 4)));
                                pos += 2;

                                foreach (uint onam in this.onams)
                                {
                                    UBitConverter.SetUInt32(pos, onam);
                                    pos += 4;
                                }

                                newField = new BethesdaField(new UArraySegment<byte>(newFieldRawData));
                                break;
                        }

                        fields.Add(newField);
                    }

                    record = BethesdaEditor.RewriteRecord(record, fields);
                    return RewriteAction.WriteReplaced;

                case _WRLD:
                    // OFST must be removed if present.
                    bool foundOfst = false;
                    List<BethesdaField> newFields = new List<BethesdaField>();
                    foreach (BethesdaField field in record.Fields)
                    {
                        if (field.Type != OFST)
                        {
                            newFields.Add(field);
                        }
                        else
                        {
                            foundOfst = true;
                        }
                    }

                    if (!foundOfst)
                    {
                        return RewriteAction.WriteOriginal;
                    }

                    record = BethesdaEditor.RewriteRecord(record, newFields);
                    return RewriteAction.WriteReplaced;

                case _DOBJ:
                    record = BethesdaEditor.OptimizeDOBJ(record);
                    return RewriteAction.WriteReplaced;
            }

            BethesdaRecord orig;
            if (!this.udrMapping.TryGetValue(record.Id, out orig))
            {
                return RewriteAction.WriteOriginal;
            }

            record = BethesdaEditor.UndeleteAndDisableReference(record, orig);
            return RewriteAction.WriteReplaced;
        }

        protected override RewriteAction ShouldWriteGroupPost(ref BethesdaGroup group)
        {
            bool safe = this.safeList[this.safeList.Count - 1];
            this.safeList.RemoveAt(this.safeList.Count - 1);
            return safe
                ? base.ShouldWriteGroupPost(ref group)
                : RewriteAction.DoNotWrite;
        }
    }
}
