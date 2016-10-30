using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Base view model — usually only to load stuff to display from serialized data.
    /// Could save data too, it depends on creation way. For more information look 
    /// at Constructors region.
    /// </summary>
    public class BaseAssistsViewModel : NotifyPropertyChanged {
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

        #region Properties
        private bool _idealLine;

        public bool IdealLine {
            get { return _idealLine; }
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
            get { return _autoBlip; }
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
            get { return _stabilityControl; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _stabilityControl)) return;
                _stabilityControl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StabilityControlRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel StabilityControlRealismLevel => StabilityControl > 20d ? RealismLevel.NonRealistic : StabilityControl > 10d ? RealismLevel.NotQuiteRealistic : StabilityControl > 0d ? RealismLevel.QuiteRealistic : RealismLevel.Realistic;

        private bool _autoBrake;

        public bool AutoBrake {
            get { return _autoBrake; }
            set {
                if (Equals(value, _autoBrake)) return;
                _autoBrake = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoBrakeRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel AutoBrakeRealismLevel => AutoBrake ? RealismLevel.NonRealistic : RealismLevel.Realistic;

        private bool _autoShifter;

        public bool AutoShifter {
            get { return _autoShifter; }
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
            get { return _slipsteamMultipler; }
            set {
                value = Math.Round(value.Clamp(0d, 10d), 1);
                if (Equals(value, _slipsteamMultipler)) return;
                _slipsteamMultipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SlipsteamMultiplerRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel SlipsteamMultiplerRealismLevel => SlipsteamMultipler > 5 ? RealismLevel.NonRealistic : !Equals(SlipsteamMultipler, 1d) ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private bool _autoClutch;

        public bool AutoClutch {
            get { return _autoClutch; }
            set {
                if (Equals(value, _autoClutch)) return;
                _autoClutch = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoClutchRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel AutoClutchRealismLevel => AutoClutch ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private AssistState _abs = AssistState.Factory;

        public AssistState Abs {
            get { return _abs; }
            set {
                if (Equals(value, _abs)) return;
                _abs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AbsRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel AbsRealismLevel => Abs == AssistState.Factory ? RealismLevel.Realistic : Abs == AssistState.Off ? RealismLevel.QuiteRealistic : RealismLevel.NotQuiteRealistic;

        private AssistState _tractionControl = AssistState.Factory;

        public AssistState TractionControl {
            get { return _tractionControl; }
            set {
                if (Equals(value, _tractionControl)) return;
                _tractionControl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TractionControlRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel TractionControlRealismLevel => TractionControl == AssistState.Factory ? RealismLevel.Realistic : TractionControl == AssistState.Off ? RealismLevel.QuiteRealistic : RealismLevel.NonRealistic;

        private bool _visualDamage = true;

        public bool VisualDamage {
            get { return _visualDamage; }
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
            get { return _damage; }
            set {
                value = Math.Round(value.Clamp(0d, 100d), 1);
                if (Equals(value, _damage)) return;
                _damage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DamageRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel DamageRealismLevel => Damage < 20d ? RealismLevel.NonRealistic : Damage < 50d ? RealismLevel.NotQuiteRealistic : Damage < 100d ? RealismLevel.QuiteRealistic : RealismLevel.Realistic;

        private double _tyreWearMultipler = 1d;

        public double TyreWearMultipler {
            get { return _tyreWearMultipler; }
            set {
                value = Math.Round(value.Clamp(0d, 5d), 2);
                if (Equals(value, _tyreWearMultipler)) return;
                _tyreWearMultipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TyreWearMultiplerRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel TyreWearMultiplerRealismLevel => TyreWearMultipler > 5 ? RealismLevel.NonRealistic : !Equals(TyreWearMultipler, 1d) ? RealismLevel.NotQuiteRealistic : RealismLevel.Realistic;

        private double _fuelConsumption = 1d;

        public double FuelConsumption {
            get { return _fuelConsumption; }
            set {
                value = Math.Round(value.Clamp(0d, 50d), 2);
                if (Equals(value, _fuelConsumption)) return;
                _fuelConsumption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelConsumptionRealismLevel));
                SaveLater();
            }
        }

        public RealismLevel FuelConsumptionRealismLevel => Math.Abs(FuelConsumption - 1f) < 0.01 ? RealismLevel.Realistic :
                Math.Abs(FuelConsumption - 1f) <= 0.5 ? RealismLevel.QuiteRealistic :
                        RealismLevel.NotQuiteRealistic;

        private bool _tyreBlankets;

        public bool TyreBlankets {
            get { return _tyreBlankets; }
            set {
                if (Equals(value, _tyreBlankets)) return;
                _tyreBlankets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TyreBlanketsRealismLevel));
                SaveLater();
            }
        }

        /* totally legit stuff */
        public RealismLevel TyreBlanketsRealismLevel => RealismLevel.Realistic;
        #endregion

        #region Saveable
        public class BoolToDoubleConverter : JsonConverter {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                writer.WriteValue((double)value);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                return reader.TokenType == JsonToken.Boolean ? ((bool)reader.Value ? 1d : 0d) : reader.Value.AsDouble();
            }

            public override bool CanConvert(Type objectType) {
                return objectType == typeof(double);
            }
        }

        private class SaveableData : IStringSerializable {
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

            [JsonConverter(typeof(BoolToDoubleConverter))]
            public double FuelConsumption;
            public bool TyreBlankets;

            public string Serialize() {
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
        protected BaseAssistsViewModel(string key, bool fixedMode) {
            Saveable = new SaveHelper<SaveableData>(key ?? DefaultKey, () => fixedMode ? null : new SaveableData {
                IdealLine = IdealLine,
                AutoBlip = AutoBlip,
                StabilityControl = StabilityControl,
                AutoBrake = AutoBrake,
                AutoShifter = AutoShifter,
                SlipSteam = SlipsteamMultipler,
                AutoClutch = AutoClutch,
                Abs = Abs,
                TractionControl = TractionControl,
                VisualDamage = VisualDamage,
                Damage = Damage,
                TyreWear = TyreWearMultipler,
                FuelConsumption = FuelConsumption,
                TyreBlankets = TyreBlankets
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
                TyreWearMultipler = o.TyreWear;
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
                TyreWearMultipler = 1d;
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
        public BaseAssistsViewModel() : this(null, false) {
            Saveable.Initialize();
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        /// <param name="serializedData"></param>
        /// <returns></returns>
        public static BaseAssistsViewModel CreateFixed([NotNull] string serializedData) {
            var result = new BaseAssistsViewModel(DefaultKey, true);
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
                TyreWearMultipler = TyreWearMultipler,
                FuelConsumption = FuelConsumption,
                TyreBlankets = TyreBlankets
            };
        }
    }
}