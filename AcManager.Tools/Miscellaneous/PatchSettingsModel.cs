using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    public class PatchSettingsModel : NotifyPropertyChanged, IDisposable, IUserPresetable {
        public enum PatchMode {
            NoShadersPatch,
            NoConfigs,
            EverythingIsFine
        }

        private static PatchSettingsModel _instance;
        private static int _referenceCount;

        public static PatchSettingsModel Create() {
            if (_instance == null) {
                _instance = new PatchSettingsModel();
                _referenceCount = 1;
            } else {
                ++_referenceCount;
            }
            return _instance;
        }

        private readonly bool _isLive;
        private readonly StoredValue _selectedPageId = Stored.Get("/Patch.SettingsPage.Selected");

        private FileSystemWatcher _watcher;

        public async void Dispose() {
            await Task.Delay(TimeSpan.FromMinutes(1d));
            if (--_referenceCount > 0) return;
            _watcher?.Dispose();
            Configs?.Dispose();
            _instance = null;
        }

        private PatchSettingsModel() {
            try {
                if (PatchHelper.GetInstalledVersion() == null) {
                    _selectedPageId.Value = null;
                }
            } catch (Exception e) {
                Logging.Error(e);
                _selectedPageId.Value = null;
            }

            Pages = new BetterObservableCollection<PatchPage>();
            PagesView = new BetterListCollectionView(Pages);
            PagesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PatchPage.Group)));

            _isLive = false;
            _dir = FileUtils.NormalizePath(Path.Combine(PatchHelper.GetRootDirectory(), "config"));
            CreateConfigs();
        }

        public PatchSettingsModel SetupWatcher() {
            if (_watcher == null) {
                _watcher = new FileSystemWatcher(AcRootDirectory.Instance.RequireValue) {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnWatcherChanged;
                _watcher.Changed += OnWatcherChanged;
                _watcher.Deleted += OnWatcherChanged;
                _watcher.Renamed += OnWatcherRenamed;
            }
            return this;
        }

        public PatchSettingsModel SetupIssues() {
            RescanPossibleIssues();
            return this;
        }

        private AsyncCommand _installPatchCommand;

        public AsyncCommand InstallPatchCommand => _installPatchCommand ?? (_installPatchCommand = new AsyncCommand(async () => {
            await PatchUpdater.Instance.CheckAndUpdateIfNeeded();
            await PatchUpdater.Instance.InstallAsync(PatchUpdater.Instance.LatestRecommendedVersion, default(CancellationToken));
        }, () => Mode != PatchMode.EverythingIsFine));

        private readonly Busy _busyCreateConfigs = new Busy(true);
        private readonly Busy _busyUpdateVersion = new Busy(true);

        private void OnFileSomethingChanged(string filename) {
            if (IsBlocked) return;

            var directory = Path.GetDirectoryName(filename);
            if (directory == null) return;

            directory = FileUtils.NormalizePath(directory);
            if (FileUtils.IsAffectedBy(_dir, filename) || string.Equals(directory, _dir, StringComparison.OrdinalIgnoreCase)) {
                _busyCreateConfigs.DoDelay(() => {
                    if ((DateTime.Now - _lastSaved).TotalSeconds < 3d) return;
                    CreateConfigs();
                    RescanPossibleIssues();
                }, 300);
                if (FileUtils.ArePathsEqual(filename, PatchHelper.GetManifestFilename())) {
                    _busyUpdateVersion.DoDelay(PatchHelper.Reload, 300);
                }
            } else if (Configs != null) {
                _busyCreateConfigs.DoDelay(() => {
                    foreach (var item in Configs
                            .SelectMany(x => x.Sections)
                            .SelectMany(x => x)
                            .OfType<PythonAppConfigPluginValue>()) {
                        if (FileUtils.IsAffectedBy(filename, item.PluginsDirectory)) {
                            item.ReloadPlugins();
                        }
                    }
                }, 300);
            }

            if (FileUtils.ArePathsEqual(directory, AcRootDirectory.Instance.RequireValue)) {
                var name = Path.GetFileName(filename);
                if (string.Equals(name, "dwrite.dll", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "acs.exe", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "acs.pdb", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "changelog.txt", StringComparison.OrdinalIgnoreCase)) {
                    RescanPossibleIssues();
                }
            }
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e) {
            OnFileSomethingChanged(e.FullPath);
        }

        private void OnWatcherRenamed(object sender, RenamedEventArgs e) {
            OnFileSomethingChanged(e.FullPath);
            OnFileSomethingChanged(e.OldFullPath);
        }

        private string _foundIssuesMessage;

        [CanBeNull]
        public string FoundIssuesMessage {
            get => _foundIssuesMessage;
            set => Apply(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), ref _foundIssuesMessage);
        }

        private class FoundIssue {
            public FoundIssue(string title) {
                Title = title;
            }

            public FoundIssue(string title, string solution, string solutionTitle, string solutionDescription) {
                Title = title;
                Solution = solution;
                SolutionTitle = solutionTitle;
                SolutionDescription = solutionDescription;
            }

            public string Title { get; }

            [CanBeNull]
            public string Solution { get; }

            [CanBeNull]
            public string SolutionTitle { get; }

            [CanBeNull]
            public string SolutionDescription { get; }
        }

        private void ApplyIssues(FoundIssue[] msgs) {
            var message = msgs.NonNull().Select(x => {
                var url = BbCodeBlock.EncodeAttribute($"cmd://settingsShadersPatch/fixPatch/{x.Solution}|||{x.SolutionDescription}");
                return x.Solution != null ? $"• {x.Title} ([url={url}]{x.SolutionTitle}[/url])" : $"• {x.Title}";
            }).JoinToString(";\n") + ".";
            FoundIssuesMessage = message == "." ? string.Empty : message;
        }

        private const string RequiredAcVersion = "1.16.3";

        private void RescanPossibleIssues() {
            if (!PatchHelper.OptionPatchSupport || PatchUpdater.Instance == null) return;
            if (PatchUpdater.Instance.NothingAtAll) {
                ApplyIssues(new FoundIssue[0]);
                return;
            }

            var changelogFile = new FileInfo(Path.Combine(AcRootDirectory.Instance.RequireValue, "changelog.txt"));
            var version = changelogFile.Exists ? File.ReadAllLines(changelogFile.FullName)[0] : null;

            var mainFile = new FileInfo(PatchHelper.GetMainFilename());
            var pdbFile = new FileInfo(Path.Combine(AcRootDirectory.Instance.RequireValue, "acs.pdb"));
            var root = PatchHelper.GetRootDirectory();

            ApplyIssues(new[] {
                mainFile.Exists ? null
                        : new FoundIssue("Main patch file “dwrite.dll” is missing in AC root folder", @"reinstallCurrent", "reinstall patch",
                                "Reinstall currently active patch version to fix the problem automatically"),
                !mainFile.Exists || !FileUtils.IsBlocked(mainFile.FullName) ? null
                        : new FoundIssue("Main patch file “dwrite.dll” is blocked as downloaded online", @"unblockPatch", "unblock file",
                                "Delete that mark Windows sets on files downloaded from remote sources, so DLL could work without any issues"),
                Directory.Exists(Path.Combine(root, "config")) ? null
                        : new FoundIssue("Base configs in “extension/config” are missing", @"reinstallCurrent", "reinstall patch",
                                "Reinstall currently active patch version to fix the problem automatically"),
                File.Exists(Path.Combine(root, "shaders.zip")) || Directory.Exists(Path.Combine(root, "shaders", "custom")) ? null
                        : new FoundIssue("Custom shaders pack “extension/shaders.zip” are missing", @"reinstallCurrent", "reinstall patch",
                                "Reinstall currently active patch version to fix the problem automatically"),
                File.Exists(Path.Combine(root, "lua", "ac_common.lua")) ? null
                        : new FoundIssue("Lua utilities in “extension/lua” are missing", @"reinstallCurrent", "reinstall patch",
                                "Reinstall currently active patch version to fix the problem automatically"),
                File.Exists(Path.Combine(root, "tzdata", "europe")) ? null
                        : new FoundIssue("Timezones information in “extension/tzdata” are missing", @"reinstallCurrent", "reinstall patch",
                                "Reinstall currently active patch version to fix the problem automatically"),
                version == null || version == RequiredAcVersion ? null
                        : new FoundIssue($"Assetto Corsa is obsolete (v{RequiredAcVersion} is required)"),
                !SettingsHolder.Drive.Use32BitVersion ? null
                        : new FoundIssue("32-bit AC is not supported", @"switchTo64Bits", "switch AC to 64 bits",
                                "Change AC settings so 64-bit version would be used (the one patch was made for)"),
                pdbFile.Exists ? null
                        : new FoundIssue("Assetto Corsa PDB file is missing", @"getPdbFile", "fix",
                                "Get recommended version of that PDB file"),
                !pdbFile.Exists || pdbFile.Length == 69218304L ? null
                        : new FoundIssue("Assetto Corsa PDB file is invalid", @"getPdbFile", "fix",
                                "Replace PDB file with recommended version of it")
            });
        }

        static PatchSettingsModel() {
            if (!PatchHelper.OptionPatchSupport) return;
            BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/reinstallCurrent"), new AsyncCommand(
                    () => PatchUpdater.Instance.ReinstallCommand?.ExecuteAsync(),
                    () => PatchUpdater.Instance.ReinstallCommand?.CanExecute() == true));
            BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/unblockPatch"), new DelegateCommand(() => {
                FileUtils.Unblock(PatchHelper.GetMainFilename());
                _instance?.RescanPossibleIssues();
            }));
            BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/switchTo64Bits"), new DelegateCommand(() => {
                SettingsHolder.Drive.Use32BitVersion = false;
                _instance?.RescanPossibleIssues();
            }));
            BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/getPdbFile"), new AsyncCommand(async () => {
                using (var waiting = WaitingDialog.Create("Loading data…")) {
                    var data = await CmApiProvider.GetStaticDataBytesAsync("acs_pdb", TimeSpan.FromDays(3d), waiting, waiting.CancellationToken);
                    if (data != null) {
                        using (var stream = new MemoryStream(data))
                        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read)) {
                            var pdbFilename = Path.Combine(AcRootDirectory.Instance.RequireValue, "acs.pdb");
                            zip.GetEntry(@"acs.pdb").ExtractToFile(pdbFilename, true);
                        }
                    }
                }
            }));
        }

        private bool _isBlocked;

        public bool IsBlocked {
            get => _isBlocked;
            set => Apply(value, ref _isBlocked, () => {
                if (!value) {
                    _busyCreateConfigs.Do(() => {
                        CreateConfigs();
                        RescanPossibleIssues();
                    });
                }
            });
        }

        public void OnPatchUpdaterChanged(object sender, PropertyChangedEventArgs e) {
            if (!PatchHelper.OptionPatchSupport) return;
            if (e.PropertyName == nameof(PatchUpdater.InstallationProgress)) {
                IsBlocked = PatchUpdater.Instance.InstallationProgress.Progress != null;
                if (IsBlocked && Configs != null) {
                    SelectedPage = BasePages.GetByIdOrDefault(PageIdInformation);
                }
            }
        }

        private DateTime _lastSaved;

        private void SaveConfigs() {
            if (!Directory.Exists(_dir)) return;
            _lastSaved = DateTime.Now;
            foreach (var config in _configs) {
                config.Save();
            }
        }

        private readonly Busy _configsSaveBusy = new Busy();
        private bool _configsPresetApplying;

        private void OnConfigsValueChanged(object sender, EventArgs e) {
            if (_configsPresetApplying) return;
            _configsSaveBusy.DoDelay(SaveConfigs, 100);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void CreateConfigs() {
            if (Configs != null) {
                Configs.ValueChanged -= OnConfigsValueChanged;
                Configs.Dispose();
            }

            if (!Directory.Exists(_dir)) {
                Mode = PatchMode.NoShadersPatch;
                Configs = null;
                return;
            }

            FileUtils.EnsureDirectoryExists(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension"));

            var selectedPageId = SelectedPage?.Id ?? _selectedPageId.Value;
            Configs = new PythonAppConfigs(new PythonAppConfigParams(_dir) {
                FilesRelativeDirectory = AcRootDirectory.Instance.Value ?? _dir,
                ScanFunc = d => Directory.GetFiles(d, "*.ini").Where(x => !Path.GetFileName(x).StartsWith(@"data_")),
                ConfigFactory = (p, f) => {
                    var fileName = Path.GetFileName(f);
                    if (fileName == null) {
                        return null;
                    }

                    var userEditedFile = Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension", fileName);
                    var cfg = PythonAppConfig.Create(p, f, true, userEditedFile);
                    if (_isLive && cfg.Sections.GetByIdOrDefault("ℹ")?.GetByIdOrDefault("LIVE_SUPPORT")?.Value == @"0") {
                        return null;
                    }

                    return string.IsNullOrWhiteSpace(cfg.ShortDescription) ? null : cfg;
                },
                SaveOnlyNonDefault = true,
                Flags = new Dictionary<string, string> {
                    [@"IS_LIVE__"] = _isLive.As<string>()
                }
            });
            Mode = Configs?.Count > 0 ? PatchMode.EverythingIsFine : PatchMode.NoConfigs;

            var configPages = Configs.Select(x =>
                    Pages.FirstOrDefault(y => y.Config == x) ?? new PatchPage(x));
            Pages.ReplaceEverythingBy(PatchHelper.OptionPatchSupport ? BasePages.Concat(configPages) : configPages);

            SelectedPage = Pages.GetByIdOrDefault(selectedPageId) ?? Pages.FirstOrDefault();
            if (Configs != null) {
                Configs.ValueChanged += OnConfigsValueChanged;
            }

            PagesView.Refresh();
        }

        private readonly string _dir;

        private PatchMode _mode;

        public PatchMode Mode {
            get => _mode;
            set => Apply(value, ref _mode, () => _installPatchCommand?.RaiseCanExecuteChanged());
        }

        public const string PageIdInformation = "information";

        public sealed class PatchPage : Displayable, IWithId {
            public PatchPage([NotNull] string name, [NotNull] string description, [NotNull] Uri source)
                    : this(name, description, source.OriginalString, source) { }

            public PatchPage([NotNull] string name, [NotNull] string description, [NotNull] string pageId, [NotNull] Uri source) {
                DisplayName = name;
                Description = description;
                Id = pageId ?? throw new ArgumentNullException(nameof(pageId));
                Source = source;
                Group = "Patch";
            }

            public PatchPage([NotNull] PythonAppConfig config) {
                if (config == null) {
                    throw new ArgumentNullException(nameof(config));
                }
                DisplayName = config.DisplayName;
                Description = config.ShortDescription;
                Config = config;
                Id = config.Id;
                Group = "Extensions";
            }

            [NotNull]
            public string Group { get; }

            [CanBeNull]
            public string Description { get; }

            [NotNull]
            public string Id { get; }

            [CanBeNull]
            public Uri Source { get; }

            [CanBeNull]
            public PythonAppConfig Config { get; }
        }

        public IReadOnlyList<PatchPage> BasePages { get; } = new[] {
            new PatchPage("About & Updates", "Installed and new versions", PageIdInformation,
                    new Uri("/Pages/ShadersPatch/ShadersInstalledDetails.xaml", UriKind.Relative)),
            new PatchPage("Cars configs", "Lights, extra instruments and much more",
                    new Uri("/Pages/ShadersPatch/ShadersDataCarsConfigs.xaml", UriKind.Relative)),
            new PatchPage("Cars textures", "For tyres to change look with setups",
                    new Uri("/Pages/ShadersPatch/ShadersDataCarsTextures.xaml", UriKind.Relative)),
            new PatchPage("Cars VAO", "Per-vertex ambient occlusion",
                    new Uri("/Pages/ShadersPatch/ShadersDataCarsVao.xaml", UriKind.Relative)),
            new PatchPage("Tracks configs", "Lights and more",
                    new Uri("/Pages/ShadersPatch/ShadersDataTracksConfigs.xaml", UriKind.Relative)),
            new PatchPage("Tracks VAO", "Per-vertex ambient occlusion",
                    new Uri("/Pages/ShadersPatch/ShadersDataTracksVao.xaml", UriKind.Relative)),
            new PatchPage("Backgrounds", "Loading screen",
                    new Uri("/Pages/ShadersPatch/ShadersDataBackgrounds.xaml", UriKind.Relative)),
        };

        public BetterObservableCollection<PatchPage> Pages { get; }

        public BetterListCollectionView PagesView { get; }

        private PatchPage _selectedPage;

        [CanBeNull]
        public PatchPage SelectedPage {
            get => _selectedPage;
            set => Apply(value, ref _selectedPage, () => {
                if (value?.Id != null) {
                    _selectedPageId.Value = value.Id;
                }
            });
        }

        private PythonAppConfigs _configs;

        [CanBeNull]
        public PythonAppConfigs Configs {
            get => _configs;
            set => Apply(value, ref _configs);
        }

        public IUserPresetable Presets => this;

        public void ResetSelectedPage() {
            _selectedPage = null;
            OnPropertyChanged(nameof(SelectedPage));
        }

        bool IUserPresetable.CanBeSaved => true;

        public string PresetableKey => "csp";

        public static PresetsCategory Category { get; } = new PresetsCategory("Custom Shaders Patch Presets", ".ini");

        public PresetsCategory PresetableCategory { get; } = Category;

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            var configs = Configs;
            if (configs == null) return;
            _configsPresetApplying = true;
            foreach (var config in configs) {
                config.ValuesIni().Clear();
            }
            foreach (var section in IniFile.Parse(data)) {
                var pieces = section.Key.Split('/');
                if (pieces.Length != 2) continue;

                var target = configs.FirstOrDefault(x => x.PresetId == pieces[0]);
                if (target == null) continue;

                var targetSection = target.ValuesIni()[pieces[1]];
                foreach (var pair in section.Value) {
                    targetSection.Set(pair.Key, pair.Value);
                }
            }
            foreach (var config in configs) {
                config.ApplyChangesFromIni();
            }
            _configsPresetApplying = false;
            SaveConfigs();
        }

        public string ExportToPresetData() {
            var configs = Configs;
            if (configs == null) return null;
            var ret = new IniFile();
            foreach (var config in configs) {
                foreach (var section in config.Export()) {
                    var target = ret[config.PresetId + "/" + section.Key];
                    foreach (var pair in section.Value) {
                        target.Set(pair.Key, pair.Value);
                    }
                }
            }
            return ret.ToString();
        }
    }
}