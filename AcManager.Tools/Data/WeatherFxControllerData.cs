using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed class WeatherFxControllerData : Displayable, IWithId {
        public class Holder {
            public BetterObservableCollection<WeatherFxControllerData> Items { get; } = new BetterObservableCollection<WeatherFxControllerData>();

            public Holder() {
                PatchHelper.Reloaded += (sender, args) => _reloadBusy.DoDelay(() => Rescan().Ignore(), 200);
                Rescan().Ignore();
            }

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
                            ? Directory.GetDirectories(ControllersDirectory()).Select(x => {
                                var id = Path.GetFileName(x);
                                var oldItem = oldItems.GetByIdOrDefault(id);
                                if (oldItem != null) {
                                    oldItem.RefreshData();
                                    return oldItem;
                                }
                                if (File.Exists(Path.Combine(x, "controller.lua"))) {
                                    return new WeatherFxControllerData(Path.GetFileName(x));
                                }
                                return null;
                            }).NonNull().ToList()
                            : new List<WeatherFxControllerData>());
                    if (Items.Count > 0 || newList.Count > 0) {
                        Items.ReplaceEverythingBy_Direct(newList);

                        var selected = ValuesStorage.Get<string>(_settingsBaseKey) ?? @"base";
                        var selectedItem = Items.GetByIdOrDefault(selected) ?? Items.FirstOrDefault(x => x.FollowsSelectedWeather);
                        if (selectedItem != null) {
                            selectedItem.IsSelectedAsBase = true;
                        }
                    }
                } catch (Exception e) {
                    Logging.Error($"Failed to update WeatherFX controllers: {e}");
                }
                _updating = false;
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

        private static string ControllersDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, PatchHelper.PatchDirectoryName, @"weather-controllers");
        }

        [CanBeNull]
        public string Author { get; set; }

        [CanBeNull]
        public string Description { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        public bool FollowsSelectedWeather { get; set; }

        private Func<IPythonAppConfigValueProvider, bool> _followsSelectedTemperatureQuery;
        private Func<IPythonAppConfigValueProvider, bool> _followsSelectedWindQuery;
        private string _nameFormat;

        private bool _followsSelectedTemperature;

        public bool FollowsSelectedTemperature {
            get => _followsSelectedTemperature;
            set => Apply(value, ref _followsSelectedTemperature);
        }

        private bool _followsSelectedWind;

        public bool FollowsSelectedWind {
            get => _followsSelectedWind;
            set => Apply(value, ref _followsSelectedWind);
        }

        private bool _isSelectedAsBase;

        public bool IsSelectedAsBase {
            get => _isSelectedAsBase;
            set => Apply(value, ref _isSelectedAsBase, () => {
                if (!value) return;
                foreach (var item in Instance.Items) {
                    if (item != this) {
                        item.IsSelectedAsBase = false;
                    }
                }
                ValuesStorage.Set(_settingsBaseKey, Id);
            });
        }

        private static string _settingsBaseKey = ".wfx-ctr-base";
        private string _settingsKey;
        private string _userSettingsFilename;

        public WeatherFxControllerData(string id) {
            Id = id;

            _settingsKey = $@".wfx-ctr.{id}";
            _userSettingsFilename = GetUserSettingsFilename(id);
            var directory = Path.Combine(ControllersDirectory(), Id);
            Config = new PythonAppConfigs(new PythonAppConfigParams(directory) {
                FilesRelativeDirectory = AcRootDirectory.Instance.RequireValue,
                ScanFunc = d => Directory.GetFiles(d, "settings.ini"),
                ConfigFactory = (p, f) => {
                    try {
                        return new []{ PythonAppConfig.Create(p, f, true, _userSettingsFilename) };
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return null;
                    }
                },
                SaveOnlyNonDefault = true,
            }).FirstOrDefault();

            if (Config != null) {
                var settings = ValuesStorage.Get<string>(_settingsKey);
                if (!string.IsNullOrEmpty(settings)) {
                    Config.Import(settings);
                }
                var busy = new Busy();
                Config.ValueChanged += (sender, args) => {
                    busy.DoDelay(OnConfigChange, 200);
                    RefreshQueries();
                };
            }

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

        public void RefreshData() {
            var directory = Path.Combine(ControllersDirectory(), Id);
            var manifest = Path.Combine(directory, "manifest.ini");

            var fallbackName = AcStringValues.NameFromId(Id, true);
            if (File.Exists(manifest)) {
                var about = new IniFile(manifest)["ABOUT"];
                DisplayName = about.GetNonEmpty("NAME", fallbackName);
                Author = about.GetNonEmpty("AUTHOR");
                Version = about.GetNonEmpty("VERSION");
                Description = about.GetNonEmpty("DESCRIPTION");
                FollowsSelectedWeather = about.GetBool("FOLLOWS_SELECTED_WEATHER", true);
                _followsSelectedTemperatureQuery = ParseQuery(about.GetNonEmpty("FOLLOWS_SELECTED_TEMPERATURE"), out _followsSelectedTemperature);
                _followsSelectedWindQuery = ParseQuery(about.GetNonEmpty("FOLLOWS_SELECTED_WIND"), out _followsSelectedWind);
                _nameFormat = about.GetNonEmpty("NAME_FORMAT");
                RefreshQueries();
            } else {
                DisplayName = fallbackName;
                FollowsSelectedWeather = true;
                FollowsSelectedTemperature = true;
                _nameFormat = null;
            }
        }

        private void RefreshQueries() {
            if ((_followsSelectedTemperatureQuery != null || _followsSelectedWindQuery != null) && Config != null) {
                var provider = new PythonAppConfigProvider(Config);
                FollowsSelectedTemperature = _followsSelectedTemperatureQuery?.Invoke(provider) != false;
                FollowsSelectedWind = _followsSelectedWindQuery?.Invoke(provider) != false;
            }
            if (_nameFormat != null && Config != null) {
                var provider = new PythonAppConfigProvider(Config);
                DisplayName = Regex.Replace(_nameFormat, @"\{([/\w]+)(~[^?:]*)?(\?[^?:]*)?(:[^?:]*)?\}", x => {
                    if (x.Groups.Count != 5) return @"?";
                    var item = provider.GetItem(x.Groups[1].Value);
                    if (item == null) {
                        return @"?";
                    }
                    bool? hit = null;
                    if (x.Groups[2].Success) {
                        hit = item.Value == x.Groups[2].Value.Substring(1);
                    }
                    if (x.Groups[3].Success && hit == true) {
                        return x.Groups[3].Value.Substring(1);
                    }
                    if (x.Groups[4].Success && hit == false) {
                        return x.Groups[4].Value.Substring(1);
                    }
                    if (hit.HasValue) {
                        return hit == true ? "yes" : "no";
                    }
                    return item.DisplayValueString;
                }, RegexOptions.Compiled).Trim();
            }
        }

        private static string GetUserSettingsFilename(string id) {
            return Path.Combine(AcPaths.GetDocumentsCfgDirectory(), PatchHelper.PatchDirectoryName,
                    @"state\lua\wfx_controller", $@"{id}__settings.ini");
        }

        private void OnConfigChange() {
            if (Config == null) return;
            ValuesStorage.Set(_settingsKey, SerializeSettings());
        }

        public string Id { get; }

        [CanBeNull]
        public PythonAppConfig Config { get; }

        [CanBeNull]
        public string SerializeSettings() {
            return Config?.Serialize();
        }

        public void DeserializeSettings(string data) {
            Config?.Import(data ?? string.Empty);
        }

        public void PublishSettings() {
            if (Config == null) return;
            FileUtils.EnsureFileDirectoryExists(_userSettingsFilename);
            File.WriteAllText(_userSettingsFilename, SerializeSettings() ?? string.Empty);
        }

        public static void PublishGenSettings(string id, string data) {
            Logging.Debug("Storing WeatherFX settings: " + id + ", " + data);
            var filename = GetUserSettingsFilename(id);
            FileUtils.EnsureFileDirectoryExists(filename);
            File.WriteAllText(filename, data);
        }

        public BbCodeBlock GetToolTip() {
            if (Description == null) return null;
            return new BbCodeBlock {
                Text = $"Author: [b]{Author ?? "?"}[/b]\nVersion: [b]{Version ?? "?"}[/b]\n{Description}"
            };
        }
    }
}