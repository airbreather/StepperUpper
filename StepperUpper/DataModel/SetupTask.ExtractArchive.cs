using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;
using AirBreather.IO;

using BethFile.Archive;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class ExtractArchive : SetupTask
        {
            public DeferredAbsolutePath ArchiveFile;

            public ImmutableArray<FileMapping> FileMappings = ImmutableArray<FileMapping>.Empty;

            public int NestedDepth;

            public ExtractArchive(XElement taskElement)
            {
                this.ArchiveFile.BaseFolder = ParseKnownFolder(taskElement.Attribute("ArchiveFileParent")?.Value, defaultIfNotSpecified: KnownFolder.AllCheckedFiles);
                this.ArchiveFile.RelativePath = taskElement.Attribute("ArchiveFile").Value;

                var fileMappings = ImmutableArray.CreateBuilder<FileMapping>();

                string mod;
                switch (mod = taskElement.Attribute("Mod")?.Value)
                {
                    case ".":
                        mod = this.ArchiveFile.RelativePath;
                        break;
                }

                switch (taskElement.Attribute("SimpleMap")?.Value)
                {
                    case "Mod":
                        fileMappings.Add(new FileMapping
                        {
                            Kind = FileMappingKind.MapFolder,
                            To = new DeferredAbsolutePath(KnownFolder.Output, "ModOrganizer/mods/" + (mod = (mod ?? this.ArchiveFile.RelativePath))),
                        });

                        break;
                }

                if (Int32.TryParse(taskElement.Attribute("NestedDepth")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int nestedDepth))
                {
                    this.NestedDepth = nestedDepth;
                }

                foreach (var el in taskElement.Elements())
                {
                    var fileMapping = new FileMapping();
                    fileMappings.Add(fileMapping);

                    string currMod = el.Attribute("Mod")?.Value ?? mod;
                    string prefix = String.Empty;
                    if (!String.IsNullOrEmpty(currMod))
                    {
                        prefix = "ModOrganizer/mods/" + currMod + "/";
                    }

                    switch (el.Name.LocalName)
                    {
                        case "MapFolder":
                        case "MapFile":
                        case "MapBSAFile":
                            fileMapping.From = el.Attribute("From")?.Value;
                            string toRelativePath = el.Attribute("To")?.Value;
                            if (toRelativePath == ".")
                            {
                                toRelativePath = fileMapping.From;
                            }

                            fileMapping.To.BaseFolder = ParseKnownFolder(el.Attribute("ToParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                            fileMapping.To.RelativePath = prefix + toRelativePath;

                            fileMapping.Kind = (FileMappingKind)Enum.Parse(typeof(FileMappingKind), el.Name.LocalName);
                            break;

                        case "SwitchMod":
                            mod = el.Attribute("To").Value;
                            fileMappings.RemoveAt(fileMappings.Count - 1);
                            break;

                        // bah, why did I put this inside of ExtractArchive?  bleh... see how much
                        // this sucks?  in case it's not obvious, if we want to override the parent,
                        // we have to specify the same parent two times: once for the ToParent in
                        // the MapFoo, and once (the same one!) in the Parent here.
                        case "Hide":
                        case "Optional":
                            KnownFolder parent = ParseKnownFolder(el.Attribute("Parent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                            string folderPathToHide = el.Attribute("Folder")?.Value;
                            if (folderPathToHide != null)
                            {
                                fileMapping.Kind = FileMappingKind.HideFolder;
                                fileMapping.PathToHide = new DeferredAbsolutePath(parent, prefix + folderPathToHide);
                            }
                            else
                            {
                                fileMapping.Kind = el.Name.LocalName == "Optional" ? FileMappingKind.Optional : FileMappingKind.HideFile;
                                fileMapping.PathToHide = new DeferredAbsolutePath(parent, prefix + el.Attribute("File").Value);
                            }

                            break;

                        default:
                            throw new NotSupportedException("Unsupported element: " + el.Name.LocalName);
                    }
                }

                this.FileMappings = fileMappings.MoveToImmutableSafe();
            }

            public enum ModOrganizerShortcut
            {
                None,
                Root,
                Single,
                SingleData
            }

            public enum FileMappingKind
            {
                MapFolder,
                MapFile,
                MapBSAFile,

                HideFile,
                HideFolder,
                Optional
            }

            public sealed class FileMapping
            {
                public FileMappingKind Kind;

                public string From;

                public DeferredAbsolutePath To;

                // at least these won't cost any more space to support...
                public DeferredAbsolutePath PathToHide
                {
                    get => this.To;
                    set => this.To = value;
                }
            }

            protected override async Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken)
            {
                string archivePath = context.ResolveFile(this.ArchiveFile).FullName;

                DirectoryInfo tempDirectory = context.ResolveFolder(new DeferredAbsolutePath(KnownFolder.Output, Path.GetRandomFileName()));

                tempDirectory.Create();
                string tempDirectoryRoot = tempDirectory.FullName;

                await SevenZipExtractor.ExtractArchiveAsync(archivePath, tempDirectory, ProcessPriorityClass.BelowNormal).ConfigureAwait(false);

                for (int i = 0; i < this.NestedDepth; i++)
                {
                    tempDirectory = tempDirectory.GetDirectories().Single();
                }

                foreach (var fileMapping in this.FileMappings)
                {
                    switch (fileMapping.Kind)
                    {
                        case FileMappingKind.MapFolder:
                            {
                                string givenFromPath = fileMapping.From ?? String.Empty;
                                DirectoryInfo toDirectory = context.ResolveFolder(fileMapping.To);
                                toDirectory.Parent.Create();

                                string fromPath = Path.Combine(tempDirectory.FullName, givenFromPath);
                                DirectoryInfo fromDirectory = new DirectoryInfo(fromPath);

                                if (givenFromPath.Length == 0)
                                {
                                    tempDirectory = toDirectory;
                                }

                                await Program.MoveDirectoryAsync(fromDirectory, toDirectory).ConfigureAwait(false);
                                break;
                            }

                        case FileMappingKind.MapFile:
                            {
                                string givenFromPath = Path.Combine(tempDirectory.FullName, fileMapping.From);
                                string fromPath = Path.Combine(tempDirectory.FullName, givenFromPath);
                                FileInfo toFile = context.ResolveFile(fileMapping.To);

                                toFile.Directory.Create();
                                if (toFile.Exists)
                                {
                                    toFile.Delete();
                                    toFile.Refresh();
                                }

                                File.Move(fromPath, toFile.FullName);
                                break;
                            }

                        case FileMappingKind.HideFolder:
                            {
                                DirectoryInfo folderToHide = context.ResolveFolder(fileMapping.PathToHide);
                                folderToHide.MoveTo(folderToHide.FullName + ".mohidden");
                                break;
                            }

                        case FileMappingKind.HideFile:
                            {
                                FileInfo fileToHide = context.ResolveFile(fileMapping.PathToHide);
                                fileToHide.MoveTo(fileToHide.FullName + ".mohidden");
                                break;
                            }

                        case FileMappingKind.Optional:
                            {
                                FileInfo fileToHide = context.ResolveFile(fileMapping.PathToHide);
                                fileToHide.MoveTo(Path.Combine(fileToHide.Directory.CreateSubdirectory("optional").FullName, fileToHide.Name));
                                break;
                            }

                        // Note: it's tempting to extract BSA files in parallel if there are multiple in
                        // a single archive here.  resist that temptation; a later one might want to
                        // overwrite files from an earlier one.
                        case FileMappingKind.MapBSAFile:
                            {
                                FileInfo bsaFile = new FileInfo(Path.Combine(tempDirectory.FullName, fileMapping.From));
                                DirectoryInfo toFolder = context.ResolveFolder(fileMapping.To);
                                await ExtractBSAFileAsync(bsaFile, toFolder, cancellationToken).ConfigureAwait(false);
                                bsaFile.Delete();
                                break;
                            }

                        default:
                            throw new NotSupportedException("Unsupported element: " + fileMapping.Kind);
                    }
                }

                if (Directory.Exists(tempDirectoryRoot))
                {
                    await Program.DeleteDirectoryAsync(new DirectoryInfo(tempDirectoryRoot)).ConfigureAwait(false);
                }
            }
        }

        private static async Task ExtractBSAFileAsync(FileInfo bsaFile, DirectoryInfo outputDirectory, CancellationToken cancellationToken)
        {
            using (var fl = AsyncFile.OpenRead(bsaFile.FullName))
            {
                // ASSUMPTION: no BSA file will contain two copies of a file with the same path; if
                // they do, then behavior is undefined (likely ambiguous, possibly can crash).
                await SkyrimArchive.ExtractAll(fl).Select(async extracted =>
                {
                    var outputFile = new FileInfo(Path.Combine(outputDirectory.FullName, extracted.Path));
                    outputFile.Directory.Create();
                    await AsyncFile.WriteAllBytesAsync(outputFile.FullName, await extracted.FileData.ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                    return Unit.Default;
                }).Merge().ToTask(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
