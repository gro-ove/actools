using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcManager.Tools;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public sealed class CarPaintColor : Displayable {
        public CarPaintColor(string name, Color defaultValue, [CanBeNull] Dictionary<string, Color> allowedValues) {
            AllowedValues = allowedValues?.Count == 0 ? null : allowedValues;
            DisplayName = name;
            Value = defaultValue;
        }

        private Color _value;

        public Color Value {
            get => _value;
            set {
                if (AllowedValues != null && !AllowedValues.ContainsValue(value)) {
                    value = AllowedValues.First().Value;
                }

                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValuePair));
            }
        }

        public KeyValuePair<string, Color> ValuePair {
            get {
                return AllowedValues?.FirstOrDefault(x => x.Value == _value) ?? new KeyValuePair<string, Color>(ToolsStrings.Common_None, Colors.Transparent);
            }
            set => Value = value.Value;
        }

        [CanBeNull]
        public Dictionary<string, Color> AllowedValues { get; }
    }
}