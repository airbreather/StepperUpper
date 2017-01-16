using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;

using BethFile;

using static StepperUpper.Cleaner;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class Clean : SetupTask
        {
            public ImmutableArray<Plugin> Plugins = ImmutableArray<Plugin>.Empty;

            public Clean(XElement taskElement)
            {
                var plugins = ImmutableArray.CreateBuilder<Plugin>();
                foreach (var el in taskElement.Elements("Plugin"))
                {
                    var plugin = new Plugin
                    {
                        ParentFiles = el.Elements("Master").Select(el2 => new DeferredAbsolutePath(KnownFolder.AllCheckedFiles, el2.Attribute("File").Value)).ToImmutableArray(),
                        RecordsToDelete = TokenizeIds(el.Element("Delete")?.Attribute("Ids").Value).ToImmutableArray(),
                        RecordsToUDR = TokenizeIds(el.Element("UDR")?.Attribute("Ids").Value).ToImmutableArray(),
                        FieldsToDelete = el.Elements("RemoveField")
                                           .Select(el2 => (recordId: UInt32.Parse(el2.Attribute("RecordId").Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                                                           fieldType: new B4S(el2.Attribute("FieldType").Value))).ToImmutableArray()
                    };

                    plugins.Add(plugin);

                    string inputPath = el.Attribute("Path")?.Value;
                    string dirtyFile = el.Attribute("DirtyFile")?.Value;
                    if (inputPath != null)
                    {
                        plugin.OutputFile.BaseFolder = ParseKnownFolder(el.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                        plugin.OutputFile.RelativePath = inputPath;
                        plugin.InputFile = plugin.OutputFile;
                    }
                    else if (dirtyFile != null)
                    {
                        plugin.InputFile.BaseFolder = ParseKnownFolder(el.Attribute("DirtyFileParent")?.Value, defaultIfNotSpecified: KnownFolder.AllCheckedFiles);
                        plugin.InputFile.RelativePath = dirtyFile;

                        plugin.OutputFile.BaseFolder = ParseKnownFolder(el.Attribute("OutputPathParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                        plugin.OutputFile.RelativePath = el.Attribute("OutputPath").Value;
                    }
                    else
                    {
                        plugin.InputFile.BaseFolder = ParseKnownFolder(el.Attribute("CleanFileParent")?.Value, defaultIfNotSpecified: KnownFolder.AllCheckedFiles);
                        plugin.InputFile.RelativePath = el.Attribute("CleanFile").Value;
                    }
                }

                this.Plugins = plugins.MoveToImmutableSafe();
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken) => DoCleaningAsync(this.GetPlugins(context));

            private static IEnumerable<uint> TokenizeIds(string ids) => Program.Tokenize(ids).Select(id => UInt32.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

            private IEnumerable<PluginForCleaning> GetPlugins(SetupContext context)
            {
                foreach (var plugin in this.Plugins)
                {
                    FileInfo fl = context.ResolveFile(plugin.InputFile);
                    string outputPath = null;

                    if (plugin.CleanInPlace)
                    {
                        outputPath = (fl = context.ResolveFile(plugin.InputFile)).FullName;
                    }
                    else if (!plugin.IsClean)
                    {
                        FileInfo outputFile = context.ResolveFile(plugin.OutputFile);
                        outputFile.Directory.Create();
                        outputPath = outputFile.FullName;
                    }

                    yield return new PluginForCleaning(
                        name: fl.FullName,
                        outputFilePath: outputPath,
                        dirtyFile: fl,
                        parentNames: plugin.ParentFiles.Select(par => context.ResolveFile(par).FullName),
                        recordsToDelete: plugin.RecordsToDelete,
                        recordsToUDR: plugin.RecordsToUDR,
                        fieldsToDelete: plugin.FieldsToDelete);
                }
            }

            public sealed class Plugin
            {
                public DeferredAbsolutePath InputFile;

                public DeferredAbsolutePath OutputFile;

                public ImmutableArray<DeferredAbsolutePath> ParentFiles = ImmutableArray<DeferredAbsolutePath>.Empty;

                public ImmutableArray<uint> RecordsToDelete = ImmutableArray<uint>.Empty;

                public ImmutableArray<uint> RecordsToUDR = ImmutableArray<uint>.Empty;

                public ImmutableArray<(uint recordId, B4S fieldType)> FieldsToDelete = ImmutableArray<(uint recordId, B4S fieldType)>.Empty;

                public bool IsClean => this.OutputFile.RelativePath == null;

                public bool CleanInPlace => this.InputFile == this.OutputFile;
            }
        }
    }
}
