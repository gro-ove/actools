using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public sealed class WeatherFxControllerData : Displayable, IWithId {
        private static BetterObservableCollection<WeatherFxControllerData> _items = new BetterObservableCollection<WeatherFxControllerData>();

        public static BetterObservableCollection<WeatherFxControllerData> Items {
            get {
                if (!_initialized) {
                    _initialized = true;
                    Initialize();
                }
                return _items;
            }
        }

        private static bool _initialized;

        private static string ControllersDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, PatchHelper.PatchDirectoryName, "weather-controllers");
        }

        private static void Initialize() {
            PatchHelper.Reloaded += (sender, args) => Rescan();
            Rescan();
        }

        private static void Rescan() {
            var oldItems = Items.ToList();
            Items.ReplaceEverythingBy_Direct(Directory.GetDirectories(ControllersDirectory()).Select(x => {
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
            }).NonNull());


            var selected = ValuesStorage.Get<string>(_settingsBaseKey) ?? "default";
            if ((Items.GetByIdOrDefault(selected) ?? Items.FirstOrDefault(x => x.FollowsLauncher)) is WeatherFxControllerData item) {
                item.IsSelectedAsBase = true;
            }
        }

        [CanBeNull]
        public string Author { get; set; }

        [CanBeNull]
        public string Description { get;  set; }

        [CanBeNull]
        public string Version { get;  set; }

        public bool FollowsLauncher { get;  set; }

        private bool _isSelectedAsBase;

        public bool IsSelectedAsBase {
            get => _isSelectedAsBase;
            set => Apply(value, ref _isSelectedAsBase, () => {
                foreach (var item in Items) {
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
                FollowsLauncher = about.GetBool("FOLLOWS_LAUNCHER", true);
            } else {
                DisplayName = fallbackName;
                FollowsLauncher = true;
            }
        }

        public WeatherFxControllerData(string id) {
            Id = id;

            RefreshData();
            _settingsKey = $@".wfx-ctr.{id}";
            _userSettingsFilename = Path.Combine(AcPaths.GetDocumentsCfgDirectory(), PatchHelper.PatchDirectoryName,
                    @"state\lua\wfx_controller", $@"{id}__settings.ini");
            var directory = Path.Combine(ControllersDirectory(), Id);
            Config = new PythonAppConfigs(new PythonAppConfigParams(directory) {
                FilesRelativeDirectory = AcRootDirectory.Instance.RequireValue,
                ScanFunc = d => Directory.GetFiles(d, "settings.ini"),
                ConfigFactory = (p, f) => {
                    try {
                        return PythonAppConfig.Create(p, f, true, _userSettingsFilename, x => x != "ABOUT");
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
                Config.ValueChanged += (sender, args) => busy.DoDelay(OnConfigChange, 200);
            }
        }

        private void OnConfigChange() {
            if (Config == null) return;
            ValuesStorage.Set(_settingsKey, Config.Export());
        }

        public string Id { get; }

        [CanBeNull]
        public PythonAppConfig Config { get; }

        [CanBeNull]
        public string SerializeSettings() {
            return Config?.Export().ToString();
        }

        public void DeserializeSettings(string data) {
            Config?.Import(data);
        }

        public void PublishSettings() {
            if (Config == null) return;
            File.WriteAllText(_userSettingsFilename, Config.Export().ToString());
        }
    }
}