namespace BethFile.Editor
{
    public abstract class Visitor
    {
        public void Visit(Record root)
        {
            this.OnBegin();
            this.VisitRecord(root);
            this.OnEnd();
        }

        protected virtual void OnBegin() { }

        protected virtual void OnEnterRecord(Record rec) { }

        protected virtual void OnExitRecord(Record rec) { }

        protected virtual void OnEnterGroup(Group grp) { }

        protected virtual void OnExitGroup(Group grp) { }

        protected virtual void OnVisitField(Field fld) { }

        protected virtual void OnEnd() { }

        private void VisitRecord(Record rec)
        {
            if (!rec.IsDummy)
            {
                this.OnEnterRecord(rec);
            }

            foreach (var fld in rec.Fields)
            {
                this.OnVisitField(fld);
            }

            foreach (var grp in rec.Subgroups)
            {
                this.VisitGroup(grp);
            }

            if (!rec.IsDummy)
            {
                this.OnExitRecord(rec);
            }
        }

        private void VisitGroup(Group grp)
        {
            this.OnEnterGroup(grp);

            foreach (var rec in grp.Records)
            {
                this.VisitRecord(rec);
            }

            this.OnExitGroup(grp);
        }
    }
}
