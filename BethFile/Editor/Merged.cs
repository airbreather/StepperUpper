using System.Collections.Generic;
using System.Threading;

namespace BethFile.Editor
{
    public sealed class Merged
    {
        private readonly Dictionary<uint, Record>[] allRecords;

        public Merged(int masterCount)
        {
            this.allRecords = new Dictionary<uint, Record>[masterCount];
        }

        public bool IsFinalized
        {
            get
            {
                for (int i = 0; i < this.allRecords.Length; i++)
                {
                    if ((this.allRecords[i] ?? Volatile.Read(ref this.allRecords[i])) == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void SetRoot(int index, Dictionary<uint, Record> map)
        {
            Volatile.Write(ref this.allRecords[index], map);
        }

        public Record FindRecord(uint id)
        {
            for (int i = 0; i < this.allRecords.Length; i++)
            {
                Record rec;
                if ((this.allRecords[i] ?? Volatile.Read(ref this.allRecords[i])).TryGetValue(id, out rec))
                {
                    return rec;
                }
            }

            return null;
        }
    }
}
