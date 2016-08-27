using System;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class RaceGridPlayerEntry : RaceGridEntry {
        public override bool SpecialEntry => true;

        public override string DisplayName => "You";

        public RaceGridPlayerEntry([NotNull] CarObject car) : base(car) {}
    }

    public class RaceGridEntry : Displayable, IDraggable {
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

        private string _name;

        [CanBeNull]
        public string Name {
            get { return _name; }
            set {
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
                if (Equals(value, _nationality)) return;
                _nationality = value;
                OnPropertyChanged();
            }
        }

        private int? _aiLevel;

        public int? AiLevel {
            get { return _aiLevel; }
            set {
                value = value?.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100);
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
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

        public event EventHandler Deleted;

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(o => {
            Deleted?.Invoke(this, EventArgs.Empty);
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
                CandidatePriority = CandidatePriority,
                Name = Name,
                Nationality = Nationality
            };
        }

        public bool Same(RaceGridEntry other) {
            // TODO: If later candidates will become customizable, check more
            return GetType().Name == other.GetType().Name && Car == other.Car && 
                    CarSkin == other.CarSkin;
        }
    }
}