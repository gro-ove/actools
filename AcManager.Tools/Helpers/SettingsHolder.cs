using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public class SettingsHolder {
        public class PeriodEntry {
            public string DisplayName { get; internal set; }

            public TimeSpan TimeSpan { get; internal set; }
        }

        public class OnlineSettings : NotifyPropertyChanged {
            internal OnlineSettings() { }

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
                    if (Equals(value, PortsEnumeration)) return;
                    ValuesStorage.Set("Settings.OnlineSettings.PortsEnumeration", value);
                    OnPropertyChanged();
                }
            }

            public string LanPortsEnumeration {
                get { return ValuesStorage.GetString("Settings.OnlineSettings.LanPortsEnumeration", "9456-9458,9556,9600-9612,9700"); }
                set {
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

        public static OnlineSettings Online { get; } = new OnlineSettings();

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
        }

        public static CommonSettings Common { get; } = new CommonSettings();

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
                    if (Equals(value, PlayerName)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerName", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayerNameOnline));
                }
            }

            public string PlayerNationality {
                get { return ValuesStorage.GetString("Settings.DriveSettings.PlayerNationality", null); }
                set {
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
                    if (Equals(value, PlayerNameOnline)) return;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
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
        }

        public static DriveSettings Drive { get; } = new DriveSettings();

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

            public string FontIconCharacter {
                get { return ValuesStorage.GetString("Settings.ContentSettings.FontIconCharacter", "A"); }
                set {
                    value = value.Substring(0, 1);
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
        }

        public static ContentSettings Content = new ContentSettings();
    }
}
