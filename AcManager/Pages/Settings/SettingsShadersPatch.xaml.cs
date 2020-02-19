using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls.Helpers;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
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

namespace AcManager.Pages.Settings {
    public partial class SettingsShadersPatch : ILocalKeyBindings {
        public static SettingsShadersPatch Instance { get; private set; }

        public static bool IsCustomShadersPatchInstalled() {
            return Directory.Exists(Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "config"));
        }

        public SettingsShadersPatch() {
            Instance = this;

            PatchHelper.Reload();
            KeyBindingsController = new LocalKeyBindingsController(this);
            /*InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ViewInExplorerCommand.Execute(null);
            }), new KeyGesture(Key.F, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ReloadCommand.Execute(null);
            }), new KeyGesture(Key.R, ModifierKeys.Control)));*/

            InitializeComponent();
            DataContext = new ViewModel(false);
            Model.PropertyChanged += OnModelPropertyChanged;
            SetKeyboardInputs();
            UpdateConfigsTabs();

            ShadersPatchEntry.InstallationStart += OnPatchInstallationStart;
            ShadersPatchEntry.InstallationEnd += OnPatchInstallationEnd;

            if (PatchHelper.OptionPatchSupport) {
                PatchUpdater.Instance.PropertyChanged += OnPatchUpdaterPropertyChanged;
            }

            this.OnActualUnload(() => {
                Model?.Dispose();
                if (PatchHelper.OptionPatchSupport) {
                    PatchUpdater.Instance.PropertyChanged -= OnPatchUpdaterPropertyChanged;
                }
                Instance = null;
            });
        }

        private void OnPatchInstallationStart(object sender, CancelEventArgs e) {
            if (Model != null) {
                if (Model.IsBlocked) {
                    e.Cancel = true;
                }
                Model.IsBlocked = true;
            }
        }

        private void OnPatchInstallationEnd(object sender, EventArgs e) {
            if (Model != null) {
                Model.IsBlocked = false;
            }
        }

