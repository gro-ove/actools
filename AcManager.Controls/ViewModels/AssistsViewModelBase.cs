using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Base view model — usually only to load stuff to display from serialized data.
    /// Could save data too, it depends on creation way. For more information look
    /// at Constructors region.
    /// </summary>
    public class AssistsViewModelBase : NotifyPropertyChanged {
        private const string DefaultKey = "AssistsViewModel.sd";

        public const string UserPresetableKeyValue = "Assists";

        /* values for combobox */

        public AssistState[] AssistStates { get; } = {
            AssistState.Off,
            AssistState.Factory,
            AssistState.On
        };

        [ValueConversion(typeof(AssistState), typeof(string))]
        private class InnerAssistStateToStringConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                switch (value as AssistState?) {
                    case AssistState.Off:
                        return ToolsStrings.AssistState_Off;
                    case AssistState.Factory:
                        return ToolsStrings.AssistState_Factory;
                    case AssistState.On:
                        return ToolsStrings.AssistState_On;
                    default:
                        return null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter AssistStateToStringConverter { get; } = new InnerAssistStateToStringConverter();

        [CanBeNull]
        private ServerInformationExtendedAssists _serverAssists;

        [CanBeNull]
        public ServerInformationExtendedAssists ServerAssists {
            get => _serverAssists;
            set {
                if (Equals(value, _serverAssists)) return;
                var oldValue = _serverAssists;
                _serverAssists = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(Damage));
                OnPropertyChanged(nameof(DamageEditable));
                OnPropertyChanged(nameof(DamageRealismLevel));

                OnPropertyChanged(nameof(TyreWearMultiplier));
                OnPropertyChanged(nameof(TyreWearMultiplierEditable));
                OnPropertyChanged(nameof(TyreWearMultiplierRealismLevel));

                OnPropertyChanged(nameof(FuelConsumption));
                OnPropertyChanged(nameof(FuelConsumptionEditable));
                OnPropertyChanged(nameof(FuelConsumptionRealismLevel));

                if (oldValue == null != (value == null)) {
                    OnPropertyChanged(nameof(AutoBrake));
                    OnPropertyChanged(nameof(AutoBrakeEditable));
                    OnPropertyChanged(nameof(TyreBlanketsEditable));
                }

                if (oldValue?.TyreBlankets != value?.TyreBlankets) {
                    OnPropertyChanged(nameof(TyreBlankets));
                    OnPropertyChanged(nameof(TyreBlanketsRealismLevel));
                }

                if (oldValue?.AutoclutchAllowed != value?.AutoclutchAllowed) {
                    OnPropertyChanged(nameof(AutoClutch));
                    OnPropertyChanged(nameof(AutoClutchEditable));
                    OnPropertyChanged(nameof(AutoClutchRealismLevel));
                }

                if (oldValue?.StabilityAllowed != value?.StabilityAllowed) {
                    OnPropertyChanged(nameof(StabilityControl));
                    OnPropertyChanged(nameof(StabilityControlEditable));
                    OnPropertyChanged(nameof(StabilityControlRealismLevel));
                }

                if (oldValue?.AbsState != value?.AbsState) {
                    OnPropertyChanged(nameof(Abs));
                    OnPropertyChanged(nameof(AbsEditable));
                    OnPropertyChanged(nameof(AbsRealismLevel));
                }

                if (oldValue?.TractionControlState != value?.TractionControlState) {
                    OnPropertyChanged(nameof(TractionControl));
                    OnPropertyChanged(nameof(TractionControlEditable));
                    OnPropertyChanged(nameof(TractionControlRealismLevel));
                }
            }
        }

        #region Properties
        private bool _idealLine;

        public bool IdealLine {
            get => _idealLine;
            set {
                if (Equals(value, _idealLine)) return;
                _idealLine = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IdealLineRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel IdealLineRealismLevel => IdealLine ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private bool _autoBlip;

        public bool AutoBlip {
            get => _autoBlip;
            set {
                if (Equals(value, _autoBlip)) return;
                _autoBlip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoBlipRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel AutoBlipRealismLevel => AutoBlip ? RealismLevel.QuiteRealistic : RealismLevel.Realistic;

        private int _stabilityControl;

        public int StabilityControl {
            get => _serverAssists?.StabilityAllowed == false ? 0 : _stabilityControl;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _stabilityControl)) return;
                _stabilityControl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StabilityControlRealismLevel));
                SaveLater();
            }
        }

        public bool StabilityControlEditable => _serverAssists?.StabilityAllowed != false;

        public RealismLevel StabilityControlRealismLevel => StabilityControl > 20d ? RealismLevel.NonRealistic : StabilityControl > 10d
                ? RealismLevel.NotQuiteRealistic : StabilityControl > 0d ? RealismLevel.QuiteRealistic : RealismLevel.Realistic;

        private bool _autoBrake;

        public bool AutoBrake {
            get => _serverAssists == null && _autoBrake;
            set {
                if (Equals(value, _autoBrake)) return;
                _autoBrake = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoBrakeRealismLevel));
                SaveLater();
            }
        }

        public bool AutoBrakeEditable => _serverAssists == null;

        public RealismLevel AutoBrakeRealismLevel => AutoBrake ? RealismLevel.NonRealistic : RealismLevel.Realistic;

        private bool _autoShifter;

        public bool AutoShifter {
            get => _autoShifter;
            set {
                if (Equals(value, _autoShifter)) return;
                _autoShifter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoShifterRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel AutoShifterRealismLevel => AutoShifter ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private double _slipsteamMultipler = 1d;

        public double SlipsteamMultipler {
            get => _slipsteamMultipler;
            set {
                value = Math.Round(value.Clamp(0d, 10d), 1);
                if (Equals(value, _slipsteamMultipler)) return;
                _slipsteamMultipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SlipsteamMultiplerRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel SlipsteamMultiplerRealismLevel
            => SlipsteamMultipler > 5 ? RealismLevel.NonRealistic : !Equals(SlipsteamMultipler, 1d) ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private bool _autoClutch;

        public bool AutoClutch {
            get => _serverAssists?.AutoclutchAllowed != false && _autoClutch;
            set {
                if (Equals(value, _autoClutch)) return;
                _autoClutch = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoClutchRealismLevel));
                SaveLater();
            }
        }

        public bool AutoClutchEditable => _serverAssists?.AutoclutchAllowed != false;

        public RealismLevel AutoClutchRealismLevel => AutoClutch ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private AssistState _abs = AssistState.Factory;

        public AssistState Abs {
            get => _serverAssists?.AbsState.ToAssistState() ?? _abs;
            set {
                if (Equals(value, _abs)) return;
                _abs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AbsRealismLevel));
                SaveLater();
            }
        }

        public bool AbsEditable => _serverAssists == null;

        public RealismLevel AbsRealismLevel => Abs == AssistState.Factory ? RealismLevel.Realistic :
                Abs == AssistState.Off ? RealismLevel.QuiteRealistic : RealismLevel.NotQuiteRealistic;

        private AssistState _tractionControl = AssistState.Factory;

        public AssistState TractionControl {
            get => _serverAssists?.TractionControlState.ToAssistState() ?? _tractionControl;
            set {
                if (Equals(value, _tractionControl)) return;
                _tractionControl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TractionControlRealismLevel));
                SaveLater();
            }
        }

        public bool TractionControlEditable => _serverAssists == null;

        public RealismLevel TractionControlRealismLevel => TractionControl == AssistState.Factory ? RealismLevel.Realistic :
                TractionControl == AssistState.Off ? RealismLevel.QuiteRealistic : RealismLevel.NonRealistic;

        private bool _visualDamage = true;

        public bool VisualDamage {
            get => _visualDamage;
            set {
                if (Equals(value, _visualDamage)) return;
                _visualDamage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VisualDamageRealismLevel));
                SaveLater();
            }
        }

        /* those damages are pretty badly implemented anyway */
        public RealismLevel VisualDamageRealismLevel => RealismLevel.Realistic;

        private double _damage = 100;

        public double Damage {
            get => _serverAssists?.DamageMultiplier ?? _damage;
            set {
                value = Math.Round(value.Clamp(0d, 100d), 1);
                if (Equals(value, _damage)) return;
                _damage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DamageRealismLevel));
                SaveLater();
            }
        }

        public bool DamageEditable => _serverAssists == null;

        public RealismLevel DamageRealismLevel => Damage < 20d ? RealismLevel.NonRealistic
                : Damage < 50d ? RealismLevel.NotQuiteRealistic : Damage < 100d ? RealismLevel.QuiteRealistic : RealismLevel.Realistic;

        private double _tyreWearMultipler = 1d;

        public double TyreWearMultiplier {
            get => _serverAssists?.TyreWearRate / 100d ?? _tyreWearMultipler;
            set {
                value = Math.Round(value.Clamp(0d, 5d), 2);
                if (Equals(value, _tyreWearMultipler)) return;
                _tyreWearMultipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TyreWearMultiplierRealismLevel));
                SaveLater();
            }
        }

        public bool TyreWearMultiplierEditable => _serverAssists == null;

        public RealismLevel TyreWearMultiplierRealismLevel => TyreWearMultiplier > 5 ? RealismLevel.NonRealistic :
                !Equals(TyreWearMultiplier, 1d) ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private double _fuelConsumption = 1d;

        public double FuelConsumption {
            get => _serverAssists?.FuelRate / 100d ?? _fuelConsumption;
            set {
                value = Math.Round(value.Clamp(0d, 50d), 2);
                if (Equals(value, _fuelConsumption)) return;
                _fuelConsumption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelConsumptionRealismLevel));
                SaveLater();
            }
        }

        public bool FuelConsumptionEditable => _serverAssists == null;

        public RealismLevel FuelConsumptionRealismLevel => Math.Abs(FuelConsumption - 1f) < 0.01 ? RealismLevel.Realistic :
                Math.Abs(FuelConsumption - 1f) <= 0.5 ? RealismLevel.QuiteRealistic :
                        RealismLevel.NotQuiteRealistic;

        private bool _tyreBlankets;

        public bool TyreBlankets {
            get => _serverAssists?.TyreBlankets ?? _tyreBlankets;
            set {
                if (Equals(value, _tyreBlankets)) return;
                _tyreBlankets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TyreBlanketsRealismLevel));
                SaveLater();
            }
        }

        public bool TyreBlanketsEditable => _serverAssists == null;

        /* totally legit stuff */
        public RealismLevel TyreBlanketsRealismLevel => RealismLevel.Realistic;
        #endregion

        #region Saveable
        private class SaveableData : IJsonSerializable {
            public bool IdealLine;
            public bool AutoBlip;
            public double StabilityControl;
            public bool AutoBrake;
            public bool AutoShifter;
            public double SlipSteam;
            public bool AutoClutch;
            public AssistState Abs;
            public AssistState TractionControl;
            public bool VisualDamage;
            public double Damage;
            public double TyreWear;

            [DefaultValue(1d),
             JsonConverter(typeof(JsonBoolToDoubleConverter)),
             JsonProperty(@"FuelConsumption", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public double FuelConsumption = 1d;

            public bool TyreBlankets;

            string IJsonSerializable.ToJson() {
                var s = new StringWriter();
                var w = new JsonTextWriter(s);

                w.WriteStartObject();
                w.Write(nameof(IdealLine), IdealLine);
                w.Write(nameof(AutoBlip), AutoBlip);
                w.Write(nameof(StabilityControl), StabilityControl);
                w.Write(nameof(AutoBrake), AutoBrake);
                w.Write(nameof(AutoShifter), AutoShifter);
                w.Write(nameof(SlipSteam), SlipSteam);
                w.Write(nameof(AutoClutch), AutoClutch);
                w.Write(nameof(Abs), Abs);
                w.Write(nameof(TractionControl), TractionControl);
                w.Write(nameof(VisualDamage), VisualDamage);
                w.Write(nameof(Damage), Damage);
                w.Write(nameof(TyreWear), TyreWear);
                w.Write(nameof(FuelConsumption), FuelConsumption);
                w.Write(nameof(TyreBlankets), TyreBlankets);
                w.WriteEndObject();

                return s.ToString();
            }
        }

        protected virtual void SaveLater() {
            Saveable.SaveLater();
        }

        protected readonly ISaveHelper Saveable;

        /// <summary>
        /// Inner constructor.
        /// </summary>
        /// <param name="key">ValuesStorage key</param>
        /// <param name="fixedMode">Prevent saving</param>
        protected AssistsViewModelBase(string key, bool fixedMode) {
            Saveable = new SaveHelper<SaveableData>(key ?? DefaultKey, () => fixedMode ? null : new SaveableData {
                IdealLine = _idealLine,
                AutoBlip = _autoBlip,
                StabilityControl = _stabilityControl,
                AutoBrake = _autoBrake,
                AutoShifter = _autoShifter,
                SlipSteam = _slipsteamMultipler,
                AutoClutch = _autoClutch,
                Abs = _abs,
                TractionControl = _tractionControl,
                VisualDamage = _visualDamage,
                Damage = _damage,
                TyreWear = _tyreWearMultipler,
                FuelConsumption = _fuelConsumption,
                TyreBlankets = _tyreBlankets
            }, o => {
                IdealLine = o.IdealLine;
                AutoBlip = o.AutoBlip;
                StabilityControl = (int)o.StabilityControl;
                AutoBrake = o.AutoBrake;
                AutoShifter = o.AutoShifter;
                SlipsteamMultipler = o.SlipSteam;
                AutoClutch = o.AutoClutch;
                Abs = o.Abs;
                TractionControl = o.TractionControl;
                VisualDamage = o.VisualDamage;
                Damage = o.Damage;
                TyreWearMultiplier = o.TyreWear;
                FuelConsumption = o.FuelConsumption;
                TyreBlankets = o.TyreBlankets;
            }, () => {
                IdealLine = false;
                AutoBlip = false;
                StabilityControl = 0;
                AutoBrake = false;
                AutoShifter = false;
                SlipsteamMultipler = 1d;
                AutoClutch = false;
                Abs = AssistState.Factory;
                TractionControl = AssistState.Factory;
                VisualDamage = true;
                Damage = 100d;
                TyreWearMultiplier = 1d;
                FuelConsumption = 1d;
                TyreBlankets = false;
            });
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Full load-and-save mode. All changes will be saved automatically and loaded
        /// later (only with this constuctor).
        /// </summary>
        public AssistsViewModelBase() : this(null, false) {
            Saveable.Initialize();
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        /// <param name="serializedData"></param>
        /// <returns></returns>
        public static AssistsViewModelBase CreateFixed([NotNull] string serializedData) {
            var result = new AssistsViewModelBase(DefaultKey, true);
            result.Saveable.Reset();
            result.Saveable.FromSerializedString(serializedData);
            return result;
        }
        #endregion

        public Game.AssistsProperties ToGameProperties() {
            return new Game.AssistsProperties {
                IdealLine = IdealLine,
                AutoBlip = AutoBlip,
                StabilityControl = StabilityControl,
                AutoBrake = AutoBrake,
                AutoShifter = AutoShifter,
                SlipSteamMultipler = SlipsteamMultipler,
                AutoClutch = AutoClutch,
                Abs = Abs,
                TractionControl = TractionControl,
                VisualDamage = VisualDamage,
                Damage = Damage,
                TyreWearMultipler = TyreWearMultiplier,
                FuelConsumption = FuelConsumption,
                TyreBlankets = TyreBlankets
            };
        }
    }
}