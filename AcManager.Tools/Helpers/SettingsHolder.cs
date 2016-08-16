using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

// ReSharper disable RedundantArgumentDefaultValue

namespace AcManager.Tools.Helpers {
    public class SettingsHolder {
        public sealed class PeriodEntry : Displayable {
            public TimeSpan TimeSpan { get; }

            public PeriodEntry() {}

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

        public class SearchEngineEntry {
            public string DisplayName { get; internal set; }

            public string Value { get; internal set; }

            public string GetUri(string s) {
                if (Content.SearchWithWikipedia) {
                    s = @"site:wikipedia.org " + s;
                }

                return string.Format(Value, Uri.EscapeDataString(s).Replace(@"%20", @"+"));
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
                set { OnlineServerId = value.Id; }
            }

            private int? _onlineServerId;

            public int OnlineServerId {
                get { return _onlineServerId ?? (_onlineServerId = ValuesStorage.GetInt("Settings.OnlineSettings.OnlineServerId", 1)).Value; }
                set {
                    if (Equals(value, _onlineServerId)) return;
                    _onlineServerId = value;
                    ValuesStorage.Set("Settings.OnlineSettings.OnlineServerId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _compactUi;

            public bool CompactUi {
                get { return _compactUi ?? (_compactUi = ValuesStorage.GetBool("Settings.OnlineSettings.CompactUi", false)).Value; }
                set {
                    if (Equals(value, _compactUi)) return;
                    _compactUi = value;
                    ValuesStorage.Set("Settings.OnlineSettings.CompactUi", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rememberPasswords;

            public bool RememberPasswords {
                get { return _rememberPasswords ?? (_rememberPasswords = ValuesStorage.GetBool("Settings.OnlineSettings.RememberPasswords", true)).Value; }
                set {
                    if (Equals(value, _rememberPasswords)) return;
                    _rememberPasswords = value;
                    ValuesStorage.Set("Settings.OnlineSettings.RememberPasswords", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverPresetsManaging;

            public bool ServerPresetsManaging {
                get {
                    return _serverPresetsManaging ??
                            (_serverPresetsManaging = ValuesStorage.GetBool("Settings.OnlineSettings.ServerPresetsManaging", false)).Value;
                }
                set {
                    if (Equals(value, _serverPresetsManaging)) return;
                    _serverPresetsManaging = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsManaging", value);
                    OnPropertyChanged();
                }
            }

            private bool? _loadServerInformationDirectly;

            public bool LoadServerInformationDirectly {
                get {
                    return _loadServerInformationDirectly ??
                            (_loadServerInformationDirectly = ValuesStorage.GetBool("Settings.OnlineSettings.LoadServerInformationDirectly", false)).Value;
                }
                set {
                    if (Equals(value, _loadServerInformationDirectly)) return;
                    _loadServerInformationDirectly = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LoadServerInformationDirectly", value);
                    OnPropertyChanged();
                }
            }

            private bool? _pingingWithThreads;

            public bool ThreadsPing {
                get { return _pingingWithThreads ?? (_pingingWithThreads = ValuesStorage.GetBool("Settings.OnlineSettings.ThreadsPing", true)).Value; }
                set {
                    if (Equals(value, _pingingWithThreads)) return;
                    _pingingWithThreads = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ThreadsPing", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingingConcurrency;

            public int PingConcurrency {
                get { return _pingingConcurrency ?? (_pingingConcurrency = ValuesStorage.GetInt("Settings.OnlineSettings.PingConcurrency", 30)).Value; }
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
                get { return _pingTimeout ?? (_pingTimeout = ValuesStorage.GetInt("Settings.OnlineSettings.PingTimeout", 2000)).Value; }
                set {
                    if (Equals(value, _pingTimeout)) return;
                    _pingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private int? _scanPingTimeout;

            public int ScanPingTimeout {
                get { return _scanPingTimeout ?? (_scanPingTimeout = ValuesStorage.GetInt("Settings.OnlineSettings.ScanPingTimeout", 1000)).Value; }
                set {
                    if (Equals(value, _scanPingTimeout)) return;
                    _scanPingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ScanPingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private string _portsEnumeration;

            public string PortsEnumeration {
                get { return _portsEnumeration ?? (_portsEnumeration = ValuesStorage.GetString("Settings.OnlineSettings.PortsEnumeration", @"9000-10000")); }
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
                get {
                    return _lanPortsEnumeration ??
                            (_lanPortsEnumeration = ValuesStorage.GetString("Settings.OnlineSettings.LanPortsEnumeration", @"9456-9458,9556,9600-9612,9700"));
                }
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
                get { return _ignoredInterfaces ?? (_ignoredInterfaces = ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces").ToList()); }
                set {
                    if (Equals(value, _ignoredInterfaces)) return;
                    _ignoredInterfaces = value.ToList();
                    ValuesStorage.Set("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }
        }

        private static OnlineSettings _online;

        public static OnlineSettings Online => _online ?? (_online = new OnlineSettings());

        public class CommonSettings : NotifyPropertyChanged {
            internal CommonSettings() {}

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
                get {
                    return _updateToNontestedVersions ??
                            (_updateToNontestedVersions = ValuesStorage.GetBool("Settings.CommonSettings.UpdateToNontestedVersions", false)).Value;
                }
                set {
                    if (Equals(value, _updateToNontestedVersions)) return;
                    _updateToNontestedVersions = value;
                    ValuesStorage.Set("Settings.CommonSettings.UpdateToNontestedVersions", value);
                    OnPropertyChanged();
                }
            }

            private bool? _createStartMenuShortcutIfMissing;

            public bool CreateStartMenuShortcutIfMissing {
                get {
                    return _createStartMenuShortcutIfMissing ??
                            (_createStartMenuShortcutIfMissing = ValuesStorage.GetBool("Settings.CommonSettings.CreateStartMenuShortcutIfMissing", false)).Value;
                }
                set {
                    if (Equals(value, _createStartMenuShortcutIfMissing)) return;
                    _createStartMenuShortcutIfMissing = value;
                    ValuesStorage.Set("Settings.CommonSettings.CreateStartMenuShortcutIfMissing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _developerMode;

            public bool DeveloperMode {
                get { return MsMode || (_developerMode ?? (_developerMode = ValuesStorage.GetBool("Settings.CommonSettings.DeveloperModeN", false)).Value); }
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
                get { return _msMode ?? (_msMode = ValuesStorage.GetBool("Settings.CommonSettings.DeveloperMode", false)).Value; }
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
                get {
                    return _fixResolutionAutomatically ??
                            (_fixResolutionAutomatically = ValuesStorage.GetBool("Settings.CommonSettings.FixResolutionAutomatically_", false)).Value;
                }
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

                public bool IsAvailable => RequiredAddonId == null || PluginsManager.Instance.IsPluginEnabled(RequiredAddonId);

                internal StarterType(string displayName, string description, string requiredAddonId = null) {
                    Id = displayName;
                    DisplayName = displayName;
                    Description = description;

                    RequiredAddonId = requiredAddonId;
                }
            }

            public static readonly StarterType OfficialStarterType = new StarterType(
                    string.Format(ToolsStrings.Common_Recommended, ToolsStrings.Settings_Starter_Official),
                    "Official way from Kunos; might be slow and unreliable, but doesn’t require patching");

            public static readonly StarterType TrickyStarterType = new StarterType(ToolsStrings.Settings_Starter_Tricky,
                    "Tricky way to start the race; one of the fastest, but doesn’t work without running Steam or Internet-connection");

            public static readonly StarterType UiModuleStarterType = new StarterType("UI Module",
                    "Adds a special UI module in original launcher which listens to some orders and runs the game; use it if you need to use both CM and original launcher at the same time");

            public static readonly StarterType StarterPlusType = new StarterType(ToolsStrings.Settings_Starter_StarterPlus,
                    "Modified version of original launcher, obsolete since 1.7 release",
                    StarterPlus.AddonId);

            public static readonly StarterType SseStarterType = new StarterType(ToolsStrings.Settings_Starter_Sse,
                    "Fastest one, runs game directly without using Steam at all; online will work, but you’ll miss all achievments",
                    SseStarter.AddonId);

            public static readonly StarterType NaiveStarterType = new StarterType(ToolsStrings.Settings_Starter_Naive,
                    "Just tries to run acs.exe directly; in most cases will fail");

            private StarterType _selectedStarterType;

            [NotNull]
            public StarterType SelectedStarterType {
                get {
                    return _selectedStarterType ??
                            (_selectedStarterType = StarterTypes.GetByIdOrDefault(ValuesStorage.GetString("Settings.DriveSettings.SelectedStarterType")) ??
                                    StarterTypes.First());
                }
                set {
                    if (Equals(value, _selectedStarterType)) return;
                    _selectedStarterType = value;
                    ValuesStorage.Set("Settings.DriveSettings.SelectedStarterType", value.Id);
                    OnPropertyChanged();

                    if (value == UiModuleStarterType && ModuleStarter.TryToInstallModule() && ModuleStarter.IsAssettoCorsaRunning) {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                            ModernDialog.ShowMessage("UI module “CM Helper” installed and activated. Don’t forget to restart AssettoCorsa.exe before racing!");
                        }));
                    }
                }
            }

            private StarterType[] _starterTypes;

            public StarterType[] StarterTypes => _starterTypes ?? (_starterTypes = new[] {
                OfficialStarterType, TrickyStarterType, UiModuleStarterType, StarterPlusType, SseStarterType, NaiveStarterType
            });

            private bool? _copyFilterToSystemForOculus;

            public bool CopyFilterToSystemForOculus {
                get {
                    return _copyFilterToSystemForOculus ??
                            (_copyFilterToSystemForOculus = ValuesStorage.GetBool("Settings.DriveSettings.CopyFilterToSystemForOculus", true)).Value;
                }
                set {
                    if (Equals(value, _copyFilterToSystemForOculus)) return;
                    _copyFilterToSystemForOculus = value;
                    ValuesStorage.Set("Settings.DriveSettings.CopyFilterToSystemForOculus", value);
                    OnPropertyChanged();
                }
            }

            private string _preCommand;

            public string PreCommand {
                get { return _preCommand ?? (_preCommand = ValuesStorage.GetString("Settings.DriveSettings.PreCommand", "")); }
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
                get { return _postCommand ?? (_postCommand = ValuesStorage.GetString("Settings.DriveSettings.PostCommand", "")); }
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
                get { return _preReplayCommand ?? (_preReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PreReplayCommand", "")); }
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
                get { return _postReplayCommand ?? (_postReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PostReplayCommand", "")); }
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
                get { return _immediateStart ?? (_immediateStart = ValuesStorage.GetBool("Settings.DriveSettings.ImmediateStart", false)).Value; }
                set {
                    if (Equals(value, _immediateStart)) return;
                    _immediateStart = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateStart", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skipPracticeResults;

            public bool SkipPracticeResults {
                get { return _skipPracticeResults ?? (_skipPracticeResults = ValuesStorage.GetBool("Settings.DriveSettings.SkipPracticeResults", false)).Value; }
                set {
                    if (Equals(value, _skipPracticeResults)) return;
                    _skipPracticeResults = value;
                    ValuesStorage.Set("Settings.DriveSettings.SkipPracticeResults", value);
                    OnPropertyChanged();
                }
            }

            private bool? _tryToLoadReplays;

            public bool TryToLoadReplays {
                get { return _tryToLoadReplays ?? (_tryToLoadReplays = ValuesStorage.GetBool("Settings.DriveSettings.TryToLoadReplays", true)).Value; }
                set {
                    if (Equals(value, _tryToLoadReplays)) return;
                    _tryToLoadReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.TryToLoadReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoSaveReplays;

            public bool AutoSaveReplays {
                get { return _autoSaveReplays ?? (_autoSaveReplays = ValuesStorage.GetBool("Settings.DriveSettings.AutoSaveReplays", false)).Value; }
                set {
                    if (Equals(value, _autoSaveReplays)) return;
                    _autoSaveReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoSaveReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoAddReplaysExtension;

            public bool AutoAddReplaysExtension {
                get {
                    return _autoAddReplaysExtension ??
                            (_autoAddReplaysExtension = ValuesStorage.GetBool("Settings.DriveSettings.AutoAddReplaysExtension", true)).Value;
                }
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
                get { return _replaysNameFormat ?? (_replaysNameFormat = ValuesStorage.GetString("Settings.DriveSettings.ReplaysNameFormat", DefaultReplaysNameFormat)); }
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
                get { return _use32BitVersion ?? (_use32BitVersion = ValuesStorage.GetBool("Settings.DriveSettings.Use32BitVersion", false)).Value; }
                set {
                    if (Equals(value, _use32BitVersion)) return;
                    _use32BitVersion = value;
                    ValuesStorage.Set("Settings.DriveSettings.Use32BitVersion", value);
                    OnPropertyChanged();
                }
            }

            private string _playerName;

            public string PlayerName {
                get { return _playerName ?? (_playerName = ValuesStorage.GetString("Settings.DriveSettings.PlayerName", null)); }
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
                get { return _playerNationality ?? (_playerNationality = ValuesStorage.GetString("Settings.DriveSettings.PlayerNationality", null)); }
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
                get {
                    return _differentPlayerNameOnline ??
                            (_differentPlayerNameOnline = ValuesStorage.GetBool("Settings.DriveSettings.DifferentPlayerNameOnline", false)).Value;
                }
                set {
                    if (Equals(value, _differentPlayerNameOnline)) return;
                    _differentPlayerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.DifferentPlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private string _playerNameOnline;

            public string PlayerNameOnline {
                get { return _playerNameOnline ?? (_playerNameOnline = ValuesStorage.GetString("Settings.DriveSettings.PlayerNameOnline", PlayerName)); }
                set {
                    value = value.Trim();
                    if (Equals(value, _playerNameOnline)) return;
                    _playerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveCacheBrands;

            public bool QuickDriveCacheBrands {
                get { return _quickDriveCacheBrands ?? (_quickDriveCacheBrands = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveCacheBrands", true)).Value; }
                set {
                    if (Equals(value, _quickDriveCacheBrands)) return;
                    _quickDriveCacheBrands = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveCacheBrands", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveExpandBounds;

            public bool QuickDriveExpandBounds {
                get { return _quickDriveExpandBounds ?? (_quickDriveExpandBounds = ValuesStorage.GetBool("Settings.DriveSettings.ExpandBounds", false)).Value; }
                set {
                    if (Equals(value, _quickDriveExpandBounds)) return;
                    _quickDriveExpandBounds = value;
                    ValuesStorage.Set("Settings.DriveSettings.ExpandBounds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _kunosCareerUserAiLevel;

            public bool KunosCareerUserAiLevel {
                get { return _kunosCareerUserAiLevel ?? (_kunosCareerUserAiLevel = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserAiLevel", false)).Value; }
                set {
                    if (Equals(value, _kunosCareerUserAiLevel)) return;
                    _kunosCareerUserAiLevel = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserAiLevel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _kunosCareerUserSkin;

            public bool KunosCareerUserSkin {
                get { return _kunosCareerUserSkin ?? (_kunosCareerUserSkin = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserSkin", true)).Value; }
                set {
                    if (Equals(value, _kunosCareerUserSkin)) return;
                    _kunosCareerUserSkin = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserSkin", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickSwitches;

            public bool QuickSwitches {
                get { return _quickSwitches ?? (_quickSwitches = ValuesStorage.GetBool("Settings.DriveSettings.QuickSwitches", true)).Value; }
                set {
                    if (Equals(value, _quickSwitches)) return;
                    _quickSwitches = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitches", value);
                    OnPropertyChanged();
                }
            }

            private string[] _quickSwitchesList;

            public string[] QuickSwitchesList {
                get {
                    return _quickSwitchesList ??
                            (_quickSwitchesList = ValuesStorage.GetStringList("Settings.DriveSettings.QuickSwitchesList", new[] {
                                @"WidgetExposure",
                                @"WidgetUiPresets",
                                @"WidgetHideDriveArms",
                                @"WidgetHideSteeringWheel"
                            }).ToArray());
                }
                set {
                    if (Equals(value, _quickSwitchesList)) return;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitchesList", value);
                    _quickSwitchesList = value;
                    OnPropertyChanged();
                }
            }

            private bool? _automaticallyConvertBmpToJpg;

            public bool AutomaticallyConvertBmpToJpg {
                get {
                    return _automaticallyConvertBmpToJpg ??
                            (_automaticallyConvertBmpToJpg = ValuesStorage.GetBool("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", false)).Value;
                }
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
                get { return _localAddress ?? (_localAddress = ValuesStorage.GetString("Settings.DriveSettings.LocalAddress", null)); }
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
                get { return _weatherSpecificClouds ?? (_weatherSpecificClouds = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificClouds", true)).Value; }
                set {
                    if (Equals(value, _weatherSpecificClouds)) return;
                    _weatherSpecificClouds = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificClouds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificPpFilter;

            public bool WeatherSpecificPpFilter {
                get { return _weatherSpecificPpFilter ?? (_weatherSpecificPpFilter = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificPpFilter", true)).Value; }
                set {
                    if (Equals(value, _weatherSpecificPpFilter)) return;
                    _weatherSpecificPpFilter = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificPpFilter", value);
                    OnPropertyChanged();
                }
            }
        }

        private static DriveSettings _drive;

        public static DriveSettings Drive => _drive ?? (_drive = new DriveSettings());

        public class ContentSettings : NotifyPropertyChanged {
            internal ContentSettings() { }

            private int? _loadingConcurrency;

            public int LoadingConcurrency {
                get {
                    return _loadingConcurrency ??
                            (_loadingConcurrency =
                                    ValuesStorage.GetInt("Settings.ContentSettings.LoadingConcurrency", BaseAcManagerNew.OptionAcObjectsLoadingConcurrency))
                                    .Value;
                }
                set {
                    value = value < 1 ? 1 : value;
                    if (Equals(value, _loadingConcurrency)) return;
                    _loadingConcurrency = value;
                    ValuesStorage.Set("Settings.ContentSettings.LoadingConcurrency", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsYearPostfix;

            public bool CarsYearPostfix {
                get { return _carsYearPostfix ?? (_carsYearPostfix = ValuesStorage.GetBool("Settings.ContentSettings.CarsYearPostfix", false)).Value; }
                set {
                    if (Equals(value, _carsYearPostfix)) return;
                    _carsYearPostfix = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsYearPostfix", value);
                    OnPropertyChanged();
                }
            }

            private bool? _changeBrandIconAutomatically;

            public bool ChangeBrandIconAutomatically {
                get {
                    return _changeBrandIconAutomatically ??
                            (_changeBrandIconAutomatically = ValuesStorage.GetBool("Settings.ContentSettings.ChangeBrandIconAutomatically", true)).Value;
                }
                set {
                    if (Equals(value, _changeBrandIconAutomatically)) return;
                    _changeBrandIconAutomatically = value;
                    ValuesStorage.Set("Settings.ContentSettings.ChangeBrandIconAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _downloadShowroomPreviews;

            public bool DownloadShowroomPreviews {
                get {
                    return _downloadShowroomPreviews ??
                            (_downloadShowroomPreviews = ValuesStorage.GetBool("Settings.ContentSettings.DownloadShowroomPreviews", true)).Value;
                }
                set {
                    if (Equals(value, _downloadShowroomPreviews)) return;
                    _downloadShowroomPreviews = value;
                    ValuesStorage.Set("Settings.ContentSettings.DownloadShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _scrollAutomatically;

            public bool ScrollAutomatically {
                get { return _scrollAutomatically ?? (_scrollAutomatically = ValuesStorage.GetBool("Settings.ContentSettings.ScrollAutomatically", true)).Value; }
                set {
                    if (Equals(value, _scrollAutomatically)) return;
                    _scrollAutomatically = value;
                    ValuesStorage.Set("Settings.ContentSettings.ScrollAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private string _fontIconCharacter;

            public string FontIconCharacter {
                get { return _fontIconCharacter ?? (_fontIconCharacter = ValuesStorage.GetString("Settings.ContentSettings.FontIconCharacter", @"A")); }
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
                get { return _skinsSkipPriority ?? (_skinsSkipPriority = ValuesStorage.GetBool("Settings.ContentSettings.SkinsSkipPriority", false)).Value; }
                set {
                    if (Equals(value, _skinsSkipPriority)) return;
                    _skinsSkipPriority = value;
                    ValuesStorage.Set("Settings.ContentSettings.SkinsSkipPriority", value);
                    OnPropertyChanged();
                }
            }

            private PeriodEntry[] _periodEntries;

            public PeriodEntry[] NewContentPeriods => _periodEntries ?? (_periodEntries = new[] {
                new PeriodEntry(TimeSpan.Zero),
                new PeriodEntry(TimeSpan.FromDays(1)),
                new PeriodEntry(TimeSpan.FromDays(3)),
                new PeriodEntry(TimeSpan.FromDays(7)),
                new PeriodEntry(TimeSpan.FromDays(14)),
                new PeriodEntry(TimeSpan.FromDays(30)),
                new PeriodEntry(TimeSpan.FromDays(60))
            });

            private PeriodEntry _newContentPeriod;

            public PeriodEntry NewContentPeriod {
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

            private bool? _deleteConfirmation;

            public bool DeleteConfirmation {
                get { return _deleteConfirmation ?? (_deleteConfirmation = ValuesStorage.GetBool("Settings.ContentSettings.DeleteConfirmation", true)).Value; }
                set {
                    if (Equals(value, _deleteConfirmation)) return;
                    _deleteConfirmation = value;
                    ValuesStorage.Set("Settings.ContentSettings.DeleteConfirmation", value);
                    OnPropertyChanged();
                }
            }

            private SearchEngineEntry[] _searchEngines;

            public SearchEngineEntry[] SearchEngines => _searchEngines ?? (_searchEngines = new[] {
                new SearchEngineEntry { DisplayName = ToolsStrings.SearchEngine_DuckDuckGo, Value = @"https://duckduckgo.com/?q={0}&ia=web" },
                new SearchEngineEntry { DisplayName = ToolsStrings.SearchEngine_Bing, Value = @"http://www.bing.com/search?q={0}" },
                new SearchEngineEntry { DisplayName = ToolsStrings.SearchEngine_Google, Value = @"https://www.google.com/search?q={0}&ie=UTF-8" },
                new SearchEngineEntry { DisplayName = ToolsStrings.SearchEngine_Yandex, Value = @"https://yandex.ru/search/?text={0}" },
                new SearchEngineEntry { DisplayName = ToolsStrings.SearchEngine_Baidu, Value = @"http://www.baidu.com/s?ie=utf-8&wd={0}" }
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
                get { return _searchWithWikipedia ?? (_searchWithWikipedia = ValuesStorage.GetBool("Settings.ContentSettings.SearchWithWikipedia", true)).Value; }
                set {
                    if (Equals(value, _searchWithWikipedia)) return;
                    _searchWithWikipedia = value;
                    ValuesStorage.Set("Settings.ContentSettings.SearchWithWikipedia", value);
                    OnPropertyChanged();
                }
            }
        }

        private static ContentSettings _content;

        public static ContentSettings Content => _content ?? (_content = new ContentSettings());

        public class CustomShowroomSettings : NotifyPropertyChanged {
            internal CustomShowroomSettings() { }

            public string[] ShowroomTypes { get; } = { ToolsStrings.CustomShowroom_Fancy, ToolsStrings.CustomShowroom_Lite };

            public string ShowroomType {
                get { return LiteByDefault ? ShowroomTypes[1] : ShowroomTypes[0]; }
                set { LiteByDefault = value == ShowroomTypes[1]; }
            }

            private bool? _liteByDefault;

            public bool LiteByDefault {
                get { return _liteByDefault ?? (_liteByDefault = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteByDefault", true)).Value; }
                set {
                    if (Equals(value, _liteByDefault)) return;
                    _liteByDefault = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteByDefault", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseFxaa;

            public bool LiteUseFxaa {
                get { return _liteUseFxaa ?? (_liteUseFxaa = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseFxaa", true)).Value; }
                set {
                    if (Equals(value, _liteUseFxaa)) return;
                    _liteUseFxaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseFxaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseMsaa;

            public bool LiteUseMsaa {
                get { return _liteUseMsaa ?? (_liteUseMsaa = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseMsaa", false)).Value; }
                set {
                    if (Equals(value, _liteUseMsaa)) return;
                    _liteUseMsaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseMsaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseBloom;

            public bool LiteUseBloom {
                get { return _liteUseBloom ?? (_liteUseBloom = ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseBloom", true)).Value; }
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
                get { return _showroomId ?? (_showroomId = ValuesStorage.GetString("Settings.CustomShowroomSettings.ShowroomId", @"showroom")); }
                set {
                    value = value?.Trim();
                    if (Equals(value, _showroomId)) return;
                    _showroomId = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.ShowroomId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _useFxaa;

            public bool UseFxaa {
                get { return _useFxaa ?? (_useFxaa = ValuesStorage.GetBool("Settings.CustomShowroomSettings.UseFxaa", true)).Value; }
                set {
                    if (Equals(value, _useFxaa)) return;
                    _useFxaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.UseFxaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smartCameraPivot;

            public bool SmartCameraPivot {
                get { return _smartCameraPivot ?? (_smartCameraPivot = ValuesStorage.GetBool("Settings.CustomShowroomSettings.SmartCameraPivot", true)).Value; }
                set {
                    if (Equals(value, _smartCameraPivot)) return;
                    _smartCameraPivot = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.SmartCameraPivot", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alternativeControlScheme;

            public bool AlternativeControlScheme {
                get {
                    return _alternativeControlScheme ??
                            (_alternativeControlScheme = ValuesStorage.GetBool("Settings.CustomShowroomSettings.AlternativeControlScheme", false)).Value;
                }
                set {
                    if (Equals(value, _alternativeControlScheme)) return;
                    _alternativeControlScheme = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.AlternativeControlScheme", value);
                    OnPropertyChanged();
                }
            }
        }

        private static CustomShowroomSettings _customShowroom;

        public static CustomShowroomSettings CustomShowroom => _customShowroom ?? (_customShowroom = new CustomShowroomSettings());

        public class SharingSettings : NotifyPropertyChanged {
            internal SharingSettings() { }

            private bool? _customIds;

            public bool CustomIds {
                get { return _customIds ?? (_customIds = ValuesStorage.GetBool("Settings.SharingSettings.CustomIds", false)).Value; }
                set {
                    if (Equals(value, _customIds)) return;
                    _customIds = value;
                    ValuesStorage.Set("Settings.SharingSettings.CustomIds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _verifyBeforeSharing;

            public bool VerifyBeforeSharing {
                get { return _verifyBeforeSharing ?? (_verifyBeforeSharing = ValuesStorage.GetBool("Settings.SharingSettings.VerifyBeforeSharing", true)).Value; }
                set {
                    if (Equals(value, _verifyBeforeSharing)) return;
                    _verifyBeforeSharing = value;
                    ValuesStorage.Set("Settings.SharingSettings.VerifyBeforeSharing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyLinkToClipboard;

            public bool CopyLinkToClipboard {
                get { return _copyLinkToClipboard ?? (_copyLinkToClipboard = ValuesStorage.GetBool("Settings.SharingSettings.CopyLinkToClipboard", true)).Value; }
                set {
                    if (Equals(value, _copyLinkToClipboard)) return;
                    _copyLinkToClipboard = value;
                    ValuesStorage.Set("Settings.SharingSettings.CopyLinkToClipboard", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareAnonymously;

            public bool ShareAnonymously {
                get { return _shareAnonymously ?? (_shareAnonymously = ValuesStorage.GetBool("Settings.SharingSettings.ShareAnonymously", false)).Value; }
                set {
                    if (Equals(value, _shareAnonymously)) return;
                    _shareAnonymously = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareAnonymously", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareWithoutName;

            public bool ShareWithoutName {
                get { return _shareWithoutName ?? (_shareWithoutName = ValuesStorage.GetBool("Settings.SharingSettings.ShareWithoutName", false)).Value; }
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
                get { return _sharingName ?? (_sharingName = ValuesStorage.GetString("Settings.SharingSettings.SharingName", null) ?? Drive.PlayerNameOnline); }
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
            internal LiveSettings() {}

            private bool? _srsEnabled;

            public bool SrsEnabled {
                get { return _srsEnabled ?? (_srsEnabled = ValuesStorage.GetBool("Settings.LiveSettings.SrsEnabled", true)).Value; }
                set {
                    if (Equals(value, _srsEnabled)) return;
                    _srsEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsCustomStyle;

            public bool SrsCustomStyle {
                get { return _srsCustomStyle ?? (_srsCustomStyle = ValuesStorage.GetBool("Settings.LiveSettings.SrsCustomStyle", true)).Value; }
                set {
                    if (Equals(value, _srsCustomStyle)) return;
                    _srsCustomStyle = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsAutoMode;

            public bool SrsAutoMode {
                get { return _srsAutoMode ?? (_srsAutoMode = ValuesStorage.GetBool("Settings.LiveSettings.SrsAutoMode", true)).Value; }
                set {
                    if (Equals(value, _srsAutoMode)) return;
                    _srsAutoMode = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsAutoMode", value);
                    OnPropertyChanged();
                }
            }

            private string _srsAutoMask;

            public string SrsAutoMask {
                get { return _srsAutoMask ?? (_srsAutoMask = ValuesStorage.GetString("Settings.LiveSettings.SrsAutoMask", @"SimRacingSystem*")); }
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
                get { return _rsrEnabled ?? (_rsrEnabled = ValuesStorage.GetBool("Settings.RsrSettings.RsrEnabled", true)).Value; }
                set {
                    if (Equals(value, _rsrEnabled)) return;
                    _rsrEnabled = value;
                    ValuesStorage.Set("Settings.RsrSettings.RsrEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrCustomStyle;

            public bool RsrCustomStyle {
                get { return _rsrCustomStyle ?? (_rsrCustomStyle = ValuesStorage.GetBool("Settings.RsrSettings.RsrCustomStyle", true)).Value; }
                set {
                    if (Equals(value, _rsrCustomStyle)) return;
                    _rsrCustomStyle = value;
                    ValuesStorage.Set("Settings.RsrSettings.RsrCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrDisableAppAutomatically;

            public bool RsrDisableAppAutomatically {
                get {
                    return _rsrDisableAppAutomatically ??
                            (_rsrDisableAppAutomatically = ValuesStorage.GetBool("Settings.LiveTimingSettings.RsrDisableAppAutomatically", false)).Value;
                }
                set {
                    if (Equals(value, _rsrDisableAppAutomatically)) return;
                    _rsrDisableAppAutomatically = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrDisableAppAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrDifferentPlayerName;

            public bool RsrDifferentPlayerName {
                get {
                    return _rsrDifferentPlayerName ??
                            (_rsrDifferentPlayerName = ValuesStorage.GetBool("Settings.LiveTimingSettings.RsrDifferentPlayerName", false)).Value;
                }
                set {
                    if (Equals(value, _rsrDifferentPlayerName)) return;
                    _rsrDifferentPlayerName = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrDifferentPlayerName", value);
                    OnPropertyChanged();
                }
            }

            private string _rsrPlayerName;

            public string RsrPlayerName {
                get { return _rsrPlayerName ?? (_rsrPlayerName = ValuesStorage.GetString("Settings.LiveTimingSettings.RsrPlayerName", Drive.PlayerName)); }
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
            internal LocaleSettings() {}

            private string _localeName;

            public string LocaleName {
                get { return _localeName ?? (_localeName = ValuesStorage.GetString("Settings.LocaleSettings.LocaleName_", @"en")); }
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
                get { return _loadUnpacked ?? (_loadUnpacked = ValuesStorage.GetBool("Settings.LocaleSettings.LoadUnpacked", false)).Value; }
                set {
                    if (Equals(value, _loadUnpacked)) return;
                    _loadUnpacked = value;
                    ValuesStorage.Set("Settings.LocaleSettings.LoadUnpacked", value);
                    OnPropertyChanged();
                }
            }

            private PeriodEntry _updatePeriod;

            [NotNull]
            public PeriodEntry UpdatePeriod {
                get {
                    return _updatePeriod ?? (_updatePeriod = Common.Periods.FirstOrDefault(x =>
                            x.TimeSpan == ValuesStorage.GetTimeSpan("Settings.LocaleSettings.UpdatePeriod", Common.Periods.ElementAt(4).TimeSpan)) ??
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
                get { return _updateOnStart ?? (_updateOnStart = ValuesStorage.GetBool("Settings.LocaleSettings.UpdateOnStart", true)).Value; }
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
    }
}
