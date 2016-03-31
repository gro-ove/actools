using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class AcObjectTypeDirectories : IDisposable {
        [NotNull]
        public readonly string EnabledDirectory;
        public readonly string DisabledDirectory;

        public AcObjectTypeDirectories([NotNull] string enabledDirectory, string disabledDirectory) {
            if (enabledDirectory == null) throw new ArgumentNullException(nameof(enabledDirectory));
            EnabledDirectory = enabledDirectory.ToLowerInvariant();
            DisabledDirectory = disabledDirectory?.ToLowerInvariant();
            Actual = true;
        }

        public void CreateIfMissing() {
            Directory.CreateDirectory(EnabledDirectory);
        }

        public AcObjectTypeDirectories([NotNull] string enabledDirectory)
            : this(enabledDirectory, enabledDirectory + "-off") {
        }

        public string GetLocation([NotNull] string id, bool enabled) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (!Actual) throw new Exception("not valid anymore");
            if (DisabledDirectory == null && !enabled) throw new Exception("cannot be disabled");
            return Path.Combine(enabled ? EnabledDirectory : DisabledDirectory, id);
        }

        private IEnumerable<string> GetSubSomething(Func<string, string[]> selector) {
            if (!Actual) throw new Exception("not valid anymore");

            List<string> enabled;
            if (Directory.Exists(EnabledDirectory)) {
                var list = selector(EnabledDirectory);
                foreach (var dir in list) {
                    yield return dir;
                }

                enabled = list.Select(x => Path.GetFileName(x)?.ToLowerInvariant()).ToList();
            } else {
                enabled = new List<string>();
            }

            if (DisabledDirectory == null || !Directory.Exists(DisabledDirectory)) yield break;
            foreach (var dir in selector(DisabledDirectory).Where(x => !enabled.Contains(x.ToLowerInvariant()))) {
                yield return dir;
            }
        }

        public IEnumerable<string> GetSubDirectories() {
            return GetSubSomething(Directory.GetDirectories);
        }

        public IEnumerable<string> GetSubDirectories(string searchPattern) {
            return GetSubSomething(x => Directory.GetDirectories(x, searchPattern));
        }

        public IEnumerable<string> GetSubFiles(string searchPattern) {
            return GetSubSomething(x => Directory.GetFiles(x, searchPattern));
        }

        public bool Actual { get; private set; }

        public void Obsolete() {
            Actual = false;
        }

        private static readonly List<DirectoryWatcher> Watchers = new List<DirectoryWatcher>();

        private static DirectoryWatcher CreateOrReuseWatcher([NotNull] string directory) {
            var watcher = Watchers.FirstOrDefault(x => x.TargetDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase));
            if (watcher != null) {
                Debug.WriteLine($"REUSE WATCHER FOR: {directory}");
                return watcher;
            }

            Debug.WriteLine($"CREATE A NEW WATCHER FOR: {directory}");
            watcher = new DirectoryWatcher(directory);
            Watchers.Add(watcher);
            return watcher;
        }

        public void Subscribe(IDirectoryListener listener) {
            Debug.WriteLine($"LISTENER SUBSCRIBED: {GetType()}, {listener.GetType()}");
            
            CreateOrReuseWatcher(EnabledDirectory).Subscribe(listener);
            if (DisabledDirectory != null) {
                CreateOrReuseWatcher(DisabledDirectory).Subscribe(listener);
            }
        }

        public void Dispose() {
            Watchers.DisposeEverything();
        }

        public bool CheckIfEnabled([NotNull] string location) {
            // TODO: What if disabled directory is in enabled (like …/content/cars and …/content/ca)
            return DisabledDirectory == null || !location.StartsWith(DisabledDirectory);
        }
    }
}
