using System;
using System.IO;
using System.Threading;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    internal abstract class TemporaryDirectoryReplacementBase {
        private readonly string _relativeDestination;
        private readonly string _relativeBackup;

        internal TemporaryDirectoryReplacementBase([NotNull] string relativeDestination, string backupPostfix = @"_backup_cm") {
            _relativeDestination = relativeDestination;
            _relativeBackup = _relativeDestination + backupPostfix;
        }

        [NotNull]
        protected abstract string GetAbsolutePath([NotNull] string relative);

        protected bool Apply([NotNull] string source) {
            if (AcRootDirectory.Instance.Value == null || !IsActive(source)) return false;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);

            if (Directory.Exists(destination)) {
                if (Directory.Exists(backup)) {
                    Directory.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                Directory.Move(destination, backup);
            }

            try {
                Apply(source, destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.NotifyBackground("Can’t replace directory", e);
            }

            return true;
        }

        protected virtual bool IsActive(string source) {
            return Directory.Exists(source);
        }

        protected virtual void Apply(string source, string destination) {
            Logging.Debug($"{source} → {destination}");
            FileUtils.HardLinkOrCopyRecursive(source, destination);
        }

        public bool Revert() {
            if (AcRootDirectory.Instance.Value == null) return false;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);

            try {
                if (Directory.Exists(backup)) {
                    for (var i = 0;; i++) {
                        try {
                            if (Directory.Exists(destination)) {
                                Directory.Delete(destination, true);
                            }

                            Directory.Move(backup, destination);
                            return true;
                        } catch (Exception) {
                            if (i > 5) throw;
                            Thread.Sleep(100);
                        }
                    }
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t restore original directory after replacing it with a temporary one", e);
            }

            return false;
        }
    }

    internal abstract class TemporaryFileReplacementBase {
        private readonly string _relativeDestination;
        private readonly bool _allowHardlinks;
        private readonly string _relativeBackup;

        internal TemporaryFileReplacementBase([NotNull] string relativeDestination, string backupPostfix = @"_backup_cm", bool allowHardlinks = true) {
            _relativeDestination = relativeDestination;
            _allowHardlinks = allowHardlinks;
            _relativeBackup = _relativeDestination + backupPostfix;
        }

        [NotNull]
        protected abstract string GetAbsolutePath([NotNull] string relative);

        protected bool Apply([NotNull] string source, bool trackChanges) {
            if (AcRootDirectory.Instance.Value == null || !File.Exists(source)) return false;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);
            if (File.Exists(destination)) {
                if (File.Exists(backup)) {
                    File.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                File.Move(destination, backup);
            }

            try {
                Logging.Debug($"{source} → {destination}");

                if (trackChanges) {
                    File.WriteAllText(destination + "_tc_cm", source);
                }
                
                if (_allowHardlinks) {
                    FileUtils.HardLinkOrCopy(source, destination);
                } else {
                    File.Copy(source, destination);
                }
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.Notify("Can’t replace files", e);
            }

            return true;
        }

        public bool Revert() {
            if (AcRootDirectory.Instance.Value == null) return false;

            try {
                var destination = GetAbsolutePath(_relativeDestination);
                var backup = GetAbsolutePath(_relativeBackup);

                var trackChanges = destination + @"_tc_cm";
                if (File.Exists(trackChanges)) {
                    try {
                        if (new FileInfo(destination).LastWriteTime > new FileInfo(trackChanges).LastWriteTime + TimeSpan.FromSeconds(5d)) {
                            var origin = File.ReadAllText(trackChanges);
                            if (File.Exists(origin)) {
                                FileUtils.Recycle(origin);
                                File.Move(destination, origin);
                            }
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                    FileUtils.TryToDelete(trackChanges);
                }

                if (File.Exists(backup)) {
                    if (File.Exists(destination)) {
                        File.Delete(destination);
                    }

                    File.Move(backup, destination);
                    return true;
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t restore original files after replacing them with temporary ones", e);
            }

            return false;
        }
    }

    internal class TemporaryFileReplacement : TemporaryFileReplacementBase {
        private readonly string _source;

        public TemporaryFileReplacement([CanBeNull] string source, [NotNull] string relativeDestination, string backupPostfix = @"_backup_cm",
                bool allowHardlinks = true) : base(relativeDestination, backupPostfix, allowHardlinks) {
            _source = source;
        }

        public bool Apply(bool trackChanges) {
            return _source != null && Apply(_source, trackChanges);
        }

        protected override string GetAbsolutePath(string relative) {
            return relative;
        }
    }
}