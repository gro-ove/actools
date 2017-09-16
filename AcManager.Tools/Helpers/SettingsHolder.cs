using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web;
using System.Windows.Forms;
using System.Windows.Threading;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

// ReSharper disable RedundantArgumentDefaultValue

namespace AcManager.Tools.Helpers {
    public enum TemperatureUnitMode {
        [Description("Celsius")]
        Celsius,

        [Description("Fahrenheit")]
        Fahrenheit,

        [Description("Celsius and Fahrenheit")]
        Both
    }

    public static class SettingsHolder {
        public sealed class PeriodEntry : Displayable {
            public TimeSpan TimeSpan { get; }

            public PeriodEntry() { }

            public PeriodEntry(TimeSpan timeSpan, string displayName = null) {
                TimeSpan = timeSpan;
                DisplayName = displayName ?? (timeSpan == TimeSpan.Zero ? ToolsStrings.Common_Disabled :
                        timeSpan == TimeSpan.MaxValue ? ToolsStrings.Settings_Period_OnOpening :
                                string.Format(ToolsStrings.Settings_PeriodFormat, timeSpan.ToReadableTime()));
            }

            public PeriodEntry(string displayName) {
                TimeSpan = TimeSpan.MaxValue;
                DisplayName = displayName;
            }
        }

        public sealed class DelayEntry : Displayable {
            public TimeSpan TimeSpan { get; }

            public DelayEntry() { }

            public DelayEntry(TimeSpan timeSpan, string displayName = null) {
                TimeSpan = timeSpan;
                DisplayName = displayName ?? (timeSpan == TimeSpan.Zero ? ToolsStrings.Common_Disabled :
                        timeSpan.ToReadableTime());
            }
        }

        public sealed class SearchEngineEntry : Displayable {
            public string Value { get; }

            public SearchEngineEntry(string name, string value) {
                DisplayName = name;
                Value = value;
            }

            public string GetUri(string s, bool allowWikipedia) {
                if (Content.SearchWithWikipedia && allowWikipedia) {
                    s = @"site:wikipedia.org " + s;
                }

                return string.Format(Value, s.UriEscape(true));
            }
        }

        public enum MissingContentType {
            Car,
            Track
        }

        public delegate string MissingContentUrlFunc(MissingContentType type, [NotNull] string id);

        public sealed class MissingContentSearchEntry : Displayable {
            public MissingContentUrlFunc Func { get; }

            public MissingContentSearchEntry(string name, MissingContentUrlFunc func, bool viaSearchEngine) {
                DisplayName = name;
                Func = func;
                ViaSearchEngine = viaSearchEngine;
            }

            public bool ViaSearchEngine { get; }

            public string GetUri([NotNull] string id, MissingContentType type) {
                var value = Func(type, id);
                return ViaSearchEngine ? Content.SearchEngine.GetUri(value, false) : value;
            }
        }

        public class OnlineServerEntry {
            private string _displayName;

            public string DisplayName => _displayName ?? (_displayName = (Id + 1).ToOrdinal(ToolsStrings.OrdinalizingSubject_Server));

            public int Id { get; internal set; }
        }

        public class OnlineSettings : NotifyPropertyChanged {
            internal OnlineSettings() { }

            private PeriodEntry[] _refreshPeriods;

            public PeriodEntry[] RefreshPeriods => _refreshPeriods ?? (_refreshPeriods = new[] {
                new PeriodEntry(TimeSpan.Zero),
                new PeriodEntry(ToolsStrings.Settings_Period_OnOpening),
                new PeriodEntry(TimeSpan.FromSeconds(3)),
                new PeriodEntry(TimeSpan.FromSeconds(5)),
                new PeriodEntry(TimeSpan.FromSeconds(10)),
                new PeriodEntry(TimeSpan.FromSeconds(25)),
                new PeriodEntry(TimeSpan.FromMinutes(1)),
                new PeriodEntry(TimeSpan.FromMinutes(2)),
                new PeriodEntry(TimeSpan.FromMinutes(5))
            });

            private PeriodEntry _refreshPeriod;

