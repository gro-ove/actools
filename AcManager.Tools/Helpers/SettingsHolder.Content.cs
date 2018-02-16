using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public partial class ContentSettings {
            internal ContentSettings() { }
            private string _cupRegistries;

            public string CupRegistries {
                get => _cupRegistries ?? (_cupRegistries =
                        ValuesStorage.Get("Settings.ContentSettings.CupRegistries", "http://cm.custom.ru/cup/"));
                set {
                    value = value.Trim();
                    if (Equals(value, _cupRegistries)) return;
                    _cupRegistries = value;
                    ValuesStorage.Set("Settings.ContentSettings.CupRegistries", value);
                    OnPropertyChanged();
                }
            }

            private bool? _displaySteerLock;

            public bool DisplaySteerLock {
                get => _displaySteerLock ?? (_displaySteerLock = ValuesStorage.Get("Settings.ContentSettings.DisplaySteerLock", false)).Value;
                set {
                    if (Equals(value, _displaySteerLock)) return;
                    _displaySteerLock = value;
                    ValuesStorage.Set("Settings.ContentSettings.DisplaySteerLock", value);
                    OnPropertyChanged();
                }
            }

            private bool? _oldLayout;

            public bool OldLayout {
                get => _oldLayout ?? (_oldLayout = ValuesStorage.Get("Settings.ContentSettings.OldLayout", false)).Value;
                set {
                    if (Equals(value, _oldLayout)) return;
                    _oldLayout = value;
                    ValuesStorage.Set("Settings.ContentSettings.OldLayout", value);
                    OnPropertyChanged();
                }
            }

            private bool? _markKunosContent;

            public bool MarkKunosContent {
                get => _markKunosContent ?? (_markKunosContent = ValuesStorage.Get("Settings.ContentSettings.MarkKunosContent", true)).Value;
                set {
                    if (Equals(value, _markKunosContent)) return;
                    _markKunosContent = value;
                    ValuesStorage.Set("Settings.ContentSettings.MarkKunosContent", value);
                    OnPropertyChanged();
                }
            }

            private bool? _mentionCmInPackedContent;

            public bool MentionCmInPackedContent {
                get => _mentionCmInPackedContent ??
                        (_mentionCmInPackedContent = ValuesStorage.Get("Settings.ContentSettings.MentionCmInPackedContent", true)).Value;
                set {
                    if (Equals(value, _mentionCmInPackedContent)) return;
                    _mentionCmInPackedContent = value;
                    ValuesStorage.Set("Settings.ContentSettings.MentionCmInPackedContent", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rateCars;

            public bool RateCars {
                get => _rateCars ?? (_rateCars = ValuesStorage.Get("Settings.ContentSettings.RateCars", false)).Value;
                set {
                    if (Equals(value, _rateCars)) return;
                    _rateCars = value;
                    ValuesStorage.Set("Settings.ContentSettings.RateCars", value);
                    OnPropertyChanged();
                }
            }

            private int? _loadingConcurrency;

            public int LoadingConcurrency {
                get
                        =>
                                _loadingConcurrency
                                        ?? (_loadingConcurrency =
                                                ValuesStorage.Get("Settings.ContentSettings.LoadingConcurrency",
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
                get => _curversInDrive ?? (_curversInDrive = ValuesStorage.Get("Settings.ContentSettings.CurversInDrive", true)).Value;
                set {
                    if (Equals(value, _curversInDrive)) return;
                    _curversInDrive = value;
                    ValuesStorage.Set("Settings.ContentSettings.CurversInDrive", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smoothCurves;

            public bool SmoothCurves {
                get => _smoothCurves ?? (_smoothCurves = ValuesStorage.Get("Settings.ContentSettings.SmoothCurves", false)).Value;
                set {
                    if (Equals(value, _smoothCurves)) return;
                    _smoothCurves = value;
                    ValuesStorage.Set("Settings.ContentSettings.SmoothCurves", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsDisplayNameCleanUp;

            public bool CarsDisplayNameCleanUp {
                get => _carsDisplayNameCleanUp
                        ?? (_carsDisplayNameCleanUp = ValuesStorage.Get("Settings.ContentSettings.CarsDisplayNameCleanUp", true)).Value;
                set {
                    if (Equals(value, _carsDisplayNameCleanUp)) return;
                    _carsDisplayNameCleanUp = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsDisplayNameCleanUp", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carsYearPostfix;

            public bool CarsYearPostfix {
                get => _carsYearPostfix ?? (_carsYearPostfix = ValuesStorage.Get("Settings.ContentSettings.CarsYearPostfix", false)).Value;
                set {
                    if (Equals(value, _carsYearPostfix)) return;
                    _carsYearPostfix = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsYearPostfix", value);
                    OnPropertyChanged();
                }
            }

            private bool? _carSkinsDisplayId;

            public bool CarSkinsDisplayId {
                get => _carSkinsDisplayId ?? (_carSkinsDisplayId = ValuesStorage.Get("Settings.ContentSettings.CarSkinsDisplayId", false)).Value;
                set {
                    if (Equals(value, _carSkinsDisplayId)) return;
                    _carSkinsDisplayId = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarSkinsDisplayId", value);
                    OnPropertyChanged();
                }
            }

            public const string SortName = "name";
            public const string SortId = "id";
            public const string SortSkinNumber = "skinNumber";

            public SettingEntryStored CarSkinsSorting { get; } = new SettingEntryStored("/Settings.ContentSettings.CarSkinsSorting", v => {
                foreach (var x in CarsManager.Instance.Loaded) {
                    var skinsManager = x.SkinsManager;
                    if (skinsManager.IsLoaded) {
                        skinsManager.UpdateList(true);
                        x.EnabledSkinsListView.Refresh();
                    }
                }
            }) {
                new DefaultSettingEntry(SortId, "Sort by ID"),
                new SettingEntry(SortName, "Sort by name"),
                new SettingEntry(SortSkinNumber, "Sort by number"),
            };

            private bool? _carsFixSpecs;

            public bool CarsFixSpecs {
                get => _carsFixSpecs ?? (_carsFixSpecs = ValuesStorage.Get("Settings.ContentSettings.CarsFixSpecs", true)).Value;
                set {
                    if (Equals(value, _carsFixSpecs)) return;
                    _carsFixSpecs = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsFixSpecs", value);
                    OnPropertyChanged();
                }
            }

            public SettingEntryStored CarsDisplayPwRatioFormat { get; } = new SettingEntryStored("/Settings.ContentSettings.CarsProperPwRatio") {
                new SettingEntry(0, "Weight-to-power (kg/cv)"),
                new SettingEntry(1, "Power-to-weight (hp/kg)"),
                new SettingEntry(2, "Power-to-weight (hp/tonne)"),
            };

            private bool? _changeBrandIconAutomatically;

            public bool ChangeBrandIconAutomatically {
                get => _changeBrandIconAutomatically ??
                        (_changeBrandIconAutomatically = ValuesStorage.Get("Settings.ContentSettings.ChangeBrandIconAutomatically", true)).Value;
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
                        (_downloadShowroomPreviews = ValuesStorage.Get("Settings.ContentSettings.DownloadShowroomPreviews", true)).Value;
                set {
                    if (Equals(value, _downloadShowroomPreviews)) return;
                    _downloadShowroomPreviews = value;
                    ValuesStorage.Set("Settings.ContentSettings.DownloadShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _scrollAutomatically;

            public bool ScrollAutomatically {
                get => _scrollAutomatically ?? (_scrollAutomatically = ValuesStorage.Get("Settings.ContentSettings.ScrollAutomatically", true)).Value;
                set {
                    if (Equals(value, _scrollAutomatically)) return;
                    _scrollAutomatically = value;
                    ValuesStorage.Set("Settings.ContentSettings.ScrollAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private string _temporaryFilesLocation;

            public string TemporaryFilesLocation {
                get => _temporaryFilesLocation ?? (_temporaryFilesLocation = ValuesStorage.Get("Settings.ContentSettings.TemporaryFilesLocation", ""));
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

            private string _fontIconCharacter;

            public string FontIconCharacter {
                get => _fontIconCharacter ?? (_fontIconCharacter = ValuesStorage.Get("Settings.ContentSettings.FontIconCharacter", @"5"));
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
                get => _skinsSkipPriority ?? (_skinsSkipPriority = ValuesStorage.Get("Settings.ContentSettings.SkinsSkipPriority", false)).Value;
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
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.ContentSettings.NewContentPeriod");
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
                get => _simpleFiltering ?? (_simpleFiltering = ValuesStorage.Get("Settings.ContentSettings.SimpleFiltering", true)).Value;
                set {
                    if (Equals(value, _simpleFiltering)) return;
                    _simpleFiltering = value;
                    ValuesStorage.Set("Settings.ContentSettings.SimpleFiltering", value);
                    OnPropertyChanged();
                    Filter.OptionSimpleMatching = value;
                }
            }

            private bool? _deleteConfirmation;
        }
    }
}