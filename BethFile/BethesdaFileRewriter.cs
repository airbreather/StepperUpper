using System;
using System.Collections.Generic;

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

        public unsafe BethesdaFile Rewrite(BethesdaFile file)
        {
            var headerRecord = file.HeaderRecord;
            bool rewrote = false;
            switch (this.ShouldWriteRecord(ref headerRecord))
            {
                case RewriteAction.WriteReplaced:
                    rewrote = true;
                    file = new BethesdaFile(headerRecord, file.TopGroups);
                    break;
            }

            uint thingCount = 0;
            Stack<BethesdaGroupReader> activeReaders = new Stack<BethesdaGroupReader>();
            int newLength = file.TopGroups.Length;
            for (int i = 0; i < newLength; i++)
            {
                BethesdaGroup topGroup = file.TopGroups[i];
                switch (this.ShouldWriteGroup(ref topGroup))
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

                ++thingCount;
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
                                case RewriteAction.WriteOriginal:
                                    ++thingCount;
                                    break;

                                case RewriteAction.WriteReplaced:
                                    // TODO: if smaller, shift later bytes back.  if bigger, but
                                    // room at end, then shift to that gap. otherwise, if bigger,
                                    // copy the whole dang thing and tell all the readers that they
                                    // should resume from the new copy.  that, or throw if no room
                                    // and force the reader to know how much of a gap to include.
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
                            switch (this.ShouldWriteGroup(ref subgroup))
                            {
                                case RewriteAction.WriteOriginal:
                                    ++thingCount;
                                    activeReaders.Push(new BethesdaGroupReader(subgroup));
                                    break;

                                case RewriteAction.DoNotWrite:
                                    DeleteAndNotifyReaders(activeReaders, subgroup.RawData);
                                    break;

                                case RewriteAction.WriteReplaced:
                                    // TODO: if smaller, shift later bytes back.  if bigger, but
                                    // room at end, then shift to that gap. otherwise, if bigger,
                                    // copy the whole dang thing and tell all the readers that they
                                    // should resume from the new copy.  that, or throw if no room
                                    // and force the reader to know how much of a gap to include.
                                    throw new NotImplementedException("going to sleep");
                            }

                            break;
                    }
                }
            }

            BethesdaGroup[] topGroups = new BethesdaGroup[newLength];
            Array.Copy(file.TopGroups, topGroups, newLength);
            if (!rewrote)
            {
                foreach (var field in file.HeaderRecord.Fields)
                {
                    if (field.Type != HEDR)
                    {
                        continue;
                    }

                    var payload = field.Payload;
                    fixed (byte* pbyte = &payload.Array[payload.Offset + 4])
                    {
                        *((uint*)pbyte) = thingCount;
                    }
                }
            }

            return new BethesdaFile(file.HeaderRecord, topGroups);
        }

        private static unsafe void DeleteAndNotifyReaders(Stack<BethesdaGroupReader> activeReaders, UArraySegment<byte> segment)
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

        protected virtual RewriteAction ShouldWriteGroup(ref BethesdaGroup group) => RewriteAction.WriteOriginal;

        private static void NukeSegment(UArraySegment<byte> segment)
        {
            byte[] arr = segment.Array;
            uint off = segment.Offset;
            uint cnt = segment.Count;
            UBuffer.BlockCopy(arr, off + cnt, arr, off, unchecked((uint)arr.LongLength) - off - cnt);
        }
    }
}
