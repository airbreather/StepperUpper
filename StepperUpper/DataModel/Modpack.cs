using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace StepperUpper
{
    internal sealed class Modpack
    {
        public string Name;

        public Game Game;

        public string PackVersion;

        public string FileVersion;

        public Version MinimumToolVersion;

        public int? LongestOutputPathLength;

        public ImmutableArray<CheckedFileGroup> CheckedFiles = ImmutableArray<CheckedFileGroup>.Empty;

        public SetupTask SetupTasksRoot = new SetupTask.Composite();

        public ImmutableArray<string> Requirements = ImmutableArray<string>.Empty;

        public Modpack() { }

        public Modpack(XElement modpackElement)
        {
            this.Name = modpackElement.Attribute("Name").Value;
            switch (modpackElement.Attribute("Game")?.Value)
            {
                case "Skyrim2011":
                    this.Game = Game.Skyrim2011;
                    break;
            }

            this.MinimumToolVersion = Version.Parse(modpackElement.Attribute("MinimumToolVersion").Value);
            this.PackVersion = modpackElement.Attribute("PackVersion").Value;
            this.FileVersion = modpackElement.Attribute("FileVersion").Value;
            this.Requirements = Program.Tokenize(modpackElement.Attribute("Requires")?.Value).ToImmutableArray();
            if (Int32.TryParse(modpackElement.Attribute("LongestOutputPathLength")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var longestOutputPathLength))
            {
                this.LongestOutputPathLength = longestOutputPathLength;
            }

            this.CheckedFiles = modpackElement
                .Element("Files")
                .Elements("Group")
                .Select(grp => new CheckedFileGroup(grp))
                .ToImmutableArray();

            this.SetupTasksRoot = new SetupTask.Composite(modpackElement.Element("Tasks"));
        }
    }
}
