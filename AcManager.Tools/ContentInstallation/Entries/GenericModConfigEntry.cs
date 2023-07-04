using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.GenericMods;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    internal class GenericModConfigEntry : ContentEntryBase {
        public override double Priority => 95d;

        private readonly string _destination;

        public GenericModConfigEntry([NotNull] string path, [NotNull] string id, string description = null)
                : base(true, path, id, id, description: description) {
            _destination = Path.Combine(SettingsHolder.GenericMods.GetModsDirectory(), id);
        }

        private bool _enabled;
        private string[] _dependsOn;

        protected sealed override bool GenericModSupportedByDesign => false;
        public override bool NeedsToBeEnabled => InternalUtils.IsAllRight;
        public override string GenericModTypeName => null;
        public override string NewFormat => "New mod {0}";

        public override string ExistingFormat => _dependsOn != null
                ? $"Update for the mod “{{0}}” (mod plus {_dependsOn.Select(x => $"“{x}”").JoinToReadableString()} will be disabled and re-enabled)"
                : _enabled ? "Update for the mod “{0}” (will be disabled and re-enabled)" : "Update for the mod “{0}”";

        protected override string[] GetFilesToRemoval(string destination) {
            return SelectedOption?.RemoveExisting == true
                    ? new[] { destination }
                    : SelectedOption?.CleanUp?.Invoke(destination)?.ToArray();
        }

        private class GenericModUpdateOption : UpdateOption {
            private readonly string _modName;

            public GenericModUpdateOption(string modName) : base("Install", true) {
                _modName = modName;
                BeforeTask = BeforeAsync;
                AfterTask = AfterAsync;
            }

            private readonly List<string> _disabled = new List<string>();

            private static async Task<GenericModsEnabler> GetEnabler() {
                return await GenericModsEnabler.GetInstanceAsync(
                        AcRootDirectory.Instance.RequireValue,
                        SettingsHolder.GenericMods.GetModsDirectory(),
                        SettingsHolder.GenericMods.UseHardLinks);
            }

            private async Task ForceDisable([CanBeNull] GenericMod mod, CancellationToken token) {
                if (mod == null || !mod.IsEnabled) return;

                var enabler = await GetEnabler();
                if (mod.DependsOn != null) {
                    foreach (var dependancy in mod.DependsOn) {
                        await ForceDisable(enabler.GetByName(dependancy), token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                    }
                }

                await enabler.DisableAsync(mod, cancellation: token).ConfigureAwait(false);
                if (!token.IsCancellationRequested) {
                    _disabled.Add(mod.DisplayName);
                }
            }

            private async Task BeforeAsync(CancellationToken token) {
                _disabled.Clear();
                var enabler = await GetEnabler();
                await ForceDisable(enabler.GetByName(_modName), token).ConfigureAwait(false);
                if (token.IsCancellationRequested) {
                    await AfterAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }

            private async Task AfterAsync(CancellationToken token) {
                var enabler = await GetEnabler();
                enabler.ReloadList();

                // It’s required to process this list backwards: the mod disabled first should be
                // re-enabled last.
                for (var i = _disabled.Count - 1; i >= 0; i--) {
                    var mod = enabler.GetByName(_disabled[i]);
                    if (mod?.IsEnabled == false) {
                        /*await Enabler.EnableAsync(mod, cancellation: token);
                            if (token.IsCancellationRequested) return;*/

                        // Do not consider CancellationToken since re-enabling should be done
                        // either way.
                        await enabler.EnableAsync(mod).ConfigureAwait(false);
                    }
                }
            }
        }

        protected override async Task EnableAfterInstallation(CancellationToken token) {
            var enabler = await GenericModsEnabler.GetInstanceAsync(
                    AcRootDirectory.Instance.RequireValue,
                    SettingsHolder.GenericMods.GetModsDirectory(),
                    SettingsHolder.GenericMods.UseHardLinks);
            enabler.ReloadList();
            var mod = enabler.GetByName(Name);
            if (mod?.IsEnabled == false) {
                await enabler.EnableAsync(mod).ConfigureAwait(false);
            }
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new GenericModUpdateOption(Name);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            return new CopyCallback(info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return Path.Combine(destination, subFilename);
            });
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(_destination);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            if (!Directory.Exists(_destination)) return Task.FromResult((Tuple<string, string>)null);

            var config = new IniFile(Path.Combine(SettingsHolder.GenericMods.GetModsDirectory(), GenericModsEnabler.ConfigFileName));
            _enabled = config["MODS"].GetInt(Name, 0) > 0;
            _dependsOn = config["DEPENDANCIES"].GetNonEmpty(Name)?.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries);
            return Task.FromResult(Directory.Exists(_destination) ? Tuple.Create(Name, (string)null) : null);
        }
    }
}