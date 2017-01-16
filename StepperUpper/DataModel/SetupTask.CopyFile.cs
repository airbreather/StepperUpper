using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class CopyFile : SetupTask
        {
            public DeferredAbsolutePath From;

            public DeferredAbsolutePath To;

            internal CopyFile() { }

            public CopyFile(XElement taskElement)
            {
                this.From.BaseFolder = ParseKnownFolder(taskElement.Attribute("FromParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.To.BaseFolder = ParseKnownFolder(taskElement.Attribute("ToParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);

                this.From.RelativePath = taskElement.Attribute("From").Value;
                this.To.RelativePath = taskElement.Attribute("To").Value;
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken)
            {
                FileInfo toFile = context.ResolveFile(this.To);
                toFile.Directory.Create();
                context.ResolveFile(this.From).CopyTo(toFile.FullName, true);
                return Task.CompletedTask;
            }
        }
    }
}
