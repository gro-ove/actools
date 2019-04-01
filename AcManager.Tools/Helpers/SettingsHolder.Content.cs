using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class ContentSettings : NotifyPropertyChanged {
            internal ContentSettings() { }
            private string _cupRegistries;

            public string CupRegistries {
                get => _cupRegistries ?? (_cupRegistries =
                        ValuesStorage.Get("Settings.ContentSettings.CupRegistries", "https://acstuff.ru/cup/"));
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

            public bool OldLayout { get; set; }

            /*private bool? _oldLayout;

            public bool OldLayout {
                get => _oldLayout ?? (_oldLayout = ValuesStorage.Get("Settings.ContentSettings.OldLayout", false)).Value;
                set {
                    if (Equals(value, _oldLayout)) return;
                    _oldLayout = value;
                    ValuesStorage.Set("Settings.ContentSettings.OldLayout", value);
                    OnPropertyChanged();
                }
            }*/

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

            private bool? _carsYearPostfixAlt;

            public bool CarsYearPostfixAlt {
                get => _carsYearPostfixAlt ?? (_carsYearPostfixAlt =
                        ValuesStorage.Get("Settings.ContentSettings.CarsYearPostfixAlt", false)).Value;
                set {
                    if (Equals(value, _carsYearPostfixAlt)) return;
                    _carsYearPostfixAlt = value;
                    ValuesStorage.Set("Settings.ContentSettings.CarsYearPostfixAlt", value);
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

            private bool? _deleteConfirmation;

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
                        case MissingContentType.Showroom:
                            return $"\"{id}\" Assetto Corsa";
                        default:
                            throw new ArgumentOutOfRangeException(nameof(type), type, null);
                    }
                }, false)
            });

            private MissingContentSearchEntry _missingContentSearch;

            public MissingContentSearchEntry MissingContentSearch {
                get {
                    return _missingContentSearch
                            ?? (_missingContentSearch = MissingContentSearchEntries.FirstOrDefault(x =>
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

            public IStorage MegaAuthenticationStorage { get; } = new Substorage(AuthenticationStorage.GeneralStorage, "Mega:");
        }

        private static ContentSettings _content;
        public static ContentSettings Content => _content ?? (_content = new ContentSettings());
    }
}