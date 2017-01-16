using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class DeleteFile : SetupTask
        {
            public DeferredAbsolutePath Path;

            public DeleteFile(XElement taskElement)
            {
                this.Path.BaseFolder = ParseKnownFolder(taskElement.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.Path.RelativePath = taskElement.Attribute("Path").Value;
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken) =>
                Program.DeleteFileAsync(context.ResolveFile(this.Path));
        }
    }
}
