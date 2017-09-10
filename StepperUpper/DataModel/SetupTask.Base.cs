using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather.Text;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        public string Id = Guid.NewGuid().ToString();

        public ImmutableArray<string> WaitFor = ImmutableArray<string>.Empty;

        private string xml;

        public static SetupTask CreateFrom(XElement taskElement)
        {
            SetupTask result;
            switch (taskElement.Name.LocalName)
            {
                case "Group":
                    result = new Composite(taskElement);
                    break;

                case "ExtractArchive":
                    result = new ExtractArchive(taskElement);
                    break;

                case "TweakINI":
                    result = new TweakINI(taskElement);
                    break;

                case "CopyFile":
                    result = new CopyFile(taskElement);
                    break;

                case "Embedded":
                    result = new Embedded(taskElement);
                    break;

                case "Clean":
                    result = new Clean(taskElement);
                    break;

                case "CreateEmptyFolder":
                    result = new CreateEmptyFolder(taskElement);
                    break;

                case "RunProcess":
                    result = new RunProcess(taskElement);
                    break;

                case "DeleteFolder":
                    result = new DeleteFolder(taskElement);
                    break;

                case "DeleteFile":
                    result = new DeleteFile(taskElement);
                    break;

                case "MoveFolder":
                    result = new MoveFolder(taskElement);
                    break;

                case "EditFile":
                    result = new EditFile(taskElement);
                    break;

                case "Dummy":
                    result = new Composite();
                    break;

                default:
                    throw new NotSupportedException("Task type " + taskElement.Name.LocalName + " is not supported.");
            }

            result.xml = taskElement.ToString();
            result.Id = taskElement.Attribute("Id")?.Value ?? Guid.NewGuid().ToString();

            // this feels clunky, but that's fine I guess...
            result.WaitFor = result.WaitFor.Union(Program.Tokenize(taskElement.Attribute("WaitFor")?.Value)).ToImmutableArray();
            return result;
        }

        public async Task DispatchAsync(SetupContext context, CancellationToken cancellationToken = default)
        {
            var tcs = context.GetNamedTask(this.Id);
            await Task.WhenAll(this.WaitFor.Select(n => (Task)context.GetNamedTask(n).Task).ToArray()).ConfigureAwait(false);
            try
            {
                await this.DispatchAsyncCore(context, cancellationToken).ConfigureAwait(false);
                tcs.SetResult(null);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken && cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("{1}Error when trying to do this task:{1}{0}{1}{1}{2}{1}", this, Environment.NewLine, ex);
                tcs.SetException(ex);
            }
        }

        public override string ToString() => this.xml;

        protected static KnownFolder ParseKnownFolder(string value, KnownFolder defaultIfNotSpecified)
        {
            switch (value)
            {
                case null:
                    return defaultIfNotSpecified;

                case "OutputFolder":
                    return KnownFolder.Output;

                case "GameInstallFolder":
                    return KnownFolder.GameInstall;

                case "GameDataFolder":
                    return KnownFolder.GameData;

                case "DownloadFolder":
                    return KnownFolder.Download;

                case "CheckedFiles":
                    return KnownFolder.AllCheckedFiles;

                default:
                    throw new NotSupportedException("Unrecognized value for the folder: " + value);
            }
        }

        protected static Encoding ParseEncoding(string value)
        {
            switch (value)
            {
                case null:
                    return null;

                case "UTF8NoBOM":
                    return EncodingEx.UTF8NoBOM;

                default:
                    throw new NotSupportedException("I don't know what encoding to use for " + value);
            }
        }

        protected abstract Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken);
    }
}
