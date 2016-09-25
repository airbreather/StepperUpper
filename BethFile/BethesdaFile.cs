namespace BethFile
{
    public sealed class BethesdaFile
    {
        public BethesdaFile(BethesdaRecord headerRecord, BethesdaGroup[] topGroups)
        {
            this.HeaderRecord = headerRecord;
            this.TopGroups = topGroups;
        }

        public BethesdaRecord HeaderRecord { get; }

        public BethesdaGroup[] TopGroups { get; }
    }
}
