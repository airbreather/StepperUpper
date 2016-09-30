namespace BethFile
{
    public class BethesdaFileVisitor
    {
        public void Visit(BethesdaFile file)
        {
            this.VisitRecord(file.HeaderRecord);
            foreach (var group in file.TopGroups)
            {
                this.VisitGroupCore(group);
            }
        }

        protected virtual void VisitFile(BethesdaFile file) { }

        protected virtual void VisitRecord(BethesdaRecord record) { }

        protected virtual void VisitGroup(BethesdaGroup group) { }

        private void VisitGroupCore(BethesdaGroup group)
        {
            this.VisitGroup(group);
            BethesdaGroupReader reader = new BethesdaGroupReader(group);
            BethesdaGroupReaderState state;
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        this.VisitRecord(reader.CurrentRecord);
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        this.VisitGroupCore(reader.CurrentSubgroup);
                        break;
                }
            }
        }
    }
}
