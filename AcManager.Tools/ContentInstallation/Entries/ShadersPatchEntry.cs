using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class ShadersPatchEntry : ContentEntryBase {
        public static string PatchFileName = "dwrite.dll";
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
                        var configs = Path.Combine(directory, "config");
                        var toRemoval = new[] {
                            Path.Combine(directory, "shaders"),
                            Path.Combine(directory, "lua"),
                            Path.Combine(directory, "tzdata")
                        }.Concat(Directory.Exists(configs) ? Directory.GetFiles(configs, "*.ini") : new string[0]).ToArray();
                        Logging.Write("Recycle:\n\t" + toRemoval.JoinToString("\n\t"));
                        FileUtils.Recycle(toRemoval);
                    }

                    File.WriteAllText(Path.Combine(directory, "version.txt"), Version);
                    first = false;
                }

                return result;
            });
        }

        private static string InstallTo() {
            return AcRootDirectory.Instance.RequireValue;
        }

        private static IEnumerable<string> ExistingInstallation() {
            yield return Path.Combine(InstallTo(), PatchFileName);
            yield return Path.Combine(InstallTo(), PatchDirectoryName);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            if (ExistingInstallation().Any(FileUtils.Exists)) {
                var version = Path.Combine(InstallTo(), PatchDirectoryName, "version.txt");
                return Task.FromResult(Tuple.Create(Name, File.Exists(version) ? File.ReadAllText(version) : null));
            }
            return Task.FromResult<Tuple<string, string>>(null);
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(InstallTo());
        }
    }
}