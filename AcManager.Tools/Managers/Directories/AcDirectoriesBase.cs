using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    /// <summary>
    /// Basic version with only an abstract concept of watching.
    /// </summary>
    public abstract class AcDirectoriesBase : IAcDirectories {
        protected readonly string EnabledDirectory;
        protected readonly string DisabledDirectory;

        public bool Actual { get; private set; }

        protected AcDirectoriesBase([NotNull] string enabledDirectory, [CanBeNull] string disabledDirectory) {
            EnabledDirectory = enabledDirectory ?? throw new ArgumentNullException(nameof(enabledDirectory));
            DisabledDirectory = disabledDirectory;
            Actual = true;
        }

        protected AcDirectoriesBase([NotNull] string enabledDirectory) : this(enabledDirectory, enabledDirectory + @"-off") {}

        public void CreateIfMissing() {
            Directory.CreateDirectory(EnabledDirectory);
        }

        public string GetId(string location) {
            var name = Path.GetFileName(location);
            if (name == null) throw new Exception(ToolsStrings.AcObject_CannotGetId);
            return name;
        }

        public string GetLocation(string id, bool enabled) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (!Actual) throw new Exception(ToolsStrings.AcObject_NotValidAnymore);
            if (DisabledDirectory == null && !enabled) throw new Exception(ToolsStrings.AcObject_CannotBeDisabled);
            return Path.Combine(enabled ? EnabledDirectory : DisabledDirectory, id);
        }

        public IEnumerable<string> GetContent(Func<string, string[]> contentFromDirectoriesSelector) {
            if (!Actual) throw new Exception(ToolsStrings.AcObject_NotValidAnymore);

            List<string> enabled;
            var enabledDirectory = EnabledDirectory;
            if (Directory.Exists(enabledDirectory)) {
                var list = contentFromDirectoriesSelector(enabledDirectory);
                enabled = new List<string>(list.Length);
                foreach (var dir in list) {
                    enabled.Add(Path.GetFileName(dir)?.ToLowerInvariant());
                    yield return dir;
                }
            } else {
                enabled = null;
            }

            var disabledDirectory = DisabledDirectory;
            if (disabledDirectory == null || !Directory.Exists(disabledDirectory)) yield break;

            List<string> sameIds = null;
            foreach (var dir in contentFromDirectoriesSelector(disabledDirectory)) {
                if (enabled != null) {
                    var id = Path.GetFileName(dir)?.ToLowerInvariant();
                    if (id != null && enabled.Contains(id)) {
                        if (sameIds == null) {
                            sameIds = new List<string> { id };
                        } else {
                            sameIds.Add(id);
                        }
                    }
                }

                yield return dir;
            }

            if (sameIds != null) {
                NonfatalError.Notify("There are entries with the same file name",
                        $@"Content Manager handles all sorts of content by their file/directory names. And, because disabled content is being moved to a separate folder (with “-off”-suffix), sometimes several entries might share the same names. This might cause CM to work very wrongly.

So, please, open “{disabledDirectory}” and either remove or rename {sameIds.Select(x => $"“{x}”").JoinToReadableString()}. Then, restart CM.");
            }
        }

        public void Obsolete() {
            Actual = false;
        }

        public string GetLocationByFilename(string filename, out bool inner) {
            var enabledDirectory = EnabledDirectory;
            var disabledDirectory = DisabledDirectory;
            var minLength = Math.Min(enabledDirectory.Length,
                    disabledDirectory?.Length ?? int.MaxValue);

            inner = false;
            while (filename.Length > minLength) {
                var parent = Path.GetDirectoryName(filename);
                if (parent == null) return null;

                if (parent == enabledDirectory || parent == disabledDirectory) {
                    return filename;
                }

                inner = true;
                filename = parent;
            }

            return null;
        }

        public string GetUniqueId(string preferredId, string postfix, bool forcePostfix, int startFrom) {
            var id = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(EnabledDirectory, preferredId), postfix, forcePostfix, startFrom));

            var disabledDirectory = DisabledDirectory;
            if (disabledDirectory != null) {
                id = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(disabledDirectory, id), postfix, forcePostfix, startFrom));
            }

            return id;
        }

        public abstract void Subscribe(IDirectoryListener listener);

        public abstract void Dispose();

        public bool CheckIfEnabled(string location) {
            return DisabledDirectory == null || FileUtils.IsAffected(EnabledDirectory, location);
        }
    }

    public abstract class MultiDirectoriesBase : IAcDirectories {
        [NotNull]
        protected readonly string EnabledDirectory;

        [CanBeNull]
        protected readonly string DisabledDirectory;

        public bool Actual { get; }

        public MultiDirectoriesBase([NotNull] string enabledDirectory, [CanBeNull] string disabledDirectory) {
            EnabledDirectory = enabledDirectory ?? throw new ArgumentNullException(nameof(enabledDirectory));
            DisabledDirectory = disabledDirectory;

            if (DisabledDirectory != null) {
                throw new NotImplementedException();
            }

            Actual = true;
        }

        protected MultiDirectoriesBase([NotNull] string enabledDirectory) : this(enabledDirectory, enabledDirectory + @"-off") {}

        public IEnumerable<string> GetContent(Func<string, string[]> contentFromDirectoriesSelector) {
            foreach (var subDirectory in Directory.GetDirectories(EnabledDirectory, "*", SearchOption.AllDirectories)) {
                foreach (var sub in contentFromDirectoriesSelector(subDirectory)) {
                    yield return sub;
                }
            }

            foreach (var sub in contentFromDirectoriesSelector(EnabledDirectory)) {
                yield return sub;
            }

            if (DisabledDirectory != null) {
                throw new NotImplementedException();
            }
        }

        public string GetId(string location) {
            return FileUtils.GetRelativePath(location, EnabledDirectory);
        }

        public string GetLocation(string id, bool enabled) {
            return Path.Combine(EnabledDirectory, id);
        }

        public bool CheckIfEnabled(string location) {
            return true;
        }

        public string GetLocationByFilename(string filename, out bool inner) {
            inner = false;
            return filename;
        }

        public string GetUniqueId(string preferredId, string postfix = "-{0}", bool forcePostfix = false, int startFrom = 1) {
            return Path.GetFileName(FileUtils.EnsureUnique(GetLocation(preferredId, true), postfix, forcePostfix, startFrom));
        }

        public abstract void Subscribe(IDirectoryListener listener);

        public abstract void Dispose();
    }
}