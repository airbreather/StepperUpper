using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;
using AirBreather.IO;
using AirBreather.Text;

using BethFile;

using static StepperUpper.Cleaner;

namespace StepperUpper
{
    internal static class SetupTasks
    {
        internal static Task DispatchAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory, IReadOnlyDictionary<string, TaskCompletionSource<object>> otherTasks)
        {
            switch (taskElement.Name.LocalName)
            {
                case "ExtractArchive":
                    return ExtractArchiveAsync(taskElement, knownFiles, dumpDirectory);

                case "TweakINI":
                    return Task.Run(() => WriteINI(taskElement, dumpDirectory));

                case "CopyFile":
                    return Task.Run(() => CopyFile(taskElement, dumpDirectory));

                case "Embedded":
                    return WriteEmbeddedFileAsync(taskElement, dumpDirectory);

                case "Clean":
                    return Task.Run(() => DoCleaningAsync(GetPlugins(taskElement, knownFiles, dumpDirectory)));

                case "CreateEmptyFolder":
                    Directory.CreateDirectory(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("Path").Value));
                    return Task.CompletedTask;

                case "RunProcess":
                    return ProcessRunner.RunProcessAsync(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("ExecutablePath").Value),
                                                         ProcessPriorityClass.Normal,
                                                         taskElement.Elements("Argument").Select(arg => GetArgument(arg, dumpDirectory)).ToArray());

