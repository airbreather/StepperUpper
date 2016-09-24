using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using AirBreather.IO;
using AirBreather.Text;

namespace StepperUpper
{
    internal static class SetupTasks
    {
        internal static Task DispatchAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory, DirectoryInfo steamInstallDirectory, IReadOnlyDictionary<Md5Checksum, string> checkedFiles)
        {
            switch (taskElement.Name.LocalName)
            {
                case "ExtractArchive":
                    return ExtractArchiveAsync(taskElement, knownFiles, dumpDirectory, steamInstallDirectory);

                case "TweakINI":
                    return Task.Run(() => WriteINI(taskElement, dumpDirectory));

                case "CopyFile":
                    return Task.Run(() => CopyFile(taskElement, dumpDirectory, checkedFiles));

                case "Embedded":
                    return WriteEmbeddedFileAsync(taskElement, dumpDirectory);

                case "Clean":
                    Console.WriteLine("TODO: This is a placeholder for code that'll automatically run plugin cleaning.");
                    return Task.CompletedTask;
            }

            throw new NotSupportedException("Task type " + taskElement.Name.LocalName + " is not supported.");
        }

        private static async Task ExtractArchiveAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory, DirectoryInfo steamInstallDirectory)
        {
            string tempDirectoryPath = Path.Combine(dumpDirectory.FullName, "EXTRACT_DUMPER" + Path.GetRandomFileName());
            DirectoryInfo tempDirectory = new DirectoryInfo(tempDirectoryPath);
            tempDirectory.Create();
            bool explicitDelete = true;

            string givenFile = taskElement.Attribute("ArchiveFile").Value;
            await SevenZipExtractor.ExtractArchiveAsync(knownFiles[givenFile].FullName, tempDirectory).ConfigureAwait(false);

            // slight hack to make the STEP XML file much more bearable.
            XAttribute simpleMO = taskElement.Attribute("SimpleMO");
            if (simpleMO != null)
            {
                DirectoryInfo fromDirectory;
                switch (simpleMO.Value)
                {
                    case "Root":
                        fromDirectory = tempDirectory;
                        explicitDelete = false;
                        break;

                    case "Single":
                        fromDirectory = tempDirectory.EnumerateDirectories().Single();
                        break;

                    case "SingleData":
                        fromDirectory = tempDirectory.EnumerateDirectories().Single(x => "Data".Equals(x.Name, StringComparison.OrdinalIgnoreCase));
                        break;

                    default:
                        throw new NotSupportedException("SimpleMO mode " + simpleMO.Value + " is not supported.");
                }

                DirectoryInfo toDirectory = new DirectoryInfo(Path.Combine(dumpDirectory.FullName, "ModOrganizer", "mods", givenFile));
                toDirectory.Parent.Create();
                Program.MoveDirectory(fromDirectory, toDirectory);
            }

            foreach (XElement element in taskElement.Elements())
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

                        string fromPath = Path.Combine(tempDirectoryPath, givenFromPath);
                        DirectoryInfo fromDirectory = new DirectoryInfo(fromPath);

                        Program.MoveDirectory(fromDirectory, toDirectory);
                        break;
                    }

                    case "MapFile":
                    {
                        string givenFromPath = element.Attribute("From").Value;
                        string givenToPath = element.Attribute("To").Value;

                        string fromPath = Path.Combine(tempDirectoryPath, givenFromPath);
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
                        // "hide"... heh...
                        string folderToHide = element.Attribute("Folder")?.Value;
                        string pathToHide = Path.Combine(dumpDirectory.FullName, folderToHide ?? element.Attribute("File").Value);
                        if (folderToHide != null)
                        {
                            Program.DeleteDirectory(new DirectoryInfo(pathToHide));
                        }
                        else
                        {
                            File.SetAttributes(pathToHide, FileAttributes.Normal);
                            File.Delete(pathToHide);
                        }

                        break;
                    }

                    default:
                        throw new NotSupportedException("Unsupported element: " + element.Name.LocalName);
                }
            }

            if (explicitDelete)
            {
                Program.DeleteDirectory(tempDirectory);
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

        private static void CopyFile(XElement taskElement, DirectoryInfo dumpDirectory, IReadOnlyDictionary<Md5Checksum, string> checkedFiles)
        {
            XAttribute fromAttribute = taskElement.Attribute("From");
            XAttribute fileAttribute = taskElement.Attribute("File");
            FileInfo fromFile = null;
            if (fromAttribute != null)
            {
                fromFile = new FileInfo(Path.Combine(dumpDirectory.FullName, fromAttribute.Value));
            }
            else
            {
                // TODO: in reality, this may come from an earlier task.
                fromFile = new FileInfo(checkedFiles[new Md5Checksum(fileAttribute.Value)]);
            }

            FileInfo toFile = new FileInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("To").Value));
            toFile.Directory.Create();
            fromFile.CopyTo(toFile.FullName, true);
        }

        private static async Task WriteEmbeddedFileAsync(XElement taskElement, DirectoryInfo dumpDirectory)
        {
            FileInfo file = new FileInfo(Path.Combine(dumpDirectory.FullName, taskElement.Attribute("File").Value));
            file.Directory.Create();
            Encoding encoding;
            switch (taskElement.Attribute("Encoding").Value)
            {
                case "UTF8NoBOM":
                    encoding = EncodingEx.UTF8NoBOM;
                    break;

                default:
                    throw new NotSupportedException("I don't know what encoding to use for " + taskElement.Attribute("Encoding").Value);
            }

            using (FileStream stream = AsyncFile.CreateSequential(file.FullName))
            using (StreamWriter writer = new StreamWriter(stream, encoding, 4096, true))
            {
                foreach (string line in taskElement.Elements("Line").Select(l => l.Value))
                {
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }
            }
        }
    }
}
