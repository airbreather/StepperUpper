using System.Collections.Generic;

namespace BethFile.Editor
{
    public sealed class ObjectVisitor : Visitor
    {
        private ObjectIdentifier curr;

        private readonly Dictionary<B4S, uint> maxForCurrRec = new Dictionary<B4S, uint>();

        public List<ObjectIdentifier> Data { get; } = new List<ObjectIdentifier>();

        protected override void OnBegin() =>
            this.curr = ObjectIdentifier.Root;

        protected override void OnEnterGroup(Group grp) =>
            this.Data.Add(this.curr = this.curr.Push(grp));

        protected override void OnEnterRecord(Record rec)
        {
            this.maxForCurrRec.Clear();
            this.Data.Add(this.curr = this.curr.Push(rec));
        }

        protected override void OnVisitField(Field fld)
        {
            uint rpt;
            this.maxForCurrRec.TryGetValue(fld.Type, out rpt);
            this.maxForCurrRec[fld.Type] = ++rpt;

            this.Data.Add(this.curr.Push(fld, rpt));
        }

        protected override void OnExitGroup(Group grp) =>
            this.curr = this.curr.Pop();

        protected override void OnExitRecord(Record rec) =>
            this.curr = this.curr.Pop();
    }
}
