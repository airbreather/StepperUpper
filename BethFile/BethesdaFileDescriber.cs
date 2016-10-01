using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AirBreather;

namespace BethFile
{
    public sealed class BethesdaFileDescriber : BethesdaFileVisitor
    {
        private readonly TextWriter writer;

        private readonly Stack<BethesdaGroup> activeGroups = new Stack<BethesdaGroup>();

        public BethesdaFileDescriber(TextWriter writer)
        {
            this.writer = writer.ValidateNotNull(nameof(writer));
        }

        protected override void OnEnterGroup(BethesdaGroup group)
        {
            this.activeGroups.Push(group);
            this.writer.WriteLine(String.Join(": ", this.activeGroups.Reverse().Select(grp => $"In group {grp}")) + ".");
            base.OnEnterGroup(group);
        }

        protected override void OnExitGroup(BethesdaGroup group)
        {
            this.activeGroups.Pop();
            base.OnExitGroup(group);
        }

        protected override void OnRecord(BethesdaRecord record)
        {
            byte[] rawData = record.RawData.ToArray();
            this.writer.WriteLine(String.Join(": ", this.activeGroups.Reverse().Select(grp => $"In group {grp}")) + $": [{record.Type}:{record.Id:X8}] (RawData: {rawData.ByteArrayToHexString()})");
            base.OnRecord(record);
        }
    }
}
