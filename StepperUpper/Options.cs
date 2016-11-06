using CommandLine;
using CommandLine.Text;

namespace StepperUpper
{
    internal sealed class Options
    {
        [OptionArray('p', "packDefinitionFiles", Required = true, HelpText = "The .xml files that define the packs.")]
        public string[] PackDefinitionFilePaths { get; set; }

        [Option('d', "downloadFolder", Required = true, HelpText = "Folder containing downloaded mod files.")]
        public string DownloadDirectoryPath { get; set; }

        [Option('s', "steamFolder", Required = true, HelpText = "Folder containing \"steamapps\".")]
        public string SteamDirectoryPath { get; set; }

        [Option('o', "outputFolder", Required = true, HelpText = "Folder to create everything in.")]
        public string OutputDirectoryPath { get; set; }

        [Option('x', "scorch", HelpText = "Delete contents of output directory if non-empty (otherwise, fail).")]
        public bool Scorch { get; set; }

        [Option("javaBinFolder", HelpText = "Folder containing javaw.exe, if needed.")]
        public string JavaBinDirectoryPath { get; set; }

        [Option("allowLongPaths", HelpText = "Skip output folder length check.")]
        public bool SkipOutputDirectoryPathLengthCheck { get; set; }

        [Option("detectMaxPath", HelpText = "Detect max output path length for each pack.")]
        public bool DetectMaxPath { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() => HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
    }
}
