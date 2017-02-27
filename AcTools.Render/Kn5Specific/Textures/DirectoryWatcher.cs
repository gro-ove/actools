using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    internal delegate void DirectoryChanged([CanBeNull] string filename);

    internal static class DirectoryWatcher {
        private class Entry {
            public FileSystemWatcher Watcher;
            public int Count;
        }

        private static readonly Dictionary<string, Entry> Watchers = new Dictionary<string, Entry>(3);

        private static FileSystemWatcher Get(string directory) {
            Entry found;
            if (Watchers.TryGetValue(directory, out found)) {
                found.Count++;
                return found.Watcher;
            }

            var watcher = new FileSystemWatcher(Path.GetDirectoryName(directory) ?? "") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            Watchers.Add(directory, new Entry {
                Count = 1,
                Watcher = watcher
            });

            return watcher;
        }

        private static void Release(FileSystemWatcher watcher) {
            var found = Watchers.FirstOrDefault(x => ReferenceEquals(x.Value.Watcher, watcher));

            if (found.Value != null && --found.Value.Count == 0) {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                Watchers.Remove(found.Key);
            } else {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                AcToolsLogging.Write("Can’t release FSW properly: " + watcher.Path);
            }
        }

        public static IDisposable Watch(string directory, DirectoryChanged callback) {
            var watcher = Get(directory);

            FileSystemEventHandler onCreated = (sender, e) => {
                var local = FileUtils.TryToGetRelativePath(e.FullPath, directory);
                if (local == string.Empty) {
                    callback(null);
                } else if (local != null) {
                    callback(e.FullPath);
                }
            };

            FileSystemEventHandler onChanged = (sender, e) => {
                var local = FileUtils.TryToGetRelativePath(e.FullPath, directory);
                if (!string.IsNullOrEmpty(local)) {
                    callback(e.FullPath);
                }
            };

            FileSystemEventHandler onDeleted = (sender, e) => {
                var local = FileUtils.TryToGetRelativePath(e.FullPath, directory);
                if (local == string.Empty) {
                    callback(null);
                } else if (local != null) {
                    callback(e.FullPath);
                }
            };

            RenamedEventHandler onRenamed = (sender, e) => {
                var localOld = FileUtils.TryToGetRelativePath(e.OldFullPath, directory);
                var localNew = FileUtils.TryToGetRelativePath(e.FullPath, directory);

                if (localOld == string.Empty || localNew == string.Empty) {
                    callback(null);
                } else {
                    if (localNew != null) {
                        callback(e.FullPath);
                    }
                    if (localOld != null) {
                        callback(e.OldFullPath);
                    }
                }
            };

            watcher.Created += onCreated;
            watcher.Changed += onChanged;
            watcher.Deleted += onDeleted;
            watcher.Renamed += onRenamed;

            return new Holder<IDisposable>(watcher, d => {
                var w = (FileSystemWatcher)d;
                w.Created -= onCreated;
                w.Changed -= onChanged;
                w.Deleted -= onDeleted;
                w.Renamed -= onRenamed;
                Release(w);
            });
        }
    }
}