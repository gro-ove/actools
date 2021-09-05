using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
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

            private bool? _worldSimSeriesEnabled;

            public bool WorldSimSeriesEnabled {
                get => _worldSimSeriesEnabled ?? (_worldSimSeriesEnabled = ValuesStorage.Get("Settings.LiveSettings.WorldSimSeriesEnabled", true)).Value;
                set {
                    if (Equals(value, _worldSimSeriesEnabled)) return;
                    _worldSimSeriesEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.WorldSimSeriesEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _trackTitanEnabled;

            public bool TrackTitanEnabled {
                get => _trackTitanEnabled ?? (_trackTitanEnabled = ValuesStorage.Get("Settings.LiveSettings.TrackTitanEnabled", true)).Value;
                set {
                    if (Equals(value, _trackTitanEnabled)) return;
                    _trackTitanEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.TrackTitanEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _UnitedRacingDataEnabled;

            public bool UnitedRacingDataEnabled {
                get => _UnitedRacingDataEnabled ?? (_UnitedRacingDataEnabled = ValuesStorage.Get("Settings.LiveSettings.UnitedRacingDataEnabled", true)).Value;
                set {
                    if (Equals(value, _UnitedRacingDataEnabled)) return;
                    _UnitedRacingDataEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.UnitedRacingDataEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceUEnabled;

            public bool RaceUEnabled {
                get => _raceUEnabled ?? (_raceUEnabled = ValuesStorage.Get("Settings.LiveSettings.RaceUEnabled", true)).Value;
                set {
                    if (Equals(value, _raceUEnabled)) return;
                    _raceUEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.RaceUEnabled", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsCustomStyle;

            public bool SrsCustomStyle {
                get => _srsCustomStyle ?? (_srsCustomStyle = ValuesStorage.Get("Settings.LiveSettings.SrsCustomStyle", false)).Value;
                set {
                    if (Equals(value, _srsCustomStyle)) return;
                    _srsCustomStyle = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsCustomStyle", value);
                    OnPropertyChanged();
                }
            }

            private bool? _srsCollectCombinations;

            public bool SrsCollectCombinations {
                get => _srsCollectCombinations ?? (_srsCollectCombinations = ValuesStorage.Get("Settings.LiveSettings.SrsCollectCombinations", true)).Value;
                set {
                    if (Equals(value, _srsCollectCombinations)) return;
                    _srsCollectCombinations = value;
                    ValuesStorage.Set("Settings.LiveSettings.SrsCollectCombinations", value);
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

            private bool? _gridFinderEnabled;

            public bool GridFinderEnabled {
                get => _gridFinderEnabled ?? (_gridFinderEnabled = ValuesStorage.Get("Settings.LiveSettings.GridFinderEnabled", true)).Value;
                set {
                    if (Equals(value, _gridFinderEnabled)) return;
                    _gridFinderEnabled = value;
                    ValuesStorage.Set("Settings.LiveSettings.GridFinderEnabled", value);
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
    }
}