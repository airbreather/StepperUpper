using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BethFile.Editor
{
    public sealed class Merged
    {
        private readonly List<Record> roots = new List<Record>();

        private readonly List<Dictionary<uint, Record>> allRecords = new List<Dictionary<uint, Record>>();

        public Merged()
        {
            this.Roots = this.roots.AsReadOnly();
        }

        public void AddRoot(Record root)
        {
            this.roots.Add(root);
            this.allRecords.Add(Doer.FindRecords(root).ToDictionary(rec => rec.Id));
        }

        public ReadOnlyCollection<Record> Roots { get; }

        public Record FindRecord(uint id)
        {
            foreach (var mapping in this.allRecords)
            {
                Record rec;
                if (mapping.TryGetValue(id, out rec))
                {
                    return rec;
                }
            }

            return null;
        }
    }
}
