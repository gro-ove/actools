using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public delegate void DirectoryChanged([CanBeNull] string filename);

    public static class SimpleDirectoryWatcher {
        private class Entry {
            public FileSystemWatcher Watcher;
            public int Count;
        }

        private static readonly Dictionary<string, Entry> Watchers = new Dictionary<string, Entry>(3);

        private static FileSystemWatcher Get(string directory) {
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            if (Watchers.TryGetValue(directory, out var found)) {
                found.Count++;
                return found.Watcher;
            }

            var watcher = new FileSystemWatcher(directory) {
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
            if (found.Value == null) {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                AcToolsLogging.Write("Can’t release FSW properly: " + watcher.Path);
            } else if (--found.Value.Count == 0) {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                Watchers.Remove(found.Key);
            }
        }

        private static IDisposable SetWatcher(string directory, DirectoryChanged callback) {
            var watcher = Get(directory);

            void OnCreated(object sender, FileSystemEventArgs e) {
                var local = FileUtils.GetPathWithin(e.FullPath, directory);
                if (local == string.Empty) {
                    callback(null);
                } else if (local != null) {
                    callback(e.FullPath);
                }
            }

            void OnChanged(object sender, FileSystemEventArgs e) {
                var local = FileUtils.GetPathWithin(e.FullPath, directory);
                if (!string.IsNullOrEmpty(local)) {
                    callback(e.FullPath);
                }
            }

            void OnDeleted(object sender, FileSystemEventArgs e) {
                var local = FileUtils.GetPathWithin(e.FullPath, directory);
                if (local == string.Empty) {
                    callback(null);
                } else if (local != null) {
                    callback(e.FullPath);
                }
            }

            void OnRenamed(object sender, RenamedEventArgs e) {
                var localOld = FileUtils.GetPathWithin(e.OldFullPath, directory);
                var localNew = FileUtils.GetPathWithin(e.FullPath, directory);

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
            }

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;

            return new Holder<IDisposable>(watcher, d => {
                var w = (FileSystemWatcher)d;
                w.Created -= OnCreated;
                w.Changed -= OnChanged;
                w.Deleted -= OnDeleted;
                w.Renamed -= OnRenamed;
                Release(w);
            });
        }

        public static IDisposable WatchFile(string filename, Action reloadAction) {
            var reloading = false;
            return SetWatcher(Path.GetDirectoryName(filename), async s => {
                if (reloading || s == null || !FileUtils.ArePathsEqual(s, filename)) return;
                reloading = true;

                try {
                    for (var i = 0; i < 3; i++) {
                        try {
                            await Task.Delay(300);
                            reloadAction();
                            break;
                        } catch (IOException e) {
                            AcToolsLogging.Write(e);
                        }
                    }
                } finally {
                    reloading = false;
                }
            });
        }

        public static IDisposable WatchDirectory(string directory, DirectoryChanged callback) {
            return SetWatcher(Path.GetDirectoryName(directory) ?? "", callback);
        }
    }
}