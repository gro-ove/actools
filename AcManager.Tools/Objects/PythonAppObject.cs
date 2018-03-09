using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class PythonAppObject : AcJsonObjectNew, ICupSupportedObject, IDraggable {
        public static readonly string[] VersionSources = { "version.txt", "changelog.txt", "readme.txt", "read me.txt" };
        private static readonly Regex VersionRegex = new Regex(@"^\s{0,4}(?:-\s*)?v?(\d+(?:\.\d+)+)", RegexOptions.Compiled);

        [CanBeNull]
        public static string GetVersion(string fileData) {
            return fileData.Split('\n').Select(x => VersionRegex.Match(x))
                           .Where(x => x.Success).Select(x => x.Groups[1].Value)
                           .Aggregate((string)null, (a, b) => a == null || a.IsVersionOlderThan(b) ? b : a);
        }

        public Lazier<string> AppIcon { get; }
        public Lazier<IReadOnlyList<PythonAppWindow>> Windows { get; }

        public PythonAppObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            AppIcon = Lazier.CreateAsync(TryToFindAppIconAsync);
            Windows = Lazier.CreateAsync(() => Task.Run(() => GetWindows().ToIReadOnlyListIfItIsNot()));
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, "ui", "ui_app.json");
        }

        protected override bool LoadJsonOrThrow() {
            if (!File.Exists(JsonFilename)) {
                ClearData();
                Name =  AcStringValues.NameFromId(Id);
                Changed = true;
                return true;
            }

            return base.LoadJsonOrThrow();
        }

        protected override void LoadVersionInfo(JObject json) {
            base.LoadVersionInfo(json);
            if (Version == null) {
                Version = TryToLoadVersion();
            }
        }

        private IEnumerable<string> GetPythonFilenames() {
            var main = Path.Combine(Location, Id + ".py");
            if (File.Exists(main)) {
                yield return main;
            }

            foreach (var file in Directory.GetFiles(Location, "*.py").Where(x => !FileUtils.ArePathsEqual(x, main))) {
                yield return file;
            }

            foreach (var file in Directory.GetDirectories(Location).SelectMany(x => Directory.GetFiles(x, "*.py"))) {
                yield return file;
            }
        }

        private IEnumerable<PythonAppWindow> GetWindows() {
            return GetPythonFilenames().Select(File.ReadAllText).SelectMany(data =>
                    Regex.Matches(data, @"(?:^|(?<=\n))#\s*[Aa]pp\s+[Ww]indow: .+|\bac\.newApp\s*\(\s*""([^""]+)\s*""\)")
                         .OfType<Match>().Select(x => x.Groups[1].Value).Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new PythonAppWindow(x)));
        }

        [ItemCanBeNull]
        private async Task<string> TryToFindAppIconAsync() {
            const string iconMissing = "_";

            var iconKey = $".AppIcon:{Id}";
            var result = CacheStorage.Get<string>(iconKey);
            if (result != null) {
                return result == iconMissing ? null : result;
            }

            var windows = await Windows.GetValueAsync();
            var icon = windows?.FirstOrDefault(x => string.Equals(x.DisplayName, Name, StringComparison.Ordinal))
                    ?? windows?.FirstOrDefault(x => string.Equals(x.DisplayName, Id, StringComparison.Ordinal))
                            ?? windows?.OrderBy(x => x.DisplayName.Length).FirstOrDefault();
            CacheStorage.Set(iconKey, icon?.IconOff ?? iconMissing);
            return icon?.IconOff;
        }

        /*protected override void LoadOrThrow() {
            Name = Id;
            TryToLoadVersion();
        }*/

        private string TryToLoadVersion() {
            try {
                foreach (var candidate in VersionSources) {
                    var filename = Path.Combine(Location, candidate);
                    if (File.Exists(filename)) {
                        var version = GetVersion(File.ReadAllText(filename));
                        if (version != null) return version;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            return null;
        }

        // public override bool HasData => true;

        /*public override void Save() {
            base.Save();
            if (Name != Id) {
                FileAcManager.RenameAsync(Id, Name, Enabled);
            }
        }*/

        private PythonAppConfigs _configs;
        private DateTime _lastSaved;

        [NotNull]
        public PythonAppConfigs GetAppConfigs() {
            if (_configs == null) {
                _configs = new PythonAppConfigs(Location, () => {
                    // We’re going to keep it in-memory for now

                    /*if (_configs == null) return;

                    foreach (var config in _configs) {
                        config.PropertyChanged -= OnConfigPropertyChanged;
                    }

                    _configs = null;*/
                });

                _configs.ValueChanged += OnConfigsValueChanged;
            }

            return _configs;
        }

        private void SaveConfigs() {
            _lastSaved = DateTime.Now;
            foreach (var config in _configs.Where(x => x.Changed)) {
                config.Save();
            }
        }

        private readonly Busy _configsSaveBusy = new Busy();

        private void OnConfigsValueChanged(object sender, EventArgs e) {
            if (_configs != null) {
                _configsSaveBusy.DoDelay(SaveConfigs, 500);
            } else {
                ((PythonAppConfigs)sender).ValueChanged -= OnConfigsValueChanged;
            }
        }

        public override bool HandleChangedFile(string filename) {
            if (_configs != null && (DateTime.Now - _lastSaved).TotalSeconds > 3d && _configs.HandleChanged(Location, filename)) {
                return true;
            }

            if (VersionSources.ArrayContains(FileUtils.GetPathWithin(filename, Location)?.ToLowerInvariant())) {
                TryToLoadVersion();
                return true;
            }

            return base.HandleChangedFile(filename);
        }

        string ICupSupportedObject.InstalledVersion => Version;
        public CupContentType CupContentType => CupContentType.App;
        public bool IsCupUpdateAvailable => CupClient.Instance.ContainsAnUpdate(CupContentType, Id, Version);
        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance.GetInformation(CupContentType, Id);

        protected override void OnVersionChanged() {
            OnPropertyChanged(nameof(ICupSupportedObject.InstalledVersion));
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        protected override KunosDlcInformation GetDlc() {
            return null;
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.AppTagsList;
        }

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            SaveAsync();
        }

        public const string DraggableFormat = "Data-PythonAppObject";
        string IDraggable.DraggableFormat => DraggableFormat;

        #region Packing
        public override bool CanBePacked() {
            return true;
        }

        public class PythonAppPackerParams : AcCommonObjectPackerParams {}

        private class PythonAppPacker : AcCommonObjectPacker<PythonAppObject, PythonAppPackerParams> {
            protected override string GetBasePath(PythonAppObject t) {
                return $"apps/python/{t.Id}";
            }

            protected override IEnumerable PackOverride(PythonAppObject t) {
                var windows = t.Windows.GetValueAsync().Result;
                if (windows != null) {
                    foreach (var window in windows) {
                        yield return Add(window.IconOn, window.IconOff);
                    }
                }

                yield return Add("*");
            }

            protected override PackedDescription GetDescriptionOverride(PythonAppObject t) {
                return new PackedDescription(t.Id, t.Name,
                    new Dictionary<string, string> {
                        ["Version"] = t.Version,
                        ["Made by"] = t.Author,
                        ["Webpage"] = t.Url,
                    }, PythonAppsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new PythonAppPacker();
        }
        #endregion
    }
}