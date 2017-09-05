using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace StepperUpper.UI
{
    public sealed class InputViewModel : ViewModelBase
    {
        public InputViewModel()
        {
            this.PackFiles = new ReadOnlyObservableCollection<FilePathViewModel>(this.packFiles);

            this.AddPackFileCommand = new RelayCommand(this.AddPackFile);
            this.DeletePackFileCommand = new RelayCommand<FilePathViewModel>(this.DeletePackFile);

            this.SelectDownloadFolderCommand = new RelayCommand(() => this.SelectFolder("Select Download Folder", val => this.DownloadFolder = val));
            this.SelectOutputFolderCommand = new RelayCommand(() => this.SelectFolder("Select Output Folder", val => this.OutputFolder = val));
            this.SelectSteamFolderCommand = new RelayCommand(() => this.SelectFolder("Select Steam Folder", val => this.SteamFolder = val));
            this.SelectJavaBinFolderCommand = new RelayCommand(() => this.SelectFolder("Select Java Bin Folder", val => this.JavaBinFolder = val));
        }

        internal InputViewModel(Options options)
            : this()
        {
            this.DownloadFolder = options.DownloadDirectoryPath;
            this.OutputFolder = options.OutputDirectoryPath;
            this.JavaBinFolder = options.JavaBinDirectoryPath;
            this.SteamFolder = options.SteamDirectoryPath;
            this.SelectedFullScreenMode = options.FullScreenMode;
            this.ScreenWidth = options.ScreenWidth;
            this.ScreenHeight = options.ScreenHeight;
            this.SelectedGraphicsPreset = options.GraphicsPreset;
            foreach (var packFile in options.PackDefinitionFilePaths ?? Enumerable.Empty<string>())
            {
                this.packFiles.Add(new FilePathViewModel(packFile));
            }
        }

        private readonly ObservableCollection<FilePathViewModel> packFiles = new ObservableCollection<FilePathViewModel>();
        public ReadOnlyObservableCollection<FilePathViewModel> PackFiles { get; }

        public ImmutableArray<FullScreenModeContainer> AvailableFullScreenModes { get; } = ImmutableArray.Create<FullScreenModeContainer>(FullScreenMode.Windowed, FullScreenMode.FullScreen, FullScreenMode.WindowedNoBorders, FullScreenMode.FullScreenNoBorders);
        public ImmutableArray<GraphicsPreset> AvailableGraphicsPresets { get; } = ImmutableArray.Create(GraphicsPreset.Poor, GraphicsPreset.Low, GraphicsPreset.Medium, GraphicsPreset.High, GraphicsPreset.Ultra);

        private string downloadFolder;
        public string DownloadFolder
        {
            get => this.downloadFolder;
            set => this.Set(() => this.DownloadFolder, ref this.downloadFolder, value);
        }

        private string outputFolder;
        public string OutputFolder
        {
            get => this.outputFolder;
            set => this.Set(() => this.OutputFolder, ref this.outputFolder, value);
        }

        private string steamFolder;
        public string SteamFolder
        {
            get => this.steamFolder;
            set => this.Set(() => this.SteamFolder, ref this.steamFolder, value);
        }

        private string javaBinFolder;
        public string JavaBinFolder
        {
            get => this.javaBinFolder;
            set => this.Set(() => this.JavaBinFolder, ref this.javaBinFolder, value);
        }

        private uint screenHeight;
        public uint ScreenHeight
        {
            get => this.screenHeight;
            set => this.Set(() => this.ScreenHeight, ref this.screenHeight, value);
        }

        private uint screenWidth;
        public uint ScreenWidth
        {
            get => this.screenWidth;
            set => this.Set(() => this.ScreenWidth, ref this.screenWidth, value);
        }

        private FullScreenModeContainer selectedFullScreenMode;
        public FullScreenModeContainer SelectedFullScreenMode
        {
            get => this.selectedFullScreenMode;
            set => this.Set(() => this.SelectedFullScreenMode, ref this.selectedFullScreenMode, value);
        }

        private GraphicsPreset selectedGraphicsPreset;
        public GraphicsPreset SelectedGraphicsPreset
        {
            get => this.selectedGraphicsPreset;
            set => this.Set(() => this.SelectedGraphicsPreset, ref this.selectedGraphicsPreset, value);
        }

        public RelayCommand AddPackFileCommand { get; }

        public RelayCommand SelectDownloadFolderCommand { get; }

        public RelayCommand SelectOutputFolderCommand { get; }

        public RelayCommand SelectSteamFolderCommand { get; }

        public RelayCommand SelectJavaBinFolderCommand { get; }

        public RelayCommand<FilePathViewModel> DeletePackFileCommand { get; }

        private void AddPackFile()
        {
            SelectPackFileMessage msg = new SelectPackFileMessage
            {
                DialogTitle = "Select Modpack File",
                FileExtension = "xml",
                FileExtensionFilter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            };

            this.MessengerInstance.Send(msg);
            if (msg.SelectedFilePath != null)
            {
                this.packFiles.Add(new FilePathViewModel(msg.SelectedFilePath));
            }
        }

        private void DeletePackFile(FilePathViewModel packFile) => this.packFiles.Remove(packFile);

        private void SelectFolder(string dialogTitle, Action<string> setter)
        {
            SelectFolderMessage msg = new SelectFolderMessage
            {
                DialogTitle = dialogTitle
            };

            this.MessengerInstance.Send(msg);
            if (msg.SelectedFolderPath != null)
            {
                setter(msg.SelectedFolderPath);
            }
        }

        public struct FullScreenModeContainer
        {
            internal FullScreenModeContainer(FullScreenMode value) => this.Value = value;

            internal FullScreenMode Value { get; }

            public static implicit operator FullScreenModeContainer(FullScreenMode value) => new FullScreenModeContainer(value);
            public static implicit operator FullScreenMode(FullScreenModeContainer value) => value.Value;

            public override string ToString()
            {
                switch (this.Value)
                {
                    case FullScreenMode.Windowed:
                        return "Windowed";

                    case FullScreenMode.FullScreen:
                        return "Full-screen";

                    case FullScreenMode.WindowedNoBorders:
                        return "Windowed (no borders)";

                    case FullScreenMode.FullScreenNoBorders:
                        return "Full-screen (no borders)";

                    default:
                        throw new NotSupportedException("Changed AvailableFullScreenModes without changing this switch-case block.");
                }
            }
        }
    }
}
