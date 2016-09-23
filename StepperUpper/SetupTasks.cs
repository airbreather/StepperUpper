using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StepperUpper
{
    internal static class SetupTasks
    {
        internal static Task DispatchAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory, DirectoryInfo steamInstallDirectory)
        {
            switch (taskElement.Name.LocalName)
            {
                case "ExtractArchive":
                    return ExtractArchiveAsync(taskElement, knownFiles, dumpDirectory, steamInstallDirectory);

                case "TweakINI":
                    WriteINI(taskElement, dumpDirectory);
                    return Task.CompletedTask;

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
                        break;

                    default:
                        throw new NotSupportedException("SimpleMO mode " + simpleMO.Value + " is not supported.");
                }

                DirectoryInfo toDirectory = new DirectoryInfo(Path.Combine(dumpDirectory.FullName, "ModOrganizer", "mods", givenFile));
                toDirectory.Parent.Create();
                Program.MoveDirectory(fromDirectory, toDirectory);
                return;
            }

            foreach (XElement mapElement in taskElement.Elements("MapFolder"))
            {
                string givenFromPath = mapElement.Attribute("From")?.Value ?? String.Empty;
                string givenToPath = mapElement.Attribute("To").Value;
                string toPath = Path.Combine(dumpDirectory.FullName, givenToPath);
                DirectoryInfo toDirectory = new DirectoryInfo(toPath);
                toDirectory.Parent.Create();

                if (givenFromPath.Length == 0)
                {
                    tempDirectory.MoveTo(toPath);
                    return;
                }

                string fromPath = Path.Combine(tempDirectoryPath, givenFromPath);
                DirectoryInfo fromDirectory = new DirectoryInfo(fromPath);

                Program.MoveDirectory(fromDirectory, toDirectory);
            }

            foreach (XElement mapElement in taskElement.Elements("MapFile"))
            {
                string givenFromPath = mapElement.Attribute("From").Value;
                string givenToPath = mapElement.Attribute("To").Value;

                string fromPath = Path.Combine(tempDirectoryPath, givenFromPath);
                string toPath = Path.Combine(dumpDirectory.FullName, givenToPath);

                FileInfo toFile = new FileInfo(toPath);
                toFile.Directory.Create();
                if (toFile.Exists)
                {
                    toFile.Delete();
                    await Task.Yield();
                }

                File.Move(fromPath, toPath);
            }

            Program.DeleteDirectory(tempDirectory);
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
    }
}
