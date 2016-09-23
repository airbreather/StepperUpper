using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather.IO;
using AirBreather.Logging;

using static System.FormattableString;

namespace StepperUpper
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int result = MainAsync(args).GetAwaiter().GetResult();
            sw.Stop();
            Console.WriteLine(Invariant($"Ran for {sw.ElapsedTicks / (double)Stopwatch.Frequency:N3} seconds."));
            return result;
        }


        private static async Task<int> MainAsync(string[] args)
        {
            Options options = new Options();
            CommandLine.Parser.Default.ParseArgumentsStrict(args, options);

            ILogger logger = Log.For(typeof(Program));
            logger.Info("Checking existing files...");

            DirectoryInfo downloadDirectory = new DirectoryInfo(options.DownloadDirectoryPath);
            DirectoryInfo steamDirectory = new DirectoryInfo(Path.Combine(options.SteamDirectoryPath, "steamapps", "common", "Skyrim", "Data"));
            IObservable<FileInfo> downloadDirectoryFiles = downloadDirectory.GetFiles().ToObservable();
            IObservable<FileInfo> steamDirectoryFiles = steamDirectory.GetFiles().ToObservable();

            IObservable<FileInfo> allFiles = Observable.Merge(downloadDirectoryFiles, steamDirectoryFiles)
                // uncomment to see what it's like if you don't have anything yet.
                ////.Take(0)
                ;

            IDictionary<Md5Checksum, string> checkedFiles =
                await CachedMd5.Calculate(allFiles)
                    .Distinct(f => f.Checksum)
                    .ToDictionary(f => f.Checksum, f => f.Path)
                    .ToTask()
                    .ConfigureAwait(false);

            XDocument doc;
            using (MemoryStream bufferStream = new MemoryStream())
            {
                using (FileStream packDefinitionFileStream = AsyncFile.OpenReadSequential(options.PackDefinitionFilePath))
                {
                    await packDefinitionFileStream.CopyToAsync(bufferStream).ConfigureAwait(false);
                }

                bufferStream.Position = 0;
                doc = XDocument.Load(bufferStream);
            }

            IGrouping<string, XElement>[] groups = doc
                .Element("Modpack")
                .Element("Files")
                .Elements("Group")
                .SelectMany(grp => grp.Elements("File"))
                .GroupBy(fl => fl.Attribute("Option")?.Value ?? fl.Attribute("Name").Value)
                .ToArray();

            IGrouping<string, XElement>[] missingGroups = groups.Where(grp => grp.All(fl => !Md5Checksums(fl).Any(ck => checkedFiles.ContainsKey(ck)))).ToArray();

            logger.Info("Finished checking existing files.");

            if (missingGroups.Length == 0)
            {
                goto success;
            }

            logger.Info("Failed to find {0} files.  Checking to see if any missing ones can be downloaded automatically.", missingGroups.Length);

            using (HttpClient client = new HttpClient())
            {
                // TODO: consider concurrency.  we have 2 downloadables, so it doesn't REALLY matter
                // yet, but there's no reason we should be this limited.
                for (int i = 0; i < missingGroups.Length; i++)
                {
                    var grp = missingGroups[i];
                    var downloadables = grp.Select(fl => new
                    {
                        Name = fl.Attribute("Name").Value,
                        Md5Checksums = Md5Checksums(fl).ToArray(),
                        DownloadUrl = fl.Attribute("DownloadUrl")?.Value,
                        CanonicalFileName = fl.Attribute("CanonicalFileName").Value
                    }).ToArray();

                    if (downloadables.Length == 0)
                    {
                        continue;
                    }

                    // TODO: try more than one if the first fails?  ehh, that's rarely going to be
                    // any better, and we don't even have any option groups with any downloadables.
                    var downloadable = downloadables[0];
                    logger.Info("{0} can be downloaded automatically from {1}.  Trying now...", downloadable.Name, downloadable.DownloadUrl);

                    var targetFile = new FileInfo(Path.Combine(downloadDirectory.FullName, downloadable.CanonicalFileName));
                    try
                    {
                        using (Stream downloadStream = await client.GetStreamAsync(downloadable.DownloadUrl).ConfigureAwait(false))
                        using (FileStream fileStream = AsyncFile.CreateSequential(targetFile.FullName))
                        {
                            await downloadStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        // ehhhhhhhhhhh..................
                        logger.Exception(ex);
                        continue;
                    }

                    var fileWithChecksum = await CachedMd5.Calculate(Observable.Return(targetFile)).ToTask().ConfigureAwait(false);
                    if (0 <= Array.IndexOf(downloadable.Md5Checksums, fileWithChecksum.Checksum))
                    {
                        logger.Info("{0} downloaded successfully, and the checksum matched.", downloadable.Name);
                        missingGroups[i] = null;
                    }
                    else
                    {
                        logger.Warn("{0} downloaded successfully, but the checksum did not match.  You're going to have to download it the hard way.", downloadable.Name);
                    }
                }
            }

            if (missingGroups.Any(grp => grp != null))
            {
                logger.Error("Some files could not be downloaded automatically.  Get them the hard way:");
                foreach (var grp in missingGroups.Where(grp => grp != null))
                {
                    switch (grp.Count())
                    {
                        case 1:
                            XElement missing = grp.First();
                            logger.Info("    {0}, URL: {{1}}", missing.Attribute("Name").Value, GetUrl(missing));
                            continue;

                        case 2:
                            logger.Info("    Either of:");
                            break;

                        default:
                            logger.Info("    Any of:");
                            break;
                    }

                    foreach (XElement missing in grp)
                    {
                        logger.Info("        {0}, URL: {1}", missing.Attribute("Name").Value, GetUrl(missing));
                    }
                }

                return 1;
            }

            success:
            logger.Info("All file requirements satisfied!");

            var dct = groups.ToDictionary(grp => grp.Key, grp => new FileInfo(checkedFiles[grp.SelectMany(Md5Checksums).First(checkedFiles.ContainsKey)]));
            DirectoryInfo dumpDirectory = new DirectoryInfo(options.OutputDirectoryPath);
            if (dumpDirectory.Exists)
            {
                DeleteDirectory(dumpDirectory);
                await Task.Yield();
            }

            dumpDirectory.Create();
            foreach (XElement task in doc.Element("Modpack").Element("Tasks").Elements("Group").SelectMany(grp => grp.Elements()))
            {
                await SetupTasks.DispatchAsync(task, dct, dumpDirectory).ConfigureAwait(false);
            }

            return 0;
        }

        internal static void DeleteDirectory(DirectoryInfo directory)
        {
            foreach (FileSystemInfo info in directory.GetFiles())
            {
                info.Attributes = FileAttributes.Normal;
                info.Delete();
            }

            foreach (DirectoryInfo info in directory.GetDirectories())
            {
                DeleteDirectory(info);
            }

            directory.Delete();
        }

        private static string GetUrl(XElement missing)
        {
            using (IEnumerator<string> tokens = Tokenize(missing.Attribute("DownloadTags").Value).GetEnumerator())
            {
                tokens.MoveNext();
                string handler = tokens.Current;
                tokens.MoveNext();
                switch (handler)
                {
                    case "steam":
                        string appId = tokens.Current;
                        return Invariant($"steam://store/{appId}");

                    case "nexus":
                        string game = tokens.Current;
                        tokens.MoveNext();
                        string modId = tokens.Current;
                        tokens.MoveNext();
                        string fileId = tokens.Current;

                        // don't tell Mom
                        ////return Invariant($"nxm://{game}/mods/{modId}/files/{fileId}");
                        return Invariant($"http://www.nexusmods.com/{game}/mods/{modId}");

                    case "generic":
                        return tokens.Current;

                    default:
                        throw new NotSupportedException("Unrecognized handler: " + handler);
                }
            }
        }

        private static IEnumerable<Md5Checksum> Md5Checksums(XElement element)
        {
            yield return new Md5Checksum(element.Attribute("MD5Checksum").Value);
            foreach (XElement alternateMD5Checksum in element.Elements("AlternateMD5Checksum"))
            {
                yield return new Md5Checksum(alternateMD5Checksum.Value);
            }
        }

        private static IEnumerable<string> Tokenize(string txt)
        {
            StringBuilder sb = new StringBuilder(txt.Length);
            foreach (char ch in txt)
            {
                switch (ch)
                {
                    case '§':
                        yield return sb.MoveToString();
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            yield return sb.MoveToString();
        }
    }
}
