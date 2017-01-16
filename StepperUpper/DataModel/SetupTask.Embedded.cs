using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather.IO;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class Embedded : SetupTask
        {
            public DeferredAbsolutePath File;

            public ImmutableArray<byte> Data;

            public Embedded(XElement taskElement)
            {
                this.File.BaseFolder = ParseKnownFolder(taskElement.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.File.RelativePath = taskElement.Attribute("File").Value;

                Encoding encoding = ParseEncoding(taskElement.Attribute("Encoding")?.Value);

                if (encoding != null)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (string line in taskElement.Elements("Line").Select(l => l.Value))
                    {
                        sb.AppendLine(line);
                    }

                    this.Data = encoding.GetBytes(sb.MoveToString()).ToImmutableArray();
                }
                else
                {
                    this.Data = Convert.FromBase64String(taskElement.Value).ToImmutableArray();
                }
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken)
            {
                FileInfo file = context.ResolveFile(this.File);
                file.Directory.Create();
                return AsyncFile.WriteAllBytesAsync(file.FullName, this.Data.ToArray());
            }
        }
    }
}
