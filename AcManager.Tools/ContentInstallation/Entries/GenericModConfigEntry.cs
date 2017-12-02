using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.GenericMods;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    internal class GenericModConfigEntry : ContentEntryBase {
        private readonly string _destination;

        public GenericModConfigEntry([NotNull] string path, [NotNull] string id, string description = null)
                : base(path, id, id, description: description) {
            _destination = Path.Combine(SettingsHolder.GenericMods.GetModsDirectory(), id);
        }

        private bool _enabled;
        private string[] _dependsOn;

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => null;
        public override string NewFormat => "New mod {0}";
        public override string ExistingFormat => _dependsOn != null
                ? $"Update for the mod “{{0}}” (mod plus {_dependsOn.Select(x => $"“{x}”").JoinToReadableString()} will be disabled and re-enabled)"
                : _enabled ? "Update for the mod “{0}” (will be disabled and re-enabled)" : "Update for the mod “{0}”";

        private class GenericModUpdateOption : UpdateOption {
            private readonly string _modName;

            private GenericModsEnabler _enabler;

            private GenericModsEnabler Enabler => _enabler ?? (_enabler =
                    new GenericModsEnabler(AcRootDirectory.Instance.RequireValue, SettingsHolder.GenericMods.GetModsDirectory(),
                            SettingsHolder.GenericMods.UseHardLinks));

            public GenericModUpdateOption(string modName) : base("Install") {
                _modName = modName;
                RemoveExisting = true;

                BeforeTask = BeforeAsync;
                AfterTask = AfterAsync;
            }

            private readonly List<string> _disabled = new List<string>();

            private async Task ForceDisable([CanBeNull] GenericMod mod, CancellationToken token) {
                if (mod == null || !mod.IsEnabled) return;

                if (mod.DependsOn != null) {
                    foreach (var dependancy in mod.DependsOn) {
                        await ForceDisable(Enabler.GetByName(dependancy), token);
                        if (token.IsCancellationRequested) return;
                    }
                }

                await Enabler.DisableAsync(mod, cancellation: token);
                if (!token.IsCancellationRequested) {
                    _disabled.Add(mod.DisplayName);
                }
            }

            private async Task BeforeAsync(CancellationToken token) {
                try {
                    _disabled.Clear();
                    await ForceDisable(Enabler.GetByName(_modName), token);
                    if (token.IsCancellationRequested) {
                        await AfterAsync(CancellationToken.None);
                    }
                } finally {
                    DisposeHelper.Dispose(ref _enabler);
                }
            }

            private async Task AfterAsync(CancellationToken token) {
                try {
                    // It’s required to process this list backwards: the mod disabled first should be
                    // re-enabled last.
                    for (var i = _disabled.Count - 1; i >= 0; i--) {
                        var mod = Enabler.GetByName(_disabled[i]);
                        if (mod?.IsEnabled == false) {
                            /*await Enabler.EnableAsync(mod, cancellation: token);
                            if (token.IsCancellationRequested) return;*/

                            // Do not consider CancellationToken since re-enabling should be done in
                            // either way.
                            await Enabler.EnableAsync(mod);
                        }
                    }
                } finally {
                    DisposeHelper.Dispose(ref _enabler);
                }
            }
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new GenericModUpdateOption(Name);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            return new CopyCallback(info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.Affects(path, filename)) return null;

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