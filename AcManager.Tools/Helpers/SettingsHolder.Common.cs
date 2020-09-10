using System;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
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

            private bool? _useImperialUnits;

            public bool UseImperialUnits {
                get => _useImperialUnits ?? (_useImperialUnits = ValuesStorage.Get("Settings.CommonSettings.UseImperialUnits", false)).Value;
                set {
                    if (Equals(value, _useImperialUnits)) return;
                    _useImperialUnits = value;
                    ValuesStorage.Set("Settings.CommonSettings.UseImperialUnits", value);
                    OnPropertyChanged();
                }
            }

            private bool? _use12HrTimeFormat;

            public bool Use12HrTimeFormat {
                get => _use12HrTimeFormat ?? (_use12HrTimeFormat = ValuesStorage.Get("Settings.CommonSettings.Use12HrTimeFormat", false)).Value;
                set {
                    if (Equals(value, _use12HrTimeFormat)) return;
                    _use12HrTimeFormat = value;
                    ValuesStorage.Set("Settings.CommonSettings.Use12HrTimeFormat", value);
                    OnPropertyChanged();
                    LocalizationHelper.Use12HrFormat = value;
                }
            }

            public static string DistanceFormat => Common.UseImperialUnits ? "{0:F1} mi" : "{0:F1} km";
            public static string DistancePostfix => Common.UseImperialUnits ? "mi" : "km";
            public static string SpaceDistancePostfix => Common.UseImperialUnits ? " mi" : " km";
            public static string SpeedFormat => Common.UseImperialUnits ? "{0:F1} mph" : "{0:F1} km/h";
            public static string SpeedPostfix => Common.UseImperialUnits ? "mph" : "km/h";
            public static string SpaceSpeedPostfix => Common.UseImperialUnits ? " mph" : " km/h";
            public static double DistanceMultiplier => Common.UseImperialUnits ? 0.621371 : 1.0;
            public static string ShortDistanceFormat => Common.UseImperialUnits ? "{0:F1} ft" : "{0:F1} m";
            public static string ShortDistancePostfix => Common.UseImperialUnits ? "ft" : "m";
            public static string SpaceShortDistancePostfix => Common.UseImperialUnits ? " ft" : " m";
            public static double ShortDistanceMultiplier => Common.UseImperialUnits ? 3.28084 : 1.0;

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
                new SettingEntry("off", "Disabled (not recommended, shared links won’t work)"),
                new SettingEntry("protocolOnly", "Content Manager protocol only (for shared links)"),
                new SettingEntry("protocolAndFiles", "Full integration (protocol and files)")
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

            private bool? _launchSteamAtStart;

            public bool LaunchSteamAtStart {
                get => _launchSteamAtStart ?? (_launchSteamAtStart = ValuesStorage.Get("Settings.CommonSettings.LaunchSteamAtStart", true)).Value;
                set {
                    if (Equals(value, _launchSteamAtStart)) return;
                    _launchSteamAtStart = value;
                    ValuesStorage.Set("Settings.CommonSettings.LaunchSteamAtStart", value);
                    OnPropertyChanged();
                }
            }

            private bool? _checkForFth;

            public bool CheckForFaultTolerantHeap {
                get => _checkForFth ?? (_checkForFth = ValuesStorage.Get("Settings.CommonSettings.CheckForFTH", true)).Value;
                set {
                    if (Equals(value, _checkForFth)) return;
                    _checkForFth = value;
                    ValuesStorage.Set("Settings.CommonSettings.CheckForFTH", value);
                    OnPropertyChanged();
                }
            }

            private bool? _developerMode;

            public bool DeveloperMode {
                get => _developerMode ?? (_developerMode = ValuesStorage.Get("Settings.CommonSettings.DeveloperMode", false)
                        || ValuesStorage.Get("Settings.CommonSettings.DeveloperModeN", false)).Value;
                set {
                    if (Equals(value, _developerMode)) return;
                    _developerMode = value;
                    ValuesStorage.Set("Settings.CommonSettings.DeveloperModeN", value);
                    OnPropertyChanged();
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
    }
}