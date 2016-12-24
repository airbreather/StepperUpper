using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AirBreather;
using AirBreather.IO;

using BethFile;
using BethFile.Editor;

namespace StepperUpper
{
    internal static class Cleaner
    {
        internal static Task DoCleaningAsync(IEnumerable<PluginForCleaning> plugins)
        {
            var parentTasks = new Dictionary<string, TaskCompletionSource<Merged>>();
            var pluginsMap = new Dictionary<string, PluginForCleaning>();
            foreach (var plugin in plugins)
            {
                pluginsMap.Add(plugin.Name, plugin);
                parentTasks.Add(plugin.Name, new TaskCompletionSource<Merged>());
            }

            var parents = new Dictionary<string, Merged>();
            var children = new Dictionary<string, List<(Merged parent, int i, string childName)>>();

            foreach (var kvp in pluginsMap)
            {
                var pluginName = kvp.Key;
                var plugin = kvp.Value;

                if ((plugin.ParentNames.Length | plugin.RecordsToUDR.Length) == 0)
                {
                    continue;
                }

                Merged parent = parents[pluginName] = new Merged(plugin.ParentNames.Length);
                for (int i = 0; i < plugin.ParentNames.Length; i++)
                {
                    var parentName = plugin.ParentNames[i];
                    var parentPlugin = pluginsMap[parentName];
                    if (!children.TryGetValue(parentName, out var childrenList))
                    {
                        childrenList = children[parentName] = new List<(Merged parent, int idx, string childName)>();
                    }

                    childrenList.Add((parent, i, pluginName));
                }
            }

            var cleans = new Dictionary<string, Task>(pluginsMap.Count);
            foreach (var kvp in pluginsMap)
            {
                var pluginName = kvp.Key;
                var plugin = kvp.Value;
                cleans.Add(pluginName, Task.Run(async () =>
                {
                    var parentTask = parentTasks[pluginName];
                    if (!parents.ContainsKey(pluginName))
                    {
                        parentTask.TrySetResult(default(Merged));
                    }

                    var root = await CleanPluginAsync(plugin, parentTask.Task).ConfigureAwait(false);
                    var saver = (plugin.RecordsToDelete.Length | plugin.RecordsToUDR.Length) == 0
                        ? Task.CompletedTask
                        : Task.Run(() => SavePluginAsync(root, plugin.OutputFilePath));

                    if (children.TryGetValue(pluginName, out var childrenList))
                    {
                        var records = Doer.FindRecords(root).ToDictionary(r => r.Id);
                        foreach (var (merged, idx, childName) in childrenList)
                        {
                            merged.SetRoot(idx, records);
                            if (merged.IsFinalized)
                            {
                                parentTasks[childName].TrySetResult(merged);
                            }
                        }
                    }

                    await saver.ConfigureAwait(false);
                }));
            }

            // at least at the time of writing, we make a pretty HUGE mess of things on the GC.  the
            // application's footprint stays kinda high without doing something like this.  this is
            // probably overkill, but it's enough to get me out of here.
            return Task.WhenAll(cleans.Values).Finally(new Thread(() =>
            {
                Thread.Sleep(1000);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GCUtility.RequestLargeObjectHeapCompaction();
                GC.Collect();
            })
            {
                IsBackground = true
            }.Start);
        }

        private static async Task<Record> CleanPluginAsync(PluginForCleaning plugin, Task<Merged> parentTask)
        {
            Task<Record> rootTask = LoadPluginAsync(plugin.DirtyFile.FullName);

            var deletes = new HashSet<uint>();
            foreach (var del in plugin.RecordsToDelete)
            {
                deletes.Add(del);
            }

            var udrs = plugin.RecordsToUDR;
            foreach (var udr in udrs)
            {
                deletes.Add(udr);
            }

            Record root = await rootTask.ConfigureAwait(false);

            Doer.PerformDeletes(root, deletes);
            Doer.PerformUDRs(root, await parentTask.ConfigureAwait(false), udrs);
            foreach (var (recordId, fieldType) in plugin.FieldsToDelete)
            {
                Doer.DeleteField(root, recordId, fieldType);
            }

            Doer.Optimize(root);

            return root;
        }

        private static async Task<Record> LoadPluginAsync(string path)
        {
            BethesdaFile bf;
            using (var fl = AsyncFile.OpenReadSequential(path))
            {
                bf = await new BethesdaFileReader(fl).ReadFileAsync().ConfigureAwait(false);
            }

            return new Record(bf);
        }

        private static Task SavePluginAsync(Record root, string path)
        {
            BethesdaFile bf = Saver.Save(root);
            var fl = AsyncFile.CreateSequential(path);
            try
            {
                return new BethesdaFileWriter(fl).WriteAsync(bf).Finally(fl.Dispose);
            }
            catch
            {
                fl.Dispose();
                throw;
            }
        }

        // anything where RecordsToDelete and RecordToUDR are both empty, the
        // "cleaning" process is a no-op and it just gets directly added.
        // other records have to wait for all their "parents" for part of their
        // own cleaning process, but some of it can start right away.
        internal sealed class PluginForCleaning
        {
            internal PluginForCleaning(string name, string outputFilePath, FileInfo dirtyFile, IEnumerable<string> parentNames, IEnumerable<uint> recordsToDelete, IEnumerable<uint> recordsToUDR, IEnumerable<(uint recordId, B4S fieldType)> fieldsToDelete)
            {
                this.Name = name;
                this.OutputFilePath = outputFilePath;
                this.DirtyFile = dirtyFile;
                this.ParentNames = parentNames.ToImmutableArray();
                this.RecordsToDelete = recordsToDelete.ToImmutableArray();
                this.RecordsToUDR = recordsToUDR.ToImmutableArray();
                this.FieldsToDelete = fieldsToDelete.ToImmutableArray();
            }

            internal string Name { get; }

            internal string OutputFilePath { get; }

            internal FileInfo DirtyFile { get; }

            internal ImmutableArray<string> ParentNames { get; }

            internal ImmutableArray<uint> RecordsToDelete { get; }

            internal ImmutableArray<uint> RecordsToUDR { get; }

            internal ImmutableArray<(uint recordId, B4S fieldType)> FieldsToDelete { get; }
        }
    }
}
