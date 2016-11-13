using GalaSoft.MvvmLight.Messaging;

namespace StepperUpper.UI
{
    public sealed class SelectPackFileMessage : MessageBase
    {
        public string SelectedFilePath { get; set; }

        public string FileExtension { get; set; }

        public string FileExtensionFilter { get; set; }

        public string DialogTitle { get; set; }
    }

    public sealed class SelectFolderMessage : MessageBase
    {
        public string SelectedFolderPath { get; set; }

        public string DialogTitle { get; set; }
    }
}
