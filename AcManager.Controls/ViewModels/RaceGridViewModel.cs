using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    public class RaceGridViewModel : NotifyPropertyChanged, IDisposable, IComparer {
        public RaceGridViewModel() {
            _randomGroup = new HierarchicalGroup("Random");
            _presetsGroup = new HierarchicalGroup("Presets");
            UpdateRandomModes();

            Modes = new BetterObservableCollection<object> {
                BuiltInGridMode.SameCar,
                _randomGroup,
                BuiltInGridMode.Custom,
                _presetsGroup,
            };

            NonfilteredList.CollectionChanged += OnCollectionChanged;
            FilteredView = new BetterListCollectionView(NonfilteredList) { CustomSort = this };
            
            Mode = BuiltInGridMode.SameCar;
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update += OnGridTypesUpdate;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (e.OldItems != null) {
                foreach (RaceGridEntry item in e.OldItems) {
                    item.PropertyChanged -= Entry_PropertyChanged;
                    item.Deleted -= Entry_Deleted;
                }
            }

            if (e.NewItems != null) {
                foreach (RaceGridEntry item in e.NewItems) {
                    item.PropertyChanged += Entry_PropertyChanged;
                    item.Deleted += Entry_Deleted;
                }
            }
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(RaceGridEntry.CandidatePriority):
                    if (Mode == BuiltInGridMode.Custom) {
                        ((RaceGridEntry)sender).CandidatePriority = 1;
                    } else {
                        Mode = BuiltInGridMode.Manual;
                    }
                    break;
            }
        }

        private void Entry_Deleted(object sender, EventArgs e) {
            DeleteEntry((RaceGridEntry)sender);
        }

        public void Dispose() {
            Logging.Debug("Dispose()");
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update -= OnGridTypesUpdate;
        }

        private void OnGridTypesUpdate(object sender, EventArgs e) {
            UpdateRandomModes();
        }

        private IRaceGridMode _mode;

        [NotNull]
        public IRaceGridMode Mode {
            get { return _mode; }
            set {
                if (Equals(value, _mode)) return;
                _mode = value;
                OnPropertyChanged();

                if (value == BuiltInGridMode.Custom) {
                    NonfilteredList.ReplaceEverythingBy(FlattenPriorities(NonfilteredList).Sort(Compare));
                    FilteredView.CustomSort = null;
                } else {
                    RebuildGridAsync().Forget();
                    FilteredView.CustomSort = this;
                }

                UpdateViewFilter();
                UpdatePlayerEntry();
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

                if (Mode == BuiltInGridMode.Custom) return;
                _playerEntry = _playerCar == null ? null : new RaceGridPlayerEntry(_playerCar);
            }

            if (Mode == BuiltInGridMode.Custom) {
                var index = NonfilteredList.IndexOf(_playerEntry);
                var pos = StartingPosition - 1;
                if (index == -1) {
                    if (pos >= 0) {
                        NonfilteredList.Insert(pos, _playerEntry);
                    }
                } else {
                    if (pos < 0) {
                        NonfilteredList.RemoveAt(index);
                    } else if (pos != index) {
                        NonfilteredList.Move(index, pos);
                    }
                }
            } else if (_playerEntry != null && NonfilteredList.Contains(_playerEntry)) {
                NonfilteredList.Remove(_playerEntry);
                _playerEntry = null;
            }
        }

        private void UpdateOpponentsNumber() {
            if (Mode == BuiltInGridMode.Custom) {
                NonfilteredList.ReplaceEverythingBy(FlattenPriorities(NonfilteredList));
                OpponentsNumber = FilteredView.Count;
            }
        }

        private IEnumerable<RaceGridEntry> FlattenPriorities(IEnumerable<RaceGridEntry> candidatesList) {
            foreach (var entry in candidatesList) {
                var p = entry.CandidatePriority;
                entry.CandidatePriority = 1;
                for (var i = 0; i < p; i++) {
                    yield return entry;
                }
            }
        }

        private readonly HierarchicalGroup _randomGroup;
        private HierarchicalGroup _presetsGroup;

        public BetterObservableCollection<object> Modes { get; }

        public class ModeMenuItem : MenuItem, ICommand {
            private readonly RaceGridViewModel _viewModel;
            private readonly BuiltInGridMode _mode;

            public ModeMenuItem(RaceGridViewModel viewModel, BuiltInGridMode mode) {
                _viewModel = viewModel;
                _mode = mode;
                Header = _mode.DisplayName;
                Command = this;
            }

            public bool CanExecute(object parameter) {
                return true;
            }

            public void Execute(object parameter) {
                _viewModel.SetModeCommand.Execute(_mode);
            }

            public event EventHandler CanExecuteChanged {
                add { }
                remove { }
            }
        }

        private ICommand _switchModeCommand;

        public ICommand SetModeCommand => _switchModeCommand ?? (_switchModeCommand = new RelayCommand(o => {
            var mode = o as BuiltInGridMode;
            if (mode != null) {
                Mode = mode;
            }
        }, o => o is BuiltInGridMode));

        private void UpdateRandomModes() {
            Logging.Debug("UpdateRandomModes()");

            var items = new List<object> {
                BuiltInGridMode.SameGroup,
                BuiltInGridMode.Filtered
            };

            var dataAdded = false;
            foreach (var entry in FilesStorage.Instance.GetContentDirectory(ContentCategory.GridTypes)) {
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
            }

            _randomGroup.ReplaceEverythingBy(items);
        }

        private bool _isBusy;

        public bool IsBusy {
            get { return _isBusy; }
            set {
                if (Equals(value, _isBusy)) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        private async Task RebuildGridAsync() {
            if (_isBusy) {
                Logging.Warning("RebuildGridAsync(): busy");
                return;
            }

            Logging.Debug("RebuildGridAsync(): start");
            try {
                _isBusy = true;
                var mode = Mode;
                await Task.Delay(50);

                OnPropertyChanged(nameof(IsBusy));

                var candidates = await FindCandidates();
                if (candidates == null || mode != Mode) return;

                NonfilteredList.ReplaceEverythingBy(candidates);
                Logging.Debug("RebuildGridAsync(): list updated");
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update race grid", e);
            } finally {
                IsBusy = false;
                Logging.Debug("RebuildGridAsync(): finished");
            }
        }

        [ItemCanBeNull]
        private async Task<IReadOnlyList<RaceGridEntry>> FindCandidates(CancellationToken cancellation = default(CancellationToken)) {
            var mode = Mode;

            // Don’t change anything in Fixed or Manual mode
            if (mode == BuiltInGridMode.Custom || mode == BuiltInGridMode.Manual) {
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
            if (mode == BuiltInGridMode.Filtered) {
                return CarsManager.Instance.EnabledOnly.Select(x => new RaceGridEntry(x)).ToArray();
            }

            // Same group mode
            if (mode == BuiltInGridMode.SameGroup) {
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
                        if (state == null) throw new Exception("Can’t initialize Lua");

                        if (mode.AffectedByCar) {
                            state.Globals[@"selected"] = _playerCar;
                        }

                        if (mode.AffectedByTrack) {
                            state.Globals[@"track"] = _track;
                        }

                        var result = state.DoString(PrepareScript(candidatesMode.Script)); // TODO: errors handling
                        if (result.Type == DataType.Boolean && !result.Boolean) return new RaceGridEntry[0];

                        var fn = result.Function;
                        if (fn == null) throw new InformativeException("AppStrings.Drive_InvalidScript", "Script should return filtering function");

                        carsEnumerable = carsEnumerable.Where(x => fn.Call(x).Boolean);
                    }

                    return carsEnumerable.Select(x => new RaceGridEntry(x)).ToArray();
                }, cancellation);
            }

            Logging.Error($"[RaceGridViewModel] Not supported mode: {mode.Id} ({mode.GetType().Name})");
            return new RaceGridEntry[0];
        }

        private static string PrepareScript(string script) {
            script = script.Trim();
            return script.Contains('\n') ? script : $"return function(tested)\nreturn {script}\nend";
        }

        public BetterObservableCollection<RaceGridEntry> NonfilteredList { get; } = new BetterObservableCollection<RaceGridEntry>();

        public BetterListCollectionView FilteredView { get; }

        private string _filterValue;

        public string FilterValue {
            get { return _filterValue; }
            set {
                value = value?.Trim();
                if (Equals(value, _filterValue)) return;
                _filterValue = value;
                OnPropertyChanged();
                UpdateViewFilter();
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

            UpdateOpponentsNumber();
        }

        [CanBeNull]
        private CarObject _playerCar;

        public void SetPlayerCar(CarObject car) {
            _playerCar = car;
            if (Mode.AffectedByCar) {
                RebuildGridAsync().Forget();
            }
        }

        [CanBeNull]
        private TrackObjectBase _track;

        public void SetTrack(TrackObjectBase track) {
            if (_track != null) {
                _track.PropertyChanged -= Track_OnPropertyChanged;
            }

            _track = track;
            if (Mode.AffectedByTrack) {
                RebuildGridAsync().Forget();
            }

            if (track != null) {
                TrackPitsNumber = FlexibleParser.ParseInt(track.SpecsPitboxes, 2);
                track.PropertyChanged += Track_OnPropertyChanged;
            }
        }

        private void Track_OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_track != null && e.PropertyName == nameof(TrackObjectBase.SpecsPitboxes)) {
                TrackPitsNumber = FlexibleParser.ParseInt(_track.SpecsPitboxes, 2);
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

                if (value && _aiLevelMin != _aiLevel) {
                    _aiLevelMin = _aiLevel;
                    OnPropertyChanged(nameof(AiLevelMin));
                    OnPropertyChanged(nameof(AiLevelInDriverName));
                }
            }
        }

        private bool _aiLevelArrangeRandomly;

        public bool AiLevelArrangeRandomly {
            get { return _aiLevelArrangeRandomly; }
            set {
                if (Equals(value, _aiLevelArrangeRandomly)) return;
                _aiLevelArrangeRandomly = value;
                OnPropertyChanged();

                if (value) {
                    AiLevelArrangeReverse = false;
                }
            }
        }

        private bool _aiLevelArrangeReverse;

        public bool AiLevelArrangeReverse {
            get { return _aiLevelArrangeReverse; }
            set {
                if (Equals(value, _aiLevelArrangeReverse)) return;
                _aiLevelArrangeReverse = value;
                OnPropertyChanged();

                if (value) {
                    AiLevelArrangeRandomly = false;
                }
            }
        }

        private const string KeyAiLevelInDriverName = "QuickDrive_GridTest.AiLevelInDriverName";

        private bool _aiLevelInDriverName = ValuesStorage.GetBool(KeyAiLevelInDriverName);

        public bool AiLevelInDriverName {
            get { return _aiLevelInDriverName && !AiLevelFixed; }
            set {
                if (Equals(value, _aiLevelInDriverName)) return;
                _aiLevelInDriverName = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyAiLevelInDriverName, value);
            }
        }

        public int Compare(object x, object y) {
            return (x as RaceGridEntry)?.Car.CompareTo((y as RaceGridEntry)?.Car) ?? 0;
        }

        public void AddEntry(CarObject car) {
            AddEntry(new RaceGridEntry(car));
        }

        public void AddEntry(RaceGridEntry entry) {
            InsertEntry(-1, entry);
        }

        public void InsertEntry(int index, CarObject car) {
            InsertEntry(index, new RaceGridEntry(car));
        }

        public void InsertEntry(int index, RaceGridEntry entry) {
            if (Mode != BuiltInGridMode.Custom) {
                Mode = BuiltInGridMode.Manual;
            }

            var c = NonfilteredList.Count;
            if (index < 0 || index > c) {
                index = c;
            }

            var o = NonfilteredList.IndexOf(entry);
            if (o == index) return;

            if (o == -1) {
                if (index == c) {
                    NonfilteredList.Insert(index, entry);
                } else {
                    NonfilteredList.Add(entry);
                }

                UpdateOpponentsNumber();
            } else if (index < c){
                NonfilteredList.Move(o, index);
            }

            if (StartingPosition != 0) {
                StartingPosition = NonfilteredList.IndexOf(_playerEntry) + 1;
            }
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
                Mode = BuiltInGridMode.Manual;
            }
        }

        private int _opponentsNumber;

        public int OpponentsNumber {
            get { return _opponentsNumber; }
            set {
                if (Mode == BuiltInGridMode.Custom) {
                    value = FilteredView.Count;
                }

                if (value < 1) value = 1;
                if (Equals(value, _opponentsNumber)) return;

                _opponentsNumber = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(StartingPositionLimit));
            }
        }

        public int StartingPositionLimit => OpponentsNumber + 1;

        private int _trackPitsNumber;

        public int TrackPitsNumber {
            get { return _trackPitsNumber; }
            set {
                if (Equals(value, _trackPitsNumber)) return;
                _trackPitsNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpponentsNumberLimit));
            }
        }

        public int OpponentsNumberLimit => TrackPitsNumber - 1;
        
        private int _startingPosition;

        public int StartingPosition {
            get { return _startingPosition; }
            set {
                value = value.Clamp(0, StartingPositionLimit);
                if (Equals(value, _startingPosition)) return;

                _startingPosition = value;

                OnPropertyChanged();
                UpdatePlayerEntry();
            }
        }
    }
}