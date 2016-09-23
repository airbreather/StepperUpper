using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StepperUpper
{
    internal static class SetupTasks
    {
        internal static Task DispatchAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory)
        {
            switch (taskElement.Name.LocalName)
            {
                case "ExtractArchive":
                    return ExtractArchiveAsync(taskElement, knownFiles, dumpDirectory);

                case "Clean":
                    Console.WriteLine("TODO: This is a placeholder for code that'll automatically run plugin cleaning.");
                    return Task.CompletedTask;
            }

            throw new NotSupportedException("Task type " + taskElement.Name.LocalName + " is not supported.");
        }

        private static async Task ExtractArchiveAsync(XElement taskElement, IReadOnlyDictionary<string, FileInfo> knownFiles, DirectoryInfo dumpDirectory)
        {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "STEP_" + Path.GetRandomFileName());
            DirectoryInfo tempDirectory = new DirectoryInfo(tempDirectoryPath);
            tempDirectory.Create();
            await SevenZipExtractor.ExtractArchiveAsync(knownFiles[taskElement.Attribute("ArchiveFile").Value].FullName, tempDirectory).ConfigureAwait(false);

            foreach (XElement mapElement in taskElement.Elements("Map"))
            {
                string givenFromPath = mapElement.Attribute("From").Value;
                string givenToPath = mapElement.Attribute("To").Value;
                string toPath = Path.Combine(dumpDirectory.FullName, givenToPath);

                if (givenFromPath.Length == 0)
                {
                    tempDirectory.MoveTo(toPath);
                    return;
                }

                string fromPath = Path.Combine(tempDirectoryPath, givenFromPath);
                DirectoryInfo fromDirectory = new DirectoryInfo(fromPath);

                fromDirectory.MoveTo(toPath);
            }

            Program.DeleteDirectory(tempDirectory);
        }
    }
}
