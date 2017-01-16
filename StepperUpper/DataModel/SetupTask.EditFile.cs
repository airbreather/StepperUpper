using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;
using AirBreather.IO;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class EditFile : SetupTask
        {
            public DeferredAbsolutePath File;

            public Encoding Encoding;

            public ImmutableArray<(string before, string line)> LinesToPrepend = ImmutableArray<(string before, string line)>.Empty;

            public ImmutableArray<(string after, string line)> LinesToAppend = ImmutableArray<(string after, string line)>.Empty;

            public ImmutableArray<(string oldLine, string newLine)> LinesToModify = ImmutableArray<(string oldLine, string newLine)>.Empty;

            public ImmutableArray<string> LinesToDelete = ImmutableArray<string>.Empty;

            public EditFile(XElement taskElement)
            {
                this.File.BaseFolder = ParseKnownFolder(taskElement.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.File.RelativePath = taskElement.Attribute("File").Value;
                this.Encoding = ParseEncoding(taskElement.Attribute("Encoding").Value) ?? throw new NotSupportedException("You shouldn't have been able to give me a null value here...");

                var linesToPrepend = ImmutableArray.CreateBuilder<(string before, string line)>();
                var linesToAppend = ImmutableArray.CreateBuilder<(string after, string line)>();
                var linesToModify = ImmutableArray.CreateBuilder<(string oldLine, string newLine)>();
                var linesToDelete = ImmutableArray.CreateBuilder<string>();

                foreach (var el in taskElement.Elements())
                {
                    switch (el.Name.LocalName)
                    {
                        case "AddLineBefore":
                            linesToPrepend.Add((before: el.Attribute("Before").Value, line: el.Attribute("Line").Value));
                            break;

                        case "AddLineAfter":
                            linesToAppend.Add((after: el.Attribute("After").Value, line: el.Attribute("Line").Value));
                            break;

                        case "ModifyLine":
                            linesToModify.Add((oldLine: el.Attribute("Old").Value, newLine: el.Attribute("New").Value));
                            break;

                        case "DeleteLine":
                            linesToDelete.Add(el.Attribute("Line").Value);
                            break;
                    }

                    this.LinesToPrepend = linesToPrepend.MoveToImmutableSafe();
                    this.LinesToAppend = linesToAppend.MoveToImmutableSafe();
                    this.LinesToModify = linesToModify.MoveToImmutableSafe();
                    this.LinesToDelete = linesToDelete.MoveToImmutableSafe();
                }
            }

            protected override async Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken)
            {
                var preAdds = this.LinesToPrepend.ToDictionary(ln => ln.before, ln => ln.line);
                var postAdds = this.LinesToAppend.ToDictionary(ln => ln.after, ln => ln.line);
                var edits = this.LinesToModify.ToDictionary(ln => ln.oldLine, ln => ln.newLine);
                var deletes = this.LinesToDelete.ToHashSet();

                FileInfo fileToEdit = context.ResolveFile(this.File);
                FileInfo tempFile = context.ResolveFile(new DeferredAbsolutePath(KnownFolder.Output, Path.GetRandomFileName()));
                using (var fl1 = AsyncFile.OpenReadSequential(fileToEdit.FullName))
                using (var rd = new StreamReader(fl1, this.Encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
                using (var fl2 = AsyncFile.CreateSequential(tempFile.FullName))
                using (var wr = new StreamWriter(fl2, this.Encoding, 4096, leaveOpen: true))
                {
                    string line;
                    while ((line = await rd.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (preAdds.TryGetValue(line, out var adds))
                        {
                            foreach (var ln in adds)
                            {
                                await wr.WriteLineAsync(ln).ConfigureAwait(false);
                            }
                        }

                        if (!deletes.Contains(line))
                        {
                            await wr.WriteLineAsync(edits.TryGetValue(line, out var ed) ? ed : line).ConfigureAwait(false);
                        }

                        if (postAdds.TryGetValue(line, out adds))
                        {
                            foreach (var ln in adds)
                            {
                                await wr.WriteLineAsync(ln).ConfigureAwait(false);
                            }
                        }
                    }
                }

                await Program.MoveFileAsync(tempFile, fileToEdit).ConfigureAwait(false);
            }
        }
    }
}
