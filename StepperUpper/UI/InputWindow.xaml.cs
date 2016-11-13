using System;

namespace StepperUpper.UI
{
    public partial class InputWindow
    {
        public InputWindow()
            : this(new InputViewModel())
        {
        }

        public InputWindow(InputViewModel viewModel)
        {
            this.ViewModel = viewModel;
            this.InitializeComponent();
        }

        public InputViewModel ViewModel { get; }

        private void OnOKButtonClick(object sender, EventArgs e) => this.DialogResult = true;
    }
}
