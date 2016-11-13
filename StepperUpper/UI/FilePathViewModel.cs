using GalaSoft.MvvmLight;

namespace StepperUpper.UI
{
    public sealed class FilePathViewModel : ViewModelBase
    {
        private string value;

        public FilePathViewModel()
        {
        }

        public FilePathViewModel(string value)
        {
            this.value = value;
        }

        public string Value
        {
            get { return this.value; }
            set { this.Set(() => this.Value, ref this.value, value); }
        }
    }
}
