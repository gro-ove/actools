using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
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

            private bool? _cachingServerAvailable;

            public bool CachingServerAvailable {
                /*get => _cachingServerAvailable ?? (_cachingServerAvailable =
                        ValuesStorage.Get("Settings.OnlineSettings.CachingServerAvailable", false)).Value;*/
                get => true;
                set {
                    if (Equals(value, _cachingServerAvailable)) return;
                    _cachingServerAvailable = value;
                    ValuesStorage.Set("Settings.OnlineSettings.CachingServerAvailable", value);
                    OnPropertyChanged();
                }
            }

            private bool? _useCachingServer;

            public bool UseCachingServer {
                get => _useCachingServer ?? (_useCachingServer =
                        ValuesStorage.Get("Settings.OnlineSettings.UseCachingServer", false)).Value;
                set {
                    if (Equals(value, _useCachingServer)) return;
                    _useCachingServer = value;
                    ValuesStorage.Set("Settings.OnlineSettings.UseCachingServer", value);
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

            private bool? _pausePingingInRace;

            public bool PausePingingInRace {
                get => _pausePingingInRace ?? (_pausePingingInRace = ValuesStorage.Get("Settings.OnlineSettings.PausePingingInRace", true)).Value;
                set {
                    if (Equals(value, _pausePingingInRace)) return;
                    _pausePingingInRace = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PausePingingInRace", value);
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

            private bool? _pingingSingleSocket;

            public bool PingingSingleSocket {
                get => _pingingSingleSocket ?? (_pingingSingleSocket = ValuesStorage.Get("Settings.OnlineSettings.PingingSingleSocket", true)).Value;
                set {
                    if (Equals(value, _pingingSingleSocket)) return;
                    _pingingSingleSocket = value;
                    ValuesStorage.Set("Settings.OnlineSettings.PingingSingleSocket", value);
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

            private SettingEntry[] _searchContentModes;

            public SettingEntry[] SearchContentModes => _searchContentModes ?? (_searchContentModes = new[] {
                new SettingEntry(0, "Disabled"),
                new SettingEntry(1, "Original content only"),
                new SettingEntry(2, "Original and ported")
            });

            private SettingEntry _searchContentMode;

            public SettingEntry SearchContentMode {
                get {
                    var saved = ValuesStorage.Get<int?>("Settings.OnlineSettings.SearchContentMode");
                    return _searchContentMode ?? (_searchContentMode = SearchContentModes.GetByIdOrDefault(saved) ?? SearchContentModes.ElementAt(2));
                }
                set {
                    if (Equals(value, _searchContentMode)) return;
                    _searchContentMode = value;
                    ValuesStorage.Set("Settings.OnlineSettings.SearchContentMode", value.IntValue ?? -1);
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

            /*private bool? _serverPresetsManaging;

            public bool ServerPresetsManaging {
                get => _serverPresetsManaging ??
                        (_serverPresetsManaging = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsManaging", false)).Value;
                set {
                    if (Equals(value, _serverPresetsManaging)) return;
                    _serverPresetsManaging = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsManaging", value);
                    OnPropertyChanged();
                }
            }*/

            private string _serverPresetsDirectory;

            public string ServerPresetsDirectory {
                get => _serverPresetsDirectory ?? (_serverPresetsDirectory = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsDirectory",
                        Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"presets")));
                set {
                    value = value.Trim();
                    if (Equals(value, _serverPresetsDirectory)) return;
                    _serverPresetsDirectory = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsDirectory", value);
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
                get => _serverPresetsUpdateDataAutomatically ?? (_serverPresetsUpdateDataAutomatically
                                = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsUpdateDataAutomatically", true)).Value;
                set {
                    if (Equals(value, _serverPresetsUpdateDataAutomatically)) return;
                    _serverPresetsUpdateDataAutomatically = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsUpdateDataAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _ServerCopyConfigsToCfgFolder;

            public bool ServerCopyConfigsToCfgFolder {
                get => _ServerCopyConfigsToCfgFolder ?? (_ServerCopyConfigsToCfgFolder
                        = ValuesStorage.Get("Settings.OnlineSettings.ServerCopyConfigsToCfgFolder", false)).Value;
                set {
                    if (Equals(value, _ServerCopyConfigsToCfgFolder)) return;
                    _ServerCopyConfigsToCfgFolder = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerCopyConfigsToCfgFolder", value);
                    OnPropertyChanged();
                }
            }

            /*private bool? _serverPresetsFitInFewerTabs;

            public bool ServerPresetsFitInFewerTabs {
                get => _serverPresetsFitInFewerTabs ??
                        (_serverPresetsFitInFewerTabs = ValuesStorage.Get("Settings.OnlineSettings.ServerPresetsFitInFewerTabs", false)).Value;
                set {
                    if (Equals(value, _serverPresetsFitInFewerTabs)) return;
                    _serverPresetsFitInFewerTabs = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerPresetsFitInFewerTabs", value);
                    OnPropertyChanged();
                }
            }*/

            private bool? _serverLogsSave;

            public bool ServerLogsSave {
                get => _serverLogsSave ?? (_serverLogsSave = ValuesStorage.Get("Settings.OnlineSettings.ServerLogsSave", true)).Value;
                set {
                    if (Equals(value, _serverLogsSave)) return;
                    _serverLogsSave = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerLogsSave", value);
                    OnPropertyChanged();
                }
            }

            private bool? _serverLogsCmFormat;

            public bool ServerLogsCmFormat {
                get => _serverLogsCmFormat ?? (_serverLogsCmFormat = ValuesStorage.Get("Settings.OnlineSettings.ServerLogsCmFormat", false)).Value;
                set {
                    if (Equals(value, _serverLogsCmFormat)) return;
                    _serverLogsCmFormat = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerLogsCmFormat", value);
                    OnPropertyChanged();
                }
            }

            private string _serverLogsDirectory;

            public string ServerLogsDirectory {
                get => _serverLogsDirectory ?? (_serverLogsDirectory = ValuesStorage.Get("Settings.OnlineSettings.ServerLogsDirectory", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _serverLogsDirectory)) return;
                    _serverLogsDirectory = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerLogsDirectory", value);
                    OnPropertyChanged();
                }
            }

            private DelayEntry[] _serverKeepLogsDurations;

            public DelayEntry[] ServerKeepLogsDurations => _serverKeepLogsDurations ?? (_serverKeepLogsDurations = new[] {
                new DelayEntry(TimeSpan.FromDays(3)),
                new DelayEntry(TimeSpan.FromDays(7)),
                new DelayEntry(TimeSpan.FromDays(14)),
                new DelayEntry(TimeSpan.FromDays(30)),
                new DelayEntry(TimeSpan.FromDays(60)),
                new DelayEntry(TimeSpan.FromDays(356))
            });

            private DelayEntry _serverKeepLogsDuration;

            public DelayEntry ServerKeepLogsDuration {
                get {
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.OnlineSettings.ServerKeepLogsDuration");
                    return _serverKeepLogsDuration ?? (_serverKeepLogsDuration = ServerKeepLogsDurations.FirstOrDefault(x => x.TimeSpan == saved) ??
                            ServerKeepLogsDurations.ElementAt(2));
                }
                set {
                    if (Equals(value, _serverKeepLogsDuration)) return;
                    _serverKeepLogsDuration = value;
                    ValuesStorage.Set("Settings.OnlineSettings.ServerKeepLogsDuration", value.TimeSpan);
                    OnPropertyChanged();
                }
            }
        }

        private static OnlineSettings _online;
        public static OnlineSettings Online => _online ?? (_online = new OnlineSettings());
    }
}