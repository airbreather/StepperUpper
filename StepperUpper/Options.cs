using System;

using CommandLine;
using CommandLine.Text;

namespace StepperUpper
{
    internal sealed class Options
    {
        [OptionArray('p', "packDefinitionFiles", HelpText = "The .xml files that define the packs.")]
        public string[] PackDefinitionFilePaths { get; set; }

        [Option('d', "downloadFolder", HelpText = "Folder containing downloaded mod files.")]
        public string DownloadDirectoryPath { get; set; }

        [Option('s', "steamFolder", HelpText = "Folder containing \"steamapps\".")]
        public string SteamDirectoryPath { get; set; }

        [Option('o', "outputFolder", HelpText = "Folder to create everything in.")]
        public string OutputDirectoryPath { get; set; }

        [Option('x', "scorch", HelpText = "Delete contents of output directory if non-empty (otherwise, fail).")]
        public bool Scorch { get; set; }

        [Option('g', "graphicsPreset", HelpText = "The BethINI graphics preset to use (options: poor, low, medium, high, ultra) (default: ultra)")]
        public GraphicsPreset GraphicsPreset { get; set; } = GraphicsPreset.Ultra;

        [Option("javaBinFolder", HelpText = "Folder containing javaw.exe, if needed.")]
        public string JavaBinDirectoryPath { get; set; }

        [Option("allowLongPaths", HelpText = "Skip output folder length check.")]
        public bool SkipOutputDirectoryPathLengthCheck { get; set; }

        [Option("detectMaxPath", HelpText = "Detect max output path length for each pack.")]
        public bool DetectMaxPath { get; set; }

        [Option("noPauseAtEnd", HelpText = "Skip the wait for input at the end.")]
        public bool NoPauseAtEnd { get; set; }

        [Option("screenHeight", HelpText = "Vertical resolution for the game, in pixels (default: primary screen height).")]
        public uint ScreenHeight { get; set; } = Convert.ToUInt32(System.Windows.SystemParameters.PrimaryScreenHeight);

        [Option("screenWidth", HelpText = "Horizontal resolution for the game, in pixels (default: primary screen width).")]
        public uint ScreenWidth { get; set; } = Convert.ToUInt32(System.Windows.SystemParameters.PrimaryScreenWidth);

        [Option("fullScreenMode", HelpText = "Full-screen mode (options: windowed, fullScreen, windowedNoBorders, fullScreenNoBorders) (default: fullScreen)")]
        public FullScreenMode FullScreenMode { get; set; } = FullScreenMode.FullScreen;

        [ParserState]
        public IParserState LastParserState { get; set; }

        internal bool MightBeValid =>
            this.PackDefinitionFilePaths?.Length > 0 &&
            this.DownloadDirectoryPath?.Length > 0 &&
            this.OutputDirectoryPath?.Length > 0;

        [HelpOption]
        public string GetUsage() => HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
    }
}
