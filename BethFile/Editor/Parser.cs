using System.Linq;

namespace BethFile.Editor
{
    public static class Parser
    {
        public static Record Parse(BethesdaFile file)
        {
            Record record = new Record(file.HeaderRecord);
            record.Subgroups.AddRange(file.TopGroups.Select(grp => new Group(grp)));
            return record;
        }
    }
}
