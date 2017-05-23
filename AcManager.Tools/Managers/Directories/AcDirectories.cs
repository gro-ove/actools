using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    /// <summary>
    /// Standart version with usual file system watchers.
    /// </summary>
    public class AcDirectories : AcDirectoriesBase {
        public AcDirectories([NotNull] string enabledDirectory, string disabledDirectory) : base(enabledDirectory, disabledDirectory) {}

        public AcDirectories([NotNull] string enabledDirectory) : base(enabledDirectory) {}

        private static readonly List<DirectoryWatcher> Watchers = new List<DirectoryWatcher>();

        private static DirectoryWatcher CreateOrReuseWatcher([NotNull] string directory) {
            var watcher = Watchers.FirstOrDefault(x => x.TargetDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase));
            if (watcher != null) return watcher;

            watcher = new DirectoryWatcher(directory);
            Watchers.Add(watcher);
            return watcher;
        }

        public override void Subscribe(IDirectoryListener listener) {
            Debug.WriteLine($"LISTENER SUBSCRIBED: {GetType()}, {listener.GetType()}");

            CreateOrReuseWatcher(EnabledDirectory).Subscribe(listener);
            if (DisabledDirectory != null) {
                CreateOrReuseWatcher(DisabledDirectory).Subscribe(listener);
            }
        }

        public override void Dispose() {
            Watchers.DisposeEverything();
        }
    }

    /// <summary>
    /// Standart version with usual file system watchers.
    /// </summary>
    public class MultiDirectories : MultiDirectoriesBase {
        public MultiDirectories([NotNull] string enabledDirectory, string disabledDirectory) : base(enabledDirectory, disabledDirectory) {}

        public MultiDirectories([NotNull] string enabledDirectory) : base(enabledDirectory) {}

        private static readonly List<DirectoryWatcher> Watchers = new List<DirectoryWatcher>();

        private static DirectoryWatcher CreateOrReuseWatcher([NotNull] string directory) {
            var watcher = Watchers.FirstOrDefault(x => x.TargetDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase));
            if (watcher != null) return watcher;

            watcher = new DirectoryWatcher(directory);
            Watchers.Add(watcher);
            return watcher;
        }

        public override void Subscribe(IDirectoryListener listener) {
            Debug.WriteLine($"LISTENER SUBSCRIBED: {GetType()}, {listener.GetType()}");

            CreateOrReuseWatcher(EnabledDirectory).Subscribe(listener);
            if (DisabledDirectory != null) {
                CreateOrReuseWatcher(DisabledDirectory).Subscribe(listener);
            }
        }

        public override void Dispose() {
            Watchers.DisposeEverything();
        }
    }
}
