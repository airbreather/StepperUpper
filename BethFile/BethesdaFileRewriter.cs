using System;
using System.Collections.Generic;
using System.Linq;
using static BethFile.B4S;

namespace BethFile
{
    public class BethesdaFileRewriter
    {
        public enum RewriteAction
        {
            WriteOriginal,
            WriteReplaced,
            DoNotWrite
        }

        public BethesdaFile Rewrite(BethesdaFile file)
        {
            BethesdaRecord headerRecord = file.HeaderRecord;
            switch (this.ShouldWriteRecord(ref headerRecord))
            {
                case RewriteAction.WriteOriginal:
                    headerRecord = file.HeaderRecord;
                    break;
            }

            Stack<BethesdaGroupReader> activeReaders = new Stack<BethesdaGroupReader>();
            int newLength = file.TopGroups.Length;
            for (int i = 0; i < newLength; i++)
            {
                BethesdaGroup topGroup = file.TopGroups[i];
                switch (this.ShouldWriteGroupPre(ref topGroup))
                {
                    case RewriteAction.DoNotWrite:
                        if (i < --newLength)
                        {
                            Array.Copy(file.TopGroups, i + 1, file.TopGroups, i, newLength - i--);
                            continue;
                        }

                        break;

                    case RewriteAction.WriteReplaced:
                        file.TopGroups[i] = topGroup;
                        break;
                }

                activeReaders.Push(new BethesdaGroupReader(topGroup));
                while (activeReaders.Count != 0)
                {
                    BethesdaGroupReader reader = activeReaders.Pop();
                    switch (reader.Read())
                    {
                        case BethesdaGroupReaderState.Record:
                            activeReaders.Push(reader);
                            BethesdaRecord record = reader.CurrentRecord;
                            switch (this.ShouldWriteRecord(ref record))
                            {
                                case RewriteAction.WriteReplaced:
                                    var origRecord = reader.CurrentRecord;
                                    var origRaw = origRecord.RawData;
                                    var currRaw = record.RawData;
                                    uint totalDataSize = topGroup.RawData.Count;

                                    if (origRaw.Count != currRaw.Count)
                                    {
                                        UBuffer.BlockCopy(origRaw, origRaw.Count, origRaw, currRaw.Count, totalDataSize - origRaw.Offset - origRaw.Count);
                                        foreach (var activeReader in activeReaders)
                                        {
                                            var grp = activeReader.Group;
                                            grp.DataSize = checked((uint)(grp.DataSize + ((int)(currRaw.Count)) - (int)(origRaw.Count)));
                                        }
                                    }

                                    UBuffer.BlockCopy(currRaw, 0, origRaw, 0, currRaw.Count);

                                    break;

                                case RewriteAction.DoNotWrite:
                                    DeleteAndNotifyReaders(activeReaders, record.RawData);
                                    break;
                            }

                            break;

                        case BethesdaGroupReaderState.Subgroup:
                            activeReaders.Push(reader);
                            BethesdaGroup subgroup = reader.CurrentSubgroup;
                            switch (this.ShouldWriteGroupPre(ref subgroup))
                            {
                                case RewriteAction.WriteOriginal:
                                    activeReaders.Push(new BethesdaGroupReader(subgroup));
                                    break;

                                case RewriteAction.DoNotWrite:
                                    DeleteAndNotifyReaders(activeReaders, subgroup.RawData);
                                    break;

                                case RewriteAction.WriteReplaced:
                                    throw new NotImplementedException("I don't yet have a reason to implement this.");
                            }

                            break;

                        case BethesdaGroupReaderState.EndOfContent:
                            BethesdaGroup origGroup = reader.Group;
                            switch (this.ShouldWriteGroupPost(ref origGroup))
                            {
                                case RewriteAction.DoNotWrite:
                                    DeleteAndNotifyReaders(activeReaders, origGroup.RawData);
                                    if (origGroup.RawData.Offset == 0)
                                    {
                                        if (i < --newLength)
                                        {
                                            Array.Copy(file.TopGroups, i + 1, file.TopGroups, i, newLength - i--);
                                            continue;
                                        }
                                    }

                                    break;

                                case RewriteAction.WriteReplaced:
                                    throw new NotImplementedException("I don't yet have a reason to implement this.");
                            }

                            break;
                    }
                }
            }

            BethesdaGroup[] topGroups = new BethesdaGroup[newLength];
            Array.Copy(file.TopGroups, topGroups, newLength);
            return new BethesdaFile(headerRecord, topGroups);
        }

        private static void DeleteAndNotifyReaders(Stack<BethesdaGroupReader> activeReaders, UArraySegment<byte> segment)
        {
            uint nukedBytes = segment.Count;
            NukeSegment(segment);

            foreach (var activeReader in activeReaders)
            {
                var grp = activeReader.Group;
                grp.DataSize -= nukedBytes;
                activeReader.NotifyDeletion();
            }
        }

        protected virtual RewriteAction ShouldWriteRecord(ref BethesdaRecord record) => RewriteAction.WriteOriginal;

        protected virtual RewriteAction ShouldWriteGroupPre(ref BethesdaGroup group) => RewriteAction.WriteOriginal;

        protected virtual RewriteAction ShouldWriteGroupPost(ref BethesdaGroup group) => RewriteAction.WriteOriginal;

        private static void NukeSegment(UArraySegment<byte> segment)
        {
            byte[] arr = segment.Array;
            uint off = segment.Offset;
            uint cnt = segment.Count;
            UBuffer.BlockCopy(arr, off + cnt, arr, off, unchecked((uint)arr.LongLength) - off - cnt);
        }
    }
}
