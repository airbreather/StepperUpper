using System;
using System.Collections.ObjectModel;
using System.Linq;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace StepperUpper.UI
{
    public sealed class InputViewModel : ViewModelBase
    {
        private readonly ObservableCollection<FilePathViewModel> packFiles = new ObservableCollection<FilePathViewModel>();

        private string downloadFolder;

        private string outputFolder;

        private string steamFolder;

        private string javaBinFolder;

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
            foreach (var packFile in options.PackDefinitionFilePaths ?? Enumerable.Empty<string>())
            {
                this.packFiles.Add(new FilePathViewModel(packFile));
            }
        }

        public ReadOnlyObservableCollection<FilePathViewModel> PackFiles { get; }

        public string DownloadFolder
        {
            get { return this.downloadFolder; }
            set { this.Set(() => this.DownloadFolder, ref this.downloadFolder, value); }
        }

        public string OutputFolder
        {
            get { return this.outputFolder; }
            set { this.Set(() => this.OutputFolder, ref this.outputFolder, value); }
        }

        public string SteamFolder
        {
            get { return this.steamFolder; }
            set { this.Set(() => this.SteamFolder, ref this.steamFolder, value); }
        }

        public string JavaBinFolder
        {
            get { return this.javaBinFolder; }
            set { this.Set(() => this.JavaBinFolder, ref this.javaBinFolder, value); }
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
    }
}
