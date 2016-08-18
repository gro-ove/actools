using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
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

namespace AcManager.Controls.ViewModels {
    public class RaceGridViewModel : NotifyPropertyChanged, IDisposable, IComparer {
        public RaceGridViewModel() {
            _randomGroup = new HierarchicalGroup("Random");
            _presetsGroup = new HierarchicalGroup("Presets");
            UpdateRandomModes();

            Modes = new BetterObservableCollection<object> {
                RaceGridMode.SameCar,
                _randomGroup,
                RaceGridMode.Custom,
                _presetsGroup,
            };

            NonfilteredList.CollectionChanged += OnCollectionChanged;
            FilteredView = new BetterListCollectionView(NonfilteredList) { CustomSort = this };
            
            Mode = RaceGridMode.SameCar;
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update += OnGridTypesUpdate;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (e.OldItems != null) {
                foreach (RaceGridEntry item in e.OldItems) {
                    item.PropertyChanged -= OpponentEntry_PropertyChanged;
                }
            }

            if (e.NewItems != null) {
                foreach (RaceGridEntry item in e.NewItems) {
                    item.PropertyChanged += OpponentEntry_PropertyChanged;
                }
            }
        }

        private void OpponentEntry_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(RaceGridEntry.CandidatePriority):
                    if (Mode != RaceGridMode.Custom) {
                        Mode = RaceGridMode.Manual;
                    }
                    break;
            }
        }

        public void Delete(RaceGridEntry entry) {
            NonfilteredList.Remove(entry);
            if (Mode != RaceGridMode.Custom) {
                Mode = RaceGridMode.Manual;
            }
        }

        public void Dispose() {
            Logging.Debug("Dispose()");
            FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update -= OnGridTypesUpdate;
        }

        private void OnGridTypesUpdate(object sender, EventArgs e) {
            UpdateRandomModes();
        }

        private RaceGridMode _mode;

        [NotNull]
        public RaceGridMode Mode {
            get { return _mode; }
            set {
                if (Equals(value, _mode)) return;
                _mode = value;
                OnPropertyChanged();
                SetViewFilter();
                RebuildGridAsync().Forget();
            }
        }

        private HierarchicalGroup _randomGroup, _presetsGroup;

        public BetterObservableCollection<object> Modes { get; }

        public class ModeMenuItem : MenuItem, ICommand {
            private readonly RaceGridViewModel _viewModel;
            private readonly RaceGridMode _mode;

            public ModeMenuItem(RaceGridViewModel viewModel, RaceGridMode mode) {
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

            public event EventHandler CanExecuteChanged;
        }

        private ICommand _switchModeCommand;

        public ICommand SetModeCommand => _switchModeCommand ?? (_switchModeCommand = new RelayCommand(o => {
            var mode = o as RaceGridMode;
            if (mode != null) {
                Mode = mode;
            }
        }, o => o is RaceGridMode));

        private void UpdateRandomModes() {
            Logging.Debug("UpdateRandomModes()");

            _randomGroup.ReplaceEverythingBy(new object[] {
                RaceGridMode.SameGroup,
                RaceGridMode.Filtered,
                RaceGridMode.Manual,
                new Separator()
            });
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
                await Task.Delay(50);

                OnPropertyChanged(nameof(IsBusy));

                var candidates = await FindCandidates();
                if (candidates == null) return;

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
            if (mode == RaceGridMode.Custom || mode == RaceGridMode.Manual) {
                return null;
            }
            
            // Basic mode, just one car
            if (mode == RaceGridMode.SameCar) {
                return _playerCar == null ? new RaceGridEntry[0] : new[] { new RaceGridEntry(_playerCar) };
            }

            // Other modes require cars list to be loaded
            if (!CarsManager.Instance.IsLoaded) {
                await CarsManager.Instance.EnsureLoadedAsync();
            }

            // Another simple mode
            if (mode == RaceGridMode.Filtered) {
                return CarsManager.Instance.EnabledOnly.Select(x => new RaceGridEntry(x)).ToArray();
            }

            // Same group mode
            if (mode == RaceGridMode.SameGroup) {
                if (_playerCar == null) return new RaceGridEntry[0];

                var parent = _playerCar.Parent ?? _playerCar;
                return parent.Children.Prepend(parent).Where(x => x.Enabled).Select(x => new RaceGridEntry(x)).ToArray();
            }

            // Entry from a JSON-file
            if (mode.AffectedByCar && _playerCar == null || mode.AffectedByTrack && _track == null) {
                return new RaceGridEntry[0];
            }

            return await Task.Run(() => {
                var carsEnumerable = (IEnumerable<CarObject>)CarsManager.Instance.EnabledOnly.ToList();

                if (!string.IsNullOrWhiteSpace(mode.Filter)) {
                    var filter = StringBasedFilter.Filter.Create(CarObjectTester.Instance, mode.Filter);
                    carsEnumerable = carsEnumerable.Where(filter.Test);
                }

                if (!string.IsNullOrWhiteSpace(mode.Script)) {
                    var state = LuaHelper.GetExtended();
                    if (state == null) throw new Exception("Can’t initialize Lua");

                    if (mode.AffectedByCar) {
                        state.Globals[@"selected"] = _playerCar;
                    }

                    if (mode.AffectedByTrack) {
                        state.Globals[@"track"] = _track;
                    }

                    var result = state.DoString(PrepareScript(mode.Script)); // TODO: errors handling
                    if (result.Type == DataType.Boolean && !result.Boolean) return new RaceGridEntry[0];

                    var fn = result.Function;
                    if (fn == null) throw new InformativeException("AppStrings.Drive_InvalidScript", "Script should return filtering function");

                    carsEnumerable = carsEnumerable.Where(x => fn.Call(x).Boolean);
                }

                return carsEnumerable.Select(x => new RaceGridEntry(x)).ToArray();
            }, cancellation);
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
                SetViewFilter();
            }
        }

        private void SetViewFilter() {
            using (FilteredView.DeferRefresh()) {
                if (string.IsNullOrEmpty(FilterValue) || Mode == RaceGridMode.SameCar) {
                    FilteredView.Filter = null;
                } else {
                    var filter = StringBasedFilter.Filter.Create(CarObjectTester.Instance, FilterValue);
                    FilteredView.Filter = o => filter.Test(((RaceGridEntry)o).Car);
                }
            }
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
            _track = track;
            if (Mode.AffectedByTrack) {
                RebuildGridAsync().Forget();
            }
        }

        private int _opponentsNumber;

        public int OpponentsNumber {
            get { return _opponentsNumber; }
            set {
                if (Equals(value, _opponentsNumber)) return;
                _opponentsNumber = value;
                OnPropertyChanged();
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

        int IComparer.Compare(object x, object y) {
            return (x as RaceGridEntry)?.Car.CompareTo((y as RaceGridEntry)?.Car) ?? 0;
        }
    }
}