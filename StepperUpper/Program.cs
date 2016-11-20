﻿using System;
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

using AirBreather;
using AirBreather.Collections;
using AirBreather.IO;

using Microsoft.Win32;

using static System.FormattableString;

namespace StepperUpper
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Options options = new Options();
            try
            {
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
            finally
            {
                if (!options.NoPauseAtEnd)
                {
                    Console.Write("Press any key to continue . . . ");
                    Console.ReadKey(intercept: true);
                    Console.WriteLine();
                }
            }
        }

        private static async Task<int> MainAsync(Options options)
        {
            var sourceDirectories = new List<string>
            {
                new DirectoryInfo(options.DownloadDirectoryPath).FullName,
            };

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
                bool requiresJava, requiresSteam, requiresSkyrim;
                string skyrimPath = null;
                string steamPath = null;

                XDocument doc;
                using (FileStream packDefinitionFileStream = AsyncFile.OpenReadSequential(packDefinitionFilePath))
                using (StreamReader reader = new StreamReader(packDefinitionFileStream, Encoding.UTF8, false, 4096, true))
                {
                    string docText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    requiresJava = docText.Contains("{JavaBinFolderForwardSlashes}", StringComparison.Ordinal);
                    requiresSteam = docText.Contains("{SteamInstallFolder}", StringComparison.Ordinal) ||
                                    docText.Contains("{SteamInstallFolderEscapeBackslashes}", StringComparison.Ordinal);

                    requiresSkyrim = docText.Contains("{SkyrimInstallFolder}", StringComparison.Ordinal) ||
                                     docText.Contains("{SkyrimInstallFolderForwardSlashes}", StringComparison.Ordinal) ||
                                     docText.Contains("{SkyrimInstallFolderEscapeBackslashes}", StringComparison.Ordinal);

                    StringBuilder docTextBuilder = new StringBuilder(docText);
                    docTextBuilder = docTextBuilder.Replace("{DumpFolderForwardSlashes}", dumpDirectory.FullName.Replace(Path.DirectorySeparatorChar, '/'))
                                                   .Replace("{DumpFolderEscapeBackslashes}", dumpDirectory.FullName.Replace("\\", "\\\\"));

                    if (requiresJava)
                    {
                        string javaBinPath = GetJavaBinDirectoryPath(options);
                        if (String.IsNullOrEmpty(javaBinPath))
                        {
                            Console.Error.WriteLine("--javaBinFolder is required for {0}.", XDocument.Parse(docTextBuilder.ToString()).Element("Modpack").Attribute("Name").Value);
                            return 6;
                        }

                        DirectoryInfo javaBinDirectory = new DirectoryInfo(javaBinPath);
                        javaBinPath = javaBinDirectory.FullName;
                        if (!javaBinDirectory.EnumerateFiles().Any(fl => "javaw.exe".Equals(fl.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.Error.WriteLine("Java bin folder {0} does not contain a file called \"javaw.exe\".", javaBinPath);
                            return 14;
                        }

                        docTextBuilder = docTextBuilder.Replace("{JavaBinFolderForwardSlashes}", javaBinPath.Replace(Path.DirectorySeparatorChar, '/'));
                    }

                    if (requiresSteam)
                    {
                        if (options.SteamDirectoryPath == null)
                        {
                            Console.Error.WriteLine("-s / --steamFolder is required for {0}.", XDocument.Parse(docTextBuilder.ToString()).Element("Modpack").Attribute("Name").Value);
                            return 11;
                        }

                        steamPath = new DirectoryInfo(options.SteamDirectoryPath).FullName;
                        docTextBuilder = docTextBuilder.Replace("{SteamInstallFolder}", steamPath)
                                                       .Replace("{SteamInstallFolderEscapeBackslashes}", steamPath.Replace("\\", "\\\\"));

                        // pack files targeting versions earlier than 0.9.3.0 would need this; other
                        // pack files won't use this anyway, so it's just a small waste of time.
                        skyrimPath = Path.Combine(steamPath, "steamapps", "common", "Skyrim");
                    }

                    if (requiresSkyrim)
                    {
                        skyrimPath = GetSkyrimDirectoryPath(options);
                        if (String.IsNullOrEmpty(skyrimPath))
                        {
                            Console.Error.WriteLine("-s / --steamFolder, or a valid Skyrim registry key, is required for {0}.", XDocument.Parse(docTextBuilder.ToString()).Element("Modpack").Attribute("Name").Value);
                            return 12;
                        }

                        skyrimPath = new DirectoryInfo(skyrimPath).FullName;
                        docTextBuilder = docTextBuilder.Replace("{SkyrimInstallFolder}", skyrimPath)
                                                       .Replace("{SkyrimInstallFolderForwardSlashes}", skyrimPath.Replace(Path.DirectorySeparatorChar, '/'))
                                                       .Replace("{SkyrimInstallFolderEscapeBackslashes}", skyrimPath.Replace("\\", "\\\\"));
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

                    if (Int32.TryParse(modpackElement.Attribute("LongestOutputPathLength")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var currMaxOutputPathLength) &&
                        longestOutputPathLength < currMaxOutputPathLength)
                    {
                        longestOutputPathLength = currMaxOutputPathLength;
                    }

                    string game;
                    switch (game = modpackElement.Attribute("Game")?.Value)
                    {
                        case null:
                            break;

                        case "Skyrim2011":
                            if (requiresSkyrim)
                            {
                                break;
                            }

                            skyrimPath = GetSkyrimDirectoryPath(options);
                            if (String.IsNullOrEmpty(skyrimPath))
                            {
                                Console.Error.WriteLine("-s / --steamFolder, or a valid Skyrim registry key, is required for {0}.", modpackName);
                                return 12;
                            }

                            skyrimPath = new DirectoryInfo(skyrimPath).FullName;
                            break;

                        default:
                            Console.Error.WriteLine("Unrecognized game: {0}", game);
                            return 13;
                    }

                    if (requiresSkyrim)
                    {
                        sourceDirectories.Add(Path.Combine(skyrimPath, "Data"));
                    }
                    else if (minimumToolVersion < new Version(0, 9, 3, 0))
                    {
                        // pack files for versions earlier than 0.9.3.0 always used Skyrim's Data
                        // directory, relative to the Steam directory, as an additional source for
                        // their input files; until we make a big enough breaking change, we might
                        // as well continue to support those old pack files.
                        if (options.SteamDirectoryPath == null)
                        {
                            Console.Error.WriteLine("-s / --steamFolder is required for {0}.", modpackName);
                            return 11;
                        }

                        steamPath = new DirectoryInfo(options.SteamDirectoryPath).FullName;
                        sourceDirectories.Add(Path.Combine(steamPath, "steamapps", "common", "Skyrim", "Data"));
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
            var sizes = new Dictionary<long, Hashes>();
            foreach (var el in groups.SelectMany(grp => grp))
            {
                var size = Int64.Parse(el.Attribute("LengthInBytes").Value, NumberStyles.None, CultureInfo.InvariantCulture);
                if (!sizes.TryGetValue(size, out var hashesToCheck))
                {
                    hashesToCheck = Hashes.Md5;
                }

                if (el.Attribute("SHA512Checksum") != null)
                {
                    hashesToCheck |= Hashes.Sha512;
                }

                sizes[size] = hashesToCheck;
            }

            var sourceFiles = sourceDirectories.Distinct(StringComparer.OrdinalIgnoreCase)
                                               .Select(dir => new DirectoryInfo(dir).EnumerateFiles()
                                                                                    .Where(fl => sizes.ContainsKey(fl.Length))
                                                                                    .Select(fl => (fl, sizes[fl.Length]))
                                                                                    .ToArray())
                                               .ToArray();

            int cnt = 0;
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                cnt = checked(cnt + sourceFiles[i].Length);
            }

            Console.Write(Invariant($"\r{cnt.ToString(CultureInfo.InvariantCulture).PadLeft(10)} file(s) remaining..."));
            IObservable<(FileInfo, Hashes)> allFiles = Observable.Merge(TaskPoolScheduler.Default, Array.ConvertAll(sourceFiles, Observable.ToObservable))
                // uncomment to see what it's like if you don't have anything yet.
                ////.Take(0)
                ;

            var checkedFiles =
                await ConcurrentHashCheck.Calculate(allFiles)
                    .Do(_ => Console.Write(Invariant($"\r{Interlocked.Decrement(ref cnt).ToString(CultureInfo.InvariantCulture).PadLeft(10)} file(s) remaining...")))
                    .Distinct(f => f.md5Checksum)
                    .ToDictionary(f => f.md5Checksum)
                    .ToTask()
                    .ConfigureAwait(false);

            Console.Write(Invariant($"\r{new string(' ', 32)}"));
            Console.Write('\r');

            var missingGroups = groups.Where(grp => !grp.Any(fl => checkedFiles.TryGetValue(new Md5Checksum(fl.Attribute("MD5Checksum").Value), out var val) &&
                                                                   new Sha512Checksum(fl.Attribute("SHA512Checksum")?.Value) == val.sha512Checksum))
                                      .ToArray();

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
                        Md5Checksum = new Md5Checksum(fl.Attribute("MD5Checksum").Value),
                        Sha512Checksum = new Sha512Checksum(fl.Attribute("SHA512Checksum")?.Value),
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

                    var targetFile = new FileInfo(Path.Combine(sourceDirectories[0], downloadable.CanonicalFileName));
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

                    var fileWithChecksum = await ConcurrentHashCheck.Calculate(Observable.Return((targetFile, Hashes.Md5 | Hashes.Sha512))).ToTask().ConfigureAwait(false);
                    if (fileWithChecksum.md5Checksum == downloadable.Md5Checksum && 
                        (downloadable.Sha512Checksum == default(Sha512Checksum) || fileWithChecksum.sha512Checksum == downloadable.Sha512Checksum))
                    {
                        Console.WriteLine("{0} downloaded successfully, and the checksum matched.", downloadable.Name);
                        missingGroups[i] = null;
                        checkedFiles[fileWithChecksum.md5Checksum] = fileWithChecksum;
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
                            if (!checksums.Add(new Md5Checksum(el.Attribute("MD5Checksum").Value)))
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
                    .ToDictionary(grp => grp.Key, grp => new FileInfo(checkedFiles[grp.Select(el => new Md5Checksum(el.Attribute("MD5Checksum").Value)).First(checkedFiles.ContainsKey)].file.FullName));

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

                            await SetupTasks.DispatchAsync(taskElement, dct, dumpDirectory, dict2).ConfigureAwait(false);

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
                toDirectory.Parent.Create();
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

            toFile.Directory.Create();

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

        private static string GetJavaBinDirectoryPath(Options options)
        {
            if (options.JavaBinDirectoryPath?.Length > 0)
            {
                return options.JavaBinDirectoryPath;
            }

            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey sub = key?.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment", writable: false) ??
                                     key?.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit", writable: false))
            using (RegistryKey ver = sub?.OpenSubKey(sub.GetValue("CurrentVersion") as string ?? String.Empty, writable: false))
            {
                string javaHome = ver?.GetValue("JavaHome") as string;
                return String.IsNullOrEmpty(javaHome)
                    ? String.Empty
                    : Path.Combine(javaHome, "bin");
            }
        }

        private static string GetSkyrimDirectoryPath(Options options)
        {
            if (options.SteamDirectoryPath?.Length > 0)
            {
                return Path.Combine(options.SteamDirectoryPath, "steamapps", "common", "Skyrim");
            }

            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey sub = key?.OpenSubKey(@"SOFTWARE\Bethesda Softworks\Skyrim", writable: false))
            {
                return sub?.GetValue("Installed Path") as string;
            }
        }
    }
}
