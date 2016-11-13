using System;
using System.Collections.Concurrent;
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

using AirBreather.Collections;
using AirBreather.IO;

using static System.FormattableString;

namespace StepperUpper
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Options options = new Options();
                if (!CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    Console.Error.WriteLine("Exiting with code 8.");
                    return 8;
                }

                if (!options.MightBeValid && !UI.Dialogs.FillOptionsAsync(options).ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    Console.Error.WriteLine("Options are invalid.  Exiting with code 9.");
                    return 9;
                }

                Stopwatch sw = Stopwatch.StartNew();
                int result = MainAsync(options).GetAwaiter().GetResult();
                sw.Stop();
                Console.WriteLine(Invariant($"Ran for {sw.ElapsedTicks / (double)Stopwatch.Frequency:N3} seconds.  Exiting with code {result}."));
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error.  Exiting with code 10.  Details:");
                Console.Error.WriteLine(ex);
                return 10;
            }
        }

        private static async Task<int> MainAsync(Options options)
        {
            DirectoryInfo downloadDirectory = new DirectoryInfo(options.DownloadDirectoryPath);
            DirectoryInfo steamDirectory = new DirectoryInfo(options.SteamDirectoryPath);
            DirectoryInfo skyrimDirectory = new DirectoryInfo(Path.Combine(steamDirectory.FullName, "steamapps", "common", "Skyrim", "Data"));

            DirectoryInfo dumpDirectory = new DirectoryInfo(options.OutputDirectoryPath);
            bool needsDelete = dumpDirectory.Exists && dumpDirectory.EnumerateFileSystemInfos().Any();
            if (needsDelete && !options.Scorch)
            {
                Console.Error.WriteLine("Output folder exists already and is not empty.  Aborting...");
                Console.WriteLine("(run with -x / --scorch to have us automatically delete instead).");
                return 2;
            }

            var modpackElements = new List<XElement>();

            // lots of strings show up multiple times each.
            StringPool pool = new StringPool();

            HashSet<string> seenSoFar = new HashSet<string>();
            int longestOutputPathLength = 0;
            foreach (string packDefinitionFilePath in options.PackDefinitionFilePaths)
            {
                XDocument doc;
                using (FileStream packDefinitionFileStream = AsyncFile.OpenReadSequential(packDefinitionFilePath))
                using (StreamReader reader = new StreamReader(packDefinitionFileStream, Encoding.UTF8, false, 4096, true))
                {
                    string docText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    int javaIdx = docText.IndexOf("{JavaBinFolderForwardSlashes}", StringComparison.Ordinal);
                    StringBuilder docTextBuilder = new StringBuilder(docText);
                    docTextBuilder = docTextBuilder.Replace("{SteamInstallFolder}", steamDirectory.FullName)
                                                   .Replace("{SteamInstallFolderEscapeBackslashes}", steamDirectory.FullName.Replace("\\", "\\\\"))
                                                   .Replace("{DumpFolderForwardSlashes}", options.OutputDirectoryPath.Replace(Path.DirectorySeparatorChar, '/'))
                                                   .Replace("{DumpFolderEscapeBackslashes}", options.OutputDirectoryPath.Replace("\\", "\\\\"));

                    if (0 <= javaIdx)
                    {
                        if (options.JavaBinDirectoryPath == null)
                        {
                            Console.Error.WriteLine("--javaBinFolder is required for {0}.", XDocument.Parse(docTextBuilder.ToString()).Element("Modpack").Attribute("Name").Value);
                            return 6;
                        }

                        docTextBuilder = docTextBuilder.Replace("{JavaBinFolderForwardSlashes}", options.JavaBinDirectoryPath.Replace(Path.DirectorySeparatorChar, '/'));
                    }

                    doc = XDocument.Parse(docTextBuilder.ToString());
                }

                doc = doc.PoolStrings(pool);

                foreach (var modpackElement in doc.Descendants("Modpack"))
                {
                    modpackElements.Add(modpackElement);

                    var minimumToolVersion = Version.Parse(modpackElement.Attribute("MinimumToolVersion").Value);
                    var currentToolVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    if (currentToolVersion < minimumToolVersion)
                    {
                        Console.Error.WriteLine("Current tool version ({0}) is lower than the minimum tool version ({1}) required for this pack.", currentToolVersion, minimumToolVersion);
                        return 3;
                    }

                    string modpackName = modpackElement.Attribute("Name").Value;
                    string packVersion = modpackElement.Attribute("PackVersion").Value;
                    string fileVersion = modpackElement.Attribute("FileVersion").Value;

                    var requirements = Tokenize(modpackElement.Attribute("Requires")?.Value).ToArray();
                    if (!seenSoFar.IsSupersetOf(requirements))
                    {
                        Console.Error.WriteLine("{0} needs to be set up in the same run, after all of the following are set up as well: {1}", modpackName, String.Join(", ", requirements));
                        return 4;
                    }

                    if (seenSoFar.Contains(modpackName))
                    {
                        Console.Error.WriteLine("Trying to set up {0} twice in the same run", modpackName);
                        return 5;
                    }

                    seenSoFar.Add(modpackName);
                    seenSoFar.Add(modpackName + ", " + packVersion);
                    seenSoFar.Add(modpackName + ", " + packVersion + ", " + fileVersion);

                    int currMaxOutputPathLength;
                    if (Int32.TryParse(modpackElement.Attribute("LongestOutputPathLength")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out currMaxOutputPathLength) &&
                        longestOutputPathLength < currMaxOutputPathLength)
                    {
                        longestOutputPathLength = currMaxOutputPathLength;
                    }
                }
            }

            // minus 1 for the path separator char.
            longestOutputPathLength = 255 - longestOutputPathLength - 1;
            if (!options.SkipOutputDirectoryPathLengthCheck &&
                longestOutputPathLength < dumpDirectory.FullName.Length)
            {
                Console.Error.WriteLine(Invariant($"Output directory ({options.OutputDirectoryPath}, {options.OutputDirectoryPath.Length} chars) exceeds the maximum supported length of {longestOutputPathLength} chars."));
                return 7;
            }

            Console.WriteLine("Checking existing files...");

            var groups = modpackElements.SelectMany(
                modpackElement =>
                    modpackElement
                        .Element("Files")
                        .Elements("Group")
                        .SelectMany(grp => grp.Elements("File"))
                        .GroupBy(fl => modpackElement.Attribute("Name").Value + "|" + (fl.Attribute("Option")?.Value ?? fl.Attribute("Name").Value)))
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
                // TODO: consider concurrency.  we have 3 downloadables, so it doesn't REALLY matter
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
                        checkedFiles[fileWithChecksum.Checksum] = fileWithChecksum.Path;
                    }
                    else
                    {
                        Console.WriteLine("{0} downloaded successfully, but the checksum did not match.  You're going to have to download it the hard way.", downloadable.Name);
                    }
                }
            }

            missingGroups = Array.FindAll(missingGroups, grp => grp != null);

            // don't indicate the same file multiple times in the same list.
            // just in case a later pack reuses files found in an earlier pack.
            var checksums = new HashSet<Md5Checksum>();
            if (missingGroups.Length != 0)
            {
                Console.Error.WriteLine("Some files could not be downloaded automatically.  You'll have to get them the hard way.");
                Console.Error.WriteLine("Displaying the details in your web browser...");
                string htmlFilePath = Path.Combine(options.OutputDirectoryPath, "missing.html");
                using (var writer = new StreamWriter(path: htmlFilePath, append: false, encoding: Encoding.UTF8))
                {
                    await writer.WriteLineAsync("<html><h1>Missing Files</h1><table border=\"2\"><tr><td><strong>Pack</strong></td><td><strong>Missing File</strong></td><td><strong>URL(s)</strong></td></tr>").ConfigureAwait(false);

                    StringBuilder sb = new StringBuilder();
                    foreach (var grp in missingGroups)
                    {
                        string modpack;
                        string file;
                        using (var enumerator = Tokenize(grp.Key).GetEnumerator())
                        {
                            enumerator.MoveNext();
                            modpack = enumerator.Current;
                            enumerator.MoveNext();
                            file = enumerator.Current;
                        }

                        string urlIfSingle = null;
                        int optionCount = 0;
                        foreach (var el in grp)
                        {
                            if (!checksums.Add(Md5Checksum(el)))
                            {
                                continue;
                            }

                            urlIfSingle = GetUrl(el);
                            sb.Append(Invariant($"<tr><td>{el.Attribute("Name").Value}</td><td><a href=\"{urlIfSingle}\">{urlIfSingle}</a></td></tr>"));
                            optionCount++;
                        }

                        string details;
                        switch (optionCount)
                        {
                            case 0:
                                continue;

                            case 1:
                                details = Invariant($"<a href=\"{urlIfSingle}\">{urlIfSingle}</a>");
                                sb.Clear();
                                break;

                            default:
                                details = Invariant($"<table border=\"1\">{sb.MoveToString()}</table>");
                                break;
                        }

                        await writer.WriteLineAsync(Invariant($"<tr><td>{modpack}</td><td>{file}</td><td>{details}</td></tr>")).ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync("</table>").ConfigureAwait(false);
                }

                Process.Start(htmlFilePath);
                return 1;
            }

            success:
            Console.WriteLine("All file requirements satisfied!");

            if (needsDelete)
            {
                Console.WriteLine("Output folder exists already.  Deleting...");
                await DeleteChildrenAsync(dumpDirectory).ConfigureAwait(false);
                Console.WriteLine("Output folder deleted.");

                needsDelete = false;
            }

            ManualResetEventSlim evt = new ManualResetEventSlim();
            foreach (var modpackElement in modpackElements)
            {
                string modpackName = modpackElement.Attribute("Name").Value;

                TaskCompletionSource<string> longestPathBox = new TaskCompletionSource<string>();
                Thread th = options.DetectMaxPath ? new Thread(() => DetectLongestPathLength(dumpDirectory.FullName, evt, longestPathBox)){ IsBackground = true } : null;
                th?.Start();
                Console.WriteLine("Starting tasks for {0}", modpackName);

                var dct = modpackElement
                    .Element("Files")
                    .Elements("Group")
                    .SelectMany(grp => grp.Elements("File"))
                    .GroupBy(fl => fl.Attribute("Option")?.Value ?? fl.Attribute("Name").Value)
                    .ToDictionary(grp => grp.Key, grp => new FileInfo(checkedFiles[grp.Select(Md5Checksum).First(checkedFiles.ContainsKey)]));

                Console.WriteLine("Starting the actual tasks (extracting archives, etc.)...");
                dumpDirectory.Create();
                var dict = modpackElement.Element("Tasks").Elements("Group").SelectMany(grp => grp.Elements()).ToDictionary(t => t.Attribute("Id")?.Value ?? Guid.NewGuid().ToString());
                var dict2 = dict.ToDictionary(kvp => kvp.Key, _ => new TaskCompletionSource<object>());

                cnt = dict.Count;
                Console.Write(Invariant($"\r{cnt.ToString(CultureInfo.InvariantCulture).PadLeft(10)} task(s) remaining..."));
                using (SemaphoreSlim consoleLock = new SemaphoreSlim(1, 1))
                {
                    await Task.WhenAll(dict.Select(kvp => Task.Run(async () =>
                    {
                        string id = kvp.Key;
                        XElement taskElement = kvp.Value;

                        try
                        {
                            await Task.WhenAll(Tokenize(taskElement.Attribute("WaitFor")?.Value).Select(x => dict2[x].Task)).ConfigureAwait(false);

                            await SetupTasks.DispatchAsync(taskElement, dct, dumpDirectory, steamDirectory, checkedFiles, dict2).ConfigureAwait(false);

                            dict2[id].TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            dict2[id].TrySetException(ex);
                            throw;
                        }
                        finally
                        {
                            await consoleLock.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                Console.Write(Invariant($"\r{(--cnt).ToString(CultureInfo.InvariantCulture).PadLeft(10)} task(s) remaining..."));
                            }
                            finally
                            {
                                consoleLock.Release();
                            }
                        }
                    }))).ConfigureAwait(false);
                }

                Console.Write(Invariant($"\r{new string(' ', 32)}"));
                Console.Write('\r');
                Console.WriteLine("All tasks completed for {0}!", modpackName);

                evt.Set();
                if (th != null)
                {
                    string longestPath = await longestPathBox.Task.ConfigureAwait(false);
                    Console.WriteLine(Invariant($"Max path: {longestPath} ({longestPath.Length} chars)"));
                }

                evt.Reset();
                Console.WriteLine();
            }

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

        internal static IEnumerable<string> Tokenize(string txt, char split = '|')
        {
            if (txt == null)
            {
                yield break;
            }

            StringBuilder sb = new StringBuilder(txt.Length);
            foreach (char ch in txt)
            {
                if (ch == split)
                {
                    yield return sb.MoveToString();
                }
                else
                {
                    sb.Append(ch);
                }
            }

            yield return sb.MoveToString();
        }

        private static void DetectLongestPathLength(string root, ManualResetEventSlim evt, TaskCompletionSource<string> box)
        {
            try
            {
                ConcurrentBag<string> filePaths = new ConcurrentBag<string>();
                using (var fsw = new FileSystemWatcher(root))
                {
                    Action<string> onFile = filePaths.Add;

                    fsw.Created += (sender, e) => onFile(e.FullPath);
                    fsw.Changed += (sender, e) => onFile(e.FullPath);
                    fsw.Renamed += (sender, e) => onFile(e.FullPath);

                    fsw.IncludeSubdirectories = true;
                    fsw.EnableRaisingEvents = true;

                    evt.Wait();

                    fsw.EnableRaisingEvents = false;
                    string maxPath = String.Empty;
                    foreach (var fl in filePaths)
                    {
                        if (maxPath.Length < fl.Length)
                        {
                            maxPath = fl;
                        }
                    }

                    if (maxPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        maxPath = maxPath.Substring(root.Length + 1);
                    }

                    box.TrySetResult(maxPath);
                }
            }
            catch (Exception ex)
            {
                box.TrySetException(ex);
            }
        }
    }
}