                case "DeleteFolder":
                    return Program.DeleteDirectoryAsync(new DirectoryInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("Path").Value)));

                case "MoveFolder":
                    return Program.MoveDirectoryAsync(new DirectoryInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("From").Value)),
                                                      new DirectoryInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("To").Value)));

                case "EditFile":
                    return EditFileAsync(taskElement, dumpDirectory);
            }

            throw new NotSupportedException("Task type " + taskElement.Name.LocalName + " is not supported.");
        }

        private static async Task ExtractArchiveAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory)
        {
            string randomFileName = Path.GetRandomFileName();
            IEnumerable<XElement> elements = taskElement.Elements();

            string givenFile = taskElement.Attribute("ArchiveFile").Value;
            bool explicitDelete = true;

            DirectoryInfo tempDirectory;

            // slight hack to make the STEP XML file much more bearable.
            XAttribute simpleMO = taskElement.Attribute("SimpleMO");
            if (simpleMO != null)
            {
                explicitDelete = false;
                tempDirectory = new DirectoryInfo(Path.Combine(dumpDirectory.FullName, "ModOrganizer", "mods", givenFile));
            }
            else
            {
                // previously, this added some user-friendly identifying marks to the folder name,
                // but that caused some paths to exceed the max path length unnecessarily.
                tempDirectory = new DirectoryInfo(Path.Combine(dumpDirectory.FullName, randomFileName));
            }

            tempDirectory.Create();
            await SevenZipExtractor.ExtractArchiveAsync(knownFiles[givenFile].FullName, tempDirectory, ProcessPriorityClass.BelowNormal).ConfigureAwait(false);

            switch (simpleMO?.Value)
            {
                case "Single":
                    DirectoryInfo singleSub = tempDirectory.GetDirectories().Single();

                    // rename randomly to ensure no temporary conflicts
                    singleSub.MoveTo(Path.Combine(tempDirectory.FullName, randomFileName));
                    elements = new[] { new XElement("MapFolder", new XAttribute("From", randomFileName), new XAttribute("To", tempDirectory.FullName)) }.Concat(elements);
                    break;

                case "SingleData":
                    DirectoryInfo singleData = tempDirectory.GetDirectories().Where(dir => "data".Equals(dir.Name, StringComparison.OrdinalIgnoreCase)).Single();

                    // rename randomly to ensure no temporary conflicts
                    singleData.MoveTo(Path.Combine(tempDirectory.FullName, randomFileName));
                    elements = new[] { new XElement("MapFolder", new XAttribute("From", randomFileName), new XAttribute("To", tempDirectory.FullName)) }.Concat(elements);
                    break;
            }

            foreach (XElement element in elements)
            {
                switch (element.Name.LocalName)
                {
                    case "MapFolder":
                        {
                            string givenFromPath = element.Attribute("From")?.Value ?? String.Empty;
                            string givenToPath = element.Attribute("To").Value;
                            string toPath = Path.Combine(dumpDirectory.FullName, givenToPath);
                            DirectoryInfo toDirectory = new DirectoryInfo(toPath);
                            toDirectory.Parent.Create();

                            if (givenFromPath.Length == 0)
                            {
                                explicitDelete = false;
                            }

                            string fromPath = Path.Combine(tempDirectory.FullName, givenFromPath);
                            DirectoryInfo fromDirectory = new DirectoryInfo(fromPath);

                            await Program.MoveDirectoryAsync(fromDirectory, toDirectory).ConfigureAwait(false);
                            break;
                        }

                    case "MapFile":
                        {
                            string givenFromPath = element.Attribute("From").Value;
                            string givenToPath = element.Attribute("To").Value;

                            string fromPath = Path.Combine(tempDirectory.FullName, givenFromPath);
                            string toPath = Path.Combine(dumpDirectory.FullName, givenToPath);

                            FileInfo toFile = new FileInfo(toPath);
                            toFile.Directory.Create();
                            if (toFile.Exists)
                            {
                                toFile.Delete();
                                toFile.Refresh();
                            }

                            File.Move(fromPath, toPath);
                            break;
                        }

                    case "Hide":
                        {
                            string folderToHide = element.Attribute("Folder")?.Value;
                            string pathToHide = Path.Combine(dumpDirectory.FullName, folderToHide ?? element.Attribute("File").Value);
                            if (folderToHide != null)
                            {
                                Directory.Move(pathToHide, pathToHide + ".mohidden");
                            }
                            else
                            {
                                File.Move(pathToHide, pathToHide + ".mohidden");
                            }

                            break;
                        }

                    case "Optional":
                        {
                            FileInfo file = new FileInfo(Path.Combine(dumpDirectory.FullName, element.Attribute("File").Value));
                            file.MoveTo(Path.Combine(file.Directory.CreateSubdirectory("optional").FullName, file.Name));
                            break;
                        }

                    default:
                        throw new NotSupportedException("Unsupported element: " + element.Name.LocalName);
                }
            }

            if (explicitDelete)
            {
                await Program.DeleteDirectoryAsync(tempDirectory).ConfigureAwait(false);
            }
        }

        private static void WriteINI(XElement taskElement, DirectoryInfo dumpDirectory)
        {
            FileInfo iniFile = new FileInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("File").Value));
            iniFile.Directory.Create();

            foreach (XElement setElement in taskElement.Elements("Set"))
            {
                NativeMethods.WritePrivateProfileString(sectionName: setElement.Attribute("Section").Value,
                                                        propertyName: setElement.Attribute("Property").Value,
                                                        value: setElement.Attribute("Value").Value,
                                                        iniFilePath: iniFile.FullName);
            }
        }

        private static void CopyFile(XElement taskElement, DirectoryInfo dumpDirectory)
        {
            XAttribute fromAttribute = taskElement.Attribute("From");
            FileInfo fromFile = new FileInfo(Path.Combine(dumpDirectory.FullName, fromAttribute.Value));
            FileInfo toFile = new FileInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("To").Value));
            toFile.Directory.Create();
            fromFile.CopyTo(toFile.FullName, true);
        }

        private static async Task WriteEmbeddedFileAsync(XElement taskElement, DirectoryInfo dumpDirectory)
        {
            FileInfo file = new FileInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("File").Value));
            file.Directory.Create();
            Encoding encoding = null;
            switch (taskElement.Attribute("Encoding")?.Value)
            {
                case null:
                    break;

                case "UTF8NoBOM":
                    encoding = EncodingEx.UTF8NoBOM;
                    break;

                default:
                    throw new NotSupportedException("I don't know what encoding to use for " + taskElement.Attribute("Encoding").Value);
            }

            using (FileStream stream = AsyncFile.CreateSequential(file.FullName))
            {
                if (encoding != null)
                {
                    using (StreamWriter writer = new StreamWriter(stream, encoding, 4096, true))
                    {
                        foreach (string line in taskElement.Elements("Line").Select(l => l.Value))
                        {
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    byte[] buf = Convert.FromBase64String(taskElement.Value);
                    await stream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
                }
            }
        }

        private static IEnumerable<PluginForCleaning> GetPlugins(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory)
        {
            var pluginsForCleaning = new List<PluginForCleaning>();
            foreach (var el in taskElement.Elements("Plugin"))
            {
                FileInfo fl;
                string outputPath;
                string name;

                string inputPath = el.Attribute("Path")?.Value;
                string dirtyFile = el.Attribute("DirtyFile")?.Value;
                if (inputPath != null)
                {
                    fl = new FileInfo(Path.Combine(dumpDirectory.FullName, inputPath));
                    outputPath = name = fl.FullName;
                }
                else if (dirtyFile != null)
                {
                    fl = knownFiles[name = dirtyFile];
                    FileInfo outputFile = new FileInfo(Path.Combine(dumpDirectory.FullName, el.Attribute("OutputPath").Value));
                    outputFile.Directory.Create();
                    outputPath = outputFile.FullName;
                }
                else
                {
                    fl = knownFiles[name = el.Attribute("CleanFile").Value];
                    outputPath = null;
                }

                if (el.Attribute("WaitFor") != null)
                {
                    throw new NotSupportedException("Plugin elements no longer support the WaitFor attribute as of 0.9.1.0.");
                }

                yield return new PluginForCleaning(
                    name: name,
                    outputFilePath: outputPath,
                    dirtyFile: fl,
                    parentNames: el.Elements("Master").Select(el2 => el2.Attribute("File").Value),
                    recordsToDelete: TokenizeIds(el.Element("Delete")?.Attribute("Ids").Value),
                    recordsToUDR: TokenizeIds(el.Element("UDR")?.Attribute("Ids").Value),
                    fieldsToDelete: el.Elements("RemoveField")
                                      .Select(el2 => (recordId: UInt32.Parse(el2.Attribute("RecordId").Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                                                      fieldType: new B4S(el2.Attribute("FieldType").Value))));
            }
        }

        private static IEnumerable<uint> TokenizeIds(string ids) => Program.Tokenize(ids).Select(id => UInt32.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        private static async Task EditFileAsync(XElement taskElement, DirectoryInfo dumpDirectory)
        {
            var preAdds = taskElement.Elements("AddLineBefore").ToDictionary(el => el.Element("Before").Value, el => el.Elements("Line").Select(ln => ln.Value).ToArray());
            var postAdds = taskElement.Elements("AddLineAfter").ToDictionary(el => el.Element("After").Value, el => el.Elements("Line").Select(ln => ln.Value).ToArray());
            var edits = taskElement.Elements("ModifyLine").ToDictionary(el => el.Element("Old").Value, el => el.Element("New").Value);
            var deletes = taskElement.Elements("DeleteLine").Select(el => el.Element("Line").Value).ToHashSet();

            Encoding encoding;
            switch (taskElement.Attribute("Encoding").Value)
            {
                case "UTF8NoBOM":
                    encoding = EncodingEx.UTF8NoBOM;
                    break;

                default:
                    throw new NotSupportedException("I don't know what encoding to use for " + taskElement.Attribute("Encoding").Value);
            }

            string path = Path.Combine(dumpDirectory.FullName, taskElement.Attribute("File").Value);
            string tmp = Path.Combine(dumpDirectory.FullName, Path.GetRandomFileName());
            using (var fl1 = AsyncFile.OpenReadSequential(path))
            using (var rd = new StreamReader(fl1, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
            using (var fl2 = AsyncFile.CreateSequential(tmp))
            using (var wr = new StreamWriter(fl2, encoding, 4096, leaveOpen: true))
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

            await Program.MoveFileAsync(new FileInfo(tmp), new FileInfo(path)).ConfigureAwait(false);
        }

        private static string GetArgument(XElement arg, DirectoryInfo dumpDirectory)
        {
            switch (arg.Attribute("Type")?.Value)
            {
                case null:
                    return arg.Value;

                case "PathUnderOutputFolder":
                    return Path.Combine(dumpDirectory.FullName, arg.Value);
            }

            throw new NotSupportedException("Argument type " + arg.Attribute("Type")?.Value + " was not recognized.");
        }
    }
}
