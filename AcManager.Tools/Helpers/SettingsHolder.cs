using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

// ReSharper disable RedundantArgumentDefaultValue

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public sealed class PeriodEntry : Displayable {
            public TimeSpan TimeSpan { get; }

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
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.OnlineSettings.RefreshPeriod");
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
                get => _onlineServerId ?? (_onlineServerId = ValuesStorage.Get("Settings.OnlineSettings.OnlineServerId", 1)).Value;
                set {
                    if (Equals(value, _onlineServerId)) return;
                    _onlineServerId = value;
                    ValuesStorage.Set("Settings.OnlineSettings.OnlineServerId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _compactUi;

            public bool CompactUi {
                get => _compactUi ?? (_compactUi = ValuesStorage.Get("Settings.OnlineSettings.CompactUi", false)).Value;
                set {
                    if (Equals(value, _compactUi)) return;
                    _compactUi = value;
                    ValuesStorage.Set("Settings.OnlineSettings.CompactUi", value);
                    OnPropertyChanged();
                }
            }

            private bool? _showBrandBadges;

            public bool ShowBrandBadges {
                get => _showBrandBadges ?? (_showBrandBadges = ValuesStorage.Get("Settings.OnlineSettings.ShowBrandBadges", true)).Value;
                set {
                    if (Equals(value, _showBrandBadges)) return;
                    _showBrandBadges = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ShowBrandBadges", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rememberPasswords;

            public bool RememberPasswords {
                get => _rememberPasswords ?? (_rememberPasswords = ValuesStorage.Get("Settings.OnlineSettings.RememberPasswords", true)).Value;
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
                        (_loadServersWithMissingContent = ValuesStorage.Get("Settings.OnlineSettings.LoadServersWithMissingContent2", true)).Value;
                set {
                    if (Equals(value, _loadServersWithMissingContent)) return;
                    _loadServersWithMissingContent = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LoadServersWithMissingContent2", value);
                    OnPropertyChanged();
                }
            }

            private bool? _integrateMinorating;

            public bool IntegrateMinorating {
                get => _integrateMinorating ?? (_integrateMinorating = ValuesStorage.Get("Settings.OnlineSettings.IntegrateMinorating", true)).Value;
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
                    var saved = ValuesStorage.Get<int?>("Settings.IntegratedSettings.FixNamesMode");
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
                        (_alwaysAllowToUsePassword = ValuesStorage.Get("Settings.OnlineSettings.AlwaysAllowToUsePassword", true)).Value;
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
                        (_loadServerInformationDirectly = ValuesStorage.Get("Settings.OnlineSettings.LoadServerInformationDirectly", false)).Value;
                set {
                    if (Equals(value, _loadServerInformationDirectly)) return;
                    _loadServerInformationDirectly = value;
                    ValuesStorage.Set("Settings.OnlineSettings.LoadServerInformationDirectly", value);
                    OnPropertyChanged();
                }
            }

            private bool? _pingOnlyOnce;

            public bool PingOnlyOnce {
                get => _pingOnlyOnce ?? (_pingOnlyOnce = ValuesStorage.Get("Settings.OnlineSettings.PingOnlyOnce", true)).Value;
                set {
                    if (Equals(value, _pingOnlyOnce)) return;
                    _pingOnlyOnce = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingOnlyOnce", value);
                    OnPropertyChanged();
                }
            }

            private bool? _pingingWithThreads;

            public bool ThreadsPing {
                get => _pingingWithThreads ?? (_pingingWithThreads = ValuesStorage.Get("Settings.OnlineSettings.ThreadsPing", false)).Value;
                set {
                    if (Equals(value, _pingingWithThreads)) return;
                    _pingingWithThreads = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ThreadsPing", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingingConcurrency;

            public int PingConcurrency {
                get => _pingingConcurrency ?? (_pingingConcurrency = ValuesStorage.Get("Settings.OnlineSettings.PingConcurrency", 10)).Value;
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
                get => _pingTimeout ?? (_pingTimeout = ValuesStorage.Get("Settings.OnlineSettings.PingTimeout", 2000)).Value;
                set {
                    if (Equals(value, _pingTimeout)) return;
                    _pingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private int? _scanPingTimeout;

            public int ScanPingTimeout {
                get => _scanPingTimeout ?? (_scanPingTimeout = ValuesStorage.Get("Settings.OnlineSettings.ScanPingTimeout", 1000)).Value;
                set {
                    if (Equals(value, _scanPingTimeout)) return;
                    _scanPingTimeout = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ScanPingTimeout", value);
                    OnPropertyChanged();
                }
            }

            private string _portsEnumeration;

            public string PortsEnumeration {
                get => _portsEnumeration ?? (_portsEnumeration = ValuesStorage.Get("Settings.OnlineSettings.PortsEnumeration", @"9000-10000"));
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
                        (_lanPortsEnumeration = ValuesStorage.Get("Settings.OnlineSettings.LanPortsEnumeration", @"9000-10000"));
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
                    ValuesStorage.Storage.SetStringList("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }

            private bool? _searchForMissingContent;

            public bool SearchForMissingContent {
                get => _searchForMissingContent ??
                        (_searchForMissingContent = ValuesStorage.Get("Settings.OnlineSettings.SearchForMissingContent", false)).Value;
                set {
                    if (Equals(value, _searchForMissingContent)) return;
                    _searchForMissingContent = value;
                    ValuesStorage.Set("Settings.OnlineSettings.SearchForMissingContent", value);
                    OnPropertyChanged();
                }
            }

            private int? _pingAttempts;

            public int PingAttempts {
                get => _pingAttempts ?? (_pingAttempts = ValuesStorage.Get("Settings.OnlineSettings.PingAttempts", 10)).Value;
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
                        (_serverPresetsManaging = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsManaging", false)).Value;
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
                        (_serverPresetsAutoSave = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsAutoSave", true)).Value;
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
                        (_serverPresetsUpdateDataAutomatically = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsUpdateDataAutomatically", true))
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
                        (_serverPresetsFitInFewerTabs = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsFitInFewerTabs", false)).Value;
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
                        (_temperatureUnitMode = ValuesStorage.Get("Settings.CommonSettings.TemperatureUnitMode", TemperatureUnitMode.Celsius)).Value;
                set {
                    if (Equals(value, _temperatureUnitMode)) return;
                    _temperatureUnitMode = value;
                    ValuesStorage.Set("Settings.CommonSettings.TemperatureUnitMode", value);
                    OnPropertyChanged();
                }
            }

            public static PeriodEntry PeriodDisabled = new PeriodEntry(TimeSpan.Zero);
            public static PeriodEntry PeriodStartup = new PeriodEntry(ToolsStrings.Settings_Period_Startup);

            private PeriodEntry[] _periodEntries;

            public PeriodEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
                PeriodDisabled,
                PeriodStartup,
                new PeriodEntry(TimeSpan.FromMinutes(30)),
                new PeriodEntry(TimeSpan.FromHours(3)),
                new PeriodEntry(TimeSpan.FromHours(6)),
                new PeriodEntry(TimeSpan.FromDays(1))
            });

            private PeriodEntry _updatePeriod;

            [NotNull]
            public PeriodEntry UpdatePeriod {
                get {
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.CommonSettings.UpdatePeriod");
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

            private SettingEntry[] _registryModes;

            public SettingEntry[] RegistryModes => _registryModes ?? (_registryModes = new[] {
                new SettingEntry("off", "Disabled (Not Recommended, Shared Links Won’t Work)"),
                new SettingEntry("protocolOnly", "Content Manager Protocol Only (For Shared Links)"),
                new SettingEntry("protocolAndFiles", "Full Integration (Protocol And Files)")
            });

            private SettingEntry _registryMode;

            [NotNull]
            public SettingEntry RegistryMode {
                get {
                    var saved = ValuesStorage.Get<string>("Settings.CommonSettings.RegistryMode");
                    return _registryMode ?? (_registryMode = RegistryModes.FirstOrDefault(x => x.Value == saved) ??
                            RegistryModes.ElementAt(2));
                }
                set {
                    if (Equals(value, _registryMode)) return;
                    _registryMode = value;
                    ValuesStorage.Set("Settings.CommonSettings.RegistryMode", value.Value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRegistryEnabled));
                    OnPropertyChanged(nameof(IsRegistryFilesIntegrationEnabled));
                }
            }

            public bool IsRegistryEnabled => RegistryMode.Value != "off";
            public bool IsRegistryFilesIntegrationEnabled => RegistryMode.Value == "protocolAndFiles";

            private bool? _updateToNontestedVersions;

            public bool UpdateToNontestedVersions {
                get => _updateToNontestedVersions ??
                        (_updateToNontestedVersions = ValuesStorage.Get("Settings.CommonSettings.UpdateToNontestedVersions", false)).Value;
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
                        (_showDetailedChangelog = ValuesStorage.Get("Settings.CommonSettings.ShowDetailedChangelog", true)).Value;
                set {
                    if (Equals(value, _showDetailedChangelog)) return;
                    _showDetailedChangelog = value;
                    ValuesStorage.Set("Settings.CommonSettings.ShowDetailedChangelog", value);
                    OnPropertyChanged();
                }
            }

            private bool? _developerMode;

            public bool DeveloperMode {
                get => MsMode || (_developerMode ?? (_developerMode = ValuesStorage.Get("Settings.CommonSettings.DeveloperModeN", false)).Value);
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
                get => _msMode ?? (_msMode = ValuesStorage.Get("Settings.CommonSettings.DeveloperMode", false)).Value;
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
                        (_fixResolutionAutomatically = ValuesStorage.Get("Settings.CommonSettings.FixResolutionAutomatically_", false)).Value;
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

        public partial class ContentSettings : NotifyPropertyChanged {
            public bool DeleteConfirmation {
                get => _deleteConfirmation ?? (_deleteConfirmation = ValuesStorage.Get("Settings.ContentSettings.DeleteConfirmation", true)).Value;
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
                            x.DisplayName == ValuesStorage.Get<string>("Settings.ContentSettings.SearchEngine")) ??
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
                get => _searchWithWikipedia ?? (_searchWithWikipedia = ValuesStorage.Get("Settings.ContentSettings.SearchWithWikipedia", true)).Value;
                set {
                    if (Equals(value, _searchWithWikipedia)) return;
                    _searchWithWikipedia = value;
                    ValuesStorage.Set("Settings.ContentSettings.SearchWithWikipedia", value);
                    OnPropertyChanged();
                }
            }

            private MissingContentSearchEntry[] _missingContentSearchEntries;

            public MissingContentSearchEntry[] MissingContentSearchEntries => _missingContentSearchEntries ?? (_missingContentSearchEntries = new[] {
                new MissingContentSearchEntry("Use selected search engine", (type, id) => $"{id} Assetto Corsa", true),
                new MissingContentSearchEntry("Use selected search engine (strict)", (type, id) => $"\"{id}\" Assetto Corsa", true),
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
                }, false)
                // new MissingContentSearchEntry("AcClub (via selected search engine)", (type, id) => $"site:assettocorsa.club {id}", true),
                // new MissingContentSearchEntry("AC Drifting Pro", (type, id) => $"http://www.acdriftingpro.com/?s={HttpUtility.UrlEncode(id)}", false),
                // new MissingContentSearchEntry("RaceDepartment (via selected search engine)", (type, id) => $"site:racedepartment.com {id}", true),
            });

            private MissingContentSearchEntry _missingContentSearch;

            public MissingContentSearchEntry MissingContentSearch {
                get {
                    return _missingContentSearch ?? (_missingContentSearch = MissingContentSearchEntries.FirstOrDefault(x =>
                            x.DisplayName == ValuesStorage.Get<string>("Settings.ContentSettings.MissingContentSearch")) ??
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
                        _carReplaceTyresDonorFilter = ValuesStorage.Get("Settings.ContentSettings.CarReplaceTyresDonorFilter", "k+");
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
                get => _useOldLiteShowroom ?? (_useOldLiteShowroom = ValuesStorage.Get("Settings.CustomShowroomSettings.UseOldLiteShowroom", false)).Value;
                set {
                    if (Equals(value, _useOldLiteShowroom)) return;
                    _useOldLiteShowroom = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.UseOldLiteShowroom", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseFxaa;

            public bool LiteUseFxaa {
                get => _liteUseFxaa ?? (_liteUseFxaa = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseFxaa", true)).Value;
                set {
                    if (Equals(value, _liteUseFxaa)) return;
                    _liteUseFxaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseFxaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseMsaa;

            public bool LiteUseMsaa {
                get => _liteUseMsaa ?? (_liteUseMsaa = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseMsaa", false)).Value;
                set {
                    if (Equals(value, _liteUseMsaa)) return;
                    _liteUseMsaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseMsaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseBloom;

            public bool LiteUseBloom {
                get => _liteUseBloom ?? (_liteUseBloom = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseBloom", true)).Value;
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
                get => _showroomId ?? (_showroomId = ValuesStorage.Get("Settings.CustomShowroomSettings.ShowroomId", @"showroom"));
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
                        (_customShowroomInstead = ValuesStorage.Get("Settings.CustomShowroomSettings.CustomShowroomInstead", false)).Value;
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
                        (_customShowroomPreviews = ValuesStorage.Get("Settings.CustomShowroomSettings.CustomShowroomPreviews", true)).Value;
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
                        (_detailedExifForPreviews = ValuesStorage.Get("Settings.CustomShowroomSettings.DetailedExifForPreviews", true)).Value;
                set {
                    if (Equals(value, _detailedExifForPreviews)) return;
                    _detailedExifForPreviews = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.DetailedExifForPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _previewsRecycleOld;

            public bool PreviewsRecycleOld {
                get => _previewsRecycleOld ?? (_previewsRecycleOld = ValuesStorage.Get("Settings.CustomShowroomSettings.PreviewsRecycleOld", true)).Value;
                set {
                    if (Equals(value, _previewsRecycleOld)) return;
                    _previewsRecycleOld = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.PreviewsRecycleOld", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smartCameraPivot;

            public bool SmartCameraPivot {
                get => _smartCameraPivot ?? (_smartCameraPivot = ValuesStorage.Get("Settings.CustomShowroomSettings.SmartCameraPivot", true)).Value;
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
                        (_alternativeControlScheme = ValuesStorage.Get("Settings.CustomShowroomSettings.AlternativeControlScheme", false)).Value;
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
                get => _customIds ?? (_customIds = ValuesStorage.Get("Settings.SharingSettings.CustomIds", false)).Value;
                set {
                    if (Equals(value, _customIds)) return;
                    _customIds = value;
                    ValuesStorage.Set("Settings.SharingSettings.CustomIds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _verifyBeforeSharing;

            public bool VerifyBeforeSharing {
                get => _verifyBeforeSharing ?? (_verifyBeforeSharing = ValuesStorage.Get("Settings.SharingSettings.VerifyBeforeSharing", true)).Value;
                set {
                    if (Equals(value, _verifyBeforeSharing)) return;
                    _verifyBeforeSharing = value;
                    ValuesStorage.Set("Settings.SharingSettings.VerifyBeforeSharing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyLinkToClipboard;

            public bool CopyLinkToClipboard {
                get => _copyLinkToClipboard ?? (_copyLinkToClipboard = ValuesStorage.Get("Settings.SharingSettings.CopyLinkToClipboard", true)).Value;
                set {
                    if (Equals(value, _copyLinkToClipboard)) return;
                    _copyLinkToClipboard = value;
                    ValuesStorage.Set("Settings.SharingSettings.CopyLinkToClipboard", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareAnonymously;

            public bool ShareAnonymously {
                get => _shareAnonymously ?? (_shareAnonymously = ValuesStorage.Get("Settings.SharingSettings.ShareAnonymously", false)).Value;
                set {
                    if (Equals(value, _shareAnonymously)) return;
                    _shareAnonymously = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareAnonymously", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareWithoutName;

            public bool ShareWithoutName {
                get => _shareWithoutName ?? (_shareWithoutName = ValuesStorage.Get("Settings.SharingSettings.ShareWithoutName", false)).Value;
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
                get => _sharingName ?? (_sharingName = ValuesStorage.Get<string>("Settings.SharingSettings.SharingName") ?? Drive.PlayerNameOnline);
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
                get => _srsEnabled ?? (_srsEnabled = ValuesStorage.Get("Settings.LiveSettings.SrsEnabled", true)).Value;
                set {
                    if (Equals(value, _srsEnabled)) return;
                    _srsEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsCustomStyle;

            public bool SrsCustomStyle {
                get => _srsCustomStyle ?? (_srsCustomStyle = ValuesStorage.Get("Settings.LiveSettings.SrsCustomStyle", true)).Value;
                set {
                    if (Equals(value, _srsCustomStyle)) return;
                    _srsCustomStyle = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsAutoMode;

            public bool SrsAutoMode {
                get => _srsAutoMode ?? (_srsAutoMode = ValuesStorage.Get("Settings.LiveSettings.SrsAutoMode", true)).Value;
                set {
                    if (Equals(value, _srsAutoMode)) return;
                    _srsAutoMode = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsAutoMode", value);
                    OnPropertyChanged();
                }
            }

            private string _srsAutoMask;

            public string SrsAutoMask {
                get => _srsAutoMask ?? (_srsAutoMask = ValuesStorage.Get("Settings.LiveSettings.SrsAutoMask", @"SimRacingSystem*"));
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
                get => _rsrEnabled ?? (_rsrEnabled = ValuesStorage.Get("Settings.RsrSettings.RsrEnabled", true)).Value;
                set {
                    if (Equals(value, _rsrEnabled)) return;
                    _rsrEnabled = value;
                    ValuesStorage.Set("Settings.RsrSettings.RsrEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrCustomStyle;

            public bool RsrCustomStyle {
                get => _rsrCustomStyle ?? (_rsrCustomStyle = ValuesStorage.Get("Settings.RsrSettings.RsrCustomStyle", true)).Value;
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
                        (_rsrDisableAppAutomatically = ValuesStorage.Get("Settings.LiveTimingSettings.RsrDisableAppAutomatically", false)).Value;
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
                        (_rsrDifferentPlayerName = ValuesStorage.Get("Settings.LiveTimingSettings.RsrDifferentPlayerName", false)).Value;
                set {
                    if (Equals(value, _rsrDifferentPlayerName)) return;
                    _rsrDifferentPlayerName = value;
                    ValuesStorage.Set("Settings.LiveTimingSettings.RsrDifferentPlayerName", value);
                    OnPropertyChanged();
                }
            }

            private string _rsrPlayerName;

            public string RsrPlayerName {
                get => _rsrPlayerName ?? (_rsrPlayerName = ValuesStorage.Get("Settings.LiveTimingSettings.RsrPlayerName", Drive.PlayerName));
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
                get => _localeName ?? (_localeName = ValuesStorage.Get("Settings.LocaleSettings.LocaleName_", @"en"));
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
                get => _loadUnpacked ?? (_loadUnpacked = ValuesStorage.Get("Settings.LocaleSettings.LoadUnpacked", false)).Value;
                set {
                    if (Equals(value, _loadUnpacked)) return;
                    _loadUnpacked = value;
                    ValuesStorage.Set("Settings.LocaleSettings.LoadUnpacked", value);
                    OnPropertyChanged();
                }
            }

            private bool? _resxLocalesMode;

            public bool ResxLocalesMode {
                get => _resxLocalesMode ?? (_resxLocalesMode = ValuesStorage.Get("Settings.LocaleSettings.ResxLocalesMode", false)).Value;
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
                            x.TimeSpan == (ValuesStorage.Get<TimeSpan?>("Settings.LocaleSettings.UpdatePeriod") ?? Common.Periods.ElementAt(4).TimeSpan)) ??
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
                get => _updateOnStart ?? (_updateOnStart = ValuesStorage.Get("Settings.LocaleSettings.UpdateOnStart", true)).Value;
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
                        (_quickDriveFastAccessButtons = ValuesStorage.Get("Settings.InterfaceSettings.QuickDriveFastAccessButtons", true)).Value;
                set {
                    if (Equals(value, _quickDriveFastAccessButtons)) return;
                    _quickDriveFastAccessButtons = value;
                    ValuesStorage.Set("Settings.InterfaceSettings.QuickDriveFastAccessButtons", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skinsSetupsNewWindow;

            public bool SkinsSetupsNewWindow {
                get => _skinsSetupsNewWindow ?? (_skinsSetupsNewWindow = ValuesStorage.Get("Settings.InterfaceSettings.SkinsSetupsNewWindow", false)).Value;
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

        public class PluginsSettings : NotifyPropertyChanged {
            internal PluginsSettings() { }

            private bool? _cefFilterAds;

            public bool CefFilterAds {
                get => _cefFilterAds ?? (_cefFilterAds = ValuesStorage.Get("Settings.PluginsSettings.CefFilterAds", false)).Value;
                set {
                    if (Equals(value, _cefFilterAds)) return;
                    _cefFilterAds = value;
                    ValuesStorage.Set("Settings.PluginsSettings.CefFilterAds", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageMemoryLimit;

            public long MontageMemoryLimit {
                get => _montageMemoryLimit ?? (_montageMemoryLimit = ValuesStorage.Get("Settings.PluginsSettings.MontageMemoryLimit", 2147483648L)).Value;
                set {
                    if (Equals(value, _montageMemoryLimit)) return;
                    _montageMemoryLimit = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageMemoryLimit", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageVramCache;

            public long MontageVramCache {
                get => _montageVramCache ?? (_montageVramCache = ValuesStorage.Get("Settings.PluginsSettings.MontageVramCache", 536870912L)).Value;
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
                get => _montageTemporaryDirectory ?? (_montageTemporaryDirectory = ValuesStorage.Get("Settings.PluginsSettings.MontageTemporaryDirectory",
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
                get => _useHardLinks ?? (_useHardLinks = ValuesStorage.Get("Settings.GenericModsSettings.UseHardLinks", true)).Value;
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
                        (_detectWhileInstalling = ValuesStorage.Get("Settings.GenericModsSettings.DetectWhileInstalling", true)).Value;
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
                        (_modsDirectory = ValuesStorage.Get("Settings.GenericModsSettings.ModsDirectory", "mods"));
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
