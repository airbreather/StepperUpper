using CommandLine;
using CommandLine.Text;

namespace StepperUpper
{
    internal sealed class Options
    {
        [Option('p', "packDefinitionFile", Required = true, HelpText = "The .xml file that defines a pack.")]
        public string PackDefinitionFilePath { get; set; }

        [Option('d', "downloadFolder", Required = true, HelpText = "Folder containing downloaded mod files.")]
        public string DownloadDirectoryPath { get; set; }

        [Option('s', "steamFolder", Required = true, HelpText = "Folder containing \"steamapps\".")]
        public string SteamDirectoryPath { get; set; }

        [Option('o', "outputFolder", Required = true, HelpText = "Folder to create everything in.")]
        public string OutputDirectoryPath { get; set; }

        [Option('x', "scorch", DefaultValue = false, HelpText = "True to erase existing files.")]
        public bool Scorch { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() => HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
    }
}
