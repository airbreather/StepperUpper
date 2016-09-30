using System.Collections.Generic;

using BethFile;

using static BethFile.B4S;

namespace StepperUpper
{
    internal sealed class RefMapBuilder : BethesdaFileVisitor
    {
        protected override void OnRecord(BethesdaRecord record)
        {
            if (record.Type == REFR)
            {
                this.References.Add(record.Id, record);
            }

            base.OnRecord(record);
        }

        internal Dictionary<uint, BethesdaRecord> References { get; } = new Dictionary<uint, BethesdaRecord>();
    }
}
