using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class WheelAxleEntry : BaseEntry<DirectInputAxle>, IDirectInputEntry {
        public bool RangeMode { get; }

        public bool GammaMode { get; }

        public WheelAxleEntry([LocalizationRequired(false)] string id, string name, bool rangeMode = true, bool gammaMode = false) : base(id, name) {
            RangeMode = rangeMode;
            GammaMode = gammaMode;
        }

        IDirectInputDevice IDirectInputEntry.Device => Input?.Device;

        protected override void OnInputChanged(DirectInputAxle oldValue, DirectInputAxle newValue) {
            if (oldValue != null) {
                oldValue.PropertyChanged -= AxlePropertyChanged;
            }

            if (newValue != null) {
                newValue.PropertyChanged += AxlePropertyChanged;
                UpdateValue();
            } else {
                Value = 0d;
            }
        }

        private double _value;

        public double Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        private void UpdateValue() {
            if (Input == null) return;

            var value = Input.Value;
            if (Invert) {
                value = 1d - value;
            }

            if (RangeMode) {
                var paddingStart = 0.01 * RangeFrom;
                var paddingEnd = 0.01 * RangeTo;
                value = ((value - paddingStart) / (0.00000001 + paddingEnd - paddingStart)).Saturate();

                if (GammaMode) {
                    value = value.Pow(Gamma);
                }
            } else {
                value = value * 2d - 1d;
                if (!Equals(value, 0d)) {
                    var negative = value < 0d;
                    value = (negative ? -value : value).Pow(Gamma) * 0.5 + 0.5;

                    if (negative) {
                        value = 1d - value;
                    }
                }
            }

            Value = value;
        }

        private void AxlePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Input.Value)) {
                UpdateValue();
            }
        }

        #region Properties
        private int _degressOfRotation = 900;

        public int DegressOfRotation {
            get => _degressOfRotation;
            set {
                value = value.Clamp(40, 1180);
                if (Equals(value, _degressOfRotation)) return;
                _degressOfRotation = value;
                OnPropertyChanged();
            }
        }

        private int _rangeFrom;

        public int RangeFrom {
            get => _rangeFrom;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _rangeFrom)) return;
                _rangeFrom = value;
                OnPropertyChanged();

                if (RangeTo < RangeFrom) {
                    RangeTo = RangeFrom;
                } else {
                    UpdateValue();
                }
            }
        }

        private int _rangeTo = 1;

        public int RangeTo {
            get => _rangeTo;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _rangeTo)) return;
                _rangeTo = value;
                OnPropertyChanged();

                if (RangeFrom > RangeTo) {
                    RangeFrom = RangeTo;
                } else {
                    UpdateValue();
                }
            }
        }

        private double _gamma = 1d;

        public double Gamma {
            get => _gamma;
            set {
                value = value.Clamp(1d, 5d);
                if (Equals(value, _gamma)) return;
                _gamma = value;
                OnPropertyChanged();
                UpdateValue();
            }
        }

        private int _filter;

        public int Filter {
            get => _filter;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _filter)) return;
                _filter = value;
                OnPropertyChanged();
            }
        }

        private int _speedSensitivity;

        public int SpeedSensitivity {
            get => _speedSensitivity;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _speedSensitivity)) return;
                _speedSensitivity = value;
                OnPropertyChanged();
            }
        }

        private bool _invert;

        public bool Invert {
            get => _invert;
            set {
                if (Equals(value, _invert)) return;
                _invert = value;
                OnPropertyChanged();
                UpdateValue();
            }
        }
        #endregion

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];

            var deviceId = section.GetInt("JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));
            Input = device?.GetAxle(section.GetInt("AXLE", -1));

            if (RangeMode) {
                if (GammaMode) {
                    Gamma = section.GetDouble("GAMMA", 1d);
                }

                var from = (int)(section.GetDouble("MIN", -1d) * 50 + 50).Clamp(0, 100);
                var to = (int)(section.GetDouble("MAX", 1d) * 50 + 50).Clamp(0, 100);

                Invert = from > to;
                RangeFrom = from;
                RangeTo = to;

                if (Invert) {
                    RangeFrom = 100 - from;
                    RangeTo = 100 - to;
                } else {
                    RangeFrom = from;
                    RangeTo = to;
                }
            } else {
                if (GammaMode) {
                    Gamma = section.GetDouble("STEER_GAMMA", 1d);
                }

                DegressOfRotation = section.GetIntNullable("__CM_ORIGINAL_LOCK") ?? section.GetInt("LOCK", 900);
                Filter = section.GetDouble("STEER_FILTER", 0d).ToIntPercentage();
                SpeedSensitivity = section.GetDouble("SPEED_SENSITIVITY", 0d).ToIntPercentage();
            }

            UpdateValue();
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("JOY", Input?.Device.Index);
            section.Set("AXLE", Input?.Id ?? -1);

            if (RangeMode) {
                if (GammaMode) {
                    section.Set("GAMMA", Gamma);
                }

                var min = 0.02 * RangeFrom - 1.0;
                var max = 0.02 * RangeTo - 1.0;

                section.Set("MIN", Invert ? -min : min);
                section.Set("MAX", Invert ? -max : max);
            } else {
                if (GammaMode) {
                    section.Set("STEER_GAMMA", Gamma);
                }

                section.Set("LOCK", DegressOfRotation);
                section.Remove("__CM_ORIGINAL_LOCK");
                section.Set("STEER_FILTER", Filter.ToDoublePercentage());
                section.Set("SPEED_SENSITIVITY", SpeedSensitivity.ToDoublePercentage());
            }
        }
    }
}