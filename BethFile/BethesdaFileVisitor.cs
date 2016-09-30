namespace BethFile
{
    public class BethesdaFileVisitor
    {
        public void Visit(BethesdaFile file)
        {
            this.OnFile(file);
            this.OnRecord(file.HeaderRecord);
            foreach (var group in file.TopGroups)
            {
                this.VisitGroupCore(group);
            }
        }

        protected virtual void OnFile(BethesdaFile file) { }

        protected virtual void OnRecord(BethesdaRecord record) { }

        protected virtual void OnEnterGroup(BethesdaGroup group) { }

        protected virtual void OnExitGroup(BethesdaGroup group) { }

        private void VisitGroupCore(BethesdaGroup group)
        {
            this.OnEnterGroup(group);
            BethesdaGroupReader reader = new BethesdaGroupReader(group);
            BethesdaGroupReaderState state;
            while ((state = reader.Read()) != BethesdaGroupReaderState.EndOfContent)
            {
                switch (state)
                {
                    case BethesdaGroupReaderState.Record:
                        this.OnRecord(reader.CurrentRecord);
                        break;

                    case BethesdaGroupReaderState.Subgroup:
                        this.VisitGroupCore(reader.CurrentSubgroup);
                        break;
                }
            }

            this.OnExitGroup(group);
        }
    }
}
