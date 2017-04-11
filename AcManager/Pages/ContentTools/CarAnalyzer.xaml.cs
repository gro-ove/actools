using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.ContentRepair;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.ContentRepairUi;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.ContentTools {
    public partial class CarAnalyzer {
        static CarAnalyzer() {
            CarRepair.AddType<CarObsoleteTyresRepair>();
            CarRepair.AddType<CarWeightRepair>();
            CarRepair.AddType<CarTorqueRepair>();
            CarRepair.AddType<CarWronglyTakenSoundRepair>();
        }

        public class BrokenDetails : NotifyPropertyChanged {
            public CarObject Car { get; }

            public ChangeableObservableCollection<ContentRepairSuggestion> Aspects { get; }

            private int _leftUnsolved;

            public int LeftUnsolved {
                get { return _leftUnsolved; }
                set {
                    if (Equals(value, _leftUnsolved)) return;
                    _leftUnsolved = value;
                    OnPropertyChanged();
                }
            }

            public BrokenDetails(CarObject car, IEnumerable<ContentRepairSuggestion> aspects) {
                Car = car;
                Aspects = new ChangeableObservableCollection<ContentRepairSuggestion>(aspects);
                LeftUnsolved = Aspects.Count;

                Aspects.ItemPropertyChanged += OnItemPropertyChanged;
            }

            private void UpdateLeftUnsolved() {
                Aspects.ReplaceIfDifferBy(Aspects.Where(x => !x.IsHidden));
                LeftUnsolved = Aspects.Count(x => !x.IsSolved && !x.IsHidden);
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(ContentRepairSuggestion.IsSolved):
                    case nameof(ContentRepairSuggestion.IsHidden):
                        UpdateLeftUnsolved();
                        break;
                }
            }

            private AsyncCommand _reloadCommand;

            public AsyncCommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new AsyncCommand(async () => {
                try {
                    var list = await Task.Run(() => CarRepair.GetObsoletableAspects(Car, true).ToList());
                    Aspects.ReplaceEverythingBy(list);
                    UpdateLeftUnsolved();
                } catch (Exception e) {
                    NonfatalError.Notify($"Can’t check {Car.DisplayName}", e);
                }
            }));

            private CommandBase _replaceSoundCommand;

            public ICommand ReplaceSoundCommand => _replaceSoundCommand ?? (_replaceSoundCommand = new AsyncCommand(() => {
                var donor = SelectCarDialog.Show();
                return donor == null ? Task.Delay(0) : Car.ReplaceSound(donor);
            }));

            #region Open In Showroom
            private CommandBase _openInShowroomCommand;

            public ICommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand<object>(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.Execute(o);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(Car, Car.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute(null);
                }
            }, o => Car.Enabled && Car.SelectedSkin != null));

            private CommandBase _openInShowroomOptionsCommand;

            public ICommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(Car, Car.SelectedSkin?.Id).ShowDialog();
            }, () => Car.Enabled && Car.SelectedSkin != null));

            private CommandBase _openInCustomShowroomCommand;

            public ICommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(Car, Car.SelectedSkin)));

            private CommandBase _driveCommand;

            public ICommand DriveCommand => _driveCommand ?? (_driveCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(Car, Car.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, () => Car.Enabled));

            private CommandBase _driveOptionsCommand;

            public ICommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(Car, Car.SelectedSkin?.Id);
            }, () => Car.Enabled));
            #endregion
        }

        #region Loading

        private bool _models;
        private string _filter, _id;

        protected override void InitializeOverride(Uri uri) {
            _models = uri.GetQueryParamBool("Models");
            _filter = uri.GetQueryParam("Filter");
            _id = uri.GetQueryParam("Id");

            InitializeComponent();
        }

        [CanBeNull]
        private static BrokenDetails GetDetails(CarObject car, bool models, bool allowEmpty) {
            if (car.AcdData?.IsEmpty != false) return null;
            
            var list = CarRepair.GetObsoletableAspects(car, models).ToList();
            return allowEmpty || list.Count > 0 ? new BrokenDetails(car, list) : null;
        }

        protected override async Task<bool> LoadOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_id != null) {
                var car = CarsManager.Instance.GetById(_id);
                if (car != null) {
                    progress.Report(AsyncProgressEntry.Indetermitate);
                    BrokenCars = new List<BrokenDetails> {
                        await Task.Run(() => GetDetails(car, _models, true))
                    };
                } else {
                    BrokenCars = new List<BrokenDetails>();
                }
            } else {
                var entries = new List<BrokenDetails>();
                var filter = _filter == null ? null : Filter.Create(CarObjectTester.Instance, _filter);

                progress.Report(AsyncProgressEntry.FromStringIndetermitate("Loading cars…"));
                await CarsManager.Instance.EnsureLoadedAsync();

                IEnumerable<CarObject> carsEnumerable = CarsManager.Instance.LoadedOnly.OrderBy(x => x.Name);
                if (filter != null) {
                    carsEnumerable = carsEnumerable.Where(filter.Test);
                }

                var cars = carsEnumerable.ToList();
                for (var i = 0; i < cars.Count; i++) {
                    var car = cars[i];
                    progress.Report(new AsyncProgressEntry(car.Name, i, cars.Count));

                    try {
                        var details = await Task.Run(() => GetDetails(car, _models, false));
                        if (details != null) {
                            entries.Add(details);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify($"Can’t check {car.DisplayName}", e);
                    }
                }

                BrokenCars = entries;
            }

            return BrokenCars.Count > 0;
        }
        #endregion

        #region Entries
        private List<BrokenDetails> _brokenCars;

        public List<BrokenDetails> BrokenCars {
            get { return _brokenCars; }
            set {
                if (Equals(value, _brokenCars)) return;
                _brokenCars = value;
                OnPropertyChanged();

                BrokenCar = value?.FirstOrDefault();
            }
        }

        private BrokenDetails _brokenCar;

        public BrokenDetails BrokenCar {
            get { return _brokenCar; }
            set {
                if (Equals(value, _brokenCar)) return;
                _brokenCar = value;
                OnPropertyChanged();
            }
        }
        #endregion

        private bool _warned;

        private void OnFixButtonClick(object sender, RoutedEventArgs e) {
            var aspect = ((FrameworkElement)sender).DataContext as ContentRepairSuggestionFix;
            if (aspect == null) return;

            if (aspect.AffectsData && !_warned) {
                if (!DataUpdateWarning.Warn(BrokenCar.Car)) return;
                _warned = true;
            }

            aspect.FixCommand.ExecuteAsync().Forget();
        }
    }
}
