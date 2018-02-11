using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    /* TODO, THE BIG ISSUE:
     * Apparently, _saveable is only used for ExportToPresetData() and ImportFromPresetData()!
     * Wouldn’t it be better to disable SaveLater() at all then? Also, what about LoadSerializedPreset()
     * and keySaveable parameter? */
    public class RaceGridViewModel : NotifyPropertyChanged, IDisposable, IComparer, IUserPresetable {
        public static bool OptionNfsPorscheNames = false;

        #region Loading and saving
        public const string PresetableKeyValue = "Race Grids";
        private const string KeySaveable = "__RaceGrid";

        private readonly ISaveHelper _saveable;

        private void SaveLater() {
            if (_saveable.SaveLater()) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool CanBeSaved => true;
        public string PresetableKey => PresetableKeyValue;
        public PresetsCategory PresetableCategory { get; } = new PresetsCategory(PresetableKeyValue);

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }

        public class SaveableData : IJsonSerializable {
            public string ModeId;
            public string FilterValue;
            public string RandomSkinsFilter;

            [CanBeNull]
            public string[] CarIds;

            [CanBeNull]
            public int[] CandidatePriorities;

            [CanBeNull]
            public double[] AiLevels;

            [CanBeNull]
            public double[] AiAggressions;

            [CanBeNull]
            public double[] Ballasts;

            [CanBeNull]
            public double[] Restrictors;

            public double PlayerBallast, PlayerRestrictor;

            [CanBeNull]
            public string[] Names, Nationalities, SkinIds;

            [CanBeNull]
            public string[] AiLimitations;

            public bool? ShuffleCandidates;
            public int? VarietyLimitation, OpponentsNumber, StartingPosition;

            public bool? AiLevelArrangeReverse;

            [UsedImplicitly]
            public bool? AiLevelArrangeRandomly /* not needed to be saved */;

            public double AiLevelArrangeRandom = 0.1, AiLevel = 95, AiLevelMin = 85;

            public bool? AiAggressionArrangeReverse;
            public double AiAggressionArrangeRandom = 0.1, AiAggression, AiAggressionMin;

            string IJsonSerializable.ToJson() {
                var s = new StringWriter();
                var w = new JsonTextWriter(s);

                w.WriteStartObject();
                w.Write("ModeId", ModeId);
                w.Write("FilterValue", FilterValue);
                if (!string.IsNullOrEmpty(RandomSkinsFilter)) {
                    w.Write("RandomSkinsFilter", RandomSkinsFilter);
                }

                w.Write("CarIds", CarIds);
                w.Write("CandidatePriorities", CandidatePriorities);
                w.Write("AiLevels", AiLevels);
                w.Write("AiAggressions", AiAggressions);

                w.Write("Ballasts", Ballasts);
                w.Write("Restrictors", Restrictors);

                w.WriteNonDefault("PlayerBallast", PlayerBallast);
                w.WriteNonDefault("PlayerRestrictor", PlayerRestrictor);

                w.Write("Names", Names);
                w.Write("Nationalities", Nationalities);
                w.Write("SkinIds", SkinIds);
                w.Write("AiLimitations", AiLimitations);

                w.Write("ShuffleCandidates", ShuffleCandidates);
                w.Write("VarietyLimitation", VarietyLimitation);
                w.Write("OpponentsNumber", OpponentsNumber);
                w.Write("StartingPosition", StartingPosition);

                w.Write("AiLevel", AiLevel);
                w.Write("AiLevelMin", AiLevelMin);
                w.Write("AiLevelArrangeRandom", AiLevelArrangeRandom);
                w.Write("AiLevelArrangeReverse", AiLevelArrangeReverse);

                w.Write("AiAggression", AiAggression);
                w.Write("AiAggressionMin", AiAggressionMin);
                w.Write("AiAggressionArrangeRandom", AiAggressionArrangeRandom);
                w.Write("AiAggressionArrangeReverse", AiAggressionArrangeReverse);

                w.WriteEndObject();

                return s.ToString();
            }
        }
        #endregion

        private bool _ignoreStartingPosition;

        public bool IgnoreStartingPosition {
            get => _ignoreStartingPosition;
            set {
                if (Equals(value, _ignoreStartingPosition)) return;
                _ignoreStartingPosition = value;
                OnPropertyChanged();
                UpdatePlayerEntry();
            }
        }

        private double _playerBallast;

        public double PlayerBallast {
            get => _playerBallast;
            set {
                if (Equals(value, _playerBallast)) return;
                _playerBallast = value;
                OnPropertyChanged();
                SaveLater();

                if (PlayerEntry != null) {
                    PlayerEntry.Ballast = value;
                }
            }
        }

        private double _playerRestrictor;

        public double PlayerRestrictor {
            get => _playerRestrictor;
            set {
                if (Equals(value, _playerRestrictor)) return;
                _playerRestrictor = value;
                OnPropertyChanged();
                SaveLater();

                if (PlayerEntry != null) {
                    PlayerEntry.Restrictor = value;
                }
            }
        }

        public RaceGridViewModel(bool ignoreStartingPosition = false, [CanBeNull] string keySaveable = KeySaveable) {
            IgnoreStartingPosition = ignoreStartingPosition;

            _saveable = new SaveHelper<SaveableData>(keySaveable, () => {
                var data = new SaveableData {
                    ModeId = Mode.Id,
                    FilterValue = FilterValue,
                    RandomSkinsFilter = RandomSkinsFilter,
                    ShuffleCandidates = ShuffleCandidates,
                    VarietyLimitation = VarietyLimitation,
                    OpponentsNumber = OpponentsNumber,
                    StartingPosition = StartingPosition,

                    AiLevel = AiLevel,
                    AiLevelMin = AiLevelMin,
                    AiLevelArrangeRandom = AiLevelArrangeRandom,
                    AiLevelArrangeReverse = AiLevelArrangeReverse,

                    AiAggression = AiAggression,
                    AiAggressionMin = AiAggressionMin,
                    AiAggressionArrangeRandom = AiAggressionArrangeRandom,
                    AiAggressionArrangeReverse = AiAggressionArrangeReverse,

                    PlayerBallast = PlayerBallast,
                    PlayerRestrictor = PlayerRestrictor,
                };

                if (Mode == BuiltInGridMode.Custom) {
                    data.CarIds = NonfilteredList.Where(x => !x.SpecialEntry).Select(x => x.Car.Id).ToArray();
                } else {
                    var priority = false;
                    data.CarIds = NonfilteredList.Select(x => {
                        if (x.CandidatePriority != 1) priority = true;
                        return x.Car.Id;
                    }).ToArray();

                    if (priority) {
                        data.CandidatePriorities = NonfilteredList.Select(x => x.CandidatePriority).ToArray();
                    }
                }

                if (data.CarIds != null) {
                    var filtered = NonfilteredList.Where(x => !x.SpecialEntry).ToList();

                    if (filtered.Any(x => x.AiLevel.HasValue)) {
                        data.AiLevels = filtered.Select(x => x.AiLevel ?? -1).ToArray();
                    }

                    if (filtered.Any(x => x.AiAggression.HasValue)) {
                        data.AiAggressions = filtered.Select(x => x.AiAggression ?? -1).ToArray();
                    }

                    if (filtered.Any(x => x.Ballast != 0)) {
                        data.Ballasts = filtered.Select(x => x.Ballast).ToArray();
                    }

                    if (filtered.Any(x => x.Restrictor != 0)) {
                        data.Restrictors = filtered.Select(x => x.Restrictor).ToArray();
                    }

                    if (filtered.Any(x => x.Name != null)) {
                        data.Names = filtered.Select(x => x.Name).ToArray();
                    }

                    if (filtered.Any(x => x.Nationality != null)) {
                        data.Nationalities = filtered.Select(x => x.Nationality).ToArray();
                    }

                    if (filtered.Any(x => x.CarSkin != null)) {
                        data.SkinIds = filtered.Select(x => x.CarSkin?.Id).ToArray();
                    }

                    if (filtered.Any(x => x.AiLimitationDetails?.IsAnySet == true)) {
                        data.AiLimitations = filtered.Select(x => x.AiLimitationDetailsData).ToArray();
                    }
                }

                return data;
            }, data => {
                ShuffleCandidates = data.ShuffleCandidates ?? true;
                VarietyLimitation = data.VarietyLimitation ?? 0;

                AiLevel = data.AiLevel;
                AiLevelMin = data.AiLevelMin;
                AiLevelArrangeRandom = data.AiLevelArrangeRandomly.HasValue ? (data.AiLevelArrangeRandomly.Value ? 1d : 0d) :
                        data.AiLevelArrangeRandom;
                AiLevelArrangeReverse = data.AiLevelArrangeReverse ?? false;

                AiAggression = data.AiAggression;
                AiAggressionMin = data.AiAggressionMin;
                AiAggressionArrangeRandom = data.AiAggressionArrangeRandom;
                AiAggressionArrangeReverse = data.AiAggressionArrangeReverse ?? false;

                FilterValue = data.FilterValue;
                ErrorMessage = null;

                // NonfilteredList is not gonna be rebuilt now, because of LoadingItself flag set by SaveableHelper
                var mode = Modes.GetByIdOrDefault<IRaceGridMode>(data.ModeId);
                if (mode == null) {
                    NonfatalError.NotifyBackground(ToolsStrings.RaceGrid_GridModeIsMissing,
                            string.Format(ToolsStrings.RaceGrid_GridModeIsMissing_Commentary, data.ModeId));
                    Mode = BuiltInGridMode.SameCar;
                } else {
                    Mode = mode;
                }

                if (Mode.CandidatesMode) {
                    if (Mode == BuiltInGridMode.CandidatesManual && data.CarIds != null) {
                        // TODO: Async?
                        NonfilteredList.ReplaceEverythingBy(data.CarIds.Select(x => CarsManager.Instance.GetById(x)).Select((x, i) => {
                            if (x == null) return null;

                            var aiLevel = data.AiLevels?.ArrayElementAtOrDefault(i);
                            var aiAggression = data.AiAggressions?.ArrayElementAtOrDefault(i);
                            var carSkinId = data.SkinIds?.ArrayElementAtOrDefault(i);
                            return new RaceGridEntry(x) {
                                CandidatePriority = data.CandidatePriorities?.ElementAtOr(i, 1) ?? 1,
                                AiLevel = aiLevel >= 0 ? aiLevel : (int?)null,
                                AiAggression = aiAggression >= 0 ? aiAggression : (int?)null,
                                Ballast = data.Ballasts?.ArrayElementAtOrDefault(i) ?? 0,
                                Restrictor = data.Restrictors?.ArrayElementAtOrDefault(i) ?? 0,
                                Name = data.Names?.ArrayElementAtOrDefault(i),
                                Nationality = data.Nationalities?.ArrayElementAtOrDefault(i),
                                CarSkin = carSkinId != null ? x.GetSkinById(carSkinId) : null,
                                AiLimitationDetailsData =  data.AiLimitations?.ArrayElementAtOrDefault(i),
                            };
                        }).NonNull());
                        _setPropertiesLater = null;
                    } else {
                        // So, we clear list of opponents to rebuild it later
                        NonfilteredList.Clear();
                        _setPropertiesLater = data;
                    }

                    SetOpponentsNumberInternal(data.OpponentsNumber ?? 7);
                } else {
                    NonfilteredList.ReplaceEverythingBy(data.CarIds?.Select(x => CarsManager.Instance.GetById(x)).Select((x, i) => {
                        if (x == null) return null;

                        var aiLevel = data.AiLevels?.ArrayElementAtOrDefault(i);
                        var aiAggression = data.AiAggressions?.ArrayElementAtOrDefault(i);
                        var carSkinId = data.SkinIds?.ArrayElementAtOrDefault(i);

                        return new RaceGridEntry(x) {
                            AiLevel = aiLevel >= 0 ? aiLevel : null,
                            AiAggression = aiAggression >= 0 ? aiAggression : (int?)null,
                            Ballast = data.Ballasts?.ArrayElementAtOrDefault(i) ?? 0,
                            Restrictor = data.Restrictors?.ArrayElementAtOrDefault(i) ?? 0,
                            Name = data.Names?.ArrayElementAtOrDefault(i),
                            Nationality = data.Nationalities?.ArrayElementAtOrDefault(i),
                            CarSkin = carSkinId != null ? x.GetSkinById(carSkinId) : null,
                            AiLimitationDetailsData =  data.AiLimitations?.ArrayElementAtOrDefault(i),
                        };
                    }).NonNull() ?? new RaceGridEntry[0]);
                    _setPropertiesLater = null;
                }

                StartingPosition = data.StartingPosition ?? 7;

                // Here, to be specific
                FinishLoading();

                PlayerBallast = data.PlayerBallast;
                PlayerRestrictor = data.PlayerRestrictor;
            }, Reset);

            _presetsHelper = new PresetsMenuHelper();
            _randomGroup = new HierarchicalGroup(ToolsStrings.RaceGrid_Random);
            UpdateRandomModes();

            Modes = new HierarchicalGroup {
                BuiltInGridMode.SameCar,
                _randomGroup,
                BuiltInGridMode.Custom,
                _presetsHelper.Create(PresetableCategory, p => {
                    ImportFromPresetData(p.ReadData());
                }, ControlsStrings.Common_Presets)
            };

            NonfilteredList.CollectionChanged += OnCollectionChanged;
            NonfilteredList.ItemPropertyChanged += OnItemPropertyChanged;
            FilteredView = new BetterListCollectionView(NonfilteredList) { CustomSort = this };

            // _saveable.Initialize();
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update += OnGridTypesUpdate;
        }

        public void FinishLoading() {
            if (Mode.CandidatesMode) {
                UpdateViewFilter();

                if (Mode != BuiltInGridMode.CandidatesManual) {
                    RebuildGridAsync().Forget();
                } else {
                    LoadLaterProperties(NonfilteredList);
                }
            } else {
                SetOpponentsNumberInternal(NonfilteredList.Count);
                UpdateOpponentsNumber();
            }

            UpdatePlayerEntry();
        }

        public void Reset() {
            ShuffleCandidates = true;
            VarietyLimitation = 0;

            AiLevel = 95;
            AiLevelMin = 85;
            AiLevelArrangeRandom = 0.1;
            AiLevelArrangeReverse = false;

            AiAggression = 0;
            AiAggressionMin = 0;
            AiAggressionArrangeRandom = 0.1;
            AiAggressionArrangeReverse = false;

            PlayerBallast = PlayerRestrictor = 0;

            FilterValue = "";
            ErrorMessage = null;
            Mode = BuiltInGridMode.SameCar;
            SetOpponentsNumberInternal(7);
            StartingPosition = 7;

            _setPropertiesLater = null;
        }

        #region FS watching
        private void OnGridTypesUpdate(object sender, EventArgs e) {
            UpdateRandomModes();
        }

        public void Dispose() {
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update -= OnGridTypesUpdate;
            _presetsHelper.Dispose();
        }
        #endregion

        #region Presets
        private readonly PresetsMenuHelper _presetsHelper;

        private ICommand _savePresetCommand;

        public ICommand SavePresetCommand => _savePresetCommand ?? (_savePresetCommand = new DelegateCommand(() => {
            var data = ExportToPresetData();
            if (data == null) return;
            PresetsManager.Instance.SavePresetUsingDialog(PresetableKey, PresetableCategory,
                    data, null /* TODO */);
        }));

        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        private async Task Share() {
            var data = ExportToPresetData();
            if (data == null) return;
            await SharingUiHelper.ShareAsync(SharedEntryType.RaceGridPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(PresetableKeyValue)), null,
                    data);
        }

        public static void LoadPreset(string presetFilename) {
            UserPresetsControl.LoadPreset(PresetableKeyValue, presetFilename);
        }

        public static void LoadSerializedPreset([NotNull] string serializedPreset, [NotNull] string keySaveable = KeySaveable) {
            if (!UserPresetsControl.LoadSerializedPreset(PresetableKeyValue, serializedPreset)) {
                ValuesStorage.Set(keySaveable, serializedPreset);
            }
        }
        #endregion

        #region Non-filtered list
        public ChangeableObservableCollection<RaceGridEntry> NonfilteredList { get; } = new ChangeableObservableCollection<RaceGridEntry>();

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            SaveLater();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(RaceGridEntry.IsDeleted):
                    DeleteEntry((RaceGridEntry)sender);
                    return;
                case nameof(RaceGridEntry.CandidatePriority):
                    if (Mode.CandidatesMode) {
                        Mode = BuiltInGridMode.CandidatesManual;
                    }
                    break;
            }

            SaveLater();
        }
        #endregion

        #region External loading (for compatibility)
        protected bool LoadingItself => LoadingFromOutside || _saveable.IsLoading;

        public bool LoadingFromOutside { private get; set; }
        #endregion

        #region Active mode
        private IRaceGridMode _mode;
        private bool _modeKeepOrder;

        [NotNull]
        public IRaceGridMode Mode {
            get => _mode;
            set {
                if (Equals(value, _mode)) return;

                var previousMode = _mode;
                _mode = value;
                OnPropertyChanged();
                SaveLater();

                ErrorMessage = null;
                FilteredView.CustomSort = value.CandidatesMode ? this : null;
                if (LoadingItself) return;

                if (value == BuiltInGridMode.SameCar) {
                    NonfilteredList.ReplaceEverythingBy(_playerCar == null ? new RaceGridEntry[0] : new[] { new RaceGridEntry(_playerCar) });
                } else if (value != BuiltInGridMode.CandidatesManual && value.CandidatesMode) {
                    RebuildGridAsync().Forget();
                } else if (!value.CandidatesMode == previousMode?.CandidatesMode) {
                    if (previousMode == BuiltInGridMode.SameCar && NonfilteredList.Count == 1) {
                        var opponent = NonfilteredList[0];
                        NonfilteredList.ReplaceEverythingBy(Enumerable.Range(0, OpponentsNumber).Select(x => x > 0 ? opponent.Clone() : opponent));
                    } else {
                        NonfilteredList.ReplaceEverythingBy(value.CandidatesMode
                                ? CombinePriorities(NonfilteredList.ApartFrom(PlayerEntry))
                                : _modeKeepOrder ? FlattenPriorities(NonfilteredList) : FlattenPriorities(NonfilteredList).Sort(Compare));
                    }
                }

                UpdateViewFilter();
                UpdateOpponentsNumber();
                UpdatePlayerEntry();
                UpdateExceeded();
            }
        }

        [CanBeNull]
        private RaceGridPlayerEntry _playerEntry;

        [CanBeNull]
        private RaceGridPlayerEntry PlayerEntry {
            get => _playerEntry;
            set {
                if (ReferenceEquals(value, _playerEntry)) return;
                _playerEntry?.UnsubscribeWeak(OnPlayerEntryPropertyChanged);
                _playerEntry = value;
                _playerEntry?.SubscribeWeak(OnPlayerEntryPropertyChanged);
            }
        }

        private void UpdatePlayerEntry() {
            if (_playerCar != PlayerEntry?.Car) {
                if (PlayerEntry != null) {
                    NonfilteredList.Remove(PlayerEntry);
                    PlayerEntry = null;
                }

                if (Mode != BuiltInGridMode.Custom || IgnoreStartingPosition) return;
                PlayerEntry = _playerCar == null ? null : new RaceGridPlayerEntry(_playerCar) {
                    Ballast = PlayerBallast,
                    Restrictor = PlayerRestrictor
                };

            }

            if (PlayerEntry == null) return;
            if (Mode == BuiltInGridMode.Custom) {
                var index = NonfilteredList.IndexOf(PlayerEntry);
                var pos = StartingPosition - 1;

                if (index == -1) {
                    if (pos > NonfilteredList.Count) {
                        NonfilteredList.Add(PlayerEntry);
                    } else if (pos >= 0) {
                        NonfilteredList.Insert(pos, PlayerEntry);
                    }
                } else {
                    if (pos < 0) {
                        NonfilteredList.RemoveAt(index);
                    } else if (pos != index) {
                        NonfilteredList.Move(index, pos);
                    }
                }
            } else if (NonfilteredList.Contains(PlayerEntry)) {
                NonfilteredList.Remove(PlayerEntry);
                PlayerEntry = null;
            }
        }

        private void OnPlayerEntryPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            switch (propertyChangedEventArgs.PropertyName) {
                case nameof(PlayerEntry.Ballast):
                    PlayerBallast = PlayerEntry?.Ballast ?? PlayerBallast;
                    break;
                case nameof(PlayerEntry.Restrictor):
                    PlayerRestrictor = PlayerEntry?.Restrictor ?? PlayerRestrictor;
                    break;
            }
        }

        private void UpdateOpponentsNumber() {
            if (!Mode.CandidatesMode) {
                SetOpponentsNumberInternal(NonfilteredList.ApartFrom(PlayerEntry).Count());
            }
        }

        private IEnumerable<RaceGridEntry> FlattenPriorities(IEnumerable<RaceGridEntry> candidates) {
            foreach (var entry in candidates) {
                var p = entry.CandidatePriority;
                entry.CandidatePriority = 1;

                yield return entry;
                for (var i = 1; i < p && i < 6; i++) {
                    yield return entry.Clone();
                }
            }
        }

        private IEnumerable<RaceGridEntry> CombinePriorities(IEnumerable<RaceGridEntry> entries) {
            var list = entries.ToList();
            var combined = new List<RaceGridEntry>();
            for (var i = 0; i < list.Count; i++) {
                var entry = list[i];
                if (combined.Contains(entry)) continue;

                var priority = 1;
                for (var j = i + 1; j < list.Count; j++) {
                    var next = list[j];
                    if (entry.Same(next)) {
                        priority++;
                        combined.Add(next);
                    }
                }

                entry.CandidatePriority = priority;
                yield return entry;
            }
        }
        #endregion

        #region Modes list
        private readonly HierarchicalGroup _randomGroup;

        public HierarchicalGroup Modes { get; }

        private ICommand _switchModeCommand;

        public ICommand SetModeCommand => _switchModeCommand ?? (_switchModeCommand = new DelegateCommand<BuiltInGridMode>(o => {
            Mode = o;
        }, o => o != null));

        private void UpdateRandomModes() {
            var items = new List<object> {
                BuiltInGridMode.CandidatesSameGroup,
                BuiltInGridMode.CandidatesFiltered,
                BuiltInGridMode.CandidatesManual
            };

            var dataAdded = false;
            foreach (var entry in FilesStorage.Instance.GetContentFiles(ContentCategory.GridTypes)) {
                CandidatesGridMode.SetNamespace(entry.Name);

                try {
                    var list = JsonConvert.DeserializeObject<List<CandidatesGridMode>>(FileUtils.ReadAllText(entry.Filename));
                    if (list.Any() && !dataAdded) {
                        items.Add(new Separator());
                        dataAdded = true;
                    }

                    if (entry.Name == "GridTypes") {
                        items.AddRange(list);
                    } else {
                        items.Add(new HierarchicalGroup(entry.Name, list));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify($"Can’t add modes from “{Path.GetFileName(entry.Filename)}”", e);
                }
            }

            _randomGroup.ReplaceEverythingBy(items);
        }
        #endregion

        #region Fix for properties for special modes
        // Used in case user set some parameters — such as ballast, for example — to a car from
        // a list built by LUA function or something. While loading, those parameters are gonna
        // end up here. And then, when grid will gonna be rebuilt, we’ll try to put them back
        // where possible.
        private SaveableData _setPropertiesLater;

        private void LoadLaterProperties(IReadOnlyList<RaceGridEntry> candidates) {
            if (_setPropertiesLater != null) {
                var data = _setPropertiesLater;
                _setPropertiesLater = null;

                if (data.CarIds != null) {
                    for (var i = 0; i < data.CarIds.Length; i++) {
                        var carId = data.CarIds[i];
                        var entry = candidates.FirstOrDefault(x => x.Car.Id == carId);
                        if (entry != null && !entry.SpecialEntry) {
                            var aiLevel = data.AiLevels?.ArrayElementAtOrDefault(i);
                            var aiAggression = data.AiAggressions?.ArrayElementAtOrDefault(i);
                            var carSkinId = data.SkinIds?.ArrayElementAtOrDefault(i);
                            entry.CandidatePriority = data.CandidatePriorities?.ElementAtOr(i, 1) ?? 1;
                            entry.AiLevel = aiLevel >= 0 ? aiLevel : null;
                            entry.AiAggression = aiAggression >= 0 ? aiAggression : null;
                            entry.Ballast = data.Ballasts?.ArrayElementAtOrDefault(i) ?? 0;
                            entry.Restrictor = data.Restrictors?.ArrayElementAtOrDefault(i) ?? 0;
                            entry.Name = data.Names?.ArrayElementAtOrDefault(i);
                            entry.Nationality = data.Nationalities?.ArrayElementAtOrDefault(i);
                            entry.CarSkin = carSkinId != null ? entry.Car.GetSkinById(carSkinId) : null;
                            entry.AiLimitationDetailsData = data.AiLimitations?.ArrayElementAtOrDefault(i);
                        }
                    }
                }
            }
        }
        #endregion

        #region Candidates building
        public bool IsBusy => _rebuildingTask != null;

        private Task _rebuildingTask;

        public Task RebuildGridAsync() {
            if (_rebuildingTask == null) {
                _rebuildingTask = RebuildGridAsyncInner();
                OnPropertyChanged(nameof(IsBusy));
            }

            return _rebuildingTask;
        }

        private async Task RebuildGridAsyncInner() {
            try {
                await Task.Delay(50);

                OnPropertyChanged(nameof(IsBusy));
                ErrorMessage = null;

                Again:
                var mode = Mode;
                var candidates = await FindCandidates();
                if (mode != Mode) goto Again;

                // I’ve seen that XKCD comic, but I still think goto is more
                // suitable than a loop here.

                if (candidates == null) return;

                // Fitting those parameters…
                LoadLaterProperties(candidates);

                NonfilteredList.ReplaceEverythingBy(candidates);
            } catch (SyntaxErrorException e) {
                Logging.Warning(e.Message);
                ErrorMessage = string.Format(ToolsStrings.Common_SyntaxErrorFormat, e.Message);
                NonfilteredList.Clear();
                NonfatalError.Notify(ToolsStrings.RaceGrid_CannotUpdate, e);
            } catch (ScriptRuntimeException e) {
                Logging.Warning(e.Message);
                ErrorMessage = e.Message;
                NonfilteredList.Clear();
            } catch (InformativeException e) when (e.SolutionCommentary == null) {
                Logging.Warning(e);
                ErrorMessage = e.Message;
                NonfilteredList.Clear();
            } catch (Exception e) {
                Logging.Warning(e);
                ErrorMessage = e.Message;
                NonfilteredList.Clear();
                NonfatalError.Notify(ToolsStrings.RaceGrid_CannotUpdate, e);
            } finally {
                _rebuildingTask = null;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        private string _errorMessage;

        public string ErrorMessage {
            get => _errorMessage;
            set {
                if (Equals(value, _errorMessage)) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        [ItemCanBeNull]
        private async Task<IReadOnlyList<RaceGridEntry>> FindCandidates(CancellationToken cancellation = default(CancellationToken)) {
            var mode = Mode;

            // Don’t change anything in Fixed or Manual mode
            if (mode == BuiltInGridMode.Custom || mode == BuiltInGridMode.CandidatesManual) {
                return null;
            }

            // Basic mode, just one car
            if (mode == BuiltInGridMode.SameCar) {
                return _playerCar == null ? new RaceGridEntry[0] : new[] { new RaceGridEntry(_playerCar) };
            }

            // Other modes require cars list to be loaded
            if (!CarsManager.Instance.IsLoaded) {
                await CarsManager.Instance.EnsureLoadedAsync();
            }

            // Another simple mode
            if (mode == BuiltInGridMode.CandidatesFiltered) {
                return CarsManager.Instance.Enabled.Select(x => new RaceGridEntry(x)).ToArray();
            }

            // Same group mode
            if (mode == BuiltInGridMode.CandidatesSameGroup) {
                if (_playerCar == null) return new RaceGridEntry[0];

                var parent = _playerCar.Parent ?? _playerCar;
                return parent.Children.Prepend(parent).Where(x => x.Enabled).Select(x => new RaceGridEntry(x)).ToArray();
            }

            // Entry from a JSON-file
            if (mode.AffectedByCar && _playerCar == null || mode.AffectedByTrack && _track == null) {
                return new RaceGridEntry[0];
            }

            if (mode is CandidatesGridMode candidatesMode) {
                return await Task.Run(() => {
                    var carsEnumerable = (IEnumerable<CarObject>)CarsManager.Instance.Enabled.ToList();

                    if (!string.IsNullOrWhiteSpace(candidatesMode.Filter)) {
                        var filter = Filter.Create(CarObjectTester.Instance, candidatesMode.Filter);
                        carsEnumerable = carsEnumerable.Where(filter.Test);
                    }

                    if (!string.IsNullOrWhiteSpace(candidatesMode.Script)) {
                        var state = LuaHelper.GetExtended();
                        if (state == null) throw new InformativeException(ToolsStrings.Common_LuaFailed);

                        if (mode.AffectedByCar) {
                            state.Globals[@"selected"] = _playerCar;
                        }

                        if (mode.AffectedByTrack) {
                            state.Globals[@"track"] = _track;
                        }

                        var result = state.DoString(PrepareScript(candidatesMode.Script));
                        if (result.Type == DataType.Boolean && !result.Boolean) return new RaceGridEntry[0];

                        var fn = result.Function;
                        if (fn == null) throw new InformativeException(ToolsStrings.RaceGrid_InvalidScriptResult);

                        carsEnumerable = carsEnumerable.Where(x => fn.Call(x).Boolean);
                    }

                    return carsEnumerable.Select(x => new RaceGridEntry(x)).ToArray();
                }, cancellation);
            }

            Logging.Error($"Not supported mode: {mode.Id} ({mode.GetType().Name})");
            return new RaceGridEntry[0];
        }

        private static string PrepareScript(string script) {
            script = script.Trim();
            return script.Contains('\n') ? script : $"return function(tested)\nreturn {script}\nend";
        }
        #endregion

        #region Filtering
        public BetterListCollectionView FilteredView { get; }

        private string _filterValue;

        [CanBeNull]
        public string FilterValue {
            get => _filterValue;
            set {
                value = value?.Trim();
                if (Equals(value, _filterValue)) return;
                _filterValue = value;
                OnPropertyChanged();

                if (LoadingItself) return;
                UpdateViewFilter();
                SaveLater();
            }
        }

        private void UpdateViewFilter() {
            using (FilteredView.DeferRefresh()) {
                if (string.IsNullOrEmpty(FilterValue) || Mode == BuiltInGridMode.SameCar || Mode == BuiltInGridMode.Custom) {
                    FilteredView.Filter = null;
                } else {
                    var filter = Filter.Create(CarObjectTester.Instance, FilterValue);
                    FilteredView.Filter = o => filter.Test(((RaceGridEntry)o).Car);
                }
            }
        }

        public int Compare(object x, object y) {
            return (x as RaceGridEntry)?.Car.CompareTo((y as RaceGridEntry)?.Car) ?? 0;
        }

        private string _randomSkinsFilter;

        public string RandomSkinsFilter {
            get => _randomSkinsFilter;
            set {
                if (Equals(value, _randomSkinsFilter)) return;
                _randomSkinsFilter = value;
                OnPropertyChanged();
                SaveLater();
            }
        }
        #endregion

        #region Car and track
        [CanBeNull]
        private CarObject _playerCar;

        [CanBeNull]
        public CarObject PlayerCar {
            get => _playerCar;
            set {
                if (Equals(_playerCar, value)) return;

                _playerCar = value;
                OnPropertyChanged();

                if (Mode.AffectedByCar) {
                    RebuildGridAsync().Forget();
                }

                if (!Mode.CandidatesMode && StartingPosition > 0) {
                    UpdatePlayerEntry();
                }
            }
        }

        [CanBeNull]
        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase PlayerTrack {
            get => _track;
            set {
                if (Equals(_track, value)) return;

                if (_track != null) {
                    _track.PropertyChanged -= Track_OnPropertyChanged;
                }

                _track = value;
                OnPropertyChanged();

                if (Mode.AffectedByTrack) {
                    RebuildGridAsync().Forget();
                }

                if (value != null) {
                    TrackPitsNumber = value.SpecsPitboxesValue;
                    value.PropertyChanged += Track_OnPropertyChanged;
                }
            }
        }

        private void Track_OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_track != null && e.PropertyName == nameof(TrackObjectBase.SpecsPitboxes)) {
                TrackPitsNumber = _track.SpecsPitboxesValue;
            }
        }

        private int _trackPitsNumber;

        public int TrackPitsNumber {
            get => _trackPitsNumber;
            set {
                if (Equals(value, _trackPitsNumber)) return;
                _trackPitsNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpponentsNumberLimit));
                OnPropertyChanged(nameof(OpponentsNumberLimited));
                UpdateExceeded();
            }
        }

        public int OpponentsNumberLimit => TrackPitsNumber - 1;

        private void UpdateExceeded() {
            if (Mode.CandidatesMode) {
                foreach (var entry in NonfilteredList.Where(x => x.ExceedsLimit)) {
                    entry.ExceedsLimit = false;
                }
            } else {
                var left = OpponentsNumberLimit;
                foreach (var entry in NonfilteredList.ApartFrom(PlayerEntry)) {
                    entry.ExceedsLimit = --left < 0;
                }
            }
        }
        #endregion

        #region Simple properties
        private bool _shuffleCandidates;

        public bool ShuffleCandidates {
            get => _shuffleCandidates;
            set {
                if (Equals(value, _shuffleCandidates)) return;
                _shuffleCandidates = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _varietyLimitation;

        public int VarietyLimitation {
            get => _varietyLimitation;
            set {
                if (Equals(value, _varietyLimitation)) return;
                _varietyLimitation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayVarietyLimitation));
                SaveLater();
            }
        }

        public string DisplayVarietyLimitation {
            get => _varietyLimitation == 0 ? ToolsStrings.AssistState_Off : PluralizingConverter.PluralizeExt(_varietyLimitation, "{0} car");
            set => VarietyLimitation = value.As<int>();
        }
        #endregion

        #region AI Level
        public int AiLevelMinimum => SettingsHolder.Drive.QuickDriveExpandBounds ? 30 : 70;

        public int AiLevelMinimumLimited => Math.Max(AiLevelMinimum, 50);

        private double _aiLevel;

        public double AiLevel {
            get => _aiLevel;
            set {
                value = value.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100);
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiLevelFixed));
                SaveLater();

                if (value < AiLevelMin) {
                    _aiLevelMin = value;
                    OnPropertyChanged(nameof(AiLevelMin));
                }
            }
        }

        private double _aiLevelMin;

        public double AiLevelMin {
            get => _aiLevelMin;
            set {
                value = value.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100);
                if (Equals(value, _aiLevelMin)) return;
                _aiLevelMin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiLevelFixed));
                SaveLater();

                if (value > AiLevel) {
                    _aiLevel = value;
                    OnPropertyChanged(nameof(AiLevel));
                }
            }
        }

        private double _aiLevelArrangeRandom;

        public double AiLevelArrangeRandom {
            get => _aiLevelArrangeRandom;
            set {
                value = value.Round(0.01);
                if (Equals(value, _aiLevelArrangeRandom)) return;
                _aiLevelArrangeRandom = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _aiLevelArrangeReverse;

        public bool AiLevelArrangeReverse {
            get => _aiLevelArrangeReverse;
            set {
                if (Equals(value, _aiLevelArrangeReverse)) return;
                _aiLevelArrangeReverse = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        public bool AiLevelInDriverName {
            get => SettingsHolder.Drive.QuickDriveAiLevelInName;
            set {
                if (Equals(value, SettingsHolder.Drive.QuickDriveAiLevelInName)) return;
                SettingsHolder.Drive.QuickDriveAiLevelInName = value;
                OnPropertyChanged();
            }
        }

        public bool AiLevelFixed => AiLevel <= AiLevelMin;
        #endregion

        #region AI Aggression
        private double _aiAggression;

        public double AiAggression {
            get => _aiAggression;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _aiAggression)) return;
                _aiAggression = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiAggressionFixed));
                SaveLater();

                if (value < AiAggressionMin) {
                    _aiAggressionMin = value;
                    OnPropertyChanged(nameof(AiAggressionMin));
                }
            }
        }

        private double _aiAggressionMin;

        public double AiAggressionMin {
            get => _aiAggressionMin;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _aiAggressionMin)) return;
                _aiAggressionMin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiAggressionFixed));
                SaveLater();

                if (value > AiAggression) {
                    _aiAggression = value;
                    OnPropertyChanged(nameof(AiAggression));
                }
            }
        }

        private double _aiAggressionArrangeRandom;

        public double AiAggressionArrangeRandom {
            get => _aiAggressionArrangeRandom;
            set {
                value = value.Round(0.01);
                if (Equals(value, _aiAggressionArrangeRandom)) return;
                _aiAggressionArrangeRandom = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _aiAggressionArrangeReverse;

        public bool AiAggressionArrangeReverse {
            get => _aiAggressionArrangeReverse;
            set {
                if (Equals(value, _aiAggressionArrangeReverse)) return;
                _aiAggressionArrangeReverse = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        public bool AiAggressionInDriverName {
            get => SettingsHolder.Drive.QuickDriveAiAggressionInName;
            set {
                if (Equals(value, SettingsHolder.Drive.QuickDriveAiAggressionInName)) return;
                SettingsHolder.Drive.QuickDriveAiAggressionInName = value;
                OnPropertyChanged();
            }
        }

        public bool AiAggressionFixed => AiAggression <= AiAggressionMin;
        #endregion

        #region Grid methods (addind, deleting)
        public void AddEntry([NotNull] CarObject car) {
            AddEntry(new RaceGridEntry(car));
        }

        public void AddEntry([NotNull] RaceGridEntry entry) {
            InsertEntry(-1, entry);
        }

        public void InsertEntry(int index, [NotNull] CarObject car) {
            InsertEntry(index, new RaceGridEntry(car));
        }

        public void InsertEntry(int index, [NotNull] RaceGridEntry entry) {
            var oldIndex = NonfilteredList.IndexOf(entry);

            var count = NonfilteredList.Count;
            var isNew = oldIndex == -1;
            var limit = isNew ? count : count - 1;
            if (index < 0 || index > limit) {
                index = limit;
            }

            if (oldIndex == index) return;
            if (Mode.CandidatesMode) {
                if (isNew) {
                    Mode = BuiltInGridMode.CandidatesManual;

                    var existed = NonfilteredList.FirstOrDefault(x => x.Same(entry));
                    if (existed != null) {
                        existed.CandidatePriority++;
                    } else {
                        NonfilteredList.Add(entry);
                    }

                    return;
                }

                NonfilteredList.ReplaceEverythingBy(NonfilteredList.Sort(Compare));
                NonfilteredList.Move(oldIndex, index);

                try {
                    _modeKeepOrder = true;
                    Mode = BuiltInGridMode.Custom;
                } finally {
                    _modeKeepOrder = false;
                }
            } else if (isNew) {
                if (index == count) {
                    NonfilteredList.Add(entry);
                } else {
                    NonfilteredList.Insert(index, entry);
                }

                UpdateOpponentsNumber();
            } else if (index != oldIndex) {
                NonfilteredList.Move(oldIndex, index);
            }

            if (StartingPosition != 0) {
                StartingPositionLimited = NonfilteredList.IndexOf(PlayerEntry) + 1;
            }

            UpdateExceeded();
        }

        public void DeleteEntry(RaceGridEntry entry) {
            if (entry is RaceGridPlayerEntry) {
                StartingPosition = 0;
                return;
            }

            NonfilteredList.Remove(entry);
            if (Mode == BuiltInGridMode.Custom) {
                UpdateOpponentsNumber();
            } else {
                Mode = BuiltInGridMode.CandidatesManual;
            }

            UpdateExceeded();
        }
        #endregion

        #region Opponents number and starting position
        private int _opponentsNumber;

        private void SetOpponentsNumberInternal(int value) {
            if (Equals(value, _opponentsNumber)) return;

            var last = Mode.CandidatesMode && _startingPosition == StartingPositionLimit;
            _opponentsNumber = value;

            OnPropertyChanged(nameof(OpponentsNumber));
            OnPropertyChanged(nameof(OpponentsNumberLimited));
            OnPropertyChanged(nameof(StartingPositionLimit));

            if (last && StartingPositionLimit > 0 /*|| _startingPosition > StartingPositionLimit*/) {
                StartingPositionLimited = StartingPositionLimit;
            } else {
                OnPropertyChanged(nameof(StartingPositionLimited));
                SaveLater();
            }
        }

        public int OpponentsNumber {
            get => _opponentsNumber;
            set {
                if (!Mode.CandidatesMode) return;
                if (value < 1) value = 1;
                SetOpponentsNumberInternal(value);
            }
        }

        public int OpponentsNumberLimited {
            get => _opponentsNumber.Clamp(0, OpponentsNumberLimit);
            set => OpponentsNumber = value.Clamp(1, OpponentsNumberLimit);
        }

        public int StartingPositionLimit => OpponentsNumberLimited + 1;

        private int _startingPosition;

        public int StartingPosition {
            get => _startingPosition;
            set {
                if (value < 0) value = 0;
                if (Equals(value, _startingPosition)) return;
                _startingPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartingPositionLimited));
                SaveLater();

                if (!LoadingItself) {
                    UpdatePlayerEntry();
                }
            }
        }

        public int StartingPositionLimited {
            get => _startingPosition.Clamp(0, StartingPositionLimit);
            set => StartingPosition = value.Clamp(0, StartingPositionLimit);
        }
        #endregion

        #region Generation
        [ItemCanBeNull]
        public async Task<IList<Game.AiCar>> GenerateGameEntries(CancellationToken cancellation = default(CancellationToken)) {
            if (IsBusy) {
                await RebuildGridAsync();
                if (cancellation.IsCancellationRequested) return null;
            }

            var opponentsNumber = OpponentsNumberLimited;
            if (FilteredView.Count == 0 || opponentsNumber == 0) {
                return new Game.AiCar[0];
            }

            var skins = await GenerateGameEntries_Skins(cancellation);
            if (cancellation.IsCancellationRequested) return null;

            var nameNationalities = GenerateGameEntries_NameNationalities(opponentsNumber);
            var aiLevels = GenerateGameEntries_AiLevels(opponentsNumber);
            var aiAggressions = GenerateGameEntries_AiAggressions(opponentsNumber);
            var final = GenerateGameEntries_FinalStep(opponentsNumber);

            if (_playerCar != null) {
                skins.GetValueOrDefault(_playerCar.Id)?.IgnoreOnce(_playerCar.SelectedSkin);
            }

            var takenNames = new List<string>(opponentsNumber);
            return final.Take(opponentsNumber).Select((entry, i) => {
                var level = entry.AiLevel ?? aiLevels?[i] ?? AiLevel;
                var aggression = entry.AiAggression ?? aiAggressions?[i] ?? AiAggression;

                var skin = entry.CarSkin ?? skins.GetValueOrDefault(entry.Car.Id)?.Next;
                var name = entry.Name;
                if (string.IsNullOrWhiteSpace(name) && SettingsHolder.Drive.QuickDriveUseSkinNames) {
                    var skinDriverNames = skin?.DriverName?.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    if (skinDriverNames?.Count > 0) {
                        name = GoodShuffle.Get(skinDriverNames).Take(skinDriverNames.Count).FirstOrDefault(x => !takenNames.Contains(x)) ?? name;
                        takenNames.Add(name);
                    }
                }

                if (string.IsNullOrWhiteSpace(name)) {
                    name = nameNationalities?[i].Name ?? @"AI #" + i;
                    takenNames.Add(name);
                }

                var nationality = (SettingsHolder.Drive.QuickDriveUseSkinNames ? skin?.Country : null)
                        ?? entry.Nationality ?? nameNationalities?[i].Nationality ?? @"Italy";
                var skinId = skin?.Id;

                string displayName;
                switch ((AiLevelInDriverName ? 1 : 0) + (AiAggressionInDriverName ? 2 : 0)) {
                    case 0:
                        displayName = name;
                        break;
                    case 1:
                        displayName = $@"{name} ({level}%)";
                        break;
                    case 2:
                        displayName = $@"{name} ({aggression}%)";
                        break;
                    case 3:
                        displayName = $@"{name} ({level}%, {aggression}%)";
                        break;
                    default:
                        displayName = name;
                        break;
                }

                var carId = entry.Car.Id;
                if (entry.AiLimitationDetails.IsActive) {
                    // TODO: Async?
                    try {
                        carId = entry.AiLimitationDetails.Apply();
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t make AI-specific version of a car", e);
                    }
                }

                return new Game.AiCar {
                    AiLevel = level,
                    AiAggression = aggression,
                    CarId = carId,
                    DriverName = displayName,
                    Nationality = nationality,
                    Ballast = entry.Ballast,
                    Restrictor = entry.Restrictor,
                    Setup = "",
                    SkinId = skinId
                };
            }).ToList();
        }

        [NotNull]
        private async Task<Dictionary<string, GoodShuffle<CarSkinObject>>> GenerateGameEntries_Skins(CancellationToken cancellation) {
            var skinsFilter = string.IsNullOrWhiteSpace(RandomSkinsFilter) ? null : Filter.Create(CarSkinObjectTester.Instance, RandomSkinsFilter);
            var skins = new Dictionary<string, GoodShuffle<CarSkinObject>>();
            foreach (var car in FilteredView.OfType<RaceGridEntry>().Where(x => x.CarSkin == null).Select(x => x.Car).Distinct()) {
                await car.SkinsManager.EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) break;

                skins[car.Id] = GoodShuffle.Get(skinsFilter == null ? car.EnabledOnlySkins : car.EnabledOnlySkins.Where(skinsFilter.Test));
                if (skins[car.Id].Size == 0) {
                    throw new InformativeException($"Skins for car {car.DisplayName} not found", "Make sure filter is not too strict.");
                }
            }
            return skins;
        }

        [CanBeNull]
        private static NameNationality[] GenerateGameEntries_NameNationalities(int opponentsNumber) {
            if (opponentsNumber == 7 && OptionNfsPorscheNames) {
                return new[] {
                    new NameNationality { Name = "Dylan", Nationality = "Wales" },
                    new NameNationality { Name = "Parise", Nationality = "Italy" },
                    new NameNationality { Name = "Steele", Nationality = "United States" },
                    new NameNationality { Name = "Wingnut", Nationality = "England" },
                    new NameNationality { Name = "Leadfoot", Nationality = "Australia" },
                    new NameNationality { Name = "Amazon", Nationality = "United States" },
                    new NameNationality { Name = "Backlash", Nationality = "United States" }
                };
            }

            return DataProvider.Instance.NationalitiesAndNames.Any()
                    ? GoodShuffle.Get(DataProvider.Instance.NationalitiesAndNamesList).Take(opponentsNumber).ToArray()
                    : null;
        }

        [CanBeNull]
        private List<double> GenerateGameEntries_AiLevels(int opponentsNumber) {
            if (AiLevelFixed) return null;

            var aiLevelsInner = from i in Enumerable.Range(0, opponentsNumber)
                                select AiLevelMin + ((opponentsNumber < 2 ? 1d : 1d - i / (opponentsNumber - 1d)) * (AiLevel - AiLevelMin)).RoundToInt();
            if (AiLevelArrangeReverse) {
                aiLevelsInner = aiLevelsInner.Reverse();
            }

            if (Equals(AiLevelArrangeRandom, 1d)) {
                aiLevelsInner = GoodShuffle.Get(aiLevelsInner);
            } else if (AiLevelArrangeRandom > 0d) {
                aiLevelsInner = LimitedShuffle.Get(aiLevelsInner, AiLevelArrangeRandom);
            }

            return aiLevelsInner.Take(opponentsNumber).ToList();
        }

        [CanBeNull]
        private List<double> GenerateGameEntries_AiAggressions(int opponentsNumber) {
            if (AiAggressionFixed) return null;

            var aiAggressionsInner = from i in Enumerable.Range(0, opponentsNumber)
                                     select AiAggressionMin
                                             + ((opponentsNumber < 2 ? 1d : 1d - i / (opponentsNumber - 1d)) * (AiAggression - AiAggressionMin)).RoundToInt();
            if (AiAggressionArrangeReverse) {
                aiAggressionsInner = aiAggressionsInner.Reverse();
            }

            if (Equals(AiAggressionArrangeRandom, 1d)) {
                aiAggressionsInner = GoodShuffle.Get(aiAggressionsInner);
            } else if (AiAggressionArrangeRandom > 0d) {
                aiAggressionsInner = LimitedShuffle.Get(aiAggressionsInner, AiAggressionArrangeRandom);
            }

            return aiAggressionsInner.Take(opponentsNumber).ToList();
        }

        [NotNull]
        private IEnumerable<RaceGridEntry> GenerateGameEntries_FinalStep(int opponentsNumber) {
            if (!Mode.CandidatesMode) return NonfilteredList.Where(x => !x.SpecialEntry);

            var allowed = VarietyLimitation <= 0 ? null
                    : GoodShuffle.Get(FilteredView.OfType<RaceGridEntry>().Select(x => x.Car).Distinct()).Take(VarietyLimitation.Clamp(1, 1000)).ToList();
            var list = FilteredView.OfType<RaceGridEntry>().SelectMany(x => allowed?.Contains(x.Car) != false
                    ? new[] { x }.Repeat(x.CandidatePriority) : new RaceGridEntry[0]).ToList();

            if (!ShuffleCandidates) {
                var skip = _playerCar;
                return LinqExtension.RangeFrom().Select(x => list.RandomElement()).Where(x => {
                    if (x.Car != skip) return true;
                    skip = null;
                    return false;
                }).Take(opponentsNumber);
            }

            var shuffled = GoodShuffle.Get(list);
            if (_playerCar != null) {
                var same = list.FirstOrDefault(x => x.Car == _playerCar);
                if (same != null) {
                    shuffled.IgnoreOnce(same);
                }
            }
            return shuffled.Take(opponentsNumber);
        }
        #endregion
    }
}