            public PeriodEntry RefreshPeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.OnlineSettings.RefreshPeriod");
                    return _refreshPeriod ?? (_refreshPeriod = RefreshPeriods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            RefreshPeriods.ElementAt(4));
                }
                set {
                    if (Equals(value, _refreshPeriod)) return;
                    _refreshPeriod = value;
                    ValuesStorage.Set("Settings.OnlineSettings.RefreshPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private OnlineServerEntry[] _onlineServers;

            public OnlineServerEntry[] OnlineServers => _onlineServers ??
                    (_onlineServers = Enumerable.Range(0, KunosApiProvider.ServersNumber).Select(x => new OnlineServerEntry { Id = x }).ToArray());

            public OnlineServerEntry OnlineServer {
                get {
                    var id = OnlineServerId;
                    return OnlineServers.FirstOrDefault(x => x.Id == id) ??
                            OnlineServers.FirstOrDefault();
                }
                set => OnlineServerId = value.Id;
            }

            private int? _onlineServerId;

            public int OnlineServerId {
                get => _onlineServerId ?? (_onlineServerId = ValuesStorage.GetInt("Settings.OnlineSettings.OnlineServerId", 1)).Value;
                set {
                    if (Equals(value, _onlineServerId)) return;
                    _onlineServerId = value;
                    ValuesStorage.Set("Settings.OnlineSettings.OnlineServerId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _compactUi;

            public bool CompactUi {
                get => _compactUi ?? (_compactUi = ValuesStorage.GetBool("Settings.OnlineSettings.CompactUi", false)).Value;
                set {
                    if (Equals(value, _compactUi)) return;
                    _compactUi = value;
                    ValuesStorage.Set("Settings.OnlineSettings.CompactUi", value);
                    OnPropertyChanged();
                }
            }

            private bool? _showBrandBadges;

            public bool ShowBrandBadges {
                get => _showBrandBadges ?? (_showBrandBadges = ValuesStorage.GetBool("Settings.OnlineSettings.ShowBrandBadges", true)).Value;
                set {
                    if (Equals(value, _showBrandBadges)) return;
                    _showBrandBadges = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ShowBrandBadges", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rememberPasswords;

            public bool RememberPasswords {
                get => _rememberPasswords ?? (_rememberPasswords = ValuesStorage.GetBool("Settings.OnlineSettings.RememberPasswords", true)).Value;
                set {
                    if (Equals(value, _rememberPasswords)) return;
                    _rememberPasswords = value;
                    ValuesStorage.Set("Settings.OnlineSettings.RememberPasswords", value);
                    OnPropertyChanged();
                }
            }

            private bool? _loadServersWithMissingContent;

            public bool LoadServersWithMissingContent {
                get => _loadServersWithMissingContent ??
                        (_loadServersWithMissingContent = ValuesStorage.GetBool("Settings.OnlineSettings.LoadServersWithMissingContent2", true)).Value;
                set {
                    if (Equals(value, _loadServersWithMissingContent)) return;
                    _loadServersWithMissingContent = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LoadServersWithMissingContent2", value);
                    OnPropertyChanged();
                }
            }

            private bool? _integrateMinorating;

            public bool IntegrateMinorating {
                get => _integrateMinorating ?? (_integrateMinorating = ValuesStorage.GetBool("Settings.OnlineSettings.IntegrateMinorating", true)).Value;
                set {
                    if (Equals(value, _integrateMinorating)) return;
                    _integrateMinorating = value;
                    ValuesStorage.Set("Settings.OnlineSettings.IntegrateMinorating", value);
                    OnPropertyChanged();
                }
            }

            private SettingEntry[] _fixNamesModes;

            public SettingEntry[] FixNamesModes => _fixNamesModes ?? (_fixNamesModes = new[] {
                new SettingEntry(0, "Disabled"),
                new SettingEntry(1, "Leading Letters “A”"),
                new SettingEntry(2, "Thorough cleaning")
            });

            private SettingEntry _fixNamesMode;

            public SettingEntry FixNamesMode {
                get {
                    var saved = ValuesStorage.GetIntNullable("Settings.IntegratedSettings.FixNamesMode");
                    return _fixNamesMode ?? (_fixNamesMode = FixNamesModes.GetByIdOrDefault(saved) ?? FixNamesModes.ElementAt(1));
                }
                set {
                    if (Equals(value, _fixNamesMode)) return;
                    _fixNamesMode = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.FixNamesMode", value.IntValue ?? -1);
                    OnPropertyChanged();
                }
            }

            private bool? _alwaysAllowToUsePassword;

            public bool AlwaysAllowToUsePassword {
                get => _alwaysAllowToUsePassword ??
                        (_alwaysAllowToUsePassword = ValuesStorage.GetBool("Settings.OnlineSettings.AlwaysAllowToUsePassword", true)).Value;
                set {
                    if (Equals(value, _alwaysAllowToUsePassword)) return;
                    _alwaysAllowToUsePassword = value;
                    ValuesStorage.Set("Settings.OnlineSettings.AlwaysAllowToUsePassword", value);
                    OnPropertyChanged();
                }
            }

            private bool? _loadServerInformationDirectly;

            public bool LoadServerInformationDirectly {
                get => _loadServerInformationDirectly ??
                        (_loadServerInformationDirectly = ValuesStorage.GetBool("Settings.OnlineSettings.LoadServerInformationDirectly", false)).Value;
                set {
                    if (Equals(value, _loadServerInformationDirectly)) return;
                    _loadServerInformationDirectly = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LoadServerInformationDirectly", value);
                    OnPropertyChanged();
                }
            }

            private bool? _pingOnlyOnce;

            public bool PingOnlyOnce {
                get => _pingOnlyOnce ?? (_pingOnlyOnce = ValuesStorage.GetBool("Settings.OnlineSettings.PingOnlyOnce", true)).Value;
                set {
                    if (Equals(value, _pingOnlyOnce)) return;
                    _pingOnlyOnce = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingOnlyOnce", value);
                    OnPropertyChanged();
                }
            }

            private bool? _pingingWithThreads;

            public bool ThreadsPing {
                get => _pingingWithThreads ?? (_pingingWithThreads = ValuesStorage.GetBool("Settings.OnlineSettings.ThreadsPing", false)).Value;
                set {
                    if (Equals(value, _pingingWithThreads)) return;
                    _pingingWithThreads = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ThreadsPing", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingingConcurrency;

            public int PingConcurrency {
                get => _pingingConcurrency ?? (_pingingConcurrency = ValuesStorage.GetInt("Settings.OnlineSettings.PingConcurrency", 10)).Value;
                set {
                    value = value.Clamp(1, 1000);
                    if (Equals(value, _pingingConcurrency)) return;
                    _pingingConcurrency = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingConcurrency", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingTimeout;

            public int PingTimeout {
                get => _pingTimeout ?? (_pingTimeout = ValuesStorage.GetInt("Settings.OnlineSettings.PingTimeout", 2000)).Value;
                set {
                    if (Equals(value, _pingTimeout)) return;
                    _pingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private int? _scanPingTimeout;

            public int ScanPingTimeout {
                get => _scanPingTimeout ?? (_scanPingTimeout = ValuesStorage.GetInt("Settings.OnlineSettings.ScanPingTimeout", 1000)).Value;
                set {
                    if (Equals(value, _scanPingTimeout)) return;
                    _scanPingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ScanPingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private string _portsEnumeration;

            public string PortsEnumeration {
                get => _portsEnumeration ?? (_portsEnumeration = ValuesStorage.GetString("Settings.OnlineSettings.PortsEnumeration", @"9000-10000"));
                set {
                    value = value.Trim();
                    if (Equals(value, _portsEnumeration)) return;
                    _portsEnumeration = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PortsEnumeration", value);
                    OnPropertyChanged();
                }
            }

            private string _lanPortsEnumeration;

            public string LanPortsEnumeration {
                get => _lanPortsEnumeration ??
                        (_lanPortsEnumeration = ValuesStorage.GetString("Settings.OnlineSettings.LanPortsEnumeration", @"9000-10000"));
                set {
                    value = value.Trim();
                    if (Equals(value, _lanPortsEnumeration)) return;
                    _lanPortsEnumeration = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LanPortsEnumeration", value);
                    OnPropertyChanged();
                }
            }

            private List<string> _ignoredInterfaces;

            public IEnumerable<string> IgnoredInterfaces {
                get => _ignoredInterfaces ?? (_ignoredInterfaces = ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces").ToList());
                set {
                    if (Equals(value, _ignoredInterfaces)) return;
                    _ignoredInterfaces = value.ToList();
                    ValuesStorage.Set("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }

            private bool? _searchForMissingContent;

            public bool SearchForMissingContent {
                get => _searchForMissingContent ??
                        (_searchForMissingContent = ValuesStorage.GetBool("Settings.OnlineSettings.SearchForMissingContent", false)).Value;
                set {
                    if (Equals(value, _searchForMissingContent)) return;
                    _searchForMissingContent = value;
                    ValuesStorage.Set("Settings.OnlineSettings.SearchForMissingContent", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingAttempts;

            public int PingAttempts {
                get => _pingAttempts ?? (_pingAttempts = ValuesStorage.GetInt("Settings.OnlineSettings.PingAttempts", 10)).Value;
                set {
                    value = value.Clamp(1, 1000);
                    if (Equals(value, _pingAttempts)) return;
                    _pingAttempts = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingAttempts", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverPresetsManaging;

            public bool ServerPresetsManaging {
                get => _serverPresetsManaging ??
                        (_serverPresetsManaging = ValuesStorage.GetBool("Settings.OnlineSettings.ServerPresetsManaging", false)).Value;
                set {
                    if (Equals(value, _serverPresetsManaging)) return;
                    _serverPresetsManaging = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsManaging", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverPresetsAutoSave;

            public bool ServerPresetsAutoSave {
                get => _serverPresetsAutoSave ??
                        (_serverPresetsAutoSave = ValuesStorage.GetBool("Settings.OnlineSettings.ServerPresetsAutoSave", true)).Value;
                set {
                    if (Equals(value, _serverPresetsAutoSave)) return;
                    _serverPresetsAutoSave = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsAutoSave", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverPresetsUpdateDataAutomatically;

            public bool ServerPresetsUpdateDataAutomatically {
                get => _serverPresetsUpdateDataAutomatically ??
                        (_serverPresetsUpdateDataAutomatically = ValuesStorage.GetBool("Settings.OnlineSettings.ServerPresetsUpdateDataAutomatically", true))
                                .Value;
                set {
                    if (Equals(value, _serverPresetsUpdateDataAutomatically)) return;
                    _serverPresetsUpdateDataAutomatically = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsUpdateDataAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverPresetsFitInFewerTabs;

            public bool ServerPresetsFitInFewerTabs {
                get => _serverPresetsFitInFewerTabs ??
                        (_serverPresetsFitInFewerTabs = ValuesStorage.GetBool("Settings.OnlineSettings.ServerPresetsFitInFewerTabs", false)).Value;
                set {
                    if (Equals(value, _serverPresetsFitInFewerTabs)) return;
                    _serverPresetsFitInFewerTabs = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsFitInFewerTabs", value);
                    OnPropertyChanged();
                }
            }
        }

        private static OnlineSettings _online;

        public static OnlineSettings Online => _online ?? (_online = new OnlineSettings());

        public class CommonSettings : NotifyPropertyChanged {
            internal CommonSettings() { }

            public TemperatureUnitMode[] TemperatureUnitModes { get; } = EnumExtension.GetValues<TemperatureUnitMode>();

            private TemperatureUnitMode? _temperatureUnitMode;

            public TemperatureUnitMode TemperatureUnitMode {
                get => _temperatureUnitMode ??
                        (_temperatureUnitMode = ValuesStorage.GetEnum("Settings.CommonSettings.TemperatureUnitMode", TemperatureUnitMode.Celsius)).Value;
                set {
                    if (Equals(value, _temperatureUnitMode)) return;
                    _temperatureUnitMode = value;
                    ValuesStorage.SetEnum("Settings.CommonSettings.TemperatureUnitMode", value);
                    OnPropertyChanged();
                }
            }

            private PeriodEntry[] _periodEntries;

            public PeriodEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
                new PeriodEntry(TimeSpan.Zero),
                new PeriodEntry(ToolsStrings.Settings_Period_Startup),
                new PeriodEntry(TimeSpan.FromMinutes(30)),
                new PeriodEntry(TimeSpan.FromHours(3)),
                new PeriodEntry(TimeSpan.FromHours(6)),
                new PeriodEntry(TimeSpan.FromDays(1))
            });

            private PeriodEntry _updatePeriod;

            [NotNull]
            public PeriodEntry UpdatePeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.CommonSettings.UpdatePeriod");
                    return _updatePeriod ?? (_updatePeriod = Periods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            Periods.ElementAt(2));
                }
                set {
                    if (Equals(value, _updatePeriod)) return;
                    _updatePeriod = value;
                    ValuesStorage.Set("Settings.CommonSettings.UpdatePeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private bool? _updateToNontestedVersions;

            public bool UpdateToNontestedVersions {
                get => _updateToNontestedVersions ??
                        (_updateToNontestedVersions = ValuesStorage.GetBool("Settings.CommonSettings.UpdateToNontestedVersions", false)).Value;
                set {
                    if (Equals(value, _updateToNontestedVersions)) return;
                    _updateToNontestedVersions = value;
                    ValuesStorage.Set("Settings.CommonSettings.UpdateToNontestedVersions", value);
                    OnPropertyChanged();
                }
            }

            private bool? _showDetailedChangelog;

            public bool ShowDetailedChangelog {
                get => _showDetailedChangelog ??
                        (_showDetailedChangelog = ValuesStorage.GetBool("Settings.CommonSettings.ShowDetailedChangelog", true)).Value;
                set {
                    if (Equals(value, _showDetailedChangelog)) return;
                    _showDetailedChangelog = value;
                    ValuesStorage.Set("Settings.CommonSettings.ShowDetailedChangelog", value);
                    OnPropertyChanged();
                }
            }

            private bool? _developerMode;

            public bool DeveloperMode {
                get => MsMode || (_developerMode ?? (_developerMode = ValuesStorage.GetBool("Settings.CommonSettings.DeveloperModeN", false)).Value);
                set {
                    if (Equals(value, _developerMode)) return;
                    _developerMode = value;
                    ValuesStorage.Set("Settings.CommonSettings.DeveloperModeN", value);
                    OnPropertyChanged();

                    if (!value) {
                        MsMode = false;
                    }
                }
            }

            private bool? _msMode;

            public bool MsMode {
                get => _msMode ?? (_msMode = ValuesStorage.GetBool("Settings.CommonSettings.DeveloperMode", false)).Value;
                set {
                    if (Equals(value, _msMode)) return;
                    _msMode = value;
                    ValuesStorage.Set("Settings.CommonSettings.DeveloperMode", value);
                    OnPropertyChanged();

                    if (value) {
                        OnPropertyChanged(nameof(DeveloperMode));
                    }
                }
            }

            private bool? _fixResolutionAutomatically;

            public bool FixResolutionAutomatically {
                get => _fixResolutionAutomatically ??
                        (_fixResolutionAutomatically = ValuesStorage.GetBool("Settings.CommonSettings.FixResolutionAutomatically_", false)).Value;
                set {
                    if (Equals(value, _fixResolutionAutomatically)) return;
                    _fixResolutionAutomatically = value;
                    ValuesStorage.Set("Settings.CommonSettings.FixResolutionAutomatically_", value);
                    OnPropertyChanged();
                }
            }
        }

        private static CommonSettings _common;

        public static CommonSettings Common => _common ?? (_common = new CommonSettings());

        public class DriveSettings : NotifyPropertyChanged {
            internal DriveSettings() {
                if (PlayerName == null) {
                    PlayerName = new IniFile(FileUtils.GetRaceIniFilename())["CAR_0"].GetNonEmpty("DRIVER_NAME") ?? ToolsStrings.Settings_DefaultPlayerName;
                    PlayerNameOnline = PlayerName;
                }

                if (PlayerNationality == null) {
                    PlayerNationality = new IniFile(FileUtils.GetRaceIniFilename())["CAR_0"].GetPossiblyEmpty("NATIONALITY");
                }
            }

            public sealed class StarterType : Displayable, IWithId {
                internal readonly string RequiredAddonId;

                public string Id { get; }

                public string Description { get; }

                public bool RequiresSteam { get; }

                public bool IsAvailable => RequiredAddonId == null || PluginsManager.Instance.IsPluginEnabled(RequiredAddonId);

                private readonly bool _nonSelectable;

                public bool IsSelectable => !_nonSelectable && IsAvailable;

                internal StarterType([Localizable(false)] string id, string displayName, string description, string requiredAddonId = null,
                        bool nonSelectable = false, bool requiresSteam = true) {
                    Id = id;
                    DisplayName = displayName;
                    Description = description;
                    RequiresSteam = requiresSteam;

                    RequiredAddonId = requiredAddonId;
                    _nonSelectable = nonSelectable;
                }
            }

            public static readonly StarterType OfficialStarterType = new StarterType(
                    "Official",
                    string.Format(ToolsStrings.Common_Recommended, ToolsStrings.Settings_Starter_Official),
                    ToolsStrings.Settings_Starter_Official_Description);

            public static readonly StarterType AppIdStarterType = new StarterType(
                    "AppID",
                    "AppID",
                    "Adds “steam_appid.txt” with AC’s Steam ID to AC root folder thus allowing to run “acs.exe” directly. Thanks to [url=\"http://www.assettocorsa.net/forum/index.php?members/zkirtaem.135368/\"]@Zkirtaem[/url] for the idea.");

            public static readonly StarterType SidePassageStarterType = new StarterType(
                    "AC Service",
                    "AC Service",
                    "Replaces original launcher by a small service. Fast and reliable. Original launcher still can be used — take a look at service’s icon in system tray.\n\nJust as a reminder (press “[?]” to read complete description): original launcher is renamed as “AssettoCorsa_original.exe”.");

            public static readonly StarterType SteamStarterType = new StarterType(
                    "Steam",
                    "Replacement",
                    "For this starter, you have to replace the original “AssettoCorsa.exe” with “Content Manager.exe”. This way, CM will get an access to Steam as if it is the original launcher.",
                    nonSelectable: true, requiresSteam: false /* because it is Steam! sort of */);

            public static readonly StarterType TrickyStarterType = new StarterType(
                    "Tricky",
                    ToolsStrings.Settings_Starter_Tricky,
                    ToolsStrings.Settings_Starter_Tricky_Description);

            public static readonly StarterType UiModuleStarterType = new StarterType(
                    "UI Module",
                    ToolsStrings.Settings_Starter_UiModule,
                    ToolsStrings.Settings_Starter_UiModule_Description);

            public static readonly StarterType StarterPlusType = new StarterType(
                    "Starter+",
                    ToolsStrings.Settings_Starter_StarterPlus,
                    ToolsStrings.Settings_Starter_StarterPlus_Description,
                    StarterPlus.AddonId);

            public static readonly StarterType SseStarterType = new StarterType(
                    "SSE",
                    ToolsStrings.Settings_Starter_Sse,
                    ToolsStrings.Settings_Starter_Sse_Description,
                    SseStarter.AddonId, requiresSteam: false);

            public static readonly StarterType NaiveStarterType = new StarterType(
                    "Naive",
                    ToolsStrings.Settings_Starter_Naive,
                    ToolsStrings.Settings_Starter_Naive_Description, requiresSteam: false);

            public static readonly StarterType DeveloperStarterType = new StarterType(
                    "Developer",
                    "Developer",
                    "Doesn’t run “acs.exe” expecting you to run it manually, only prepares “race.ini” and waits for “race_out.json” to change.",
                    requiresSteam: false);

            private StarterType _selectedStarterType;

            [NotNull]
            public StarterType SelectedStarterType {
                get => _selectedStarterType ??
                        (_selectedStarterType = StarterTypes.GetByIdOrDefault(ValuesStorage.GetString("Settings.DriveSettings.SelectedStarterType")) ??
                                StarterTypes.First());
                set {
                    if (Equals(value, _selectedStarterType)) return;
                    _selectedStarterType = value;
                    ValuesStorage.Set("Settings.DriveSettings.SelectedStarterType", value.Id);
                    OnPropertyChanged();

                    if (value == UiModuleStarterType && ModuleStarter.TryToInstallModule() && ModuleStarter.IsAssettoCorsaRunning) {
                        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke((Action)(() => {
                            ModernDialog.ShowMessage(ToolsStrings.Settings_Starter_UiModule_JustInstalled);
                        }));
                    }

                    if (value != SidePassageStarterType) {
                        SidePassageStarter.UninstallSidePassage();
                    }
                }
            }

            private bool? _fallbackIfNotAvailable;

            public bool StarterFallbackIfNotAvailable {
                get => _fallbackIfNotAvailable ??
                        (_fallbackIfNotAvailable = ValuesStorage.GetBool("Settings.DriveSettings.FallbackIfNotAvailable", true)).Value;
                set {
                    if (Equals(value, _fallbackIfNotAvailable)) return;
                    _fallbackIfNotAvailable = value;
                    ValuesStorage.Set("Settings.DriveSettings.FallbackIfNotAvailable", value);
                    OnPropertyChanged();
                }
            }

            private bool? _acServiceStopAtExit;

            public bool AcServiceStopAtExit {
                get => _acServiceStopAtExit ?? (_acServiceStopAtExit = ValuesStorage.GetBool("Settings.DriveSettings.AcServiceStopAtExit", true)).Value;
                set {
                    if (Equals(value, _acServiceStopAtExit)) return;
                    _acServiceStopAtExit = value;
                    ValuesStorage.Set("Settings.DriveSettings.AcServiceStopAtExit", value);
                    OnPropertyChanged();
                }
            }

            private StarterType[] _starterTypes;

            public StarterType[] StarterTypes => _starterTypes ?? (_starterTypes = new[] {
                OfficialStarterType,
                AppIdStarterType,
                SidePassageStarterType,
                SteamStarterType,
                TrickyStarterType,
                UiModuleStarterType,
                // StarterPlusType,
                SseStarterType,
                NaiveStarterType,
                DeveloperStarterType
            });

            private bool? _presetsPerModeAutoUpdate;

            public bool PresetsPerModeAutoUpdate {
                get => _presetsPerModeAutoUpdate ??
                        (_presetsPerModeAutoUpdate = ValuesStorage.GetBool("Settings.DriveSettings.PresetsPerModeAutoUpdate", true)).Value;
                set {
                    if (Equals(value, _presetsPerModeAutoUpdate)) return;
                    _presetsPerModeAutoUpdate = value;
                    ValuesStorage.Set("Settings.DriveSettings.PresetsPerModeAutoUpdate", value);
                    OnPropertyChanged();
                }
            }

            private bool? _watchForSharedMemory;

            public bool WatchForSharedMemory {
                get => _watchForSharedMemory ?? (_watchForSharedMemory = ValuesStorage.GetBool("Settings.DriveSettings.WatchForSharedMemory", true)).Value;
                set {
                    if (Equals(value, _watchForSharedMemory)) return;
                    _watchForSharedMemory = value;
                    ValuesStorage.Set("Settings.DriveSettings.WatchForSharedMemory", value);
                    OnPropertyChanged();
                }
            }

            private List<string> _ignoredInterfaces;

            public IEnumerable<string> IgnoredInterfaces {
                get => _ignoredInterfaces ?? (_ignoredInterfaces = ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces").ToList());
                set {
                    if (Equals(value, _ignoredInterfaces)) return;
                    _ignoredInterfaces = value.ToList();
                    ValuesStorage.Set("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }

            private bool? _hideWhileRacing;

            public bool HideWhileRacing {
                get => _hideWhileRacing ?? (_hideWhileRacing = ValuesStorage.GetBool("Settings.DriveSettings.HideWhileRacing", true)).Value;
                set {
                    if (Equals(value, _hideWhileRacing)) return;
                    _hideWhileRacing = value;
                    ValuesStorage.Set("Settings.DriveSettings.HideWhileRacing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _saveDevAppsInAppsPresets;

            public bool SaveDevAppsInAppsPresets {
                get => _saveDevAppsInAppsPresets ??
                        (_saveDevAppsInAppsPresets = ValuesStorage.GetBool("Settings.DriveSettings.SaveDevAppsInAppsPresets", false)).Value;
                set {
                    if (Equals(value, _saveDevAppsInAppsPresets)) return;
                    _saveDevAppsInAppsPresets = value;
                    ValuesStorage.Set("Settings.DriveSettings.SaveDevAppsInAppsPresets", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyFilterToSystemForOculus;

            public bool CopyFilterToSystemForOculus {
                get => _copyFilterToSystemForOculus ??
                        (_copyFilterToSystemForOculus = ValuesStorage.GetBool("Settings.DriveSettings.CopyFilterToSystemForOculus", true)).Value;
                set {
                    if (Equals(value, _copyFilterToSystemForOculus)) return;
                    _copyFilterToSystemForOculus = value;
                    ValuesStorage.Set("Settings.DriveSettings.CopyFilterToSystemForOculus", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickIntegration;

            public bool SidekickIntegration {
                get => _sidekickIntegration ?? (_sidekickIntegration = ValuesStorage.GetBool("Settings.DriveSettings.SidekickIntegration", true)).Value;
                set {
                    if (Equals(value, _sidekickIntegration)) return;
                    _sidekickIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingKunos;

            public bool SidekickUpdateExistingKunos {
                get => _sidekickUpdateExistingKunos ??
                        (_sidekickUpdateExistingKunos = ValuesStorage.GetBool("Settings.DriveSettings.SidekickUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingKunos)) return;
                    _sidekickUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingMods;

            public bool SidekickUpdateExistingMods {
                get => _sidekickUpdateExistingMods ??
                        (_sidekickUpdateExistingMods = ValuesStorage.GetBool("Settings.DriveSettings.SidekickUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingMods)) return;
                    _sidekickUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerImportValues;

            public bool SidekickOdometerImportValues {
                get => _sidekickOdometerImportValues ??
                        (_sidekickOdometerImportValues = ValuesStorage.GetBool("Settings.DriveSettings.sidekickOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerImportValues)) return;
                    _sidekickOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerExportValues;

            public bool SidekickOdometerExportValues {
                get => _sidekickOdometerExportValues ??
                        (_sidekickOdometerExportValues = ValuesStorage.GetBool("Settings.DriveSettings.sidekickOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerExportValues)) return;
                    _sidekickOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsIntegration;

            public bool RaceEssentialsIntegration {
                get => _raceEssentialsIntegration ??
                        (_raceEssentialsIntegration = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsIntegration", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsIntegration)) return;
                    _raceEssentialsIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingKunos;

            public bool RaceEssentialsUpdateExistingKunos {
                get => _raceEssentialsUpdateExistingKunos ??
                        (_raceEssentialsUpdateExistingKunos = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingKunos)) return;
                    _raceEssentialsUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingMods;

            public bool RaceEssentialsUpdateExistingMods {
                get => _raceEssentialsUpdateExistingMods ??
                        (_raceEssentialsUpdateExistingMods = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingMods)) return;
                    _raceEssentialsUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerImportValues;

            public bool StereoOdometerImportValues {
                get => _stereoOdometerImportValues ??
                        (_stereoOdometerImportValues = ValuesStorage.GetBool("Settings.DriveSettings.stereoOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerImportValues)) return;
                    _stereoOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerExportValues;

            public bool StereoOdometerExportValues {
                get => _stereoOdometerExportValues ??
                        (_stereoOdometerExportValues = ValuesStorage.GetBool("Settings.DriveSettings.stereoOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerExportValues)) return;
                    _stereoOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private string _preCommand;

            public string PreCommand {
                get => _preCommand ?? (_preCommand = ValuesStorage.GetString("Settings.DriveSettings.PreCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _preCommand)) return;
                    _preCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PreCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _postCommand;

            public string PostCommand {
                get => _postCommand ?? (_postCommand = ValuesStorage.GetString("Settings.DriveSettings.PostCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _postCommand)) return;
                    _postCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PostCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _preReplayCommand;

            public string PreReplayCommand {
                get => _preReplayCommand ?? (_preReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PreReplayCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _preReplayCommand)) return;
                    _preReplayCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PreReplayCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _postReplayCommand;

            public string PostReplayCommand {
                get => _postReplayCommand ?? (_postReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PostReplayCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _postReplayCommand)) return;
                    _postReplayCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PostReplayCommand", value);
                    OnPropertyChanged();
                }
            }

            private bool? _immediateStart;

            public bool ImmediateStart {
                get => _immediateStart ?? (_immediateStart = ValuesStorage.GetBool("Settings.DriveSettings.ImmediateStart", false)).Value;
                set {
                    if (Equals(value, _immediateStart)) return;
                    _immediateStart = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateStart", value);
                    OnPropertyChanged();
                }
            }

            private bool? _immediateCancel;

            public bool ImmediateCancel {
                get => _immediateCancel ?? (_immediateCancel = ValuesStorage.GetBool("Settings.DriveSettings.ImmediateCancel", false)).Value;
                set {
                    if (Equals(value, _immediateCancel)) return;
                    _immediateCancel = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateCancel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _continueOnEscape;

            public bool ContinueOnEscape {
                get => _continueOnEscape ?? (_continueOnEscape = ValuesStorage.GetBool("Settings.DriveSettings.ContinueOnEscape", false)).Value;
                set {
                    if (Equals(value, _continueOnEscape)) return;
                    _continueOnEscape = value;
                    ValuesStorage.Set("Settings.DriveSettings.ContinueOnEscape", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skipPracticeResults;

            public bool SkipPracticeResults {
                get => _skipPracticeResults ?? (_skipPracticeResults = ValuesStorage.GetBool("Settings.DriveSettings.SkipPracticeResults", false)).Value;
                set {
                    if (Equals(value, _skipPracticeResults)) return;
                    _skipPracticeResults = value;
                    ValuesStorage.Set("Settings.DriveSettings.SkipPracticeResults", value);
                    OnPropertyChanged();
                }
            }

            private int? _raceResultsLimit;

            public int RaceResultsLimit {
                get => _raceResultsLimit ?? (_raceResultsLimit = ValuesStorage.GetInt("Settings.DriveSettings.RaceResultsLimit", 1000)).Value;
                set {
                    value = value.Round(10);
                    if (Equals(value, _raceResultsLimit)) return;
                    _raceResultsLimit = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceResultsLimit", value);
                    OnPropertyChanged();
                }
            }

            private bool? _tryToLoadReplays;

            public bool TryToLoadReplays {
                get => _tryToLoadReplays ?? (_tryToLoadReplays = ValuesStorage.GetBool("Settings.DriveSettings.TryToLoadReplays", true)).Value;
                set {
                    if (Equals(value, _tryToLoadReplays)) return;
                    _tryToLoadReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.TryToLoadReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoSaveReplays;

            public bool AutoSaveReplays {
                get => _autoSaveReplays ?? (_autoSaveReplays = ValuesStorage.GetBool("Settings.DriveSettings.AutoSaveReplays", false)).Value;
                set {
                    if (Equals(value, _autoSaveReplays)) return;
                    _autoSaveReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoSaveReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoAddReplaysExtension;

            public bool AutoAddReplaysExtension {
                get => _autoAddReplaysExtension ??
                        (_autoAddReplaysExtension = ValuesStorage.GetBool("Settings.DriveSettings.AutoAddReplaysExtension", true)).Value;
                set {
                    if (Equals(value, _autoAddReplaysExtension)) return;
                    _autoAddReplaysExtension = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoAddReplaysExtension", value);
                    OnPropertyChanged();
                }
            }

            public string DefaultReplaysNameFormat => @"_autosave_{car.id}_{track.id}_{date_ac}.acreplay";

            private string _replaysNameFormat;

            public string ReplaysNameFormat {
                get
                        =>
                                _replaysNameFormat ??
                                        (_replaysNameFormat = ValuesStorage.GetString("Settings.DriveSettings.ReplaysNameFormat", DefaultReplaysNameFormat));
                set {
                    value = value?.Trim();
                    if (Equals(value, _replaysNameFormat)) return;
                    _replaysNameFormat = value;
                    ValuesStorage.Set("Settings.DriveSettings.ReplaysNameFormat", value);
                    OnPropertyChanged();
                }
            }

            private bool? _use32BitVersion;

            public bool Use32BitVersion {
                get => _use32BitVersion ?? (_use32BitVersion = ValuesStorage.GetBool("Settings.DriveSettings.Use32BitVersion", false)).Value;
                set {
                    if (Equals(value, _use32BitVersion)) return;
                    _use32BitVersion = value;
                    ValuesStorage.Set("Settings.DriveSettings.Use32BitVersion", value);
                    OnPropertyChanged();
                }
            }

            private bool? _runSteamIfNeeded;

            public bool RunSteamIfNeeded {
                get => _runSteamIfNeeded ?? (_runSteamIfNeeded = ValuesStorage.GetBool("Settings.DriveSettings.RunSteamIfNeeded", true)).Value;
                set {
                    if (Equals(value, _runSteamIfNeeded)) return;
                    _runSteamIfNeeded = value;
                    ValuesStorage.Set("Settings.DriveSettings.RunSteamIfNeeded", value);
                    OnPropertyChanged();
                }
            }

            private string _playerName;

            public string PlayerName {
                get => _playerName ?? (_playerName = ValuesStorage.GetString("Settings.DriveSettings.PlayerName", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _playerName)) return;
                    _playerName = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerName", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayerNameOnline));
                }
            }

            private string _playerNationality;

            public string PlayerNationality {
                get => _playerNationality ?? (_playerNationality = ValuesStorage.GetString("Settings.DriveSettings.PlayerNationality", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _playerNationality)) return;
                    _playerNationality = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNationality", value);
                    OnPropertyChanged();
                }
            }

            private bool? _differentPlayerNameOnline;

            public bool DifferentPlayerNameOnline {
                get => _differentPlayerNameOnline ??
                        (_differentPlayerNameOnline = ValuesStorage.GetBool("Settings.DriveSettings.DifferentPlayerNameOnline", false)).Value;
                set {
                    if (Equals(value, _differentPlayerNameOnline)) return;
                    _differentPlayerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.DifferentPlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private string _playerNameOnline;

            public string PlayerNameOnline {
                get => _playerNameOnline ?? (_playerNameOnline = ValuesStorage.GetString("Settings.DriveSettings.PlayerNameOnline", PlayerName));
                set {
                    value = value.Trim();
                    if (Equals(value, _playerNameOnline)) return;
                    _playerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveTrackDayViaPractice;

            public bool QuickDriveTrackDayViaPractice {
                get => _quickDriveTrackDayViaPractice ??
                        (_quickDriveTrackDayViaPractice = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveTrackDayViaPractice", true)).Value;
                set {
                    if (Equals(value, _quickDriveTrackDayViaPractice)) return;
                    _quickDriveTrackDayViaPractice = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveTrackDayViaPractice", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveExpandBounds;

            public bool QuickDriveExpandBounds {
                get => _quickDriveExpandBounds ?? (_quickDriveExpandBounds = ValuesStorage.GetBool("Settings.DriveSettings.ExpandBounds", false)).Value;
                set {
                    if (Equals(value, _quickDriveExpandBounds)) return;
                    _quickDriveExpandBounds = value;
                    ValuesStorage.Set("Settings.DriveSettings.ExpandBounds", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AiLevelMinimum));
                }
            }

            private bool? _quickDriveAiLimitations;

            public bool QuickDriveAiLimitations {
                get => _quickDriveAiLimitations ?? (_quickDriveAiLimitations = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveAiLimitations", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLimitations)) return;
                    _quickDriveAiLimitations = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAiLimitations", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiLevelInName;

            public bool QuickDriveAiLevelInName {
                get => _quickDriveAiLevelInName ?? (_quickDriveAiLevelInName = ValuesStorage.GetBool("RaceGrid.AiLevelInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLevelInName)) return;
                    _quickDriveAiLevelInName = value;
                    ValuesStorage.Set("RaceGrid.AiLevelInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiAggressionInName;

            public bool QuickDriveAiAggressionInName {
                get
                        =>
                                _quickDriveAiAggressionInName ??
                                        (_quickDriveAiAggressionInName = ValuesStorage.GetBool("RaceGrid.AiAggressionInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiAggressionInName)) return;
                    _quickDriveAiAggressionInName = value;
                    ValuesStorage.Set("RaceGrid.AiAggressionInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveUseSkinNames;

            public bool QuickDriveUseSkinNames {
                get => _quickDriveUseSkinNames ?? (_quickDriveUseSkinNames = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveUseSkinNames", true)).Value
                        ;
                set {
                    if (Equals(value, _quickDriveUseSkinNames)) return;
                    _quickDriveUseSkinNames = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveUseSkinNames", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveCheckTrack;

            public bool QuickDriveCheckTrack {
                get => _quickDriveCheckTrack ?? (_quickDriveCheckTrack = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveCheckTrack", true)).Value;
                set {
                    if (Equals(value, _quickDriveCheckTrack)) return;
                    _quickDriveCheckTrack = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveCheckTrack", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alwaysRecordGhost;

            public bool AlwaysRecordGhost {
                get => _alwaysRecordGhost ?? (_alwaysRecordGhost = ValuesStorage.GetBool("Settings.DriveSettings.AlwaysRecordGhost", false)).Value;
                set {
                    if (Equals(value, _alwaysRecordGhost)) return;
                    _alwaysRecordGhost = value;
                    ValuesStorage.Set("Settings.DriveSettings.AlwaysRecordGhost", value);
                    OnPropertyChanged();
                }
            }

            public int AiLevelMinimum => QuickDriveExpandBounds ? 30 : 70;

            private bool? _kunosCareerUserAiLevel;

            public bool KunosCareerUserAiLevel {
                get => _kunosCareerUserAiLevel ?? (_kunosCareerUserAiLevel = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserAiLevel", true)).Value
                        ;
                set {
                    if (Equals(value, _kunosCareerUserAiLevel)) return;
                    _kunosCareerUserAiLevel = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserAiLevel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _kunosCareerUserSkin;

            public bool KunosCareerUserSkin {
                get => _kunosCareerUserSkin ?? (_kunosCareerUserSkin = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserSkin", true)).Value;
                set {
                    if (Equals(value, _kunosCareerUserSkin)) return;
                    _kunosCareerUserSkin = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserSkin", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickSwitches;

            public bool QuickSwitches {
                get => _quickSwitches ?? (_quickSwitches = ValuesStorage.GetBool("Settings.DriveSettings.QuickSwitches", true)).Value;
                set {
                    if (Equals(value, _quickSwitches)) return;
                    _quickSwitches = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitches", value);
                    OnPropertyChanged();
                }
            }

            private string[] _quickSwitchesList;

            public string[] QuickSwitchesList {
                get => _quickSwitchesList ??
                        (_quickSwitchesList = ValuesStorage.GetStringList("Settings.DriveSettings.QuickSwitchesList", new[] {
                            @"WidgetExposure",
                            @"WidgetUiPresets",
                            @"WidgetHideDriveArms",
                            @"WidgetHideSteeringWheel"
                        }).ToArray());
                set {
                    if (Equals(value, _quickSwitchesList)) return;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitchesList", value);
                    _quickSwitchesList = value;
                    OnPropertyChanged();
                }
            }

            private bool? _automaticallyConvertBmpToJpg;

            public bool AutomaticallyConvertBmpToJpg {
                get => _automaticallyConvertBmpToJpg ??
                        (_automaticallyConvertBmpToJpg = ValuesStorage.GetBool("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", false)).Value;
                set {
                    if (Equals(value, _automaticallyConvertBmpToJpg)) return;
                    _automaticallyConvertBmpToJpg = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", value);
                    OnPropertyChanged();
                }
            }

            private string _localAddress;

            [CanBeNull]
            public string LocalAddress {
                get => _localAddress ?? (_localAddress = ValuesStorage.GetString("Settings.DriveSettings.LocalAddress", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _localAddress)) return;
                    _localAddress = value;
                    ValuesStorage.Set("Settings.DriveSettings.LocalAddress", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificClouds;

            public bool WeatherSpecificClouds {
                get => _weatherSpecificClouds ?? (_weatherSpecificClouds = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificClouds", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificClouds)) return;
                    _weatherSpecificClouds = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificClouds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificPpFilter;

            public bool WeatherSpecificPpFilter {
                get
                        =>
                                _weatherSpecificPpFilter ??
                                        (_weatherSpecificPpFilter = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificPpFilter", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificPpFilter)) return;
                    _weatherSpecificPpFilter = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificPpFilter", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificTyreSmoke;

            public bool WeatherSpecificTyreSmoke {
                get => _weatherSpecificTyreSmoke ??
                        (_weatherSpecificTyreSmoke = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificTyreSmoke", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificTyreSmoke)) return;
                    _weatherSpecificTyreSmoke = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificTyreSmoke", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rhmIntegration;

            public bool RhmIntegration {
                get => _rhmIntegration ??
                        (_rhmIntegration = ValuesStorage.GetBool("Settings.DriveSettings.RhmIntegration", false)).Value;
                set {
                    if (Equals(value, _rhmIntegration)) return;
                    _rhmIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmIntegration", value);
                    OnPropertyChanged();
                }
            }

            private string _rhmLocation;

            [CanBeNull]
            public string RhmLocation {
                get => _rhmLocation ?? (_rhmLocation = ValuesStorage.GetString("Settings.DriveSettings.RhmLocation", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _rhmLocation)) return;
                    _rhmLocation = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmLocation", value);
                    OnPropertyChanged();
                }
            }

            private string _rhmSettingsLocation;

            public string RhmSettingsLocation {
                get => _rhmSettingsLocation ?? (_rhmSettingsLocation = ValuesStorage.GetString("Settings.DriveSettings.RhmSettingsLocation",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RealHeadMotion", "Settings.xml")));
                set {
                    value = value.Trim();
                    if (Equals(value, _rhmSettingsLocation)) return;
                    _rhmSettingsLocation = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmSettingsLocation", value);
                    OnPropertyChanged();
                }
            }

            private DelayEntry[] _rhmKeepAlivePeriods;

            public DelayEntry[] RhmKeepAlivePeriods => _rhmKeepAlivePeriods ?? (_rhmKeepAlivePeriods = new[] {
                new DelayEntry(TimeSpan.Zero),
                new DelayEntry(TimeSpan.FromMinutes(2)),
                new DelayEntry(TimeSpan.FromMinutes(5)),
                new DelayEntry(TimeSpan.FromMinutes(15)),
                new DelayEntry(TimeSpan.FromMinutes(30)),
                new DelayEntry(TimeSpan.FromHours(1)),
                new DelayEntry(TimeSpan.FromHours(3))
            });

            private DelayEntry _rhmKeepAlivePeriod;

            public DelayEntry RhmKeepAlivePeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.DriveSettings.RhmKeepAlivePeriod");
                    return _rhmKeepAlivePeriod ?? (_rhmKeepAlivePeriod = RhmKeepAlivePeriods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            RhmKeepAlivePeriods.ElementAt(4));
                }
                set {
                    if (Equals(value, _rhmKeepAlivePeriod)) return;
                    _rhmKeepAlivePeriod = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmKeepAlivePeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _selectRhmLocationCommand;

            public DelegateCommand SelectRhmLocationCommand => _selectRhmLocationCommand ?? (_selectRhmLocationCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = "Real Head Motion|RealHeadMotionAssettoCorsa.exe|Applications (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Real Head Motion Application",
                    InitialDirectory = Path.GetDirectoryName(RhmLocation) ?? "",
                    FileName = Path.GetFileName(RhmLocation) ?? ""
                };

                if (dialog.ShowDialog() == true) {
                    RhmLocation = dialog.FileName;
                }
            }));

            private DelegateCommand _selectRhmSettingsLocationCommand;

            public DelegateCommand SelectRhmSettingsLocationCommand
                => _selectRhmSettingsLocationCommand ?? (_selectRhmSettingsLocationCommand = new DelegateCommand(() => {
                    var dialog = new OpenFileDialog {
                        Filter = "Real Head Motion Settings|Settings.xml|XML Files (*.xml)|*.xml|All files (*.*)|*.*",
                        Title = "Select Real Head Motion Settings",
                        InitialDirectory = Path.GetDirectoryName(RhmSettingsLocation) ?? "",
                        FileName = Path.GetFileName(RhmSettingsLocation) ?? ""
                    };

                    if (dialog.ShowDialog() == true) {
                        RhmSettingsLocation = dialog.FileName;
                    }
                }));
        }

        private static DriveSettings _drive;

        public static DriveSettings Drive => _drive ?? (_drive = new DriveSettings());

        public class ContentSettings : NotifyPropertyChanged {
            internal ContentSettings() { }

            private bool? _displaySteerLock;

            public bool DisplaySteerLock {
                get => _displaySteerLock ?? (_displaySteerLock = ValuesStorage.GetBool("Settings.ContentSettings.DisplaySteerLock", false)).Value;
                set {
                    if (Equals(value, _displaySteerLock)) return;
                    _displaySteerLock = value;
                    ValuesStorage.Set("Settings.ContentSettings.DisplaySteerLock", value);
                    OnPropertyChanged();
                }
            }

            private bool? _oldLayout;

            public bool OldLayout {
                get => _oldLayout ?? (_oldLayout = ValuesStorage.GetBool("Settings.ContentSettings.OldLayout", false)).Value;
                set {
                    if (Equals(value, _oldLayout)) return;
                    _oldLayout = value;
                    ValuesStorage.Set("Settings.ContentSettings.OldLayout", value);
                    OnPropertyChanged();
                }
            }

            private bool? _mentionCmInPackedContent;

            public bool MentionCmInPackedContent {
                get => _mentionCmInPackedContent ??
                        (_mentionCmInPackedContent = ValuesStorage.GetBool("Settings.ContentSettings.MentionCmInPackedContent", true)).Value;
                set {
                    if (Equals(value, _mentionCmInPackedContent)) return;
                    _mentionCmInPackedContent = value;
                    ValuesStorage.Set("Settings.ContentSettings.MentionCmInPackedContent", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rateCars;

            public bool RateCars {
                get => _rateCars ?? (_rateCars = ValuesStorage.GetBool("Settings.ContentSettings.RateCars", false)).Value;
                set {
                    if (Equals(value, _rateCars)) return;
                    _rateCars = value;
                    ValuesStorage.Set("Settings.ContentSettings.RateCars", value);
                    OnPropertyChanged();
                }
            }

            private int? _loadingConcurrency;

            public int LoadingConcurrency {
                get => _loadingConcurrency ?? (_loadingConcurrency = ValuesStorage.GetInt("Settings.ContentSettings.LoadingConcurrency",
                        BaseAcManagerNew.OptionAcObjectsLoadingConcurrency)).Value;
                set {
                    value = value < 1 ? 1 : value;
                    if (Equals(value, _loadingConcurrency)) return;
                    _loadingConcurrency = value;
                    ValuesStorage.Set("Settings.ContentSettings.LoadingConcurrency", value);
                    OnPropertyChanged();
                }
            }

            private bool? _curversInDrive;

            public bool CurversInDrive {
                get => _curversInDrive ?? (_curversInDrive = ValuesStorage.GetBool("Settings.ContentSettings.CurversInDrive", true)).Value;
                set {
                    if (Equals(value, _curversInDrive)) return;
                    _curversInDrive = value;
                    ValuesStorage.Set("Settings.ContentSettings.CurversInDrive", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smoothCurves;

            public bool SmoothCurves {
                get => _smoothCurves ?? (_smoothCurves = ValuesStorage.GetBool("Settings.ContentSettings.SmoothCurves", false)).Value;
                set {
                    if (Equals(value, _smoothCurves)) return;
                    _smoothCurves = value;
                    ValuesStorage.Set("Settings.ContentSettings.SmoothCurves", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsYearPostfix;

            public bool CarsYearPostfix {
                get => _carsYearPostfix ?? (_carsYearPostfix = ValuesStorage.GetBool("Settings.ContentSettings.CarsYearPostfix", false)).Value;
                set {
                    if (Equals(value, _carsYearPostfix)) return;
                    _carsYearPostfix = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsYearPostfix", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carSkinsDisplayId;

            public bool CarSkinsDisplayId {
                get => _carSkinsDisplayId ?? (_carSkinsDisplayId = ValuesStorage.GetBool("Settings.ContentSettings.CarSkinsDisplayId", false)).Value;
                set {
                    if (Equals(value, _carSkinsDisplayId)) return;
                    _carSkinsDisplayId = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarSkinsDisplayId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsFixSpecs;

            public bool CarsFixSpecs {
                get => _carsFixSpecs ?? (_carsFixSpecs = ValuesStorage.GetBool("Settings.ContentSettings.CarsFixSpecs", true)).Value;
                set {
                    if (Equals(value, _carsFixSpecs)) return;
                    _carsFixSpecs = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsFixSpecs", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsProperPwRatio;

            public bool CarsProperPwRatio {
                get => _carsProperPwRatio ?? (_carsProperPwRatio = ValuesStorage.GetBool("Settings.ContentSettings.CarsProperPwRatio", false)).Value;
                set {
                    if (Equals(value, _carsProperPwRatio)) return;
                    _carsProperPwRatio = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsProperPwRatio", value);
                    OnPropertyChanged();
                }
            }

            private bool? _changeBrandIconAutomatically;

            public bool ChangeBrandIconAutomatically {
                get => _changeBrandIconAutomatically ??
                        (_changeBrandIconAutomatically = ValuesStorage.GetBool("Settings.ContentSettings.ChangeBrandIconAutomatically", true)).Value;
                set {
                    if (Equals(value, _changeBrandIconAutomatically)) return;
                    _changeBrandIconAutomatically = value;
                    ValuesStorage.Set("Settings.ContentSettings.ChangeBrandIconAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _downloadShowroomPreviews;

            public bool DownloadShowroomPreviews {
                get => _downloadShowroomPreviews ??
                        (_downloadShowroomPreviews = ValuesStorage.GetBool("Settings.ContentSettings.DownloadShowroomPreviews", true)).Value;
                set {
                    if (Equals(value, _downloadShowroomPreviews)) return;
                    _downloadShowroomPreviews = value;
                    ValuesStorage.Set("Settings.ContentSettings.DownloadShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _scrollAutomatically;

            public bool ScrollAutomatically {
                get => _scrollAutomatically ?? (_scrollAutomatically = ValuesStorage.GetBool("Settings.ContentSettings.ScrollAutomatically", true)).Value;
                set {
                    if (Equals(value, _scrollAutomatically)) return;
                    _scrollAutomatically = value;
                    ValuesStorage.Set("Settings.ContentSettings.ScrollAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private string _temporaryFilesLocation;

            public string TemporaryFilesLocation {
                get => _temporaryFilesLocation ?? (_temporaryFilesLocation = ValuesStorage.GetString("Settings.ContentSettings.TemporaryFilesLocation", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _temporaryFilesLocation)) return;
                    _temporaryFilesLocation = value;
                    ValuesStorage.Set("Settings.ContentSettings.TemporaryFilesLocation", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TemporaryFilesLocationValue));
                }
            }

            public string TemporaryFilesLocationValue => TemporaryFilesLocation == "" ? Path.GetTempPath() : TemporaryFilesLocation;

            private string _rdLogin;

            public string RdLogin {
                get => _rdLogin ?? (_rdLogin = ValuesStorage.GetEncryptedString("Settings.ContentSettings.RdLogin", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _rdLogin)) return;
                    _rdLogin = value;
                    ValuesStorage.SetEncrypted("Settings.ContentSettings.RdLogin", value);
                    OnPropertyChanged();
                }
            }

            private string _rdPassword;

            public string RdPassword {
                get => _rdPassword ?? (_rdPassword = ValuesStorage.GetEncryptedString("Settings.ContentSettings.RdPassword", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _rdPassword)) return;
                    _rdPassword = value;
                    ValuesStorage.SetEncrypted("Settings.ContentSettings.RdPassword", value);
                    OnPropertyChanged();
                }
            }

            private string _rdProxy;

            public string RdProxy {
                get => _rdProxy ?? (_rdProxy = ValuesStorage.GetString("Settings.ContentSettings.RdProxy", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _rdProxy)) return;
                    _rdProxy = value;
                    ValuesStorage.Set("Settings.ContentSettings.RdProxy", value);
                    OnPropertyChanged();
                }
            }

            private string _fontIconCharacter;

            public string FontIconCharacter {
                get => _fontIconCharacter ?? (_fontIconCharacter = ValuesStorage.GetString("Settings.ContentSettings.FontIconCharacter", @"A"));
                set {
                    value = value?.Trim().Substring(0, 1);
                    if (Equals(value, _fontIconCharacter)) return;
                    _fontIconCharacter = value;
                    ValuesStorage.Set("Settings.ContentSettings.FontIconCharacter", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skinsSkipPriority;

            public bool SkinsSkipPriority {
                get => _skinsSkipPriority ?? (_skinsSkipPriority = ValuesStorage.GetBool("Settings.ContentSettings.SkinsSkipPriority", false)).Value;
                set {
                    if (Equals(value, _skinsSkipPriority)) return;
                    _skinsSkipPriority = value;
                    ValuesStorage.Set("Settings.ContentSettings.SkinsSkipPriority", value);
                    OnPropertyChanged();
                }
            }

            private DelayEntry[] _periodEntries;

            public DelayEntry[] NewContentPeriods => _periodEntries ?? (_periodEntries = new[] {
                new DelayEntry(TimeSpan.Zero),
                new DelayEntry(TimeSpan.FromDays(1)),
                new DelayEntry(TimeSpan.FromDays(3)),
                new DelayEntry(TimeSpan.FromDays(7)),
                new DelayEntry(TimeSpan.FromDays(14)),
                new DelayEntry(TimeSpan.FromDays(30)),
                new DelayEntry(TimeSpan.FromDays(60))
            });

            private DelayEntry _newContentPeriod;

            public DelayEntry NewContentPeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.ContentSettings.NewContentPeriod");
                    return _newContentPeriod ?? (_newContentPeriod = NewContentPeriods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            NewContentPeriods.ElementAt(4));
                }
                set {
                    if (Equals(value, _newContentPeriod)) return;
                    _newContentPeriod = value;
                    ValuesStorage.Set("Settings.ContentSettings.NewContentPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private bool? _simpleFiltering;

            public bool SimpleFiltering {
                get => _simpleFiltering ?? (_simpleFiltering = ValuesStorage.GetBool("Settings.ContentSettings.SimpleFiltering", true)).Value;
                set {
                    if (Equals(value, _simpleFiltering)) return;
                    _simpleFiltering = value;
                    ValuesStorage.Set("Settings.ContentSettings.SimpleFiltering", value);
                    OnPropertyChanged();
                    Filter.OptionSimpleMatching = value;
                }
            }

            private bool? _deleteConfirmation;

            public bool DeleteConfirmation {
                get => _deleteConfirmation ?? (_deleteConfirmation = ValuesStorage.GetBool("Settings.ContentSettings.DeleteConfirmation", true)).Value;
                set {
                    if (Equals(value, _deleteConfirmation)) return;
                    _deleteConfirmation = value;
                    ValuesStorage.Set("Settings.ContentSettings.DeleteConfirmation", value);
                    OnPropertyChanged();
                }
            }

            private SearchEngineEntry[] _searchEngines;

            public SearchEngineEntry[] SearchEngines => _searchEngines ?? (_searchEngines = new[] {
                new SearchEngineEntry(ToolsStrings.SearchEngine_DuckDuckGo, @"https://duckduckgo.com/?q={0}&ia=web"),
                new SearchEngineEntry(ToolsStrings.SearchEngine_Bing, @"http://www.bing.com/search?q={0}"),
                new SearchEngineEntry(ToolsStrings.SearchEngine_Google, @"https://www.google.com/search?q={0}&ie=UTF-8"),
                new SearchEngineEntry(ToolsStrings.SearchEngine_Yandex, @"https://yandex.ru/search/?text={0}"),
                new SearchEngineEntry(ToolsStrings.SearchEngine_Baidu, @"http://www.baidu.com/s?ie=utf-8&wd={0}")
            });

            private SearchEngineEntry _searchEngine;

            public SearchEngineEntry SearchEngine {
                get {
                    return _searchEngine ?? (_searchEngine = SearchEngines.FirstOrDefault(x =>
                            x.DisplayName == ValuesStorage.GetString("Settings.ContentSettings.SearchEngine")) ??
                            SearchEngines.First());
                }
                set {
                    if (Equals(value, _searchEngine)) return;
                    _searchEngine = value;
                    ValuesStorage.Set("Settings.ContentSettings.SearchEngine", value.DisplayName);
                    OnPropertyChanged();
                }
            }

            private bool? _searchWithWikipedia;

            public bool SearchWithWikipedia {
                get => _searchWithWikipedia ?? (_searchWithWikipedia = ValuesStorage.GetBool("Settings.ContentSettings.SearchWithWikipedia", true)).Value;
                set {
                    if (Equals(value, _searchWithWikipedia)) return;
                    _searchWithWikipedia = value;
                    ValuesStorage.Set("Settings.ContentSettings.SearchWithWikipedia", value);
                    OnPropertyChanged();
                }
            }

            private MissingContentSearchEntry[] _missingContentSearchEntries;

            public MissingContentSearchEntry[] MissingContentSearchEntries => _missingContentSearchEntries ?? (_missingContentSearchEntries = new[] {
                new MissingContentSearchEntry("Use selected search engine", (type, id) => $"{id}", true),
                new MissingContentSearchEntry("Use selected search engine (strict)", (type, id) => $"\"{id}\"", true),
                new MissingContentSearchEntry("Assetto-DB.com (by ID, strict)", (type, id) => {
                    switch (type) {
                        case MissingContentType.Car:
                            return $"http://assetto-db.com/car/{id}";
                        case MissingContentType.Track:
                            if (!id.Contains(@"/")) id = $@"{id}/{id}";
                            return $"http://assetto-db.com/track/{id}";
                        default:
                            throw new ArgumentOutOfRangeException(nameof(type), type, null);
                    }
                }, false),
                // new MissingContentSearchEntry("AcClub (via selected search engine)", (type, id) => $"site:assettocorsa.club {id}", true),
                new MissingContentSearchEntry("AC Drifting Pro", (type, id) => $"http://www.acdriftingpro.com/?s={HttpUtility.UrlEncode(id)}", false),
                // new MissingContentSearchEntry("RaceDepartment (via selected search engine)", (type, id) => $"site:racedepartment.com {id}", true),
            });

            private MissingContentSearchEntry _missingContentSearch;

            public MissingContentSearchEntry MissingContentSearch {
                get {
                    return _missingContentSearch ?? (_missingContentSearch = MissingContentSearchEntries.FirstOrDefault(x =>
                            x.DisplayName == ValuesStorage.GetString("Settings.ContentSettings.MissingContentSearch")) ??
                            MissingContentSearchEntries.First());
                }
                set {
                    if (Equals(value, _missingContentSearch)) return;
                    _missingContentSearch = value;
                    ValuesStorage.Set("Settings.ContentSettings.MissingContentSearch", value.DisplayName);
                    OnPropertyChanged();
                }
            }

            private string _carReplaceTyresDonorFilter;

            public string CarReplaceTyresDonorFilter {
                get {
                    if (_carReplaceTyresDonorFilter == null) {
                        _carReplaceTyresDonorFilter = ValuesStorage.GetString("Settings.ContentSettings.CarReplaceTyresDonorFilter", "k+");
                        if (string.IsNullOrWhiteSpace(_carReplaceTyresDonorFilter)) {
                            _carReplaceTyresDonorFilter = "*";
                        }
                    }

                    return _carReplaceTyresDonorFilter;
                }
                set {
                    value = value.Trim();
                    if (Equals(value, _carReplaceTyresDonorFilter)) return;
                    _carReplaceTyresDonorFilter = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarReplaceTyresDonorFilter", value);
                    OnPropertyChanged();
                }
            }
        }

        private static ContentSettings _content;

        public static ContentSettings Content => _content ?? (_content = new ContentSettings());

        public class CustomShowroomSettings : NotifyPropertyChanged {
            internal CustomShowroomSettings() { }

            private bool? _useOldLiteShowroom;

            public bool UseOldLiteShowroom {
                get => _useOldLiteShowroom ?? (_useOldLiteShowroom = ValuesStorage.GetBool("Settings.CustomShowroomSettings.UseOldLiteShowroom", false)).Value;
                set {
                    if (Equals(value, _useOldLiteShowroom)) return;
                    _useOldLiteShowroom = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.UseOldLiteShowroom", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseFxaa;

            public bool LiteUseFxaa {
                get => _liteUseFxaa ?? (_liteUseFxaa = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseFxaa", true)).Value;
                set {
                    if (Equals(value, _liteUseFxaa)) return;
                    _liteUseFxaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseFxaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseMsaa;

            public bool LiteUseMsaa {
                get => _liteUseMsaa ?? (_liteUseMsaa = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseMsaa", false)).Value;
                set {
                    if (Equals(value, _liteUseMsaa)) return;
                    _liteUseMsaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseMsaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseBloom;

            public bool LiteUseBloom {
                get => _liteUseBloom ?? (_liteUseBloom = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseBloom", true)).Value;
                set {
                    if (Equals(value, _liteUseBloom)) return;
                    _liteUseBloom = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseBloom", value);
                    OnPropertyChanged();
                }
            }

            private string _showroomId;

            [CanBeNull]
            public string ShowroomId {
                get => _showroomId ?? (_showroomId = ValuesStorage.GetString("Settings.CustomShowroomSettings.ShowroomId", @"showroom"));
                set {
                    value = value?.Trim();
                    if (Equals(value, _showroomId)) return;
                    _showroomId = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.ShowroomId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _customShowroomInstead;

            public bool CustomShowroomInstead {
                get => _customShowroomInstead ??
                        (_customShowroomInstead = ValuesStorage.GetBool("Settings.CustomShowroomSettings.CustomShowroomInstead", false)).Value;
                set {
                    if (Equals(value, _customShowroomInstead)) return;
                    _customShowroomInstead = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.CustomShowroomInstead", value);
                    OnPropertyChanged();
                }
            }

            private bool? _customShowroomPreviews;

            public bool CustomShowroomPreviews {
                get => _customShowroomPreviews ??
                        (_customShowroomPreviews = ValuesStorage.GetBool("Settings.CustomShowroomSettings.CustomShowroomPreviews", true)).Value;
                set {
                    if (Equals(value, _customShowroomPreviews)) return;
                    _customShowroomPreviews = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.CustomShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _detailedExifForPreviews;

            public bool DetailedExifForPreviews {
                get => _detailedExifForPreviews ??
                        (_detailedExifForPreviews = ValuesStorage.GetBool("Settings.CustomShowroomSettings.DetailedExifForPreviews", true)).Value;
                set {
                    if (Equals(value, _detailedExifForPreviews)) return;
                    _detailedExifForPreviews = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.DetailedExifForPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _previewsRecycleOld;

            public bool PreviewsRecycleOld {
                get => _previewsRecycleOld ?? (_previewsRecycleOld = ValuesStorage.GetBool("Settings.CustomShowroomSettings.PreviewsRecycleOld", true)).Value;
                set {
                    if (Equals(value, _previewsRecycleOld)) return;
                    _previewsRecycleOld = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.PreviewsRecycleOld", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smartCameraPivot;

            public bool SmartCameraPivot {
                get => _smartCameraPivot ?? (_smartCameraPivot = ValuesStorage.GetBool("Settings.CustomShowroomSettings.SmartCameraPivot", true)).Value;
                set {
                    if (Equals(value, _smartCameraPivot)) return;
                    _smartCameraPivot = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.SmartCameraPivot", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alternativeControlScheme;

            public bool AlternativeControlScheme {
                get => _alternativeControlScheme ??
                        (_alternativeControlScheme = ValuesStorage.GetBool("Settings.CustomShowroomSettings.AlternativeControlScheme", false)).Value;
                set {
                    if (Equals(value, _alternativeControlScheme)) return;
                    _alternativeControlScheme = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.AlternativeControlScheme", value);
                    OnPropertyChanged();
                }
            }

            /*private string[] _paintShopSources;

            public string[] PaintShopSources {
                get => _paintShopSources ?? (_paintShopSources = ValuesStorage.GetStringList("Settings.CustomShowroomSettings.PaintShopSources", new[] {
                    "https://github.com/MadMat13/CM-Paint-Shop/"
                }).ToArray());
                set {
                    value = value.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    if (Equals(value, _paintShopSources)) return;
                    _paintShopSources = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.PaintShopSources", value);
                    OnPropertyChanged();
                }
            }*/
        }

        private static CustomShowroomSettings _customShowroom;

        public static CustomShowroomSettings CustomShowroom => _customShowroom ?? (_customShowroom = new CustomShowroomSettings());

        public class SharingSettings : NotifyPropertyChanged {
            internal SharingSettings() { }

            private bool? _customIds;

            public bool CustomIds {
                get => _customIds ?? (_customIds = ValuesStorage.GetBool("Settings.SharingSettings.CustomIds", false)).Value;
                set {
                    if (Equals(value, _customIds)) return;
                    _customIds = value;
                    ValuesStorage.Set("Settings.SharingSettings.CustomIds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _verifyBeforeSharing;

            public bool VerifyBeforeSharing {
                get => _verifyBeforeSharing ?? (_verifyBeforeSharing = ValuesStorage.GetBool("Settings.SharingSettings.VerifyBeforeSharing", true)).Value;
                set {
                    if (Equals(value, _verifyBeforeSharing)) return;
                    _verifyBeforeSharing = value;
                    ValuesStorage.Set("Settings.SharingSettings.VerifyBeforeSharing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyLinkToClipboard;

            public bool CopyLinkToClipboard {
                get => _copyLinkToClipboard ?? (_copyLinkToClipboard = ValuesStorage.GetBool("Settings.SharingSettings.CopyLinkToClipboard", true)).Value;
                set {
                    if (Equals(value, _copyLinkToClipboard)) return;
                    _copyLinkToClipboard = value;
                    ValuesStorage.Set("Settings.SharingSettings.CopyLinkToClipboard", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareAnonymously;

            public bool ShareAnonymously {
                get => _shareAnonymously ?? (_shareAnonymously = ValuesStorage.GetBool("Settings.SharingSettings.ShareAnonymously", false)).Value;
                set {
                    if (Equals(value, _shareAnonymously)) return;
                    _shareAnonymously = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareAnonymously", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareWithoutName;

            public bool ShareWithoutName {
                get => _shareWithoutName ?? (_shareWithoutName = ValuesStorage.GetBool("Settings.SharingSettings.ShareWithoutName", false)).Value;
                set {
                    if (Equals(value, _shareWithoutName)) return;
                    _shareWithoutName = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareWithoutName", value);
                    OnPropertyChanged();
                }
            }

            private string _sharingName;

            [CanBeNull]
            public string SharingName {
                get => _sharingName ?? (_sharingName = ValuesStorage.GetString("Settings.SharingSettings.SharingName", null) ?? Drive.PlayerNameOnline);
                set {
                    value = value?.Trim();

                    if (value?.Length > 60) {
                        value = value.Substring(0, 60);
                    }

                    if (Equals(value, _sharingName)) return;
                    _sharingName = value;
                    ValuesStorage.Set("Settings.SharingSettings.SharingName", value);
                    OnPropertyChanged();
                }
            }
        }

        private static SharingSettings _sharing;

        public static SharingSettings Sharing => _sharing ?? (_sharing = new SharingSettings());

        public class LiveSettings : NotifyPropertyChanged {
            internal LiveSettings() { }

            private bool? _srsEnabled;

            public bool SrsEnabled {
                get => _srsEnabled ?? (_srsEnabled = ValuesStorage.GetBool("Settings.LiveSettings.SrsEnabled", true)).Value;
                set {
                    if (Equals(value, _srsEnabled)) return;
                    _srsEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsCustomStyle;

            public bool SrsCustomStyle {
                get => _srsCustomStyle ?? (_srsCustomStyle = ValuesStorage.GetBool("Settings.LiveSettings.SrsCustomStyle", true)).Value;
                set {
                    if (Equals(value, _srsCustomStyle)) return;
                    _srsCustomStyle = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsAutoMode;

            public bool SrsAutoMode {
                get => _srsAutoMode ?? (_srsAutoMode = ValuesStorage.GetBool("Settings.LiveSettings.SrsAutoMode", true)).Value;
                set {
                    if (Equals(value, _srsAutoMode)) return;
                    _srsAutoMode = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsAutoMode", value);
                    OnPropertyChanged();
                }
            }

            private string _srsAutoMask;

            public string SrsAutoMask {
                get => _srsAutoMask ?? (_srsAutoMask = ValuesStorage.GetString("Settings.LiveSettings.SrsAutoMask", @"SimRacingSystem*"));
                set {
                    value = value.Trim();
                    if (Equals(value, _srsAutoMask)) return;
                    _srsAutoMask = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsAutoMask", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrEnabled;

            public bool RsrEnabled {
                get => _rsrEnabled ?? (_rsrEnabled = ValuesStorage.GetBool("Settings.RsrSettings.RsrEnabled", true)).Value;
                set {
                    if (Equals(value, _rsrEnabled)) return;
                    _rsrEnabled = value;
                    ValuesStorage.Set("Settings.RsrSettings.RsrEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrCustomStyle;

            public bool RsrCustomStyle {
                get => _rsrCustomStyle ?? (_rsrCustomStyle = ValuesStorage.GetBool("Settings.RsrSettings.RsrCustomStyle", true)).Value;
                set {
                    if (Equals(value, _rsrCustomStyle)) return;
                    _rsrCustomStyle = value;
                    ValuesStorage.Set("Settings.RsrSettings.RsrCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrDisableAppAutomatically;

            public bool RsrDisableAppAutomatically {
                get => _rsrDisableAppAutomatically ??
                        (_rsrDisableAppAutomatically = ValuesStorage.GetBool("Settings.LiveTimingSettings.RsrDisableAppAutomatically", false)).Value;
                set {
                    if (Equals(value, _rsrDisableAppAutomatically)) return;
                    _rsrDisableAppAutomatically = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrDisableAppAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrDifferentPlayerName;

            public bool RsrDifferentPlayerName {
                get => _rsrDifferentPlayerName ??
                        (_rsrDifferentPlayerName = ValuesStorage.GetBool("Settings.LiveTimingSettings.RsrDifferentPlayerName", false)).Value;
                set {
                    if (Equals(value, _rsrDifferentPlayerName)) return;
                    _rsrDifferentPlayerName = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrDifferentPlayerName", value);
                    OnPropertyChanged();
                }
            }

            private string _rsrPlayerName;

            public string RsrPlayerName {
                get => _rsrPlayerName ?? (_rsrPlayerName = ValuesStorage.GetString("Settings.LiveTimingSettings.RsrPlayerName", Drive.PlayerName));
                set {
                    value = value.Trim();
                    if (Equals(value, _rsrPlayerName)) return;
                    _rsrPlayerName = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrPlayerName", value);
                    OnPropertyChanged();
                }
            }
        }

        private static LiveSettings _live;

        public static LiveSettings Live => _live ?? (_live = new LiveSettings());

        public class LocaleSettings : NotifyPropertyChanged {
            internal LocaleSettings() { }

            private string _localeName;

            public string LocaleName {
                get => _localeName ?? (_localeName = ValuesStorage.GetString("Settings.LocaleSettings.LocaleName_", @"en"));
                set {
                    value = value.Trim();
                    if (Equals(value, _localeName)) return;
                    _localeName = value;
                    ValuesStorage.Set("Settings.LocaleSettings.LocaleName_", value);
                    OnPropertyChanged();
                }
            }

            private bool? _loadUnpacked;

            public bool LoadUnpacked {
                get => _loadUnpacked ?? (_loadUnpacked = ValuesStorage.GetBool("Settings.LocaleSettings.LoadUnpacked", false)).Value;
                set {
                    if (Equals(value, _loadUnpacked)) return;
                    _loadUnpacked = value;
                    ValuesStorage.Set("Settings.LocaleSettings.LoadUnpacked", value);
                    OnPropertyChanged();
                }
            }

            private bool? _resxLocalesMode;

            public bool ResxLocalesMode {
                get => _resxLocalesMode ?? (_resxLocalesMode = ValuesStorage.GetBool("Settings.LocaleSettings.ResxLocalesMode", false)).Value;
                set {
                    if (Equals(value, _resxLocalesMode)) return;
                    _resxLocalesMode = value;
                    ValuesStorage.Set("Settings.LocaleSettings.ResxLocalesMode", value);
                    OnPropertyChanged();
                }
            }

            private PeriodEntry _updatePeriod;

            [NotNull]
            public PeriodEntry UpdatePeriod {
                get {
                    return _updatePeriod ?? (_updatePeriod = Common.Periods.FirstOrDefault(x =>
                            x.TimeSpan == (ValuesStorage.GetTimeSpan("Settings.LocaleSettings.UpdatePeriod") ?? Common.Periods.ElementAt(4).TimeSpan)) ??
                            Common.Periods.First());
                }
                set {
                    if (Equals(value, _updatePeriod)) return;
                    _updatePeriod = value;
                    ValuesStorage.Set("Settings.LocaleSettings.UpdatePeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private bool? _updateOnStart;

            public bool UpdateOnStart {
                get => _updateOnStart ?? (_updateOnStart = ValuesStorage.GetBool("Settings.LocaleSettings.UpdateOnStart", true)).Value;
                set {
                    if (Equals(value, _updateOnStart)) return;
                    _updateOnStart = value;
                    ValuesStorage.Set("Settings.LocaleSettings.UpdateOnStart", value);
                    OnPropertyChanged();
                }
            }
        }

        private static LocaleSettings _locale;

        public static LocaleSettings Locale => _locale ?? (_locale = new LocaleSettings());

        public class InterfaceSettings : NotifyPropertyChanged {
            internal InterfaceSettings() { }

            private bool? _quickDriveFastAccessButtons;

            public bool QuickDriveFastAccessButtons {
                get => _quickDriveFastAccessButtons ??
                        (_quickDriveFastAccessButtons = ValuesStorage.GetBool("Settings.InterfaceSettings.QuickDriveFastAccessButtons", true)).Value;
                set {
                    if (Equals(value, _quickDriveFastAccessButtons)) return;
                    _quickDriveFastAccessButtons = value;
                    ValuesStorage.Set("Settings.InterfaceSettings.QuickDriveFastAccessButtons", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skinsSetupsNewWindow;

            public bool SkinsSetupsNewWindow {
                get => _skinsSetupsNewWindow ?? (_skinsSetupsNewWindow = ValuesStorage.GetBool("Settings.InterfaceSettings.SkinsSetupsNewWindow", false)).Value;
                set {
                    if (Equals(value, _skinsSetupsNewWindow)) return;
                    _skinsSetupsNewWindow = value;
                    ValuesStorage.Set("Settings.InterfaceSettings.SkinsSetupsNewWindow", value);
                    OnPropertyChanged();
                }
            }
        }

        private static InterfaceSettings _interface;

        public static InterfaceSettings Interface => _interface ?? (_interface = new InterfaceSettings());

        public class IntegratedSettings : NotifyPropertyChanged {
            internal IntegratedSettings() { }

            private DelayEntry[] _periodEntries;

            public DelayEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
                new DelayEntry(TimeSpan.FromHours(1)),
                new DelayEntry(TimeSpan.FromHours(3)),
                new DelayEntry(TimeSpan.FromHours(6)),
                new DelayEntry(TimeSpan.FromHours(12)),
                new DelayEntry(TimeSpan.FromDays(1))
            });

            private DelayEntry _theSetupMarketCacheListPeriod;

            public DelayEntry TheSetupMarketCacheListPeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.IntegratedSettings.TheSetupMarketCacheListPeriod");
                    return _theSetupMarketCacheListPeriod ?? (_theSetupMarketCacheListPeriod = Periods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            Periods.ElementAt(3));
                }
                set {
                    if (Equals(value, _theSetupMarketCacheListPeriod)) return;
                    _theSetupMarketCacheListPeriod = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheListPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private DelayEntry _theSetupMarketCacheDataPeriod;

            public DelayEntry TheSetupMarketCacheDataPeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.IntegratedSettings.TheSetupMarketCacheDataPeriod");
                    return _theSetupMarketCacheDataPeriod ?? (_theSetupMarketCacheDataPeriod = Periods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            Periods.ElementAt(2));
                }
                set {
                    if (Equals(value, _theSetupMarketCacheDataPeriod)) return;
                    _theSetupMarketCacheDataPeriod = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheDataPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private bool? _theSetupMarketCacheServer;

            public bool TheSetupMarketCacheServer {
                get => _theSetupMarketCacheServer ?? (_theSetupMarketCacheServer =
                        ValuesStorage.GetBool("Settings.IntegratedSettings.TheSetupMarketCacheServer", true)).Value;
                set {
                    if (Equals(value, _theSetupMarketCacheServer)) return;
                    _theSetupMarketCacheServer = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheServer", value);
                    OnPropertyChanged();
                }
            }

            private bool? _theSetupMarketTab;

            public bool TheSetupMarketTab {
                get => _theSetupMarketTab ?? (_theSetupMarketTab = ValuesStorage.GetBool("Settings.IntegratedSettings.TheSetupMarketTab", false)).Value;
                set {
                    if (Equals(value, _theSetupMarketTab)) return;
                    _theSetupMarketTab = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketTab", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TheSetupMarketCounter));
                }
            }

            private bool? _theSetupMarketCounter;

            public bool TheSetupMarketCounter {
                get => TheSetupMarketTab && (_theSetupMarketCounter ??
                        (_theSetupMarketCounter = ValuesStorage.GetBool("Settings.IntegratedSettings.TheSetupMarketCounter", false)).Value);
                set {
                    if (Equals(value, _theSetupMarketCounter)) return;
                    _theSetupMarketCounter = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCounter", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrLimitTemperature;

            public bool RsrLimitTemperature {
                get => _rsrLimitTemperature ?? (_rsrLimitTemperature = ValuesStorage.GetBool("Settings.IntegratedSettings.RsrLimitTemperature", true)).Value;
                set {
                    if (Equals(value, _rsrLimitTemperature)) return;
                    _rsrLimitTemperature = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.RsrLimitTemperature", value);
                    OnPropertyChanged();
                }
            }
        }

        private static IntegratedSettings _integrated;

        public static IntegratedSettings Integrated => _integrated ?? (_integrated = new IntegratedSettings());

        public class PluginsSettings : NotifyPropertyChanged {
            internal PluginsSettings() { }

            private bool? _cefFilterAds;

            public bool CefFilterAds {
                get => _cefFilterAds ?? (_cefFilterAds = ValuesStorage.GetBool("Settings.PluginsSettings.CefFilterAds", false)).Value;
                set {
                    if (Equals(value, _cefFilterAds)) return;
                    _cefFilterAds = value;
                    ValuesStorage.Set("Settings.PluginsSettings.CefFilterAds", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageMemoryLimit;

            public long MontageMemoryLimit {
                get => _montageMemoryLimit ?? (_montageMemoryLimit = ValuesStorage.GetLong("Settings.PluginsSettings.MontageMemoryLimit", 2147483648L)).Value;
                set {
                    if (Equals(value, _montageMemoryLimit)) return;
                    _montageMemoryLimit = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageMemoryLimit", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageVramCache;

            public long MontageVramCache {
                get => _montageVramCache ?? (_montageVramCache = ValuesStorage.GetLong("Settings.PluginsSettings.MontageVramCache", 536870912L)).Value;
                set {
                    if (Equals(value, _montageVramCache)) return;
                    _montageVramCache = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageVramCache", value);
                    OnPropertyChanged();
                }
            }

            public string MontageDefaultTemporaryDirectory => Path.Combine(Path.GetTempPath(), "CMMontage");

            private string _montageTemporaryDirectory;

            public string MontageTemporaryDirectory {
                get => _montageTemporaryDirectory ?? (_montageTemporaryDirectory = ValuesStorage.GetString("Settings.PluginsSettings.MontageTemporaryDirectory",
                        MontageDefaultTemporaryDirectory));
                set {
                    value = value.Trim();
                    if (Equals(value, _montageTemporaryDirectory)) return;
                    _montageTemporaryDirectory = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageTemporaryDirectory", value);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _changeMontageTemporaryDirectoryCommand;

            public DelegateCommand ChangeMontageTemporaryDirectoryCommand
                => _changeMontageTemporaryDirectoryCommand ?? (_changeMontageTemporaryDirectoryCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = MontageTemporaryDirectory
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        MontageTemporaryDirectory = dialog.SelectedPath;
                    }
                }));
        }

        private static PluginsSettings _plugins;

        public static PluginsSettings Plugins => _plugins ?? (_plugins = new PluginsSettings());

        public class GenericModsSettings : NotifyPropertyChanged {
            internal GenericModsSettings() { }

            private bool? _useHardLinks;

            public bool UseHardLinks {
                get => _useHardLinks ?? (_useHardLinks = ValuesStorage.GetBool("Settings.GenericModsSettings.UseHardLinks", true)).Value;
                set {
                    if (Equals(value, _useHardLinks)) return;
                    _useHardLinks = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.UseHardLinks", value);
                    OnPropertyChanged();
                }
            }

            private bool? _detectWhileInstalling;

            public bool DetectWhileInstalling {
                get => _detectWhileInstalling ??
                        (_detectWhileInstalling = ValuesStorage.GetBool("Settings.GenericModsSettings.DetectWhileInstalling", true)).Value;
                set {
                    if (Equals(value, _detectWhileInstalling)) return;
                    _detectWhileInstalling = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.DetectWhileInstalling", value);
                    OnPropertyChanged();
                }
            }

            private string _modsDirectory;

            public string ModsDirectory {
                get => _modsDirectory ??
                        (_modsDirectory = ValuesStorage.GetString("Settings.GenericModsSettings.ModsDirectory", "mods"));
                set {
                    value = value.Trim();
                    if (Equals(value, _modsDirectory)) return;
                    _modsDirectory = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.ModsDirectory", value);
                    OnPropertyChanged();
                }
            }

            public string GetModsDirectory() {
                var value = ModsDirectory;

                if (string.IsNullOrWhiteSpace(value)) {
                    value = "mods";
                }

                if (!Path.IsPathRooted(value)) {
                    value = Path.Combine(AcRootDirectory.Instance.RequireValue, value);
                }

                return value;
            }
        }

        private static GenericModsSettings _genericMods;

        public static GenericModsSettings GenericMods => _genericMods ?? (_genericMods = new GenericModsSettings());
    }
}
