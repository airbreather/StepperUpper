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

using AirBreather;
using AirBreather.IO;
using AirBreather.Logging;

using BethFile;
using BethFile.Editor;

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
            // uncomment to run the testing plugin cleaning stuff.
            ////await CleanPluginsAsync().ConfigureAwait(false);
            ////return 0;
            Options options = new Options();
            CommandLine.Parser.Default.ParseArgumentsStrict(args, options);

            ILogger logger = Log.For(typeof(Program));
            logger.Info("Checking existing files...");

            DirectoryInfo downloadDirectory = new DirectoryInfo(options.DownloadDirectoryPath);
            DirectoryInfo steamDirectory = new DirectoryInfo(options.SteamDirectoryPath);
            DirectoryInfo skyrimDirectory = new DirectoryInfo(Path.Combine(steamDirectory.FullName, "steamapps", "common", "Skyrim", "Data"));
            IObservable<FileInfo> downloadDirectoryFiles = downloadDirectory.GetFiles().ToObservable();
            IObservable<FileInfo> skyrimDirectoryFiles = skyrimDirectory.GetFiles().ToObservable();

            IObservable<FileInfo> allFiles = Observable.Merge(downloadDirectoryFiles, skyrimDirectoryFiles)
                // uncomment to see what it's like if you don't have anything yet.
                ////.Take(0)
                ;

            Dictionary<Md5Checksum, string> checkedFiles =
                await CachedMd5.Calculate(allFiles)
                    .Distinct(f => f.Checksum)
                    .ToDictionary(f => f.Checksum, f => f.Path)
                    .Select(d => new Dictionary<Md5Checksum, string>(d))
                    .ToTask()
                    .ConfigureAwait(false);

            XDocument doc;
            using (FileStream packDefinitionFileStream = AsyncFile.OpenReadSequential(options.PackDefinitionFilePath))
            using (StreamReader reader = new StreamReader(packDefinitionFileStream, Encoding.UTF8, false, 4096, true))
            {
                string docText = await reader.ReadToEndAsync().ConfigureAwait(false);
                docText = docText.Replace("{SteamInstallFolder}", steamDirectory.FullName)
                                 .Replace("{SteamInstallFolderEscapeBackslashes}", steamDirectory.FullName.Replace("\\", "\\\\"));
                doc = XDocument.Parse(docText);
            }

            IGrouping<string, XElement>[] groups = doc
                .PoolStrings() // lots of strings show up multiple times each.
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
            var dict = doc.Element("Modpack").Element("Tasks").Elements("Group").SelectMany(grp => grp.Elements()).ToDictionary(t => t.Attribute("Id")?.Value ?? Guid.NewGuid().ToString());
            var dict2 = dict.ToDictionary(kvp => kvp.Key, _ => new TaskCompletionSource<object>());
            await Task.WhenAll(dict.Select(kvp => Task.Run(async () =>
            {
                string id = kvp.Key;
                XElement taskElement = kvp.Value;

                try
                {
                    string waitId = taskElement.Attribute("WaitFor")?.Value;
                    if (waitId != null)
                    {
                        await dict2[waitId].Task.ConfigureAwait(false);
                    }

                    await SetupTasks.DispatchAsync(taskElement, dct, dumpDirectory, steamDirectory, checkedFiles).ConfigureAwait(false);
                    dict2[id].TrySetResult(null);
                }
                catch (Exception ex)
                {
                    dict2[id].TrySetException(ex);
                    throw;
                }
            }))).ConfigureAwait(false);

            return 0;
        }

        internal static void MoveDirectory(DirectoryInfo fromDirectory, DirectoryInfo toDirectory)
        {
            fromDirectory.Refresh();
            toDirectory.Refresh();
            try
            {
                fromDirectory.MoveTo(toDirectory.FullName);
                return;
            }
            catch
            {
                // you knew it wouldn't be this easy every time.
            }

            foreach (FileInfo file in fromDirectory.GetFiles())
            {
                string targetPath = Path.Combine(toDirectory.FullName, file.Name);
                if (File.Exists(targetPath))
                {
                    File.SetAttributes(targetPath, FileAttributes.Normal);
                    File.Delete(targetPath);
                }

                file.MoveTo(Path.Combine(toDirectory.FullName, file.Name));
            }

            foreach (DirectoryInfo subFromDirectory in fromDirectory.GetDirectories())
            {
                MoveDirectory(subFromDirectory, toDirectory.CreateSubdirectory(subFromDirectory.Name));
            }

            DeleteDirectory(fromDirectory);
            toDirectory.Refresh();
        }

        internal static void DeleteDirectory(DirectoryInfo directory)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    foreach (FileInfo info in directory.GetFiles())
                    {
                        info.Attributes = FileAttributes.Normal;
                        info.Delete();
                    }

                    foreach (DirectoryInfo info in directory.GetDirectories())
                    {
                        DeleteDirectory(info);
                    }

                    directory.Delete();
                    return;
                }
                catch
                {
                    if (sw.Elapsed.TotalSeconds > 10)
                    {
                        throw;
                    }
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

        internal static IEnumerable<Md5Checksum> Md5Checksums(XElement element) => Md5ChecksumsWithIds(element).Select(kvp => kvp.Value);

        internal static IEnumerable<KeyValuePair<string, Md5Checksum>> Md5ChecksumsWithIds(XElement element)
        {
            yield return KeyValuePair.Create("default", new Md5Checksum(element.Attribute("MD5Checksum").Value));
            foreach (XElement alternateMD5Checksum in element.Elements("AlternateMD5Checksum"))
            {
                yield return KeyValuePair.Create(alternateMD5Checksum.Attribute("Id").Value, new Md5Checksum(alternateMD5Checksum.Value));
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

        #region PluginCleaningTest

        // change this to wherever is appropriate for you.
        private const string BaseDirectory = @"C:\Games\Steam\steamapps\common\Skyrim\Data\";

        private static async Task CleanPluginsAsync()
        {
            // we "fire and forget" a lot of things that we don't care about for
            // any reason other than making sure we don't terminate until it's
            // all done.  this will hold those tasks.
            List<Task> backgroundTasks = new List<Task>();

            // clean Update.esm first.
            // load the master.
            Task<Record> skyrimMasterTask = Task.Run(() => LoadPluginAsync(BaseDirectory + "Skyrim.esm.bak"));
            Task<Record> updateMasterTask = Task.Run(() => LoadPluginAsync(BaseDirectory + "Update.esm.bak"));

            ////Task<Record> baselineUpdateMasterTask = Task.Run(() => LoadPluginAsync(BaseDirectory + "Update.esm.true"));
            Task<Record> baselineUpdateMasterTask = Task.FromException<Record>(new InvalidOperationException("You forgot to swap this line with the one above it."));

            uint[] udrs = { 0x2BD60, 0x81979, 0x109CD2 };
            HashSet<uint> deletes = new HashSet<uint> { 0x9418, 0x95B0, 0x13384, 0x133A5, 0x142ED, 0x1691F, 0x19E53, 0x1A26F, 0x1A270, 0x1A332, 0x1B07F, 0x1BB9B, 0x20554, 0x206AE, 0x21553, 0x223E7, 0x242BA, 0x26C4D, 0x2850A, 0x28923, 0x2E655, 0x35369, 0x3636C, 0x36516, 0x38382, 0x3ECD5, 0x45923, 0x4D8E2, 0x54B6F, 0x74069, 0x76F44, 0x76F45, 0x7DCFC, 0x93807, 0x95125, 0x9B239, 0x9BA65, 0x9CCD3, 0x9CCD4, 0x9DD77, 0x9F179, 0x9F823, 0xA6D5E, 0xA6D5F, 0xA6D60, 0xA6D61, 0xABD88, 0xABD96, 0xB702D, 0xBAC24, 0xBD77F, 0xBD78C, 0xC0ED9, 0xC4F2D, 0xCBA9A, 0xD7886, 0xD93FA, 0xD9431, 0xD944F, 0xE1720, 0xE1726, 0xE1727, 0xE519C, 0xE6DF1, 0xE89EF, 0xF9956, 0xFE946, 0xFE94B, 0xFE950, 0x100EFB, 0x102ED2, 0x104D27, 0x106CE9, 0x106CEA, 0x108A63, 0x10C064, 0x10C065, 0x10D2AD, 0x10D2AE, 0x10E3BF, 0x10F8AC, 0x10F8B0, 0x10FEA3, 0x10FEA4, 0x10FEA5 };

            // UDRs also get treated as "deletes", because we always merge in a
            // brand new copy of the record from the base.  conveniently, if we
            // delete it first, then we don't have to worry about conditionally
            // doing something special for records that were moved in the dirty
            // versions of the plugins before ultimately being deleted.
            deletes.UnionWith(udrs);

            // we can do deletes before Skyrim.esm is done loading.
            Record updateMaster = await updateMasterTask.ConfigureAwait(false);
            Doer.PerformDeletes(updateMaster, deletes);

            // Skyrim.esm has the true data for UDRs, so we need it now.
            Merged m = new Merged();
            m.AddRoot(await skyrimMasterTask.ConfigureAwait(false));

            Doer.PerformUDRs(updateMaster, m, udrs);
            Doer.Optimize(updateMaster);

            // save off the cleaned version and descriptions of what we did.
            backgroundTasks.Add(Task.Run(() => FinalizeAsync(updateMaster, baselineUpdateMasterTask, "Update")));

            Console.WriteLine("Finished cleaning Update (it's saving now).");

            // Update.esm is a master for the others.  well, not Hearthfire, but
            // it's awkward to take advantage of that.
            m.AddRoot(updateMaster);

            backgroundTasks.Add(Task.Run(() => CleanDragonbornAsync(m)));
            backgroundTasks.Add(Task.Run(() => CleanDawnguardAsync(m)));
            backgroundTasks.Add(Task.Run(() => CleanHearthfireAsync(m)));

            await Task.WhenAll(backgroundTasks).ConfigureAwait(false);
        }

        private static Task CleanDragonbornAsync(Merged m)
        {
            uint[] udrs = { 0x32AEE, 0x32AEB, 0xC2CF1, 0xC2CF2, 0xC2CF3, 0xC2CF4, 0xC2CF5, 0xC2CF6 };
            HashSet<uint> deletes = new HashSet<uint> { 0x1B4CF, 0x38382, 0xBC05, 0xB3A6, 0x79BD8, 0x79BD9, 0x79BDA, 0xFC118, 0xFC126, 0xD366C, 0x9A38F, 0xFC12A, 0xA17DA, 0xA17DB, 0xA17DE, 0xA17E0, 0xEF5A1, 0xEF5A2, 0xEF5A3, 0xEF5A5, 0x81C5A, 0x896A9, 0x15FCE, 0x1320E, 0x30B13, 0x101732, 0xCA210, 0x2D512, 0x9FF6D, 0x101C01, 0xE7A58, 0x13BAF, 0x1365F, 0x39D11, 0x100098E, 0x3DE4E, 0x750BB, 0x6FD69, 0x753D0, 0xD6ADB, 0xB0EF7, 0xF5B05, 0x101989, 0x32AF8 };

            return CleanDlcAsync("Dragonborn", deletes, udrs, m);
        }

        private static Task CleanDawnguardAsync(Merged m)
        {
            uint[] udrs = { 0x6A2FC, 0x100814, 0x10081C, 0x6AD96, 0x23C63, 0x6005F, 0x41ED9, 0x41EDF, 0x41EE2, 0x71F2B, 0x71F2C, 0x107CF1, 0x41EDB, 0x7ABFC, 0x7AD42, 0xCE5F9, 0xCE5FA, 0xCE600, 0xCE602, 0xCE603, 0xE663E, 0xCE60B, 0xCE60F, 0xCE60E, 0xCE60D, 0xD1908, 0xD1909, 0xD190A, 0xD12EE, 0xD12ED, 0xD12EB, 0xD12D4, 0xD12B6, 0xD12B5, 0xD12B4, 0xD12B3, 0xD12B2, 0xD12B1, 0xD12B0, 0xD12AF, 0xD12AE, 0xD127E, 0xD1280, 0xD1281, 0xD128A, 0x96790, 0xD129D, 0xD129E, 0xD1310, 0xD1311, 0xD1327, 0xD8C66, 0xEF171, 0xF9625, 0x848D2, 0x848D3, 0x100815, 0x100816, 0x100817, 0x106E11, 0xC745F, 0xC7461, 0x791FD, 0xDC9F3, 0xEA5E0, 0x74CE7, 0x74B93, 0x75AF8, 0x74C4B, 0x10C190, 0xA307E, 0xA307F, 0x10D0F9, 0x10D0F8, 0x10D0F6, 0x10D0FD, 0x32AED, 0x8BF4E, 0x8BF4B, 0x8BC55, 0x62C6E, 0x62C6D };
            HashSet<uint> deletes = new HashSet<uint> { 0x5E9, 0x3785, 0x3CC5, 0x3D07, 0x3D4A, 0x3D6B, 0x6DD1, 0x6DF1, 0x6F85, 0x6FC6, 0x6FE7, 0x709E, 0x713B, 0x9010, 0x902D, 0x9085, 0x9132, 0x9369, 0x9387, 0x95C7, 0x987A, 0x987B, 0x987C, 0x989A, 0x989B, 0x9A06, 0x9A42, 0x9AD6, 0x9CDF, 0xBA3E, 0xBA5F, 0xBC13, 0x1320E, 0x13344, 0x13909, 0x142B5, 0x1522A, 0x165A8, 0x166A9, 0x1676A, 0x1676C, 0x16F5D, 0x17054, 0x17136, 0x17EA9, 0x18460, 0x185F5, 0x1879D, 0x19A20, 0x1A202, 0x1A27A, 0x1EE71, 0x1EE75, 0x1EE7A, 0x1F80E, 0x1FA4C, 0x1FED2, 0x20E16, 0x2124F, 0x2137B, 0x216A7, 0x23776, 0x237B4, 0x237C8, 0x237CF, 0x237D3, 0x237D7, 0x237DF, 0x23AB9, 0x24313, 0x25402, 0x268FB, 0x26C6F, 0x26E0E, 0x2707A, 0x29EB3, 0x2D5D1, 0x2E118, 0x2E13B, 0x2E470, 0x2E910, 0x2EC23, 0x300E6, 0x3029D, 0x30B13, 0x31729, 0x32AEC, 0x32AF9, 0x3506D, 0x361EC, 0x37EE9, 0x38382, 0x38731, 0x39F67, 0x39F6C, 0x3A1E0, 0x3A825, 0x3B4A5, 0x3CA00, 0x3D62B, 0x40760, 0x411BD, 0x453E9, 0x472C0, 0x47AE9, 0x48427, 0x4A04E, 0x4B73B, 0x4BB58, 0x4BCEA, 0x4F4EC, 0x50EE4, 0x5254C, 0x52FEB, 0x54D0E, 0x55936, 0x59398, 0x5C626, 0x5C80C, 0x5DB98, 0x5DBA9, 0x5EAC7, 0x5FAE0, 0x60020, 0x69857, 0x69CEB, 0x6AD8A, 0x6B54D, 0x6B66F, 0x6C3B6, 0x6D1E6, 0x6D1E7, 0x6F945, 0x7434B, 0x7434D, 0x7434E, 0x74AFB, 0x75083, 0x75084, 0x76255, 0x76294, 0x7829A, 0x782B4, 0x78304, 0x79184, 0x7933D, 0x79A58, 0x79A66, 0x79B15, 0x7AB30, 0x7E342, 0x7E5DE, 0x7E5DF, 0x7E5E1, 0x7E64D, 0x7E8D4, 0x7E98A, 0x7E998, 0x7E9C8, 0x7E9CB, 0x7E9D2, 0x7E9FE, 0x7EB27, 0x7F3AA, 0x7F3D1, 0x7F3D6, 0x7F3D7, 0x7F3EE, 0x8A1C3, 0x8A1C4, 0x8A1CE, 0x8A1CF, 0x8A1DC, 0x8A1DD, 0x8A1DE, 0x8A1DF, 0x8D2BB, 0x8D2BC, 0x8F645, 0x904AC, 0x92A59, 0x92A5A, 0x92A5B, 0x92A5C, 0x92A5D, 0x92B22, 0x934E1, 0x934F1, 0x94BAB, 0x973F8, 0x97A34, 0x98501, 0x99571, 0x99C60, 0x9B7AA, 0x9BCD3, 0x9BCD4, 0xA0367, 0xA348A, 0xA3492, 0xA3496, 0xA3498, 0xA39C1, 0xA39C6, 0xAF0EE, 0xB0EE6, 0xB1784, 0xB2FC8, 0xBBCB4, 0xBD6F7, 0xBE46A, 0xBF650, 0xC029E, 0xC2D3C, 0xC3CA9, 0xC57DF, 0xC57E0, 0xC57E1, 0xC57E2, 0xC57E3, 0xC57E5, 0xC57E7, 0xC57E8, 0xC57E9, 0xC57EA, 0xC57EB, 0xC57EC, 0xC57ED, 0xC57EE, 0xC580E, 0xC6500, 0xC6E03, 0xC6E04, 0xC6E07, 0xC6E09, 0xC6E0C, 0xC7860, 0xC7913, 0xC7EFE, 0xC7EFF, 0xC7F00, 0xC7F01, 0xC8962, 0xC9D9A, 0xC9F09, 0xCA44B, 0xCAD03, 0xCB4F3, 0xCB4F4, 0xCCDAA, 0xCCE05, 0xCCE06, 0xCCE07, 0xCCE09, 0xCCE0A, 0xCCE0B, 0xCCE0C, 0xCCE0D, 0xCCE0E, 0xD0472, 0xD1283, 0xD1284, 0xD1285, 0xD1287, 0xD1289, 0xD128E, 0xD1291, 0xD1292, 0xD1293, 0xD12B8, 0xD12B9, 0xD12BA, 0xD12BE, 0xD12C1, 0xD12CE, 0xD12CF, 0xD12D0, 0xD12D2, 0xD1309, 0xD130A, 0xD130B, 0xD1339, 0xD133A, 0xD18DE, 0xD1B93, 0xD2F12, 0xD56F2, 0xD8F45, 0xD8F71, 0xD8F7B, 0xD9003, 0xD939D, 0xDC390, 0xDDF93, 0xE24CF, 0xE24D0, 0xE24D3, 0xE3DE8, 0xE4612, 0xE4924, 0xE492A, 0xE4B27, 0xE67B4, 0xE67B9, 0xE67BC, 0xE88FC, 0xE8910, 0xE8919, 0xE891A, 0xEC580, 0xEC6B8, 0xEC6B9, 0xEF63C, 0xF11EC, 0xF13DE, 0xF13F2, 0xF13F3, 0xF13F4, 0xF13F9, 0xF23FD, 0xF26C3, 0xF2E24, 0xF335E, 0xF3360, 0xF3361, 0xF3362, 0xF44D5, 0xF48D8, 0xF48D9, 0xF6243, 0xF6244, 0xF6295, 0xF629A, 0xF62B6, 0xF62B7, 0xF62B8, 0xF62BB, 0xF62BF, 0xF62C0, 0xF6736, 0xF77F8, 0xFA20F, 0xFA210, 0xFCC26, 0xFD677, 0xFD67A, 0xFD67C, 0xFE150, 0xFE151, 0xFFDE2, 0xFFDE7, 0xFFDE8, 0xFFDE9, 0xFFDEA, 0xFFDEB, 0xFFDEC, 0xFFDED, 0xFFDEE, 0xFFDEF, 0xFFDF0, 0xFFDF1, 0xFFDF2, 0xFFDF3, 0xFFDF4, 0xFFDF5, 0xFFDF6, 0xFFDF7, 0xFFE7C, 0xFFE7E, 0xFFE88, 0xFFE8C, 0x100697, 0x100825, 0x100E8C, 0x101291, 0x1017FB, 0x10197F, 0x101DE2, 0x101F2D, 0x101F2E, 0x101F2F, 0x101F30, 0x101F34, 0x102EE9, 0x102EEC, 0x102EF1, 0x102EF4, 0x102EFA, 0x10312A, 0x10315C, 0x10315D, 0x10315F, 0x103160, 0x10359B, 0x10359F, 0x1035A0, 0x1035A1, 0x1038FF, 0x10390E, 0x105294, 0x105A87, 0x105A88, 0x105A8A, 0x105A8B, 0x105A8C, 0x105A8E, 0x105A8F, 0x105A90, 0x105F44, 0x1063DB, 0x1075BF, 0x1075C2, 0x10873A, 0x1087AF, 0x10888C, 0x108948, 0x108998, 0x10AA48, 0x10C453, 0x10C488, 0x10CDD7, 0x10D103, 0x10DA27, 0x10E2AE, 0x10E627, 0x100098A, 0x1000991 };

            return CleanDlcAsync("Dawnguard", deletes, udrs, m);
        }

        private static Task CleanHearthfireAsync(Merged m)
        {
            uint[] udrs = { 0x5F2E6, 0x5DAD1, 0x5F2E5, 0x5F2E4, 0xE7F99, 0xE7F9E, 0xE8D9C, 0xE8D9D, 0x5F0B1, 0x10C205, 0xDF72E };
            HashSet<uint> deletes = new HashSet<uint> { 0x41B98, 0x941DF, 0x105318, 0x10531A, 0x10FB38, 0x10FB39, 0x5156A, 0x5156C, 0x5BEF2, 0x941E3, 0x10C310, 0x37DE1, 0xD2996, 0xFEAE7, 0x1A6F5, 0x1A6F6, 0x1A6F7, 0x1A6F9, 0x1A6FC, 0x1A6FF, 0x1DB52, 0x252CB, 0x36F00, 0x506F9, 0x101FE5, 0x4E3F3, 0x3837F, 0x3837C, 0xFF280, 0xF2E23, 0xF2E1A, 0xF13F2, 0xF13F3, 0xF13F4, 0xF2E24, 0x5DB1B, 0xF13EC, 0x9A81, 0x105A73, 0x105A74, 0x105A90, 0x105AA7, 0x1050E2, 0x1050E3, 0x96A1, 0x96D3, 0xEF63C, 0x101F34, 0x101F2D, 0x101F2F, 0x101F30, 0x84791, 0x101F25, 0xFA3E1, 0x1379A, 0xD71FB, 0x9F3B7, 0x8DF0A, 0x1C5C5, 0x16CEE, 0x56B9F, 0xE2637, 0x60503, 0xDF540, 0x814B0, 0xDF527, 0x168DC, 0x38380, 0x3837E, 0x92BD, 0x9731, 0x25CF9, 0x14132, 0x136BA, 0x13668, 0x1347E, 0x13477, 0x1329B, 0x13294, 0x132A9, 0x6BA97, 0xAE009, 0xC6472, 0xA7B3A, 0xC7F10, 0xC7F0F, 0xC7F0E, 0xC6E3D, 0xC6E36, 0xA7B34, 0x2E435, 0xA348A, 0x198A0, 0x1A637, 0x198D4, 0x198DA, 0x198D9, 0x1A66C, 0x1C18E, 0x1C18D, 0x13483, 0x13482, 0x66262, 0x199CF, 0x19E0F, 0x1B090, 0x1B08E, 0x1348A, 0x13489, 0xE4EC8 };

            return CleanDlcAsync("HearthFires", deletes, udrs, m);
        }

        private static async Task CleanDlcAsync(string dlcName, HashSet<uint> deletes, uint[] udrs, Merged m)
        {
            deletes.UnionWith(udrs);
            Task<Record> dlcMasterTask = Task.Run(() => LoadPluginAsync(BaseDirectory + dlcName + ".esm.bak"));

            ////Task<Record> baselineDlcMasterTask = Task.Run(() => LoadPluginAsync(BaseDirectory + dlcName + ".esm.true"));
            Task<Record> baselineDlcMasterTask = Task.FromException<Record>(new InvalidOperationException("You forgot to swap this line with the one above it."));

            Record dlcMaster = await dlcMasterTask.ConfigureAwait(false);
            Doer.PerformDeletes(dlcMaster, deletes);
            Doer.PerformUDRs(dlcMaster, m, udrs);
            Doer.Optimize(dlcMaster);
            Console.WriteLine("Finished cleaning " + dlcName + " (it's saving now).");

            await FinalizeAsync(dlcMaster, baselineDlcMasterTask, dlcName).ConfigureAwait(false);
        }

        private static async Task<Record> LoadPluginAsync(string pluginName)
        {
            using (var fl = AsyncFile.OpenReadSequential(pluginName))
            {
                return new Record(await new BethesdaFileReader(fl).ReadFileAsync().ConfigureAwait(false));
            }
        }

        private static async Task FinalizeAsync(Record cleaned, Task<Record> origTask, string name)
        {
            using (var fl = AsyncFile.CreateSequential(BaseDirectory + name + ".esm.myclean"))
            {
                await new BethesdaFileWriter(fl).WriteAsync(Saver.Save(cleaned)).ConfigureAwait(false);
            }

            Console.WriteLine("Finished saving " + name + ".");

            // uncomment if something changes and I need to go back to this part again.
            ////await DebuggingThingAsync(cleaned, origTask, name).ConfigureAwait(false);
        }

        private static async Task DebuggingThingAsync(Record cleaned, Task<Record> origTask, string name)
        {
            var sortedMine = Doer.Sort(cleaned);
            using (var fl = File.CreateText(BaseDirectory + name + ".dsc_mine.txt"))
            {
                new BethesdaFileDescriber(fl).Visit(Saver.Save(sortedMine));
            }

            var sortedGood = Doer.Sort(await origTask.ConfigureAwait(false));
            using (var fl = File.CreateText(BaseDirectory + name + ".dsc_good.txt"))
            {
                new BethesdaFileDescriber(fl).Visit(Saver.Save(sortedGood));
            }

            Console.WriteLine("All that debugging stuff is finished for " + name + " now.");
        }

        #endregion PluginCleaningTest
    }
}
