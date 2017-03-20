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
using AcManager.Tools.Filters;
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
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

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

        public string PresetableCategory => PresetableKeyValue;

        public string PresetableKey => PresetableKeyValue;

        public string DefaultPreset => null;

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

            [CanBeNull]
            public string[] CarIds;

            [CanBeNull]
            public int[] CandidatePriorities;

            [CanBeNull]
            public int[] AiLevels;

            [CanBeNull]
            public string[] Names, Nationalities, SkinIds;

            public bool? AiLevelFixed, AiLevelArrangeRandomly, AiLevelArrangeReverse, ShuffleCandidates;
            public double? AiLevelArrangeRandom;
            public int? AiLevel, AiLevelMin, OpponentsNumber, StartingPosition;

            string IJsonSerializable.ToJson() {
                var s = new StringWriter();
                var w = new JsonTextWriter(s);
                
                w.WriteStartObject();
                w.Write("ModeId", ModeId);
                w.Write("FilterValue", FilterValue);

                w.Write("CarIds", CarIds);
                w.Write("CandidatePriorities", CandidatePriorities);
                w.Write("AiLevels", AiLevels);

                w.Write("Names", Names);
                w.Write("Nationalities", Nationalities);
                w.Write("SkinIds", SkinIds);

                w.Write("AiLevelFixed", AiLevelFixed);
                w.Write("AiLevelArrangeRandom", AiLevelArrangeRandom);
                w.Write("AiLevelArrangeReverse", AiLevelArrangeReverse);
                w.Write("AiLevel", AiLevel);
                w.Write("AiLevelMin", AiLevelMin);
                w.Write("OpponentsNumber", OpponentsNumber);
                w.Write("StartingPosition", StartingPosition);
                w.WriteEndObject();

                return s.ToString();
            }
        }
        #endregion

        private bool _ignoreStartingPosition;

        public bool IgnoreStartingPosition {
            get { return _ignoreStartingPosition; }
            set {
                if (Equals(value, _ignoreStartingPosition)) return;
                _ignoreStartingPosition = value;
                OnPropertyChanged();
                UpdatePlayerEntry();
            }
        }

        public RaceGridViewModel(bool ignoreStartingPosition = false, [CanBeNull] string keySaveable = KeySaveable) {
            IgnoreStartingPosition = ignoreStartingPosition;

            _saveable = new SaveHelper<SaveableData>(keySaveable, () => {
                var data = new SaveableData {
                    ModeId = Mode.Id,
                    FilterValue = FilterValue,
                    ShuffleCandidates = ShuffleCandidates,
                    AiLevelFixed = AiLevelFixed,
                    AiLevelArrangeRandom = AiLevelArrangeRandom,
                    AiLevelArrangeReverse = AiLevelArrangeReverse,
                    AiLevel = AiLevel,
                    AiLevelMin = AiLevelMin,
                    OpponentsNumber = OpponentsNumber,
                    StartingPosition = StartingPosition,
                };

                if (Mode == BuiltInGridMode.CandidatesManual) {
                    var priority = false;
                    data.CarIds = NonfilteredList.Select(x => {
                        if (x.CandidatePriority != 1) priority = true;
                        return x.Car.Id;
                    }).ToArray();

                    if (priority) {
                        data.CandidatePriorities = NonfilteredList.Select(x => x.CandidatePriority).ToArray();
                    }
                } else if (Mode == BuiltInGridMode.Custom) {
                    data.CarIds = NonfilteredList.Where(x => !x.SpecialEntry).Select(x => x.Car.Id).ToArray();
                }

                if (data.CarIds != null) {
                    var filtered = NonfilteredList.Where(x => !x.SpecialEntry).ToList();

                    if (filtered.Any(x => x.AiLevel.HasValue)) {
                        data.AiLevels = filtered.Select(x => x.AiLevel ?? -1).ToArray();
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
                }

                return data;
            }, data => {
                ShuffleCandidates = data.ShuffleCandidates ?? true;
                AiLevelFixed = data.AiLevelFixed ?? false;
                AiLevelArrangeRandom = data.AiLevelArrangeRandomly.HasValue ? (data.AiLevelArrangeRandomly.Value ? 1d : 0d) :
                        data.AiLevelArrangeRandom ?? 0.1d;
                AiLevelArrangeReverse = data.AiLevelArrangeReverse ?? false;
                AiLevel = data.AiLevel ?? 95;
                AiLevelMin = data.AiLevelMin ?? 85;

                FilterValue = data.FilterValue;
                ErrorMessage = null;

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

                            var aiLevel = data.AiLevels?.ElementAtOrDefault(i);
                            var carSkinId = data.SkinIds?.ElementAtOrDefault(i);
                            return new RaceGridEntry(x) {
                                CandidatePriority = data.CandidatePriorities?.ElementAtOr(i, 1) ?? 1,
                                AiLevel = aiLevel >= 0 ? aiLevel : (int?)null,
                                Name = data.Names?.ElementAtOrDefault(i),
                                Nationality = data.Nationalities?.ElementAtOrDefault(i),
                                CarSkin = carSkinId != null ? x.GetSkinById(carSkinId) : null,
                            };
                        }).NonNull());
                    } else {
                        NonfilteredList.Clear();
                    }

                    SetOpponentsNumberInternal(data.OpponentsNumber ?? 7);
                } else {
                    NonfilteredList.ReplaceEverythingBy(data.CarIds?.Select(x => CarsManager.Instance.GetById(x)).Select((x, i) => {
                        if (x == null) return null;

                        var aiLevel = data.AiLevels?.ElementAtOrDefault(i);
                        var carSkinId = data.SkinIds?.ElementAtOrDefault(i);

                        return new RaceGridEntry(x) {
                            AiLevel = aiLevel >= 0 ? aiLevel : null,
                            Name = data.Names?.ElementAtOrDefault(i),
                            Nationality = data.Nationalities?.ElementAtOrDefault(i),
                            CarSkin = carSkinId != null ? x.GetSkinById(carSkinId) : null,
                        };
                    }).NonNull() ?? new RaceGridEntry[0]);
                }
                
                StartingPosition = data.StartingPosition ?? 7;
                FinishLoading();
            }, Reset);

            _presetsHelper = new PresetsMenuHelper();

            _randomGroup = new HierarchicalGroup(ToolsStrings.RaceGrid_Random);
            UpdateRandomModes();

            Modes = new HierarchicalGroup {
                BuiltInGridMode.SameCar,
                _randomGroup,
                BuiltInGridMode.Custom,
                _presetsHelper.Create(PresetableKeyValue, p => {
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

                if (Mode != BuiltInGridMode.CandidatesManual && Mode != BuiltInGridMode.SameCar) {
                    RebuildGridAsync().Forget();
                }
            } else {
                SetOpponentsNumberInternal(NonfilteredList.Count);
                UpdateOpponentsNumber();
            }
            
            UpdatePlayerEntry();
        }

        public void Reset() {
            ShuffleCandidates = true;
            AiLevelFixed = false;
            AiLevelArrangeRandom = 0.1;
            AiLevelArrangeReverse = false;
            AiLevel = 95;
            AiLevelMin = 85;

            FilterValue = "";
            ErrorMessage = null;
            Mode = BuiltInGridMode.SameCar;
            SetOpponentsNumberInternal(7);
            StartingPosition = 7;
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
            get { return _mode; }
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
                                ? CombinePriorities(NonfilteredList.ApartFrom(_playerEntry))
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

        private void UpdatePlayerEntry() {
            if (_playerCar != _playerEntry?.Car) {
                if (_playerEntry != null) {
                    NonfilteredList.Remove(_playerEntry);
                    _playerEntry = null;
                }

                if (Mode != BuiltInGridMode.Custom || IgnoreStartingPosition) return;
                _playerEntry = _playerCar == null ? null : new RaceGridPlayerEntry(_playerCar);
            }

            if (_playerEntry == null) return;
            if (Mode == BuiltInGridMode.Custom) {
                var index = NonfilteredList.IndexOf(_playerEntry);
                var pos = StartingPosition - 1;

                if (index == -1) {
                    if (pos > NonfilteredList.Count) {
                        NonfilteredList.Add(_playerEntry);
                    } else if (pos >= 0) {
                        NonfilteredList.Insert(pos, _playerEntry);
                    }
                } else {
                    if (pos < 0) {
                        NonfilteredList.RemoveAt(index);
                    } else if (pos != index) {
                        NonfilteredList.Move(index, pos);
                    }
                }
            } else if (NonfilteredList.Contains(_playerEntry)) {
                NonfilteredList.Remove(_playerEntry);
                _playerEntry = null;
            }
        }

        private void UpdateOpponentsNumber() {
            if (!Mode.CandidatesMode) {
                SetOpponentsNumberInternal(NonfilteredList.ApartFrom(_playerEntry).Count());
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

                again:
                var mode = Mode;
                var candidates = await FindCandidates();
                if (mode != Mode) goto again;

                // I’ve seen that XKCD comic, but I still think goto is more 
                // suitable than a loop here

                if (candidates == null) return;
                NonfilteredList.ReplaceEverythingBy(candidates);
            } catch (SyntaxErrorException e) {
                ErrorMessage = string.Format(ToolsStrings.Common_SyntaxErrorFormat, e.Message);
                NonfatalError.Notify(ToolsStrings.RaceGrid_CannotUpdate, e);
            } catch (ScriptRuntimeException e) {
                ErrorMessage = e.Message;
            } catch (InformativeException e) when (e.SolutionCommentary == null) {
                ErrorMessage = e.Message;
            } catch (Exception e) {
                ErrorMessage = e.Message;
                NonfatalError.Notify(ToolsStrings.RaceGrid_CannotUpdate, e);
            } finally {
                _rebuildingTask = null;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        private string _errorMessage;

        public string ErrorMessage {
            get { return _errorMessage; }
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
                return CarsManager.Instance.EnabledOnly.Select(x => new RaceGridEntry(x)).ToArray();
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

            var candidatesMode = mode as CandidatesGridMode;
            if (candidatesMode != null) {
                return await Task.Run(() => {
                    var carsEnumerable = (IEnumerable<CarObject>)CarsManager.Instance.EnabledOnly.ToList();

                    if (!string.IsNullOrWhiteSpace(candidatesMode.Filter)) {
                        var filter = StringBasedFilter.Filter.Create(CarObjectTester.Instance, candidatesMode.Filter);
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

        public string FilterValue {
            get { return _filterValue; }
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
                    var filter = StringBasedFilter.Filter.Create(CarObjectTester.Instance, FilterValue);
                    FilteredView.Filter = o => filter.Test(((RaceGridEntry)o).Car);
                }
            }
        }

        public int Compare(object x, object y) {
            return (x as RaceGridEntry)?.Car.CompareTo((y as RaceGridEntry)?.Car) ?? 0;
        }
        #endregion

        #region Car and track
        [CanBeNull]
        private CarObject _playerCar;

        [CanBeNull]
        public CarObject PlayerCar {
            get { return _playerCar; }
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
            get { return _track; }
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
            get { return _trackPitsNumber; }
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
                foreach (var entry in NonfilteredList.ApartFrom(_playerEntry)) {
                    entry.ExceedsLimit = --left < 0;
                }
            }
        }
        #endregion

        #region Simple properties
        private bool _shuffleCandidates;

        public bool ShuffleCandidates {
            get { return _shuffleCandidates; }
            set {
                if (Equals(value, _shuffleCandidates)) return;
                _shuffleCandidates = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        public int AiLevelMinimum => SettingsHolder.Drive.QuickDriveExpandBounds ? 30 : 70;

        public int AiLevelMinimumLimited => Math.Max(AiLevelMinimum, 50);

        private int _aiLevel;

        public int AiLevel {
            get { return _aiLevel; }
            set {
                value = value.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100);
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                SaveLater();

                if (!AiLevelFixed && value >= AiLevelMin) return;
                _aiLevelMin = value;
                OnPropertyChanged(nameof(AiLevelMin));
            }
        }

        private int _aiLevelMin;

        public int AiLevelMin {
            get { return _aiLevelMin; }
            set {
                if (AiLevelFixed) return;

                value = value.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100);
                if (Equals(value, _aiLevelMin)) return;
                _aiLevelMin = value;
                OnPropertyChanged();
                SaveLater();

                if (value > AiLevel) {
                    _aiLevel = value;
                    OnPropertyChanged(nameof(AiLevel));
                }
            }
        }

        private bool _aiLevelFixed;

        public bool AiLevelFixed {
            get { return _aiLevelFixed; }
            set {
                if (Equals(value, _aiLevelFixed)) return;
                _aiLevelFixed = value;
                OnPropertyChanged();
                SaveLater();

                if (value && _aiLevelMin != _aiLevel) {
                    _aiLevelMin = _aiLevel;
                    OnPropertyChanged(nameof(AiLevelMin));
                    OnPropertyChanged(nameof(AiLevelInDriverName));
                }
            }
        }

        private double _aiLevelArrangeRandom;

        public double AiLevelArrangeRandom {
            get { return _aiLevelArrangeRandom; }
            set {
                value = value.Round(0.01);
                if (Equals(value, _aiLevelArrangeRandom)) return;
                _aiLevelArrangeRandom = value;
                OnPropertyChanged();
                SaveLater();

                //if (value) {
                //    AiLevelArrangeReverse = false;
                //}
            }
        }

        private bool _aiLevelArrangeReverse;

        public bool AiLevelArrangeReverse {
            get { return _aiLevelArrangeReverse; }
            set {
                if (Equals(value, _aiLevelArrangeReverse)) return;
                _aiLevelArrangeReverse = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        public bool AiLevelInDriverName {
            get { return !AiLevelFixed && SettingsHolder.Drive.QuickDriveAiLevelInName; }
            set {
                if (Equals(value, SettingsHolder.Drive.QuickDriveAiLevelInName)) return;
                SettingsHolder.Drive.QuickDriveAiLevelInName = value;
                OnPropertyChanged();
            }
        }
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
                StartingPositionLimited = NonfilteredList.IndexOf(_playerEntry) + 1;
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
            get { return _opponentsNumber; }
            set {
                if (!Mode.CandidatesMode) return;
                if (value < 1) value = 1;
                SetOpponentsNumberInternal(value);
            }
        }

        public int OpponentsNumberLimited {
            get { return _opponentsNumber.Clamp(0, OpponentsNumberLimit); }
            set { OpponentsNumber = value.Clamp(1, OpponentsNumberLimit); }
        }

        public int StartingPositionLimit => OpponentsNumberLimited + 1;

        private int _startingPosition;

        public int StartingPosition {
            get { return _startingPosition; }
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
            get { return _startingPosition.Clamp(0, StartingPositionLimit); }
            set { StartingPosition = value.Clamp(0, StartingPositionLimit); }
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

            var skins = new Dictionary<string, GoodShuffle<CarSkinObject>>();
            foreach (var car in FilteredView.OfType<RaceGridEntry>().Where(x => x.CarSkin == null).Select(x => x.Car).Distinct()) {
                await car.SkinsManager.EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) return null;

                skins[car.Id] = GoodShuffle.Get(car.EnabledOnlySkins);
            }

            NameNationality[] nameNationalities;
            if (opponentsNumber == 7 && OptionNfsPorscheNames) {
                nameNationalities = new[] {
                    new NameNationality { Name = "Dylan", Nationality = "Wales" },
                    new NameNationality { Name = "Parise", Nationality = "Italy" },
                    new NameNationality { Name = "Steele", Nationality = "United States" },
                    new NameNationality { Name = "Wingnut", Nationality = "England" },
                    new NameNationality { Name = "Leadfoot", Nationality = "Australia" },
                    new NameNationality { Name = "Amazon", Nationality = "United States" },
                    new NameNationality { Name = "Backlash", Nationality = "United States" }
                };
            } else if (DataProvider.Instance.NationalitiesAndNames.Any()) {
                nameNationalities = GoodShuffle.Get(DataProvider.Instance.NationalitiesAndNamesList).Take(opponentsNumber).ToArray();
            } else {
                nameNationalities = null;
            }

            List<int> aiLevels;
            if (AiLevelFixed) {
                aiLevels = null;
            } else {
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
                
                aiLevels = aiLevelsInner.Take(opponentsNumber).ToList();
                Logging.Debug("AI levels: " + aiLevels.Select(x => $@"{x}%").JoinToString(@", "));
            }

            IEnumerable<RaceGridEntry> final;
            if (Mode.CandidatesMode) {
                var list = FilteredView.OfType<RaceGridEntry>().SelectMany(x => new[] { x }.Repeat(x.CandidatePriority)).ToList();

                if (ShuffleCandidates) {
                    var shuffled = GoodShuffle.Get(list);

                    if (_playerCar != null) {
                        var same = list.FirstOrDefault(x => x.Car == _playerCar);
                        if (same != null) {
                            shuffled.IgnoreOnce(same);
                        }
                    }

                    final = shuffled.Take(opponentsNumber);
                } else {
                    var skip = _playerCar;
                    final = LinqExtension.RangeFrom().Select(x => list.RandomElement()).Where(x => {
                        if (x.Car == skip) {
                            skip = null;
                            return false;
                        }

                        return true;
                    }).Take(opponentsNumber);
                }
            } else {
                final = NonfilteredList.Where(x => !x.SpecialEntry);
            }

            if (_playerCar != null) {
                skins.GetValueOrDefault(_playerCar.Id)?.IgnoreOnce(_playerCar.SelectedSkin);
            }

            var takenNames = new List<string>(opponentsNumber);

            return final.Take(opponentsNumber).Select((entry, i) => {
                var level = entry.AiLevel ?? aiLevels?[i] ?? 100;

                var skin = entry.CarSkin;
                if (skin == null) {
                    skin = skins.GetValueOrDefault(entry.Car.Id)?.Next;
                }

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

                var nationality = entry.Nationality ?? nameNationalities?[i].Nationality ?? @"Italy";
                var skinId = skin?.Id;

                return new Game.AiCar {
                    AiLevel = level,
                    CarId = entry.Car.Id,
                    DriverName = AiLevelInDriverName ? $@"{name} ({level}%)" : name,
                    Nationality = nationality,
                    Setup = "",
                    SkinId = skinId
                };
            }).ToList();
        }
        #endregion
    }
}