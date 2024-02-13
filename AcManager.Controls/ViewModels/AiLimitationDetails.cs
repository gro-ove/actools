using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Processes;
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
            private set => Apply(value, ref _isAnySet);
        }

        private bool _isActive;

        public bool IsActive {
            get => _isActive;
            private set => Apply(value, ref _isActive);
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

        public AiLimitationDetails([NotNull] CarObject car) {
            _car = car ?? throw new ArgumentNullException(nameof(car));

            _finalGearRatio = Lazier.Create(() => {
                var data = _car.AcdData;
                if (data == null) return null;

                var entry = new CarSetupEntry("FINAL_GEAR_RATIO", null, data);
                if (!(entry.Values?.Count > 1)) return null;

                entry.Value = entry.DefaultValue;
                entry.PropertyChanged += OnGearRatioPropertyChanged;
                CarSetupEntry.UpdateRatioMaxSpeed(new[]{ entry }, data, null, false);
                return entry;
            });

            _tyres = Lazier.Create(() => {
                var result = new ChangeableObservableCollection<AiLimitationTyre>(
                        _car.AcdData?.GetIniFile("tyres.ini").GetSections("FRONT", -1).Select(x => new AiLimitationTyre(x)).ToList());
                result.ItemPropertyChanged += OnTyrePropertyChanged;
                return result;
            });
        }

        private readonly Lazier<CarSetupEntry> _finalGearRatio;

        [CanBeNull]
        public CarSetupEntry FinalGearRatio => _finalGearRatio.Value;

        private readonly Lazier<ChangeableObservableCollection<AiLimitationTyre>> _tyres;

        [NotNull]
        public ChangeableObservableCollection<AiLimitationTyre> Tyres => _tyres.RequireValue;

        public event EventHandler Changed;

        private void UpdateIsActive() {
            IsAnySet = TyresWearMultiplier != 1d || FuelMaxMultiplier != 1d
                    || _tyres.IsSet && Tyres.Any(x => !x.IsAllowed)
                    || _finalGearRatio.IsSet && FinalGearRatio?.Value != FinalGearRatio?.DefaultValue;
            IsActive = IsEnabled && IsAnySet && SettingsHolder.Drive.QuickDriveAiLimitations;
        }

        private void OnGearRatioPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(CarSetupEntry.Value)) {
                UpdateIsActive();
                Changed?.Invoke(this, EventArgs.Empty);

                if (_car.AcdData != null && FinalGearRatio != null) {
                    CarSetupEntry.UpdateRatioMaxSpeed(new[] { FinalGearRatio }, _car.AcdData, null, false);
                }
            }
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
                Header = "Allowed tyres",
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

        public string GetChecksum() {
            var result = TyresWearMultiplier.GetHashCode();

            if (FuelMaxMultiplier != 1d) {
                result = (result * 397) ^ FuelMaxMultiplier.GetHashCode();
            }

            if (_tyres.IsSet) {
                foreach (var tyre in Tyres) {
                    if (!tyre.IsAllowed) {
                        result = (result * 397) ^ tyre.DisplayName.GetHashCode();
                    }
                }
            }

            if (_finalGearRatio.IsSet && FinalGearRatio?.Value != FinalGearRatio?.DefaultValue) {
                result = (result * 397) ^ FinalGearRatio.Value.GetHashCode();
            }

            return BitConverter.GetBytes(result).ToHexString().ToLowerInvariant();
        }

        public void Apply(Game.AiCar entry) {
            if (!IsActive || !SettingsHolder.Drive.QuickDriveAiLimitations) return;

            if (PatchHelper.IsFeatureSupported(PatchHelper.FeatureAiLimitations)) {
                var e = string.Empty;
                if (TyresWearMultiplier != 1d) {
                    e += $@"w{TyresWearMultiplier:F2}";
                }
                if (FuelMaxMultiplier != 1d) {
                    e += $@"f{FuelMaxMultiplier:F2}";
                }
                if (_tyres.IsSet && Tyres.Any(x => !x.IsAllowed)) {
                    foreach (var disabled in Tyres.Where(x => !x.IsAllowed).Select(x => x.DisplayName)) {
                        e += $@"d{disabled}";
                    }
                }
                if (_finalGearRatio.IsSet && FinalGearRatio?.Value != FinalGearRatio?.DefaultValue && FinalGearRatio != null) {
                    e += $@"r{FinalGearRatio.Value:F4}";
                }
                if (e != string.Empty) {
                    entry.AiRestrictions = e;
                }
                return;
            }

            var newId = $@"__cm_tmp_{_car.Id}_{GetChecksum()}";
            FakeCarsHelper.CreateFakeCar(_car, newId, acd => {
                var data = _car.AcdData;
                if (data != null) {
                    var tyresIni = data.GetIniFile("tyres.ini").Clone();

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

                    if (_tyres.IsSet && Tyres.Any(x => !x.IsAllowed)) {
                        var toRemovalNames = Tyres.Where(x => !x.IsAllowed).Select(x => x.DisplayName).ToList();
                        tyresIni.SetSections("FRONT", -1, tyresIni.GetSections("FRONT", -1).Where(x => !toRemovalNames.Contains(x.GetTyreName())).ToList());
                        tyresIni.SetSections("REAR", -1, tyresIni.GetSections("REAR", -1).Where(x => !toRemovalNames.Contains(x.GetTyreName())).ToList());
                        tyresIni["COMPOUND_DEFAULT"].Set("INDEX", 0);
                        acd.SetEntry("tyres.ini", tyresIni.ToString());
                    }

                    if (_finalGearRatio.IsSet && FinalGearRatio?.Value != FinalGearRatio?.DefaultValue) {
                        var newValue = FinalGearRatio.ValuePair.Value;
                        var drivetrainIni = data.GetIniFile("drivetrain.ini");
                        drivetrainIni["GEARS"].Set("FINAL", newValue);
                        acd.SetEntry("drivetrain.ini", drivetrainIni.ToString());

                        var setupIni = data.GetIniFile("setup.ini");
                        setupIni.Remove("FINAL_GEAR_RATIO");
                        acd.SetEntry("setup.ini", setupIni.ToString());
                    }
                }
            });

            entry.CarId = newId;
        }

        [CanBeNull]
        public string Save() {
            if (!IsAnySet && !IsEnabled) return null;
            var j = new JObject();

            if (IsEnabled) {
                j[@"enabled"] = true;
            }

            if (TyresWearMultiplier != 1d) {
                j[@"wearMultiplier"] = TyresWearMultiplier;
            }

            if (FuelMaxMultiplier != 1d) {
                j[@"fuelMultiplier"] = FuelMaxMultiplier;
            }

            if (_tyres.IsSet && Tyres.Any(x => !x.IsAllowed)) {
                j[@"disabledTyres"] = new JArray(Tyres.Where(x => !x.IsAllowed).Select(x => x.DisplayName));
            }

            if (_finalGearRatio.IsSet && FinalGearRatio?.Value != FinalGearRatio?.DefaultValue) {
                j[@"finalGearRatio"] = FinalGearRatio.Value;
            }

            return j.ToString(Formatting.None);
        }

        public void Load(string serialized) {
            try {
                var j = string.IsNullOrWhiteSpace(serialized) ? new JObject() : JObject.Parse(serialized);
                IsEnabled = j.GetBoolValueOnly("enabled") ?? false;
                TyresWearMultiplier = j.GetDoubleValueOnly("wearMultiplier") ?? 1d;
                FuelMaxMultiplier = j.GetDoubleValueOnly("fuelMultiplier") ?? 1d;

                var tyres = (j[@"disabledTyres"] as JArray)?.Select(x => x?.ToString()).ToList();
                if (tyres != null || _tyres.IsSet) {
                    Tyres.ForEach(x => x.IsAllowed = tyres?.Contains(x.DisplayName) != false);
                }

                var finalGearRatio = j.GetDoubleValueOnly("finalGearRatio");
                if ((finalGearRatio.HasValue || _finalGearRatio.IsSet) && FinalGearRatio != null) {
                    FinalGearRatio.Value = finalGearRatio ?? FinalGearRatio.DefaultValue;
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void CopyFrom(AiLimitationDetails a) {
            TyresWearMultiplier = a.TyresWearMultiplier;
            FuelMaxMultiplier = a.FuelMaxMultiplier;

            if (_tyres.IsSet || a._tyres.IsSet) {
                Tyres.ForEach(x => x.IsAllowed = a.Tyres.GetByIdOrDefault(x.DisplayName)?.IsAllowed != false);
            }

            if ((_finalGearRatio.IsSet || a._finalGearRatio.IsSet) && FinalGearRatio != null) {
                FinalGearRatio.Value = a.FinalGearRatio?.Value ?? FinalGearRatio.DefaultValue;
            }
        }
    }
}