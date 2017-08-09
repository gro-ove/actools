using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Controls.ViewModels {
    public class AiLimitationDetails : NotifyPropertyChanged {
        [NotNull]
        private readonly CarObject _car;

        private bool _isEnabled;

        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
                UpdateIsActive();
            }
        }

        private bool _isAnySet;

        public bool IsAnySet {
            get => _isAnySet;
            private set {
                if (Equals(value, _isAnySet)) return;
                _isAnySet = value;
                OnPropertyChanged();
            }
        }

        private bool _isActive;

        public bool IsActive {
            get => _isActive;
            private set {
                if (Equals(value, _isActive)) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        private double _tyresWearMultiplier = 1d;

        public double TyresWearMultiplier {
            get => _tyresWearMultiplier;
            set {
                if (Equals(value, _tyresWearMultiplier)) return;
                _tyresWearMultiplier = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
                UpdateIsActive();
            }
        }

        private double _fuelMaxMultiplier = 1d;

        public double FuelMaxMultiplier {
            get => _fuelMaxMultiplier;
            set {
                if (Equals(value, _fuelMaxMultiplier)) return;
                _fuelMaxMultiplier = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
                UpdateIsActive();
            }
        }

        private ChangeableObservableCollection<AiLimitationTyre> _tyres;

        public ChangeableObservableCollection<AiLimitationTyre> Tyres {
            get {
                if (_tyres == null) {
                    _tyres = new ChangeableObservableCollection<AiLimitationTyre>(
                            _car.AcdData?.GetIniFile("tyres.ini").GetSections("FRONT", -1).Select(x => new AiLimitationTyre(x)).ToList());
                    _tyres.ItemPropertyChanged += OnTyrePropertyChanged;
                }
                return _tyres;
            }
        }

        public event EventHandler Changed;

        private void UpdateIsActive() {
            IsAnySet = Tyres.Any(x => !x.IsAllowed) || TyresWearMultiplier != 1d || FuelMaxMultiplier != 1d;
            IsActive = IsEnabled && IsAnySet && SettingsHolder.Drive.QuickDriveAiLimitations;
        }

        private void OnTyrePropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AiLimitationTyre.IsAllowed)) {
                UpdateIsActive();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private DelegateCommand _showMenuCommand;

        public DelegateCommand ShowMenuCommand => _showMenuCommand ?? (_showMenuCommand = new DelegateCommand(() => {
            var tyres = new MenuItem {
                Header = "Allowed Tyres",
                StaysOpenOnClick = true
            };

            foreach (var tyre in Tyres) {
                var item = new MenuItem {
                    Header = tyre.DisplayName,
                    StaysOpenOnClick = true,
                    IsCheckable = true
                };

                item.SetBinding(MenuItem.IsCheckedProperty, new Binding {
                    Path = new PropertyPath(nameof(tyre.IsAllowed)),
                    Source = tyre
                });

                tyres.Items.Add(item);
            }

            new ContextMenu()
                    .AddItem(tyres)
                    .IsOpen = true;
        }));

        public AiLimitationDetails([NotNull] CarObject car) {
            _car = car ?? throw new ArgumentNullException(nameof(car));
        }

        public string GetChecksum() {
            var result = TyresWearMultiplier.GetHashCode();

            if (FuelMaxMultiplier != 1d) {
                result = (result * 397) ^ FuelMaxMultiplier.GetHashCode();
            }

            foreach (var tyre in Tyres) {
                if (!tyre.IsAllowed) {
                    result = (result * 397) ^ tyre.DisplayName.GetHashCode();
                }
            }

            return BitConverter.GetBytes(result).ToHexString().ToLowerInvariant();
        }

        public string Apply() {
            if (!IsActive || !SettingsHolder.Drive.QuickDriveAiLimitations) return _car.Id;

            var newId = "__cm_tmp_" + _car.Id + "_" + GetChecksum();
            FakeCarsHelper.CreateFakeCar(_car, newId, acd => {
                var data = _car.AcdData;
                if (data != null) {
                    var tyresIni = data.GetIniFile("tyres.ini").Clone();

                    if (Tyres.Any(x => !x.IsAllowed)) {
                        var toRemovalNames = Tyres.Where(x => !x.IsAllowed).Select(x => x.DisplayName).ToList();
                        tyresIni.SetSections("FRONT", -1, tyresIni.GetSections("FRONT", -1).Where(x => !toRemovalNames.Contains(x.GetTyreName())).ToList());
                        tyresIni.SetSections("REAR", -1, tyresIni.GetSections("REAR", -1).Where(x => !toRemovalNames.Contains(x.GetTyreName())).ToList());
                        tyresIni["COMPOUND_DEFAULT"].Set("INDEX", 0);
                        acd.SetEntry("tyres.ini", tyresIni.ToString());
                    }

                    var wear = TyresWearMultiplier.Round(0.01);
                    var multiplier = 1d / Math.Max(wear, 0.00001);

                    if (wear != 1d) {
                        foreach (var lutName in tyresIni.GetSections("FRONT", -1).Concat(
                                tyresIni.GetSections("REAR", -1)).Select(x => x.GetNonEmpty("WEAR_CURVE")).NonNull().Distinct()) {
                            acd.SetEntry(lutName, new LutDataFile(
                                    data.GetLutFile(lutName).Values.ScaleHorizontallyBy(multiplier)).ToString());
                        }
                    }

                    var fuel = FuelMaxMultiplier.Round(0.001);
                    if (fuel != 1d) {
                        var carIni = data.GetIniFile("car.ini").Clone();
                        var fuelSection = carIni["FUEL"];

                        var newValue = fuelSection.GetDouble("MAX_FUEL", 50d) * fuel;
                        fuelSection.Set("MAX_FUEL", newValue);
                        if (fuelSection.GetDouble("FUEL", 30d) > newValue) {
                            fuelSection.Set("FUEL", newValue);
                        }

                        acd.SetEntry("car.ini", carIni.ToString());
                    }
                }
            });

            return newId;
        }

        [CanBeNull]
        public string Save() {
            if (!IsAnySet && !IsEnabled) return null;
            var j = new JObject();

            if (IsEnabled) {
                j["enabled"] = true;
            }

            if (Tyres.Any(x => !x.IsAllowed)) {
                j["disabledTyres"] = new JArray(Tyres.Where(x => !x.IsAllowed).Select(x => x.DisplayName));
            }

            if (TyresWearMultiplier != 1d) {
                j["wearMultiplier"] = TyresWearMultiplier;
            }

            if (FuelMaxMultiplier != 1d) {
                j["fuelMultiplier"] = FuelMaxMultiplier;
            }

            return j.ToString(Formatting.None);
        }

        public void Load(string serialized) {
            try {
                if (!string.IsNullOrWhiteSpace(serialized)) {
                    var j = JObject.Parse(serialized);
                    var tyres = (j["disabledTyres"] as JArray)?.Select(x => x?.ToString()).ToList();
                    IsEnabled = j.GetBoolValueOnly("enabled") ?? false;
                    Tyres.ForEach(x => x.IsAllowed = tyres?.Contains(x.DisplayName) != false);
                    TyresWearMultiplier = j.GetDoubleValueOnly("wearMultiplier") ?? 1d;
                    FuelMaxMultiplier = j.GetDoubleValueOnly("fuelMultiplier") ?? 1d;
                    return;
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            Tyres.ForEach(x => x.IsAllowed = true);
            TyresWearMultiplier = 1d;
            FuelMaxMultiplier = 1d;
        }

        public void CopyFrom(AiLimitationDetails a) {
            Tyres.ForEach(x => x.IsAllowed = a.Tyres.GetByIdOrDefault(x.DisplayName)?.IsAllowed != false);
            TyresWearMultiplier = a.TyresWearMultiplier;
            FuelMaxMultiplier = a.FuelMaxMultiplier;
        }
    }
}