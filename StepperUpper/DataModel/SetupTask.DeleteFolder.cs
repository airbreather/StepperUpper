using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class DeleteFolder : SetupTask
        {
            public DeferredAbsolutePath Path;

            public DeleteFolder(XElement taskElement)
            {
                this.Path.BaseFolder = ParseKnownFolder(taskElement.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.Path.RelativePath = taskElement.Attribute("Path").Value;
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken) =>
                Program.DeleteDirectoryAsync(context.ResolveFolder(this.Path));
        }
    }
}
