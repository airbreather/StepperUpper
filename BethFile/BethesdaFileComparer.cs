using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using AirBreather;
using AirBreather.Collections;

namespace BethFile
{
    public static class BethesdaFileComparer
    {
        private static readonly CallbackComparer<UArraySegment<byte>> PayloadComparer = new CallbackComparer<UArraySegment<byte>>(Compare);

        private static readonly List<string> Indents = new List<string>();

        public static string Compare(BethesdaFile first, BethesdaFile second)
        {
            StringBuilder sb = new StringBuilder();
            Compare(first.HeaderRecord, second.HeaderRecord, sb, 0);

            List<BethesdaGroup> firstGroups = first.TopGroups.ToList();
            List<BethesdaGroup> secondGroups = second.TopGroups.ToList();

            firstGroups.Sort(MetaCompare);
            secondGroups.Sort(MetaCompare);

            int indentLevel = 0;
            string indent = Indent(indentLevel++);

            int i = 0, j = 0;
            while (i < firstGroups.Count || j < secondGroups.Count)
            {
                if (j == secondGroups.Count || MetaCompare(firstGroups[i], secondGroups[j]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyL: " + firstGroups[i++]);
                }
                else if (i == firstGroups.Count || MetaCompare(secondGroups[j], firstGroups[i]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyR: " + secondGroups[j++]);
                }
                else
                {
                    Compare(firstGroups[i++], secondGroups[j++], sb, indentLevel);
                }
            }

            return sb.ToString();
        }

        private static void Compare(BethesdaGroup first, BethesdaGroup second, StringBuilder sb, int indentLevel)
        {
            if (Compare(first.RawData, second.RawData) == 0)
            {
                return;
            }

            string indent = Indent(indentLevel++);
            sb.Append(indent).AppendLine(first.ToString());

            CompareHeaders(new UArraySegment<byte>(first.Start, 24), new UArraySegment<byte>(second.Start, 24), sb, indentLevel);

            List<BethesdaRecord> firstRecords = new List<BethesdaRecord>();
            List<BethesdaGroup> firstGroups = new List<BethesdaGroup>();

            BethesdaGroupReader reader = new BethesdaGroupReader(first);
            BethesdaGroupReaderState state;
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        firstRecords.Add(reader.CurrentRecord);
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        firstGroups.Add(reader.CurrentSubgroup);
                        break;
                }
            }

            firstRecords.Sort(MetaCompare);
            firstGroups.Sort(MetaCompare);

            List<BethesdaRecord> secondRecords = new List<BethesdaRecord>();
            List<BethesdaGroup> secondGroups = new List<BethesdaGroup>();

            reader = new BethesdaGroupReader(second);
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        secondRecords.Add(reader.CurrentRecord);
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        secondGroups.Add(reader.CurrentSubgroup);
                        break;
                }
            }

            secondRecords.Sort(MetaCompare);
            secondGroups.Sort(MetaCompare);

