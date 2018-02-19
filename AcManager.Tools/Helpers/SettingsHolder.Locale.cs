using System;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
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
    }
}