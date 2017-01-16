using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StepperUpper
{
    internal sealed class SetupContext
    {
        private static readonly SemaphoreSlim consoleLock = new SemaphoreSlim(1, 1);

        public readonly DirectoryInfo DownloadDirectory;

        public readonly DirectoryInfo OutputDirectory;

        public readonly DirectoryInfo GameInstallDirectory;

        public readonly DirectoryInfo GameDataDirectory;

        public readonly ReadOnlyDictionary<string, Task<FileInfo>> KnownFiles;

        private ReadOnlyDictionary<string, (SetupTask setupTask, TaskCompletionSource<object> tcs)> tasks;

        private int remainingTaskCount;

        public DirectoryInfo CurrentDirectory => new DirectoryInfo(Environment.CurrentDirectory);

        public SetupContext(DirectoryInfo downloadDirectory, DirectoryInfo outputDirectory, DirectoryInfo gameInstallDirectory, DirectoryInfo gameDataDirectory, ReadOnlyDictionary<string, Task<FileInfo>> knownFiles, SetupTask taskRoot)
        {
            this.DownloadDirectory = downloadDirectory;
            this.OutputDirectory = outputDirectory;
            this.GameInstallDirectory = gameInstallDirectory;
            this.GameDataDirectory = gameDataDirectory;
            this.KnownFiles = knownFiles;
            this.tasks = new ReadOnlyDictionary<string, (SetupTask setupTask, TaskCompletionSource<object> tcs)>(Flatten(taskRoot).ToDictionary(t => t.Id, setupTask =>
            {
                var tcs = new TaskCompletionSource<object>();
                if (!(setupTask is SetupTask.Composite))
                {
                    ++this.remainingTaskCount;
                    tcs.Task.ContinueWith(t =>
                    {
                        Interlocked.Decrement(ref this.remainingTaskCount);
                        this.NotifyRemainingTasks();
                    });
                }

                return (setupTask, tcs);
            }));
            this.remainingTaskCount = tasks.Values.Count(v => !(v.setupTask is SetupTask.Composite));
        }

        public DirectoryInfo Resolve(KnownFolder folder)
        {
            switch (folder)
            {
                case KnownFolder.Current:
                    return this.CurrentDirectory;

                case KnownFolder.Output:
                    return this.OutputDirectory;

                case KnownFolder.GameInstall:
                    return this.GameInstallDirectory;

                case KnownFolder.GameData:
                    return this.GameDataDirectory;

                case KnownFolder.Download:
                    return this.DownloadDirectory;

                case KnownFolder.AllCheckedFiles:
                    throw new ArgumentOutOfRangeException(nameof(folder), folder, "There's no single directory for AllCheckedFiles.  airbreather made a mistake somewhere.  Let him know.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(folder), folder, "Unrecognized value.");
            }
        }

        public DirectoryInfo ResolveFolder(DeferredAbsolutePath path) =>
            path.BaseFolder == KnownFolder.AllCheckedFiles
                ? throw new ArgumentOutOfRangeException(nameof(path), path, "We don't put folders into AllCheckedFiles.  airbreather made a mistake somewhere.  Let him know.")
                : new DirectoryInfo(this.ResolvePath(path));

        public FileInfo ResolveFile(DeferredAbsolutePath path) => new FileInfo(this.ResolvePath(path));

        private string ResolvePath(DeferredAbsolutePath path)
        {
            // files in the game data and download folders are not referred to directly by name.
            switch (path.BaseFolder)
            {
                // TODO: conveniently, until #8 is implemented, this is never going to block... but
                // when / if it does get implemented, this is going to want to work differently.
                case KnownFolder.AllCheckedFiles:
                    return this.KnownFiles[path.RelativePath].Result.FullName;
            }

            return Path.Combine(
                    this.Resolve(path.BaseFolder).FullName,
                    path.RelativePath);
        }

        public TaskCompletionSource<object> GetNamedTask(string name) => this.tasks?[name].tcs;

        public void NotifyFinished()
        {
            this.NotifyRemainingTasks();
            this.tasks = null;
        }

        public async void NotifyRemainingTasks()
        {
            await consoleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.tasks != null)
                {
                    Console.Write($"\r{this.remainingTaskCount.ToString(CultureInfo.InvariantCulture).PadLeft(10)} task(s) remaining...");
                }
            }
            finally
            {
                consoleLock.Release();
            }
        }

        public Task WaitForCheckedFileAsync(string name) => this.WaitForCheckedFileAsync(name, CancellationToken.None);

        public async Task WaitForCheckedFileAsync(string name, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            using (var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), false))
            {
                // no need to do another ConfigureAwait(false) since the outer await is guaranteed
                // to complete synchronously anyway.
                await (await Task.WhenAny(this.KnownFiles[name], tcs.Task).ConfigureAwait(false));
            }
        }

        public Task WaitForAntedecentTasksAsync(ImmutableArray<string> names) => this.WaitForAntedecentTasksAsync(names, CancellationToken.None);

        public async Task WaitForAntedecentTasksAsync(ImmutableArray<string> names, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            using (var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), false))
            {
                // no need to do another ConfigureAwait(false) since the outer await is guaranteed
                // to complete synchronously anyway.
                await (await Task.WhenAny(Task.WhenAll(names.Select(n => (Task)this.GetNamedTask(n).Task)), tcs.Task).ConfigureAwait(false));
            }
        }

        private static IEnumerable<SetupTask> Flatten(SetupTask root)
        {
            var stack = new Stack<SetupTask>(10);
            stack.Push(root);
            while (stack.Count != 0)
            {
                yield return root = stack.Pop();
                if (root is SetupTask.Composite parent)
                {
                    foreach (var ch in parent.Children)
                    {
                        stack.Push(ch);
                    }
                }
            }
        }
    }
}