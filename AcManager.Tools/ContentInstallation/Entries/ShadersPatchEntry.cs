using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class ShadersPatchEntry : ContentEntryBase {
        public static bool IsBusy { get; private set; }
        public static CancelEventHandler InstallationStart;
        public static EventHandler InstallationEnd;

        public static string PatchDirectoryName = "extension";

        private readonly List<string> _toInstall;

        public ShadersPatchEntry([NotNull] string path, IEnumerable<string> items, [CanBeNull] string version)
                : base(path, "", version: version) {
            _toInstall = items.ToList();
        }

        public override double Priority => 1001d;

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => "Custom Shaders Patch";
        public override string NewFormat => "Custom Shaders Patch";
        public override string ExistingFormat => "Update for Custom Shaders Patch";

        private static readonly UpdateOption CleanOption = new UpdateOption("Clean old configs and shaders first", false);

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption(ToolsStrings.Installator_UpdateEverything, false);
            yield return CleanOption;
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            var first = true;
            var cleanInstall = SelectedOption == CleanOption;

            var args = new CancelEventArgs();
            InstallationStart?.Invoke(null, args);
            if (args.Cancel) {
                throw new InformativeException("Can’t install two things at once");
            }

            var installedLogStream = new MemoryStream();
            var installedLog = new StreamWriter(installedLogStream);
            IsBusy = true;

            return new CopyCallback(info => {
                var filename = info.Key;

                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)
                        || !_toInstall.Contains(info.Key) && !_toInstall.Any(x => FileUtils.IsAffectedBy(info.Key, x))) {
                    return null;
                }

                var relativePath = FileUtils.GetRelativePath(filename, path);
                var result = Path.Combine(InstallTo(), relativePath);

                if (first) {
                    var directory = Path.Combine(InstallTo(), PatchDirectoryName);
                    if (!Directory.Exists(directory)) {
                        Directory.CreateDirectory(directory);
                    } else if (cleanInstall) {
                        PatchVersionInfo.RemovePatch(false).Wait();
                    }

                    FileUtils.TryToDelete(PatchHelper.GetInstalledLog());
                    FileUtils.TryToDelete(Path.Combine(PatchHelper.GetRootDirectory(), "config", "data_manifest.ini"));
                    first = false;

                    installedLog.WriteLine(@"# Generated automatically during last patch installation via Content Manager.");
                    installedLog.WriteLine(@"# Do not edit, unless have to for some reason.");
                    installedLog.WriteLine(@"# This particular log was generated during custom ZIP-file installation.");
                    installedLog.WriteLine(@"version: 0");
                    installedLog.WriteLine(@"build: 0");
                }

                installedLog.WriteLine($@"file: {relativePath}:0:0:0");

                return result;
            }, dispose: () => {
                installedLog.Dispose();
                File.WriteAllBytes(PatchHelper.GetInstalledLog(), installedLogStream.ToArray());
                installedLogStream.Dispose();
                InstallationEnd?.Invoke(null, EventArgs.Empty);
                IsBusy = false;
                PatchHelper.Reload();

                if (PatchUpdater.Instance != null) {
                    PatchUpdater.Instance.ForceVersion.Value = true;
                }
            });
        }

        private static string InstallTo() {
            return AcRootDirectory.Instance.RequireValue;
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var version = PatchHelper.GetInstalledVersion();
            return version != null ? Task.FromResult(Tuple.Create(Name, version)) : Task.FromResult<Tuple<string, string>>(null);
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(InstallTo());
        }
    }
}