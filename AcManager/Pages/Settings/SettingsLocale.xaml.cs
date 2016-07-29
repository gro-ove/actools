using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsLocale {
        public ViewModel Model => (ViewModel)DataContext;

        public SettingsLocale() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;

            Model.OnLoaded();
            _loaded = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;

            Model.OnUnloaded();
            _loaded = false;
        }

        public sealed class LocaleEntry : Displayable {
            public string Id { get; }

            public string Version { get; }
            
            public double Coverity { get; }

            public long Size { get; }

            public bool IsSupported { get; }

            public bool CanBeUpdated { get; }

            private bool _isInstalled;

            public bool IsInstalled {
                get { return _isInstalled; }
                set {
                    if (Equals(value, _isInstalled)) return;
                    _isInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsInstalled));
                }
            }

            public string DisplayHint => Id == null ? "Set locale by its ID" : IsSupported ? "Officially supported" :
                    !IsInstalled ? $"{Size.ToReadableSize()} ({Coverity * 100:F1}%)" : Equals(Coverity, 1d) ? "Installed" :
                            string.Format("{0} ({1:F1}%)", "Installed", Coverity * 100);

            public LocaleEntry([Localizable(false)] string id, string version, double coverity = 1d, long size = 0L) {
                Id = id;
                Version = version;
                Coverity = coverity;
                Size = size;
                IsSupported = LocaleHelper.IsSupported(id);

                CanBeUpdated = id != null && !Equals(coverity, 1d);

                var name = id == null ? "Custom" : new CultureInfo(id).NativeName.ToTitle();
                DisplayName = name;
            }
        }

        private class LocaleComparer : IEqualityComparer<LocaleEntry> {
            public bool Equals(LocaleEntry x, LocaleEntry y) {
                return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(LocaleEntry obj) {
                return obj.Id?.ToLowerInvariant().GetHashCode() ?? 0;
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public bool ForceLocalesFlag { get; } = AppArguments.Has(AppFlag.ForceLocale);

            public BetterObservableCollection<LocaleEntry> Locales { get; }

            private LocaleEntry _currentLocale;

            public LocaleEntry CurrentLocale {
                get { return _currentLocale; }
                set {
                    if (Equals(value, _currentLocale)) return;
                    var prev = _currentLocale;
                    _currentLocale = value;
                    OnPropertyChanged();

                    if (prev?.Id == null || value?.Id == null) {
                        OnPropertyChanged(nameof(CustomLocale));
                    }

                    if (value?.Id != null) {
                        Locale.LocaleName = value.Id;
                    }

                    LocaleUpdater.InstalledVersion = value?.Version;
                    LocaleUpdater.CheckAndUpdateIfNeededCommand.Execute(null);
                }
            }
            
            public bool CustomLocale => CurrentLocale.Id == null;

            internal ViewModel() {
                Locales = new BetterObservableCollection<LocaleEntry>(LoadLocal().Distinct(new LocaleComparer()));
                LoadCurrentLocale();
                LoadOtherLocales();
            }

            public void OnLoaded() {
                Locale.PropertyChanged += Locale_PropertyChanged;
            }

            public void OnUnloaded() {
                Locale.PropertyChanged -= Locale_PropertyChanged;
            }

            private void Locale_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Locale.LocaleName):
                        LoadCurrentLocale();
                        CommandManager.InvalidateRequerySuggested();
                        break;

                    case nameof(Locale.LoadUnpacked):
                        ReloadLocal();
                        break;
                }
            }

            private void ReloadLocal() {
                Locales.ReplaceEverythingBy(LoadLocal().Distinct(new LocaleComparer()));
                LoadCurrentLocale();
                LoadOtherLocales();
            }

            private void Reload() {
                _online = null;
                ReloadLocal();
            }

            private IEnumerable<LocaleEntry> LoadLocal() {
                yield return new LocaleEntry("en", null);

                var locales = FilesStorage.Instance.GetDirectory("Locales");
                if (Directory.Exists(locales)) {
                    foreach (var manifest in Directory.GetFiles(locales, "*.pak")
                            .Select(LocalePackageManifest.FromPackage).NonNull()) {
                        yield return new LocaleEntry(manifest.Id, manifest.Version, manifest.Coverity) {
                            IsInstalled = true
                        };
                    }

                    if (Locale.LoadUnpacked) {
                        var regex = new Regex(@"^[a-z]{2}(?:-[a-z]{2,5})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        foreach (var id in Directory.GetDirectories(locales)
                                                    .Select(Path.GetFileNameWithoutExtension)
                                                    .Where(x => regex.IsMatch(x))) {
                            yield return new LocaleEntry(id, null) {
                                IsInstalled = true
                            };
                        }
                    }
                }

                yield return new LocaleEntry(null, null);
            }

            private void LoadCurrentLocale() {
                CurrentLocale = Locales.FirstOrDefault(x => x.Id == SettingsHolder.Locale.LocaleName);
                if (CurrentLocale == null) {
                    CurrentLocale = Locales.Last();
                }
            }

            private LocalePackageManifest[] _online;
            private async void LoadOtherLocales() {
                _online = _online ?? await CmApiProvider.GetAsync<LocalePackageManifest[]>("locales/list");
                if (_online == null) return;

                foreach (var entry in _online) {
                    if (Locales.Any(x => string.Equals(x.Id, entry.Id, StringComparison.OrdinalIgnoreCase))) continue;
                    // TODO: check if there is an update
                    Locales.Insert(Locales.Count - 1, new LocaleEntry(entry.Id, entry.Version, entry.Coverity, entry.Size));
                }

                LoadCurrentLocale();
            }

            public SettingsHolder.CommonSettings Common => SettingsHolder.Common;

            public SettingsHolder.LocaleSettings Locale => SettingsHolder.Locale;

            public LocaleUpdater LocaleUpdater => LocaleUpdater.Instance;

            private RelayCommand _addReplaysExtensionsCommand;

            public RelayCommand AddReplaysExtensionsCommand => _addReplaysExtensionsCommand ?? (_addReplaysExtensionsCommand = new RelayCommand(o => {
                ReplaysExtensionSetter.RenameAll();
            }, o => ReplaysExtensionSetter.HasWithoutExtension()));

            private ICommand _prepareCustomCommand;

            public ICommand PrepareUnpackedCommand => _prepareCustomCommand ?? (_prepareCustomCommand = new AsyncCommand(async o => {
                var localeName = Prompt.Show(
                        "What locale are you going to work with (you can see some of them [url=\"https://msdn.microsoft.com/en-us/library/ms533052(v=vs.85).aspx\"]here[/url])? Enter it (but you can always change it later):",
                        "Locale ID", SettingsHolder.Locale.LocaleName, "?", "You can use some country-specific locale as well as just language-specific",
                        maxLength: 7);
                if (localeName == null) return;

                string destination;
                try {
                    using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                        destination = await LocaleUpdater.Instance.InstallCustom(localeName, waiting, waiting.CancellationToken);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.AdditionalContent_CannotUnpack, e);
                    return;
                }

                if (destination == null) {
                    NonfatalError.Notify("Can’t load base package for translation", ToolsStrings.Common_MakeSureInternetWorks);
                } else {
                    Locale.LoadUnpacked = true;
                    Locale.LocaleName = localeName;
                    ReloadLocal();
                    WindowsHelper.ViewDirectory(destination);
                }
            }));

            private ICommand _submitUnpackedCommand;

            public ICommand SubmitUnpackedCommand => _submitUnpackedCommand ?? (_submitUnpackedCommand = new AsyncCommand(async o => {
                var directory = FilesStorage.Instance.CombineFilename("Locales", Locale.LocaleName);
                if (!Directory.Exists(directory)) return;

                if (SteamIdHelper.Instance.Value == null || SteamIdHelper.Instance.Value.Length < 5) {
                    NonfatalError.Notify("Can't send unpacked locale",
                            "Please, try another way, like, for example, through [url=\"mailto:cm-support@assettocorsa.club\"]e-mail[/url].");
                    return;
                }


                try {
                    var message = Prompt.Show(
                            "You’re going to send an unpacked locale to developers. Thanks in advance!\n\nWould you like to add some notes? Maybe your name for About page?",
                            "Additional Noted", watermark: @"?", multiline: true);
                    if (message == null) return;
                    await Task.Run(() => AppReporter.SendUnpackedLocale(directory, message));
                    Toast.Show("Locale Sent", AppStrings.About_ReportAnIssue_Sent_Message);
                } catch (Exception e) {
                    NonfatalError.Notify("Can't send unpacked locale",
                            "Please, try another way, like, for example, through [url=\"mailto:cm-support@assettocorsa.club\"]e-mail[/url].", e);
                }
            }, o => Directory.Exists(FilesStorage.Instance.CombineFilename("Locales", Locale.LocaleName)), 3000));

            private ICommand _restartCommand;

            public ICommand RestartCommand => _restartCommand ?? (_restartCommand = new RelayCommand(o => {
                WindowsHelper.RestartCurrentApplication();
            }));

            private RelayCommand _navigateCommand;

            public RelayCommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new RelayCommand(o => {
                WindowsHelper.ViewInBrowser(o?.ToString());
            }));
        }
    }
}
