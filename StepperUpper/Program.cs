using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather.IO;

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

            DirectoryInfo downloadDirectory = new DirectoryInfo(options.DownloadDirectoryPath);
            DirectoryInfo steamDirectory = new DirectoryInfo(options.SteamDirectoryPath);
            DirectoryInfo skyrimDirectory = new DirectoryInfo(Path.Combine(steamDirectory.FullName, "steamapps", "common", "Skyrim", "Data"));

            XDocument doc;
            using (FileStream packDefinitionFileStream = AsyncFile.OpenReadSequential(options.PackDefinitionFilePath))
            using (StreamReader reader = new StreamReader(packDefinitionFileStream, Encoding.UTF8, false, 4096, true))
            {
                string docText = await reader.ReadToEndAsync().ConfigureAwait(false);
                docText = docText.Replace("{SteamInstallFolder}", steamDirectory.FullName)
                                 .Replace("{SteamInstallFolderEscapeBackslashes}", steamDirectory.FullName.Replace("\\", "\\\\"))
                                 .Replace("{DumpFolderForwardSlashes}", options.OutputDirectoryPath.Replace(Path.DirectorySeparatorChar, '/'));
                doc = XDocument.Parse(docText);
            }

            // lots of strings show up multiple times each.
            doc = doc.PoolStrings();

            var modpackElement = doc.Element("Modpack");

            var minimumToolVersion = Version.Parse(modpackElement.Attribute("MinimumToolVersion").Value);
            var currentToolVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (currentToolVersion < minimumToolVersion)
            {
                Console.Error.WriteLine("Current tool version ({0}) is lower than the minimum tool version ({1}) required for this pack.", currentToolVersion, minimumToolVersion);
                return 3;
            }

            // "Requires" was added in 0.9.1.0.
            var requires = modpackElement.Attribute("Requires").Value;

            DirectoryInfo dumpDirectory = new DirectoryInfo(options.OutputDirectoryPath);
            bool needsDelete = requires == "CleanSlate" && dumpDirectory.Exists && dumpDirectory.EnumerateFileSystemInfos().Any();
            if (needsDelete & !options.Scorch)
            {
                Console.Error.WriteLine("Output folder exists already.  Aborting...");
                Console.WriteLine("(run with -x / --scorch to have us automatically delete instead).");
                return 2;
            }

            if (needsDelete)
            {
                Console.WriteLine("Output folder exists already.  Deleting...");
                await DeleteChildrenAsync(dumpDirectory).ConfigureAwait(false);
                Console.WriteLine("Output folder deleted.");
            }
            else if (requires != "CleanSlate")
            {
                Console.WriteLine("This pack requires {0} to have been installed in order to work.  Do you promise that you installed that? (y/n)", requires);
                string result;
                while (!String.Equals((result = Console.ReadLine()), "y", StringComparison.OrdinalIgnoreCase) &&
                       !String.Equals(result, "n", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Please answer just plain y or n.");
                }

                if (String.Equals(result, "n", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("{0} is not installed.  Please install that first before proceeding.", requires);
                    return 4;
                }
            }

            Console.WriteLine("Checking existing files...");

            var groups = modpackElement
                .Element("Files")
                .Elements("Group")
                .SelectMany(grp => grp.Elements("File"))
                .GroupBy(fl => fl.Attribute("Option")?.Value ?? fl.Attribute("Name").Value)
                .ToArray();

            // we hit all the files in the Skyrim directory, which has lots of gigabytes of BSAs we
            // don't care about, and the MO download folder itself might have tons of crap we don't
            // care about either.  quick and easy fix is to just look at files whose lengths match.
            // not absolutely 100% perfect, but good enough.
            var sizes = new HashSet<long>(groups.SelectMany(grp => grp.Select(el => Int64.Parse(el.Attribute("LengthInBytes").Value, NumberStyles.None, CultureInfo.InvariantCulture))));

            FileInfo[] downloadDirectoryFiles = downloadDirectory.EnumerateFiles().Where(fl => sizes.Contains(fl.Length)).ToArray();
            FileInfo[] skyrimDirectoryFiles = skyrimDirectory.EnumerateFiles().Where(fl => sizes.Contains(fl.Length)).ToArray();

            int cnt = checked(downloadDirectoryFiles.Length + skyrimDirectoryFiles.Length);

            Console.Write(Invariant($"\r{cnt.ToString(CultureInfo.InvariantCulture).PadLeft(10)} file(s) remaining..."));
            IObservable<FileInfo> allFiles = Observable.Merge(TaskPoolScheduler.Default, downloadDirectoryFiles.ToObservable(), skyrimDirectoryFiles.ToObservable())
                // uncomment to see what it's like if you don't have anything yet.
                ////.Take(0)
                ;

            Dictionary<Md5Checksum, string> checkedFiles =
                await ConcurrentMd5.Calculate(allFiles)
                    .Do(_ => Console.Write(Invariant($"\r{Interlocked.Decrement(ref cnt).ToString(CultureInfo.InvariantCulture).PadLeft(10)} file(s) remaining...")))
                    .Distinct(f => f.Checksum)
                    .ToDictionary(f => f.Checksum, f => f.Path)
                    .Select(d => new Dictionary<Md5Checksum, string>(d))
                    .ToTask()
                    .ConfigureAwait(false);

            Console.Write(Invariant($"\r{new string(' ', 32)}"));
            Console.Write('\r');

            IGrouping<string, XElement>[] missingGroups = groups.Where(grp => !grp.Any(fl => checkedFiles.ContainsKey(Md5Checksum(fl)))).ToArray();

            Console.WriteLine("Finished checking existing files.");

            if (missingGroups.Length == 0)
            {
                goto success;
            }

            Console.WriteLine("Failed to find {0} files.  Checking to see if any missing ones can be downloaded automatically.", missingGroups.Length);

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
                        Md5Checksum = Md5Checksum(fl),
                        DownloadUrl = fl.Attribute("DownloadUrl")?.Value,
                        CanonicalFileName = fl.Attribute("CanonicalFileName").Value
                    }).Where(x => x.DownloadUrl != null).ToArray();

                    if (downloadables.Length == 0)
                    {
                        continue;
                    }

                    // TODO: try more than one if the first fails?  ehh, that's rarely going to be
                    // any better, and we don't even have any option groups with any downloadables.
                    var downloadable = downloadables[0];
                    Console.WriteLine("{0} can be downloaded automatically from {1}.  Trying now...", downloadable.Name, downloadable.DownloadUrl);

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
                        Console.Error.WriteLine(ex);
                        continue;
                    }

                    var fileWithChecksum = await ConcurrentMd5.Calculate(Observable.Return(targetFile)).ToTask().ConfigureAwait(false);
                    if (fileWithChecksum.Checksum == downloadable.Md5Checksum)
                    {
                        Console.WriteLine("{0} downloaded successfully, and the checksum matched.", downloadable.Name);
                        missingGroups[i] = null;
                    }
                    else
                    {
                        Console.WriteLine("{0} downloaded successfully, but the checksum did not match.  You're going to have to download it the hard way.", downloadable.Name);
                    }
                }
            }

            missingGroups = Array.FindAll(missingGroups, grp => grp != null);
            if (missingGroups.Length != 0)
            {
                Console.Error.WriteLine("Some files could not be downloaded automatically.  Get them the hard way:");
                foreach (var grp in missingGroups)
                {
                    switch (grp.Count())
                    {
                        case 1:
                            XElement missing = grp.First();
                            Console.WriteLine("    {0}, URL: {1}", missing.Attribute("Name").Value, GetUrl(missing));
                            continue;

                        case 2:
                            Console.WriteLine("    Either of:");
                            break;

                        default:
                            Console.WriteLine("    Any of:");
                            break;
                    }

                    foreach (XElement missing in grp)
                    {
                        Console.WriteLine("        {0}, URL: {1}", missing.Attribute("Name").Value, GetUrl(missing));
                    }
                }

                return 1;
            }

            success:
            Console.WriteLine("All file requirements satisfied!");

            var dct = groups.ToDictionary(grp => grp.Key, grp => new FileInfo(checkedFiles[grp.Select(Md5Checksum).First(checkedFiles.ContainsKey)]));

            Console.WriteLine("Starting the actual tasks (extracting archives, etc.)...");
            dumpDirectory.Create();
            var dict = doc.Element("Modpack").Element("Tasks").Elements("Group").SelectMany(grp => grp.Elements()).ToDictionary(t => t.Attribute("Id")?.Value ?? Guid.NewGuid().ToString());
            var dict2 = dict.ToDictionary(kvp => kvp.Key, _ => new TaskCompletionSource<object>());

            cnt = dict.Count;
            Console.Write(Invariant($"\r{cnt.ToString(CultureInfo.InvariantCulture).PadLeft(10)} task(s) remaining..."));
            await Task.WhenAll(dict.Select(kvp => Task.Run(async () =>
            {
                string id = kvp.Key;
                XElement taskElement = kvp.Value;

                try
                {
                    await Task.WhenAll(Tokenize(taskElement.Attribute("WaitFor")?.Value).Select(x => dict2[x].Task)).ConfigureAwait(false);

                    await SetupTasks.DispatchAsync(taskElement, dct, dumpDirectory, steamDirectory, checkedFiles, dict2)
                        .Finally(() => Console.Write(Invariant($"\r{Interlocked.Decrement(ref cnt).ToString(CultureInfo.InvariantCulture).PadLeft(10)} task(s) remaining...")))
                        .ConfigureAwait(false);

                    dict2[id].TrySetResult(null);
                }
                catch (Exception ex)
                {
                    dict2[id].TrySetException(ex);
                    throw;
                }
            }))).ConfigureAwait(false);

            Console.Write(Invariant($"\r{new string(' ', 32)}"));
            Console.Write('\r');
            Console.WriteLine("All tasks completed!");
            return 0;
        }

        internal static async Task MoveDirectoryAsync(DirectoryInfo fromDirectory, DirectoryInfo toDirectory)
        {
            try
            {
                fromDirectory.MoveTo(toDirectory.FullName);
                return;
            }
            catch
            {
                // if it were this easy every time, I wouldn't need this...
            }

            await Task.WhenAll(
                Task.WhenAll(Array.ConvertAll(fromDirectory.GetFiles(), f => MoveFileAsync(f, new FileInfo(Path.Combine(toDirectory.FullName, f.Name))))),
                Task.WhenAll(Array.ConvertAll(fromDirectory.GetDirectories(), d => MoveDirectoryAsync(d, toDirectory.CreateSubdirectory(d.Name))))).ConfigureAwait(false);
            await DeleteDirectoryAsync(fromDirectory).ConfigureAwait(false);
        }

        internal static async Task MoveFileAsync(FileInfo fromFile, FileInfo toFile)
        {
            if (toFile.Exists)
            {
                await DeleteFileAsync(toFile).ConfigureAwait(false);
            }

            while (true)
            {
                try
                {
                    fromFile.MoveTo(toFile.FullName);
                    return;
                }
                catch
                {
                    await default(NonCapturingYield);
                }
            }
        }

        internal static async Task DeleteDirectoryAsync(DirectoryInfo directory)
        {
            await DeleteChildrenAsync(directory).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    directory.Delete();
                    return;
                }
                catch
                {
                    await default(NonCapturingYield);
                }
            }
        }

        internal static Task DeleteChildrenAsync(DirectoryInfo directory) =>
            Task.WhenAll(
                Task.WhenAll(Array.ConvertAll(directory.GetFiles(), DeleteFileAsync)),
                Task.WhenAll(Array.ConvertAll(directory.GetDirectories(), DeleteDirectoryAsync)));

        internal static async Task DeleteFileAsync(FileInfo file)
        {
            while (true)
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    return;
                }
                catch
                {
                    await default(NonCapturingYield);
                }
            }
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

        internal static Md5Checksum Md5Checksum(XElement element) => new Md5Checksum(element.Attribute("MD5Checksum").Value);

        internal static IEnumerable<string> Tokenize(string txt)
        {
            if (txt == null)
            {
                yield break;
            }

            StringBuilder sb = new StringBuilder(txt.Length);
            foreach (char ch in txt)
            {
                switch (ch)
                {
                    case '|':
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
