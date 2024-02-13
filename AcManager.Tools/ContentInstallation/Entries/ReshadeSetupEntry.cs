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
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class ReshadeSetupEntry : ContentEntryBase {
        public static string ReshadeFileName = "dxgi.dll";
        public static string ReshadeConfigFileName = "dxgi.ini";
        public static string ReshadeDefaultShaders = "reshade-shaders";

        private readonly List<string> _toInstall;

        public ReshadeSetupEntry([NotNull] string path, string presetName, IEnumerable<string> toInstall)
                : base(true, path, presetName, null, presetName) {
            _toInstall = toInstall.Prepend(ReshadeFileName, ReshadeConfigFileName).ToList();
        }

        public override double Priority => 1000d;

        protected sealed override bool GenericModSupportedByDesign => IsNew;
        public override string GenericModTypeName => "Reshade";
        public override string NewFormat => "Reshade setup {0}";
        public override string ExistingFormat => "Update for Reshade setup {0}";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst, false);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            return new CopyCallback(info => {
                if (string.Equals(info.Key, ReshadeConfigFileName, StringComparison.OrdinalIgnoreCase)) {
                    FileUtils.Recycle(ExistingInstallation().ToArray());
                }

                return _toInstall.Contains(info.Key) || _toInstall.Any(x => FileUtils.IsAffectedBy(info.Key, x))
                        ? Path.Combine(AcRootDirectory.Instance.RequireValue, info.Key) : null;
            });
        }

        private static IEnumerable<string> ExistingInstallation() {
            yield return Path.Combine(AcRootDirectory.Instance.RequireValue, ReshadeFileName);
            yield return Path.Combine(AcRootDirectory.Instance.RequireValue, ReshadeConfigFileName);
            yield return Path.Combine(AcRootDirectory.Instance.RequireValue, ReshadeDefaultShaders);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            if (ExistingInstallation().Any(FileUtils.Exists)) {
                return Task.FromResult(Tuple.Create(Name, (string)null));
            }

            return Task.FromResult<Tuple<string, string>>(null);
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(AcRootDirectory.Instance.RequireValue);
        }
    }
}