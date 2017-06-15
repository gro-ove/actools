using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class RaceGridPlayerEntry : RaceGridEntry {
        public override bool SpecialEntry => true;

        public override string DisplayName => ToolsStrings.RaceGrid_You;

        internal RaceGridPlayerEntry([NotNull] CarObject car) : base(car) {}
    }

    public class RaceGridEntry : Displayable, IDraggable, IDraggableCloneable {
        public virtual bool SpecialEntry => false;

        public override string DisplayName => Car.DisplayName;

        private bool _exceedsLimit;

        public bool ExceedsLimit {
            get { return _exceedsLimit; }
            set {
                if (Equals(value, _exceedsLimit)) return;
                _exceedsLimit = value;
                OnPropertyChanged();
            }
        }

        public bool CanBeCloned => !SpecialEntry;

        object IDraggableCloneable.Clone() {
            return Clone();
        }

        private CarObject _car;

        [NotNull]
        public CarObject Car {
            get { return _car; }
            set {
                if (Equals(value, _car)) return;
                _car = value;
                OnPropertyChanged();

                if (CarSkin != null) {
                    CarSkin = value.GetFirstSkinOrNull();
                }
            }
        }

        private CarSkinObject _carSkin;

        [CanBeNull]
        public CarSkinObject CarSkin {
            get { return _carSkin; }
            set {
                if (Equals(value, _carSkin)) return;
                _carSkin = value;
                OnPropertyChanged();
            }
        }

        private ICommand _randomSkinCommand;

        public ICommand RandomSkinCommand => _randomSkinCommand ?? (_randomSkinCommand = new DelegateCommand(() => {
            CarSkin = null;
        }));

        private DelegateCommand _skinDialogCommand;

        public DelegateCommand SkinDialogCommand => _skinDialogCommand ?? (_skinDialogCommand = new DelegateCommand(() => {
            var control = new CarBlock {
                Car = Car,
                SelectedSkin = CarSkin ?? Car.SelectedSkin,
                SelectSkin = true,
                OpenShowroom = true
            };

            var dialog = new ModernDialog {
                Content = control,
                Width = 640,
                Height = 720,
                MaxWidth = 640,
                MaxHeight = 720,
                SizeToContent = SizeToContent.Manual,
                Title = Car.DisplayName
            };

            dialog.Buttons = new[] { dialog.OkButton, dialog.CancelButton };
            dialog.ShowDialog();
            if (!dialog.IsResultOk) return;

            if (SpecialEntry) {
                Car.SelectedSkin = control.SelectedSkin;
            } else {
                CarSkin = control.SelectedSkin;
            }
        }));

        private string _name;

        [CanBeNull]
        public string Name {
            get { return _name; }
            set {
                if (value != null) {
                    value = value.Trim();
                    if (value.Length == 0) value = null;
                }

                if (Equals(value, _name)) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _nationality;

        [CanBeNull]
        public string Nationality {
            get { return _nationality; }
            set {
                if (value != null) {
                    value = value.Trim();
                    if (value.Length == 0) value = null;
                }

                if (Equals(value, _nationality)) return;
                _nationality = value;
                OnPropertyChanged();
            }
        }

        private double? _aiLevel;

        public double? AiLevel {
            get { return _aiLevel; }
            set {
                value = value?.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100d);
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputAiLevel));
            }
        }

        public string InputAiLevel {
            get { return _aiLevel?.ToString(CultureInfo.CurrentUICulture); }
            set { AiLevel = FlexibleParser.TryParseDouble(value); }
        }

        private double? _aiAggression;

        public double? AiAggression {
            get { return _aiAggression; }
            set {
                value = value?.Clamp(0, 100d);
                if (Equals(value, _aiAggression)) return;
                _aiAggression = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputAiAggression));
            }
        }

        public string InputAiAggression {
            get { return _aiAggression?.ToString(CultureInfo.CurrentUICulture); }
            set { AiAggression = FlexibleParser.TryParseDouble(value); }
        }

        private double _ballast;

        public double Ballast {
            get { return _ballast; }
            set {
                if (Equals(value, _ballast)) return;
                _ballast = value;
                OnPropertyChanged();
            }
        }

        private double _restrictor;

        public double Restrictor {
            get { return _restrictor; }
            set {
                if (Equals(value, _restrictor)) return;
                _restrictor = value;
                OnPropertyChanged();
            }
        }

        private int _candidatePriority = 1;

        public int CandidatePriority {
            get { return _candidatePriority; }
            set {
                value = value.Clamp(1, 100);
                if (Equals(value, _candidatePriority)) return;
                _candidatePriority = value;
                OnPropertyChanged();
            }
        }

        public RaceGridEntry([NotNull] CarObject car) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            _car = car;
            _aiLevel = null;
        }

        private bool _isDeleted;

        public bool IsDeleted {
            get { return _isDeleted; }
            set {
                if (Equals(value, _isDeleted)) return;
                _isDeleted = value;
                OnPropertyChanged();
            }
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            IsDeleted = true;
        }));

        public override string ToString() {
            return DisplayName;
        }

        public const string DraggableFormat = "Data-RaceGridEntry";

        string IDraggable.DraggableFormat => DraggableFormat;

        public RaceGridEntry Clone() {
            return new RaceGridEntry(Car) {
                CarSkin = CarSkin,
                AiLevel = AiLevel,
                AiAggression = AiAggression,
                Ballast = Ballast,
                Restrictor = Restrictor,
                CandidatePriority = CandidatePriority,
                Name = Name,
                Nationality = Nationality
            };
        }

        public bool Same(RaceGridEntry other) {
            return GetType().Name == other.GetType().Name && Car == other.Car &&
                    CarSkin == other.CarSkin && Name == other.Name && Nationality == other.Nationality && AiLevel == other.AiLevel;
        }
    }
}