            int i = 0, j = 0;
            while (i < firstRecords.Count || j < secondRecords.Count)
            {
                if (j == secondRecords.Count || MetaCompare(firstRecords[i], secondRecords[j]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyL: " + firstRecords[i++]);
                }
                else if (i == firstRecords.Count || MetaCompare(secondRecords[j], firstRecords[i]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyR: " + secondRecords[j++]);
                }
                else
                {
                    Compare(firstRecords[i++], secondRecords[j++], sb, indentLevel);
                }
            }

            i = 0;
            j = 0;
            while (i < firstGroups.Count || j < secondGroups.Count)
            {
                if (j == secondGroups.Count || MetaCompare(firstGroups[i], secondGroups[j]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyL: " + firstGroups[i++]);
                }
                else if (i == firstGroups.Count || MetaCompare(secondGroups[j], firstGroups[i]) < 0)
                {
                    sb.Append(indent).AppendLine("OnlyR: " + secondGroups[j++]);
                }
                else
                {
                    Compare(firstGroups[i++], secondGroups[j++], sb, indentLevel);
                }
            }
        }

        private static void Compare(BethesdaRecord first, BethesdaRecord second, StringBuilder sb, int indentLevel)
        {
            if (Compare(first.RawData, second.RawData) == 0)
            {
                return;
            }

            sb.Append(Indent(indentLevel++)).AppendLine(first.ToString());

            string indent = Indent(indentLevel);

            CompareHeaders(new UArraySegment<byte>(first.Start, 24), new UArraySegment<byte>(second.Start, 24), sb, indentLevel);

            // stable sort is probably important enough to be worth a speed cost.
            List<BethesdaField> firstFields = first.Fields.OrderBy(f => f.FieldType).ThenBy(f => f.Payload, PayloadComparer).ToList();
            List<BethesdaField> secondFields = second.Fields.OrderBy(f => f.FieldType).ThenBy(f => f.Payload, PayloadComparer).ToList();

            int i = 0, j = 0;
            while (i < firstFields.Count || j < secondFields.Count)
            {
                if (j == secondFields.Count || (i != firstFields.Count && firstFields[i].FieldType < secondFields[j].FieldType))
                {
                    sb.Append(indent).AppendLine("OnlyL: " + firstFields[i++]);
                }
                else if (i == firstFields.Count || (j != secondFields.Count && secondFields[j].FieldType < firstFields[i].FieldType))
                {
                    sb.Append(indent).AppendLine("OnlyR: " + secondFields[j++]);
                }
                else
                {
                    Compare(firstFields[i++], secondFields[j++], sb, indentLevel);
                }
            }
        }

        private static void Compare(BethesdaField first, BethesdaField second, StringBuilder sb, int indentLevel)
        {
            if (Compare(first.Payload, second.Payload) == 0)
            {
                return;
            }

            string indent = Indent(indentLevel);
            sb.Append(indent).AppendLine(first.ToString())
              .Append(indent).AppendLine("vs.")
              .Append(indent).AppendLine(second.ToString());
        }

        private static void CompareHeaders(UArraySegment<byte> firstHeader, UArraySegment<byte> secondHeader, StringBuilder sb, int indentLevel)
        {
            if (Compare(firstHeader, secondHeader) == 0)
            {
                return;
            }

            string indent = Indent(indentLevel++);
            sb.Append(indent).AppendLine("Headers Differ!")
              .Append(indent).AppendLine(firstHeader.ToArray().ByteArrayToHexString())
              .Append(indent).AppendLine("vs.")
              .Append(indent).AppendLine(secondHeader.ToArray().ByteArrayToHexString());
        }

        private static unsafe int Compare(UArraySegment<byte> first, UArraySegment<byte> second)
        {
            int defaulter = 0;
            uint minCnt = first.Count;
            if (first.Count < second.Count)
            {
                defaulter = -1;
            }
            else if (second.Count < minCnt)
            {
                defaulter = 1;
                minCnt = second.Count;
            }

            int cmp;
            fixed (void* ptr1 = &first.Array[first.Offset])
            fixed (void* ptr2 = &second.Array[second.Offset])
            {
                cmp = memcmp(ptr1, ptr2, new UIntPtr(minCnt));
            }

            return cmp == 0
                ? defaulter
                : cmp;
        }

        private static string Indent(int cnt)
        {
            for (int i = Indents.Count; i <= cnt; i++)
            {
                Indents.Add(new string('\t', i));
            }

            return Indents[cnt];
        }

        private static int MetaCompare(BethesdaRecord r1, BethesdaRecord r2)
        {
            int cmp = r1.RecordType.CompareTo(r2.RecordType);
            return cmp == 0
                ? r1.Id.CompareTo(r2.Id)
                : cmp;
        }

        private static int MetaCompare(BethesdaGroup g1, BethesdaGroup g2)
        {
            int cmp = g1.GroupType.CompareTo(g2.GroupType);
            return cmp == 0
                ? g1.Label.CompareTo(g2.Label)
                : cmp;
        }

        [DllImport("msvcrt.dll")]
        private static extern unsafe int memcmp(void* ptr1, void* ptr2, UIntPtr count);
    }
}
