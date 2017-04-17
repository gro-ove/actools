using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace LicensePlates {
    public abstract class PlateValueBase : INotifyPropertyChanged {
        internal static readonly Random Random = new Random(Guid.NewGuid().GetHashCode());

        public string Name { get; }

        [CanBeNull]
        public string DefaultValue { get; }

        [CanBeNull]
        public Func<string> RandomFunc { get; }

        protected PlateValueBase(string name, object defaultValue) {
            Name = name;
            DefaultValue = defaultValue as string;
            RandomFunc = defaultValue as Func<string>;
        }

        public abstract PlateValueBase Clone();

        protected abstract string GetRandom();

        private string _value;

        public string Value {
            get { return _value; }
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnValueChanged();
            }
        }

        protected virtual void OnValueChanged() {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ResultValue));
        }

        protected virtual string ConvertValue(string userValue) {
            return userValue;
        }

        public string ResultValue => (_value == null ? null : ConvertValue(_value)) ?? DefaultValue ?? RandomFunc?.Invoke() ?? GetRandom();

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum InputLength {
        Fixed,
        Varying
    }

    public class InputTextValue : PlateValueBase {
        public int Length { get; }

        public InputLength LengthMode { get; }

        public InputTextValue(string name, object defaultValue,  int length, InputLength lengthMode)
                : base(name, defaultValue) {
            Length = length;
            LengthMode = lengthMode;
        }

        protected override string GetRandom() {
            return Guid.NewGuid()
                       .ToString()
                       .Replace("-", string.Empty)
                       .Substring(0, Math.Min(32, LengthMode == InputLength.Fixed ? Length : Random.Next(Math.Min(Length / 2, 2), Length + 1)))
                       .ToUpperInvariant();
        }

        public override PlateValueBase Clone() {
            return new InputTextValue(Name, DefaultValue, Length, LengthMode) {
                Value = Value
            };
        }
    }

    public class InputNumberValue : PlateValueBase {
        public int Length { get; }

        public int From { get; }

        public int To { get; }

        public InputNumberValue(string name, object defaultValue, int length, int from, int to) : base(name, defaultValue) {
            Length = length;
            From = from;
            To = to;
        }

        protected override void OnValueChanged() {
            base.OnValueChanged();
            OnPropertyChanged(nameof(NumberValue));
        }

        public double NumberValue {
            get {
                double v;
                return double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : From;
            }
            set {
                var v = Math.Round(value).ToString(CultureInfo.InvariantCulture);
                if (Equals(v, Value)) return;
                Value = v;
            }
        }

        protected override string ConvertValue(string userValue) {
            int value;
            return int.TryParse(userValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value.ToString("D" + Length) :
                    base.ConvertValue(userValue);
        }

        protected override string GetRandom() {
            return ((int)(Random.NextDouble() * (1 + To - From) + From)).ToString("D" + Length);
        }

        public override PlateValueBase Clone() {
            return new InputNumberValue(Name, DefaultValue, Length, From, To) {
                Value = Value
            };
        }
    }

    public class InputSelectValue : PlateValueBase {
        public List<string> Values { get; }

        private readonly List<KeyValuePair<string, string>> _values;

        public InputSelectValue(string name, object defaultValue, List<KeyValuePair<string, string>> values)
                : base(name, defaultValue) {
            _values = values;
            Values = _values.Select(x => x.Value).ToList();
        }

        protected override string ConvertValue(string userValue) {
            return _values.FirstOrDefault(x => string.Equals(x.Value, userValue, StringComparison.OrdinalIgnoreCase)).Key ?? userValue;
        }

        protected override string GetRandom() {
            return _values[Random.Next(Values.Count)].Key;
        }

        public override PlateValueBase Clone() {
            return new InputSelectValue(Name, DefaultValue, _values) {
                Value = Value
            };
        }
    }
}