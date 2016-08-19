using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class RaceGridPlayerEntry : RaceGridEntry {
        public override string DisplayName => "You";

        public RaceGridPlayerEntry([NotNull] CarObject car) : base(car) {}
    }

    public class RaceGridEntry : Displayable {
        public override string DisplayName => Car.DisplayName;

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
            _car = car;
            _aiLevel = null;
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(o => {
            (o as RaceGridViewModel)?.DeleteOpponent(this);
        }));

        public override string ToString() {
            return Car.DisplayName;
        }
    }
}