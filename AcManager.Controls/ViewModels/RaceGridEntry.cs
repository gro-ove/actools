using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public class RaceGridEntry : Displayable, IDraggable, IDraggableCloneable {
        public virtual bool SpecialEntry => false;

        public override string DisplayName => Car.DisplayName;

        private bool _exceedsLimit;

        public bool ExceedsLimit {
            get => _exceedsLimit;
            set => Apply(value, ref _exceedsLimit);
        }

        public bool CanBeCloned => !SpecialEntry;

        object IDraggableCloneable.Clone() {
            return Clone();
        }

        private CarObject _car;

        [NotNull]
        public CarObject Car {
            get => _car;
            set {
                if (Equals(value, _car)) return;
                _car = value;
                OnPropertyChanged();

                if (CarSkin != null) {
                    CarSkin = value.GetFirstSkinOrNull();
                }

                AiLimitationDetails = new AiLimitationDetails(value);
            }
        }

        private CarSkinObject _carSkin;

        [CanBeNull]
        public CarSkinObject CarSkin {
            get => _carSkin;
            set => Apply(value, ref _carSkin);
        }

        private ICommand _randomSkinCommand;

        public ICommand RandomSkinCommand => _randomSkinCommand ?? (_randomSkinCommand = new DelegateCommand(() => { CarSkin = null; }));

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
            get => _name;
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
            get => _nationality;
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
            get => _aiLevel;
            set {
                value = value?.Clamp(SettingsHolder.Drive.AiLevelMinimum, 100d);
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputAiLevel));
            }
        }

        public string InputAiLevel {
            get => _aiLevel?.ToString(CultureInfo.CurrentUICulture);
            set => AiLevel = FlexibleParser.TryParseDouble(value);
        }

        private double? _aiAggression;

        public double? AiAggression {
            get => _aiAggression;
            set {
                value = value?.Clamp(0, 100d);
                if (Equals(value, _aiAggression)) return;
                _aiAggression = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputAiAggression));
            }
        }

        public string InputAiAggression {
            get => _aiAggression?.ToString(CultureInfo.CurrentUICulture);
            set => AiAggression = FlexibleParser.TryParseDouble(value);
        }

        private double _ballast;

        public double Ballast {
            get => _ballast;
            set => Apply(value, ref _ballast);
        }

        private double _restrictor;

        public double Restrictor {
            get => _restrictor;
            set => Apply(value, ref _restrictor);
        }

        private AiLimitationDetails _aiLimitationDetails;

        [CanBeNull]
        public AiLimitationDetails AiLimitationDetails {
            get => _aiLimitationDetails;
            private set {
                if (Equals(value, _aiLimitationDetails)) return;

                if (_aiLimitationDetails != null) {
                    _aiLimitationDetails.Changed -= OnAiLimitationDetailsChanged;
                }

                _aiLimitationDetails = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiLimitationDetailsData));

                if (value != null) {
                    value.Changed += OnAiLimitationDetailsChanged;
                }
            }
        }

        private void OnAiLimitationDetailsChanged(object sender, EventArgs eventArgs) {
            OnPropertyChanged(nameof(AiLimitationDetailsData));
        }

        public string AiLimitationDetailsData {
            get => AiLimitationDetails?.Save();
            set => AiLimitationDetails?.Load(value);
        }

        private int _candidatePriority = 1;

        public int CandidatePriority {
            get => _candidatePriority;
            set {
                value = value.Clamp(1, 100);
                if (Equals(value, _candidatePriority)) return;
                _candidatePriority = value;
                OnPropertyChanged();
            }
        }

        public RaceGridEntry([NotNull] CarObject car) {
            _car = car ?? throw new ArgumentNullException(nameof(car));
            _aiLevel = null;
            AiLimitationDetails = new AiLimitationDetails(car);
        }

        private bool _isDeleted;

        public bool IsDeleted {
            get => _isDeleted;
            set => Apply(value, ref _isDeleted);
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => { IsDeleted = true; }));

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
                Nationality = Nationality,
                AiLimitationDetailsData = AiLimitationDetailsData
            };
        }

        public bool Same(RaceGridEntry other) {
            return GetType().Name == other.GetType().Name && Car == other.Car &&
                    CarSkin == other.CarSkin && Name == other.Name && Nationality == other.Nationality && AiLevel == other.AiLevel;
        }

        #region Sequential skins stuff
        private int? _sequentialSkinsMaxNumber;
        private Dictionary<int, GoodShuffle<CarSkinObject>> _sequentialSkins;

        public static void InitializeSequentialSkins([NotNull] IReadOnlyList<RaceGridEntry> entries, [CanBeNull] IFilter<CarSkinObject> filter,
                int ignoreNumber) {
            var availableNumbers = entries.SelectMany(x => x.GetSkins(filter, ignoreNumber).Select(y => y.SkinNumber.As(-1)))
                                          .Where(x => x > 0).OrderBy(x => x).Distinct().ToList();
            var maxNumber = availableNumbers.MaxOrDefault();
            foreach (var entry in entries) {
                entry._sequentialSkinsMaxNumber = maxNumber > 1 ? maxNumber : (int?)null;
                entry._sequentialSkins = entry.GetSkins(filter, ignoreNumber).GroupBy(x => x.SkinNumber.As(-1)).Where(x => x.Key > 0)
                                              .ToDictionary(x => availableNumbers.IndexOf(x.Key), GoodShuffle.Get);
            }
        }

        private IEnumerable<CarSkinObject> GetSkins(IFilter<CarSkinObject> filter, int ignoreNumber) {
            return Car.EnabledOnlySkins.Where(y => (filter == null || filter.Test(y)) && y.SkinNumber.As(-1) != ignoreNumber);
        }

        public bool HasSkinFor(int zeroBasedIndex) {
            return _sequentialSkins?.ContainsKey(GetSkinNumber(zeroBasedIndex)) == true;
        }

        [CanBeNull]
        public CarSkinObject GetSkinFor(int zeroBasedIndex) {
            return _sequentialSkins?.GetValueOrDefault(GetSkinNumber(zeroBasedIndex))?.Next;
        }

        private int GetSkinNumber(int zeroBasedIndex) {
            return _sequentialSkinsMaxNumber > 2 ? zeroBasedIndex % _sequentialSkinsMaxNumber.Value : zeroBasedIndex;
        }
        #endregion
    }
}