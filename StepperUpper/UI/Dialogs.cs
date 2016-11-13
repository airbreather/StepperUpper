using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;

using Ookii.Dialogs.Wpf;

namespace StepperUpper.UI
{
    internal static class Dialogs
    {
        internal static async Task<bool> FillOptionsAsync(Options options)
        {
            var wpfDispatcherBox = new TaskCompletionSource<Dispatcher>();
            var wpfThread = new Thread(() =>
            {
                try
                {
                    wpfDispatcherBox.TrySetResult(Dispatcher.CurrentDispatcher);
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    wpfDispatcherBox.TrySetException(ex);
                }
            });

            wpfThread.SetApartmentState(ApartmentState.STA);
            wpfThread.Start();
            var wpfDispatcher = await wpfDispatcherBox.Task.ConfigureAwait(false);
            try
            {
                return await wpfDispatcher.InvokeAsync(() =>
                {
                    DispatcherHelper.Initialize();
                    var viewModel = new InputViewModel(options);
                    var window = new InputWindow(viewModel);
                    Register(Messenger.Default, window);

                    if (window.ShowDialog() != true)
                    {
                        return false;
                    }

                    options.PackDefinitionFilePaths = viewModel.PackFiles.Select(fp => fp.Value).ToArray();
                    options.JavaBinDirectoryPath = viewModel.JavaBinFolder;
                    options.OutputDirectoryPath = viewModel.OutputFolder;
                    options.DownloadDirectoryPath = viewModel.DownloadFolder;
                    options.SteamDirectoryPath = viewModel.SteamFolder;
                    return options.MightBeValid;
                }).Task.ConfigureAwait(false);
            }
            finally
            {
                wpfDispatcher.InvokeShutdown();
                wpfThread.Join();
            }
        }

        private static void Register(IMessenger messenger, object recipient)
        {
            messenger.Register<SelectPackFileMessage>(recipient, SelectPackFile);
            messenger.Register<SelectFolderMessage>(recipient, SelectFolder);
        }

        private static void SelectPackFile(SelectPackFileMessage message)
        {
            var sender = message.Target as Window;

            var dlg = new VistaOpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = message.FileExtension,
                DereferenceLinks = true,
                Filter = message.FileExtensionFilter
            };

            if (message.DialogTitle != null)
            {
                dlg.Title = message.DialogTitle;
            }

            if (dlg.ShowDialog(sender) != true)
            {
                return;
            }

            message.SelectedFilePath = dlg.FileName;
        }

        private static void SelectFolder(SelectFolderMessage message)
        {
            var sender = message.Target as Window;

            var dlg = new VistaFolderBrowserDialog();

            if (message.DialogTitle != null)
            {
                dlg.Description = message.DialogTitle;
                dlg.UseDescriptionForTitle = true;
            }

            if (dlg.ShowDialog(sender) != true)
            {
                return;
            }

            message.SelectedFolderPath = dlg.SelectedPath;
        }
    }
}
