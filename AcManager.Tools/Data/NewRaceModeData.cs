using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public sealed class NewRaceModeData : Displayable, IWithId {
        public class Holder {
            public BetterObservableCollection<NewRaceModeData> Items { get; } = new BetterObservableCollection<NewRaceModeData>();

            public Holder() {
                PatchHelper.Reloaded += (sender, args) => _reloadBusy.DoDelay(() => Rescan().Ignore(), 200);
                Rescan().Ignore();
            }

            public bool IsReady { get; private set; }

            private readonly Busy _reloadBusy = new Busy();
            public event EventHandler Reloaded;
            private bool _updating;
            private bool _needsAnotherUpdate;

            private async Task Rescan() {
                if (_updating) {
                    _needsAnotherUpdate = true;
                    return;
                }
                _updating = true;

                try {
                    var oldItems = Items.ToList();
                    var newList = await Task.Run(() => PatchHelper.IsFeatureSupported(PatchHelper.WeatherFxLauncherControlled)
                            ? Directory.GetDirectories(NewModesDirectory()).Select(x => {
                                var id = Path.GetFileName(x);
                                var oldItem = oldItems.GetByIdOrDefault(id);
                                if (oldItem != null) {
                                    oldItem.RefreshData();
                                    return oldItem;
                                }
                                if (File.Exists(Path.Combine(x, "mode.lua"))) {
                                    return new NewRaceModeData(Path.GetFileName(x));
                                }
                                return null;
                            }).NonNull().ToList()
                            : new List<NewRaceModeData>());
                    if (Items.Count > 0 || newList.Count > 0) {
                        Items.ReplaceEverythingBy_Direct(newList);
                    }
                } catch (Exception e) {
                    Logging.Error($"Failed to update new modes: {e}");
                }
                _updating = false;
                IsReady = true;
                if (_needsAnotherUpdate) {
                    _needsAnotherUpdate = false;
                    Rescan().Ignore();
                } else {
                    Reloaded?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private static Holder _instance;

        public static Holder Instance => _instance ?? (_instance = new Holder());

        private static string NewModesDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, PatchHelper.PatchDirectoryName, @"lua\new-modes");
        }

        [CanBeNull]
        public string Author { get; set; }

        [CanBeNull]
        public string Description { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        public NewRaceModeData(string id) {
            Id = id;
            RefreshData();
        }

        private static Func<IPythonAppConfigValueProvider, bool> ParseQuery(string query, out bool fixedValue) {
            if (query?.Length > 1) {
                fixedValue = true;
                return PythonAppConfigValue.CreateDisabledFunc(query, false, x => x);
            }
            fixedValue = query.As(true);
            return null;
        }

        public string Location => Path.Combine(NewModesDirectory(), Id);

        public void RefreshData() {
            var manifest = Path.Combine(Location, "manifest.ini");
            var fallbackName = AcStringValues.NameFromId(Id, true);
            if (File.Exists(manifest)) {
                var about = new IniFile(manifest)["ABOUT"];
                DisplayName = about.GetNonEmpty("NAME", fallbackName);
                Author = about.GetNonEmpty("AUTHOR");
                Version = about.GetNonEmpty("VERSION");
                Description = about.GetNonEmpty("DESCRIPTION");
            } else {
                DisplayName = fallbackName;
            }
        }

        public string GetUserSettingsFilename() {
            return Path.Combine(AcPaths.GetDocumentsCfgDirectory(), PatchHelper.PatchDirectoryName,
                    @"state\lua\new_modes", $@"{Id}__settings.ini");
        }

        public string Id { get; }

        public void PublishSettings(string settings) {
            var filename = GetUserSettingsFilename();
            if (string.IsNullOrEmpty(settings)) {
                FileUtils.TryToDelete(filename);
            } else {
                FileUtils.EnsureFileDirectoryExists(filename);
                File.WriteAllText(filename, settings);
            }
        }

        public BbCodeBlock GetToolTip() {
            if (Description == null) return null;
            return new BbCodeBlock {
                Text = $"Author: [b]{Author ?? "?"}[/b]\nVersion: [b]{Version ?? "?"}[/b]\n{Description}"
            };
        }
    }
}