        private void OnPatchUpdaterPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PatchUpdater.NothingAtAll)) {
                ActionExtension.InvokeInMainThreadAsync(() => UpdateContentTranslate(true));
            }
            ActionExtension.InvokeInMainThreadAsync(() => Model.OnPatchUpdaterChanged(sender, e));
        }

        private EasingFunctionBase _selectionEasingFunction;

        private void UpdateContentTranslate(bool animated) {
            if (!PatchHelper.OptionPatchSupport) return;
            var width = PatchUpdater.Instance.NothingAtAll ? (GridSplitter.GetWidth() ?? 0d) : 0d;

            if (animated) {
                var easing = _selectionEasingFunction ?? (_selectionEasingFunction = (EasingFunctionBase)FindResource(@"StandardEase"));
                ((TranslateTransform)LinksList.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
                ((TranslateTransform)GridSplitter.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
                ((TranslateTransform)ContentCell.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width / 3.5, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
            } else {
                ((TranslateTransform)LinksList.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)LinksList.RenderTransform).X = -width;
                ((TranslateTransform)GridSplitter.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)GridSplitter.RenderTransform).X = -width;
                ((TranslateTransform)ContentCell.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)ContentCell.RenderTransform).X = -width / 3.5;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            UpdateContentTranslate(false);
        }

        private void OnSplitterMoved(object sender, ModernTabSplitter.MovedEventArgs e) {
            UpdateContentTranslate(false);
        }

        private void SetKeyboardInputs() {
            KeyBindingsController.Set(Model.SelectedPage?.Config?.Sections.SelectMany().OfType<PythonAppConfigKeyValue>());
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.SelectedPage)) {
                SetKeyboardInputs();
                UpdateConfigsTabs();
            }
        }

        private void UpdateConfigsTabs() {
            try {
                ConfigTab.Content = Model.SelectedPage?.Config;
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public enum Mode {
            NoShadersPatch,
            NoConfigs,
            EverythingIsFine
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private readonly bool _isLive;
            private readonly StoredValue _selectedPageId = Stored.Get("/Patch.SettingsPage.Selected");

            private FileSystemWatcher _watcher;

            public ViewModel(bool isLive) {
                Logging.Here();

                try {
                    if (PatchHelper.GetInstalledVersion() == null) {
                        _selectedPageId.Value = null;
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    _selectedPageId.Value = null;
                }

                Logging.Debug(AcRootDirectory.Instance.Value);
                Logging.Debug(PatchHelper.GetRootDirectory());
                Logging.Debug(FileUtils.NormalizePath(Path.Combine(PatchHelper.GetRootDirectory(), "config")));

                Pages = new BetterObservableCollection<PatchPage>();
                PagesView = new BetterListCollectionView(Pages);
                PagesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PatchPage.Group)));

                Logging.Here();

                _isLive = isLive;
                _dir = FileUtils.NormalizePath(Path.Combine(PatchHelper.GetRootDirectory(), "config"));
                Logging.Debug(_dir);

                _watcher = new FileSystemWatcher(AcRootDirectory.Instance.RequireValue) {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                Logging.Here();

                _watcher.Created += OnWatcherChanged;
                _watcher.Changed += OnWatcherChanged;
                _watcher.Deleted += OnWatcherChanged;
                _watcher.Renamed += OnWatcherRenamed;
                Logging.Debug(_watcher);

                CreateConfigs();
                RescanPossibleIssues();
            }

            private AsyncCommand _installPatchCommand;

            public AsyncCommand InstallPatchCommand => _installPatchCommand ?? (_installPatchCommand = new AsyncCommand(async () => {
                await PatchUpdater.Instance.CheckAndUpdateIfNeeded();
                await PatchUpdater.Instance.InstallAsync(PatchUpdater.Instance.LatestRecommendedVersion, default(CancellationToken));
            }, () => Mode != Mode.EverythingIsFine));

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
                if (!PatchHelper.OptionPatchSupport) return;
                if (PatchUpdater.Instance.NothingAtAll) {
                    ApplyIssues(new FoundIssue[0]);
                    return;
                }

                var changelogFile = new FileInfo(Path.Combine(AcRootDirectory.Instance.RequireValue, "changelog.txt"));
                string version;
                if (changelogFile.Exists) {
                    version = File.ReadAllLines(changelogFile.FullName)[0];
                } else {
                    version = null;
                }

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

            static ViewModel() {
                if (!PatchHelper.OptionPatchSupport) return;
                BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/reinstallCurrent"), PatchUpdater.Instance.ReinstallCommand);
                BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/unblockPatch"), new DelegateCommand(() => {
                    FileUtils.Unblock(PatchHelper.GetMainFilename());
                    Instance?.Model?.RescanPossibleIssues();
                }));
                BbCodeBlock.AddLinkCommand(new Uri("cmd://settingsShadersPatch/fixPatch/switchTo64Bits"), new DelegateCommand(() => {
                    SettingsHolder.Drive.Use32BitVersion = false;
                    Logging.Debug(Instance);
                    Logging.Debug(Instance?.Model);
                    Instance?.Model?.RescanPossibleIssues();
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

            private void OnConfigsValueChanged(object sender, EventArgs e) {
                _configsSaveBusy.DoDelay(SaveConfigs, 100);
            }

            private void CreateConfigs() {
                if (Configs != null) {
                    Configs.ValueChanged -= OnConfigsValueChanged;
                    Configs.Dispose();
                }

                if (!Directory.Exists(_dir)) {
                    Mode = Mode.NoShadersPatch;
                    Configs = null;
                    return;
                }

                FileUtils.EnsureDirectoryExists(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension"));
                var anyConfigFound = false;

                Configs = new PythonAppConfigs(new PythonAppConfigParams(_dir) {
                    FilesRelativeDirectory = AcRootDirectory.Instance.Value ?? _dir,
                    ScanFunc = d => Directory.GetFiles(d, "*.ini").Where(x => !Path.GetFileName(x).StartsWith(@"data_")),
                    ConfigFactory = (p, f) => {
                        var fileName = Path.GetFileName(f);
                        if (fileName == null) return null;
                        anyConfigFound = true;
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

                Mode = Configs?.Count > 0 ? Mode.EverythingIsFine : Mode.NoConfigs;
                Pages.ReplaceEverythingBy_Direct(PatchHelper.OptionPatchSupport
                        ? BasePages.Concat(Configs.Select(x => new PatchPage(x)))
                        : Configs.Select(x => new PatchPage(x)));

                SelectedPage = Pages?.GetByIdOrDefault(_selectedPageId.Value) ?? Pages?.FirstOrDefault();
                if (Configs != null) {
                    Configs.ValueChanged += OnConfigsValueChanged;
                }

                PagesView.Refresh();
            }

            private readonly string _dir;
            // private readonly IDisposable _patchDirectoryWatcher;
            // private readonly IDisposable _configDirectoryWatcher;
            // private readonly IDisposable _dwriteWatcher;

            private Mode _mode;

            public Mode Mode {
                get => _mode;
                set => Apply(value, ref _mode, () => {
                    _installPatchCommand?.RaiseCanExecuteChanged();
                });
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

            public void Dispose() {
                /* _patchDirectoryWatcher?.Dispose();
                _configDirectoryWatcher?.Dispose();
                _dwriteWatcher?.Dispose(); */
                _watcher?.Dispose();
                Configs?.Dispose();
            }
        }

        public LocalKeyBindingsController KeyBindingsController { get; }

        public static ICommand GetShowSettingsCommand() {
            return new AsyncCommand(() => {
                var dlg = new ModernDialog {
                    ShowTitle = false,
                    Content = new SettingsShadersPatchPopup(),
                    MinHeight = 400,
                    MinWidth = 450,
                    MaxHeight = 99999,
                    MaxWidth = 700,
                    Padding = new Thickness(0),
                    ButtonsMargin = new Thickness(8),
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    BlurBackground = true,
                    ShowTopBlob = false,
                    Topmost = true,
                    Title = "Custom Shaders Patch settings",
                    LocationAndSizeKey = @".CustomShadersPatchDialog",
                    Owner = null,
                    Buttons = new Control[0],
                    BorderThickness = new Thickness(0),
                    Opacity = 0.9,
                    BorderBrush = new SolidColorBrush(Colors.Transparent)
                };

                dlg.Background = new SolidColorBrush(((Color)dlg.FindResource("WindowBackgroundColor")).SetAlpha(200));

                return dlg.ShowAndWaitAsync();
            });
        }
    }
}