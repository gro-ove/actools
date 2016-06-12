using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

// ReSharper disable RedundantArgumentDefaultValue

namespace AcManager.Tools.Helpers {
    public class SettingsHolder {
        public class PeriodEntry {
            public string DisplayName { get; internal set; }

            public TimeSpan TimeSpan { get; internal set; }
        }

        public class SearchEngineEntry {
            public string DisplayName { get; internal set; }

            public string Value { get; internal set; }

            public string GetUri(string s) {
                if (Content.SearchWithWikipedia) {
                    s = "site:wikipedia.org " + s;
                }

                return string.Format(Value, Uri.EscapeDataString(s).Replace("%20", "+"));
            }
        }

        public class OnlineServerEntry {
            private string _displayName;

            public string DisplayName => _displayName ?? (_displayName = (Id + 1).GetOrdinalReadable());

            public int Id { get; internal set; }
        }

        public class OnlineSettings : NotifyPropertyChanged {
            internal OnlineSettings() { }

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

            public int OnlineServerId {
                get { return ValuesStorage.GetInt("Settings.OnlineSettings.OnlineServerId", 0); }
                set {
                    if (Equals(value, OnlineServerId)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.OnlineServerId", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OnlineServerId));
                }
            }

            public bool RememberPasswords {
                get { return ValuesStorage.GetBool("Settings.OnlineSettings.RememberPasswords", true); }
                set {
                    if (Equals(value, RememberPasswords)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.RememberPasswords", value);
                    OnPropertyChanged();
                }
            }

            public string PortsEnumeration {
                get { return ValuesStorage.GetString("Settings.OnlineSettings.PortsEnumeration", "9000-10000"); }
                set {
                    value = value.Trim();
                    if (Equals(value, PortsEnumeration)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.PortsEnumeration", value);
                    OnPropertyChanged();
                }
            }

            public string LanPortsEnumeration {
                get { return ValuesStorage.GetString("Settings.OnlineSettings.LanPortsEnumeration", "9456-9458,9556,9600-9612,9700"); }
                set {
                    value = value.Trim();
                    if (Equals(value, LanPortsEnumeration)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.LanPortsEnumeration", value);
                    OnPropertyChanged();
                }
            }

            public IEnumerable<string> IgnoredInterfaces {
                get { return ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces"); }
                set {
                    if (Equals(value, IgnoredInterfaces)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }
        }

        private static OnlineSettings _online;

        public static OnlineSettings Online => _online ?? (_online = new OnlineSettings());

        public class CommonSettings : NotifyPropertyChanged {
            internal CommonSettings() { }

            private PeriodEntry[] _periodEntries;

            public PeriodEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
                new PeriodEntry { DisplayName = "Disabled", TimeSpan = TimeSpan.Zero },
                new PeriodEntry { DisplayName = "On startup only", TimeSpan = TimeSpan.MaxValue },
                new PeriodEntry { DisplayName = "Every 30 minutes", TimeSpan = TimeSpan.FromMinutes(30) },
                new PeriodEntry { DisplayName = "Every three hours", TimeSpan = TimeSpan.FromHours(3) },
                new PeriodEntry { DisplayName = "Every ten hours", TimeSpan = TimeSpan.FromHours(6) },
                new PeriodEntry { DisplayName = "Every day", TimeSpan = TimeSpan.FromDays(1) }
            });

            public PeriodEntry UpdatePeriod {
                get {
                    return Periods.FirstOrDefault(x =>
                            x.TimeSpan == ValuesStorage.GetTimeSpan("Settings.CommonSettings.UpdatePeriod", Periods.ElementAt(2).TimeSpan)) ??
                            Periods.FirstOrDefault();
                }
                set {
                    if (Equals(value, UpdatePeriod)) return;
                    ValuesStorage.Set("Settings.CommonSettings.UpdatePeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            public bool UpdateToNontestedVersions {
                get { return ValuesStorage.GetBool("Settings.CommonSettings.UpdateToNontestedVersions", false); }
                set {
                    if (Equals(value, UpdateToNontestedVersions)) return;
                    ValuesStorage.Set("Settings.CommonSettings.UpdateToNontestedVersions", value);
                    OnPropertyChanged();
                }
            }

            public bool CreateStartMenuShortcutIfMissing {
                get { return ValuesStorage.GetBool("Settings.CommonSettings.CreateStartMenuShortcutIfMissing", false); }
                set {
                    if (Equals(value, CreateStartMenuShortcutIfMissing)) return;
                    ValuesStorage.Set("Settings.CommonSettings.CreateStartMenuShortcutIfMissing", value);
                    OnPropertyChanged();
                }
            }

            public bool DeveloperMode {
                get { return ValuesStorage.GetBool("Settings.CommonSettings.DeveloperMode", false); }
                set {
                    if (Equals(value, DeveloperMode)) return;
                    ValuesStorage.Set("Settings.CommonSettings.DeveloperMode", value);
                    OnPropertyChanged();
                }
            }

            public bool FixResolutionAutomatically {
                get { return ValuesStorage.GetBool("Settings.CommonSettings.FixResolutionAutomatically", true); }
                set {
                    if (Equals(value, FixResolutionAutomatically)) return;
                    ValuesStorage.Set("Settings.CommonSettings.FixResolutionAutomatically", value);
                    OnPropertyChanged();
                }
            }
        }

        private static CommonSettings _common;

        public static CommonSettings Common => _common ?? (_common = new CommonSettings());

        public class DriveSettings : NotifyPropertyChanged {
            internal DriveSettings() {
                if (PlayerName == null) {
                    PlayerName = new IniFile(FileUtils.GetRaceIniFilename())["CAR_0"].Get("DRIVER_NAME") ?? "Player";
                    PlayerNameOnline = PlayerName;
                }

                if (PlayerNationality == null) {
                    PlayerNationality = new IniFile(FileUtils.GetRaceIniFilename())["CAR_0"].Get("NATIONALITY");
                }
            }

            public class StarterType : NotifyPropertyChanged, IWithId {
                internal readonly string RequiredAddonId;

                public string Id { get; }

                public string DisplayName { get; }

                public bool IsAvailable => RequiredAddonId == null || AppAddonsManager.Instance.IsAddonEnabled(RequiredAddonId);

                internal StarterType(string displayName, string requiredAddonId = null) {
                    Id = displayName;
                    DisplayName = displayName;

                    RequiredAddonId = requiredAddonId;
                }
            }

            public static readonly StarterType TrickyStarterType = new StarterType("Tricky");
            public static readonly StarterType StarterPlusType = new StarterType("Starter+", StarterPlus.AddonId);
            public static readonly StarterType SseStarterType = new StarterType("SSE", SseStarter.AddonId);
            public static readonly StarterType NaiveStarterType = new StarterType("Naive");

            public StarterType SelectedStarterType {
                get {
                    return StarterTypes.GetByIdOrDefault(ValuesStorage.GetString("Settings.DriveSettings.SelectedStarterType")) ??
                            StarterTypes.FirstOrDefault();
                }
                set {
                    if (Equals(value, SelectedStarterType)) return;
                    ValuesStorage.Set("Settings.DriveSettings.SelectedStarterType", value.Id);
                    OnPropertyChanged();
                }
            }

            private StarterType[] _starterTypes;

            public StarterType[] StarterTypes => _starterTypes ?? (_starterTypes = new[] {
                TrickyStarterType, StarterPlusType, SseStarterType, NaiveStarterType
            });

            public string PreCommand {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PreCommand", ""); }
                set {
                    value = value.Trim();
                    if (Equals(value, PreCommand)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PreCommand", value);
                    OnPropertyChanged();
                }
            }

            public string PostCommand {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PostCommand", ""); }
                set {
                    value = value.Trim();
                    if (Equals(value, PostCommand)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PostCommand", value);
                    OnPropertyChanged();
                }
            }

            public bool ImmediateStart {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.ImmediateLaunch", false); }
                set {
                    if (Equals(value, ImmediateStart)) return;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateLaunch", value);
                    OnPropertyChanged();
                }
            }

            public bool SkipPracticeResults {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.SkipPracticeResults", false); }
                set {
                    if (Equals(value, SkipPracticeResults)) return;
                    ValuesStorage.Set("Settings.DriveSettings.SkipPracticeResults", value);
                    OnPropertyChanged();
                }
            }

            public bool AutoSaveReplays {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.AutoSaveReplays", false); }
                set {
                    if (Equals(value, AutoSaveReplays)) return;
                    ValuesStorage.Set("Settings.DriveSettings.AutoSaveReplays", value);
                    OnPropertyChanged();
                }
            }

            public bool AutoAddReplaysExtension {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.AutoAddReplaysExtension", false); }
                set {
                    if (Equals(value, AutoAddReplaysExtension)) return;
                    ValuesStorage.Set("Settings.DriveSettings.AutoAddReplaysExtension", value);
                    OnPropertyChanged();
                }
            }

            public string DefaultReplaysNameFormat => "_autosave_{car.id}_{track.id}_{date_ac}.acreplay";

            public string ReplaysNameFormat {
                get { return ValuesStorage.GetString("Settings.DriveSettings.ReplaysNameFormat", "_autosave_{car.id}_{track.id}_{date_ac}.acreplay"); }
                set {
                    value = value?.Trim();
                    if (Equals(value, ReplaysNameFormat)) return;
                    ValuesStorage.Set("Settings.DriveSettings.ReplaysNameFormat", value);
                    OnPropertyChanged();
                }
            }

            public bool Use32BitVersion {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.Use32BitVersion", false); }
                set {
                    if (Equals(value, Use32BitVersion)) return;
                    ValuesStorage.Set("Settings.DriveSettings.Use32BitVersion", value);
                    OnPropertyChanged();
                }
            }

            public string PlayerName {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PlayerName", null); }
                set {
                    value = value?.Trim();
                    if (Equals(value, PlayerName)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerName", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayerNameOnline));
                }
            }

            public string PlayerNationality {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PlayerNationality", null); }
                set {
                    value = value?.Trim();
                    if (Equals(value, PlayerNationality)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNationality", value);
                    OnPropertyChanged();
                }
            }

            public bool DifferentPlayerNameOnline {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.DifferentPlayerNameOnline", false); }
                set {
                    if (Equals(value, DifferentPlayerNameOnline)) return;
                    ValuesStorage.Set("Settings.DriveSettings.DifferentPlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            public string PlayerNameOnline {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PlayerNameOnline", PlayerName); }
                set {
                    value = value.Trim();
                    if (Equals(value, PlayerNameOnline)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            public bool QuickDriveExpandBounds {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.ExpandBounds", false); }
                set {
                    if (Equals(value, QuickDriveExpandBounds)) return;
                    ValuesStorage.Set("Settings.DriveSettings.ExpandBounds", value);
                    OnPropertyChanged();
                }
            }

            public bool KunosCareerUserAiLevel {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserAiLevel", false); }
                set {
                    if (Equals(value, KunosCareerUserAiLevel)) return;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserAiLevel", value);
                    OnPropertyChanged();
                }
            }

            public bool KunosCareerUserSkin {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserSkin", true); }
                set {
                    if (Equals(value, KunosCareerUserSkin)) return;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserSkin", value);
                    OnPropertyChanged();
                }
            }

            public bool QuickSwitches {
                get { return ValuesStorage.GetBool("Settings.DriveSettings.QuickSwitches_WIP", false); } // TODO
                set {
                    if (Equals(value, QuickSwitches)) return;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitches_WIP", value);
                    OnPropertyChanged();
                }
            }
        }

        private static DriveSettings _drive;

        public static DriveSettings Drive => _drive ?? (_drive = new DriveSettings());

        public class ContentSettings : NotifyPropertyChanged {
            internal ContentSettings() { }

            public int LoadingConcurrency {
                get { return ValuesStorage.GetInt("Settings.ContentSettings.LoadingConcurrency", BaseAcManagerNew.OptionAcObjectsLoadingConcurrency); }
                set {
                    value = value < 1 ? 1 : value;
                    if (Equals(value, LoadingConcurrency)) return;
                    ValuesStorage.Set("Settings.ContentSettings.LoadingConcurrency", value);
                    OnPropertyChanged();
                }
            }

            public bool ChangeBrandIconAutomatically {
                get { return ValuesStorage.GetBool("Settings.ContentSettings.ChangeBrandIconAutomatically", true); }
                set {
                    if (Equals(value, ChangeBrandIconAutomatically)) return;
                    ValuesStorage.Set("Settings.ContentSettings.ChangeBrandIconAutomatically", value);
                    OnPropertyChanged();
                }
            }

            public bool DownloadShowroomPreviews {
                get { return ValuesStorage.GetBool("Settings.ContentSettings.DownloadShowroomPreviews", true); }
                set {
                    if (Equals(value, DownloadShowroomPreviews)) return;
                    ValuesStorage.Set("Settings.ContentSettings.DownloadShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            public bool ScrollAutomatically {
                get { return ValuesStorage.GetBool("Settings.ContentSettings.ScrollAutomatically", true); }
                set {
                    if (Equals(value, ScrollAutomatically)) return;
                    ValuesStorage.Set("Settings.ContentSettings.ScrollAutomatically", value);
                    OnPropertyChanged();
                }
            }

            public string FontIconCharacter {
                get { return ValuesStorage.GetString("Settings.ContentSettings.FontIconCharacter", "A"); }
                set {
                    value = value?.Trim().Substring(0, 1);
                    if (Equals(value, FontIconCharacter)) return;
                    ValuesStorage.Set("Settings.ContentSettings.FontIconCharacter", value);
                    OnPropertyChanged();
                }
            }

            private PeriodEntry[] _periodEntries;

            public PeriodEntry[] NewContentPeriods => _periodEntries ?? (_periodEntries = new[] {
                new PeriodEntry { DisplayName = "Disabled", TimeSpan = TimeSpan.Zero },
                new PeriodEntry { DisplayName = "One day", TimeSpan = TimeSpan.FromDays(1) },
                new PeriodEntry { DisplayName = "Three days", TimeSpan = TimeSpan.FromDays(3) },
                new PeriodEntry { DisplayName = "Week", TimeSpan = TimeSpan.FromDays(7) },
                new PeriodEntry { DisplayName = "Two weeks", TimeSpan = TimeSpan.FromDays(14) },
                new PeriodEntry { DisplayName = "Month", TimeSpan = TimeSpan.FromDays(30) }
            });

            public PeriodEntry NewContentPeriod {
                get {
                    return NewContentPeriods.FirstOrDefault(x =>
                            x.TimeSpan == ValuesStorage.GetTimeSpan("Settings.ContentSettings.NewContentPeriod", NewContentPeriods.ElementAt(4).TimeSpan)) ??
                            NewContentPeriods.FirstOrDefault();
                }
                set {
                    if (Equals(value, NewContentPeriod)) return;
                    ValuesStorage.Set("Settings.ContentSettings.NewContentPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private SearchEngineEntry[] _searchEngines;

            public SearchEngineEntry[] SearchEngines => _searchEngines ?? (_searchEngines = new[] {
                new SearchEngineEntry { DisplayName = "DuckDuckGo", Value = "https://duckduckgo.com/?q={0}&ia=web" },
                new SearchEngineEntry { DisplayName = "Bing", Value = "http://www.bing.com/search?q={0}" },
                new SearchEngineEntry { DisplayName = "Google", Value = "https://www.google.ru/search?q={0}&ie=UTF-8" },
                new SearchEngineEntry { DisplayName = "Yandex", Value = "https://yandex.ru/search/?text={0}" },
                new SearchEngineEntry { DisplayName = "Baidu", Value = "http://www.baidu.com/s?ie=utf-8&wd={0}" }
            });

            public SearchEngineEntry SearchEngine {
                get {
                    return SearchEngines.FirstOrDefault(x =>
                            x.DisplayName == ValuesStorage.GetString("Settings.ContentSettings.SearchEngine")) ??
                            SearchEngines.FirstOrDefault();
                }
                set {
                    if (Equals(value, SearchEngine)) return;
                    ValuesStorage.Set("Settings.ContentSettings.SearchEngine", value.DisplayName);
                    OnPropertyChanged();
                }
            }

            public bool SearchWithWikipedia {
                get { return ValuesStorage.GetBool("Settings.ContentSettings.SearchWithWikipedia", true); }
                set {
                    if (Equals(value, SearchWithWikipedia)) return;
                    ValuesStorage.Set("Settings.ContentSettings.SearchWithWikipedia", value);
                    OnPropertyChanged();
                }
            }
        }

        private static ContentSettings _content;

        public static ContentSettings Content => _content ?? (_content = new ContentSettings());

        public class CustomShowroomSettings : NotifyPropertyChanged {
            internal CustomShowroomSettings() { }

            public string[] ShowroomTypes { get; } = { "Custom", "Lite" };

            public string ShowroomType {
                get { return LiteByDefault ? ShowroomTypes[1] : ShowroomTypes[0]; }
                set { LiteByDefault = value == ShowroomTypes[1]; }
            }

            public bool LiteByDefault {
                get { return ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteByDefault", true); }
                set {
                    if (Equals(value, LiteByDefault)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteByDefault", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowroomType));
                }
            }

            public bool LiteUseFxaa {
                get { return ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseFxaa", true); }
                set {
                    if (Equals(value, LiteUseFxaa)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseFxaa", value);
                    OnPropertyChanged();
                }
            }

            public bool LiteUseMsaa {
                get { return ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseMsaa", false); }
                set {
                    if (Equals(value, LiteUseMsaa)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseMsaa", value);
                    OnPropertyChanged();
                }
            }

            public bool LiteUseBloom {
                get { return ValuesStorage.GetBool("Settings.CustomShowroomSettings.LiteUseBloom", true); }
                set {
                    if (Equals(value, LiteUseBloom)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseBloom", value);
                    OnPropertyChanged();
                }
            }

            [CanBeNull]
            public string ShowroomId {
                get { return ValuesStorage.GetString("Settings.CustomShowroomSettings.ShowroomId", "showroom"); }
                set {
                    if (Equals(value, ShowroomId)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.ShowroomId", value);
                    OnPropertyChanged();
                }
            }

            public bool UseFxaa {
                get { return ValuesStorage.GetBool("Settings.CustomShowroomSettings.UseFxaa", true); }
                set {
                    if (Equals(value, UseFxaa)) return;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.UseFxaa", value);
                    OnPropertyChanged();
                }
            }
        }

        private static CustomShowroomSettings _customShowroom;

        public static CustomShowroomSettings CustomShowroom => _customShowroom ?? (_customShowroom = new CustomShowroomSettings());

        public class SharingSettings : NotifyPropertyChanged {
            internal SharingSettings() { }

            public bool CustomIds {
                get { return ValuesStorage.GetBool("Settings.SharingSettings.CustomIds", false); }
                set {
                    if (Equals(value, CustomIds)) return;
                    ValuesStorage.Set("Settings.SharingSettings.CustomIds", value);
                    OnPropertyChanged();
                }
            }

            public bool VerifyBeforeSharing {
                get { return ValuesStorage.GetBool("Settings.SharingSettings.VerifyBeforeSharing", true); }
                set {
                    if (Equals(value, VerifyBeforeSharing)) return;
                    ValuesStorage.Set("Settings.SharingSettings.VerifyBeforeSharing", value);
                    OnPropertyChanged();
                }
            }

            public bool CopyLinkToClipboard {
                get { return ValuesStorage.GetBool("Settings.SharingSettings.CopyLinkToClipboard", true); }
                set {
                    if (Equals(value, CopyLinkToClipboard)) return;
                    ValuesStorage.Set("Settings.SharingSettings.CopyLinkToClipboard", value);
                    OnPropertyChanged();
                }
            }

            public bool ShareAnonymously {
                get { return ValuesStorage.GetBool("Settings.SharingSettings.ShareAnonymously", false); }
                set {
                    if (Equals(value, ShareAnonymously)) return;
                    ValuesStorage.Set("Settings.SharingSettings.ShareAnonymously", value);
                    OnPropertyChanged();
                }
            }

            public bool ShareWithoutName {
                get { return ValuesStorage.GetBool("Settings.SharingSettings.ShareWithoutName", false); }
                set {
                    if (Equals(value, ShareWithoutName)) return;
                    ValuesStorage.Set("Settings.SharingSettings.ShareWithoutName", value);
                    OnPropertyChanged();
                }
            }

            [CanBeNull]
            public string SharingName {
                get { return ValuesStorage.GetString("Settings.SharingSettings.SharingName", null) ?? Drive.PlayerNameOnline; }
                set {
                    if (value?.Length > 60) {
                        value = value?.Substring(0, 60);
                    }

                    if (Equals(value, SharingName)) return;
                    ValuesStorage.Set("Settings.SharingSettings.SharingName", value);
                    OnPropertyChanged();
                }
            }
        }

        private static SharingSettings _sharing;

        public static SharingSettings Sharing => _sharing ?? (_sharing = new SharingSettings());
    }
}
