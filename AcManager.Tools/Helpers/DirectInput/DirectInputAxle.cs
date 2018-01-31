using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputAxle : InputProviderBase<double>, IDirectInputProvider {
        public string DefaultName { get; }

        public DirectInputAxle(IDirectInputDevice device, int id) : base(id) {
            Device = device;
            DefaultName = string.Format(ToolsStrings.Input_Axle, (id + 1).ToInvariantString());
            SetDisplayParams(null, true);
        }

        protected override void SetDisplayName(string displayName) {
            if (displayName?.Length > 2) {
                var index = displayName.IndexOf(';');
                if (index != -1) {
                    ShortName = displayName.Substring(0, index);
                    DisplayName = displayName.Substring(index + 1).ToTitle();
                } else {
                    var abbreviation = displayName.Where((x, i) => i == 0 || char.IsWhiteSpace(displayName[i - 1])).Take(3).JoinToString();
                    ShortName = abbreviation.ToUpper();
                    DisplayName = displayName.ToTitle();
                }
            } else {
                ShortName = displayName?.ToTitle() ?? (Id + 1).As<string>();
                DisplayName = string.Format(ToolsStrings.Input_Axle, ShortName);
            }
        }

        public IDirectInputDevice Device { get; }

        private double _roundedValue;

        public double RoundedValue {
            get => _roundedValue;
            set {
                if (Equals(value, _roundedValue)) return;
                _roundedValue = value;
                OnPropertyChanged();
            }
        }

        private double _delta;

        public double Delta {
            get => _delta;
            set {
                if (Equals(value, _delta)) return;
                _delta = value;
                OnPropertyChanged();
            }
        }

        protected override void OnValueChanged() {
            var value = Value;
            if ((value - RoundedValue).Abs() < 0.01) return;

            Delta = value - RoundedValue;
            RoundedValue = value;
        }

        public override string ToString() {
            return $"DirectInputAxle({Device.DisplayName}, {ShortName})";
        }
    }
}