using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigKeyValue : PythonAppConfigValue, ILocalKeyBindingInput {
        public string DisplayValue => Value.ToReadableKey();

        public new Keys Value {
            get => (Keys?)FlexibleParser.TryParseInt(base.Value) ?? Keys.None;
            set {
                if (Equals(value, Value)) return;
                base.Value = ((int)value).ToInvariantString();
            }
        }

        protected override void OnValueChanged() {
            IsWaiting = false;
            OnPropertyChanged(nameof(DisplayValue));
        }

        private bool _isWaiting;

        public bool IsWaiting {
            get => _isWaiting;
            set => Apply(value, ref _isWaiting);
        }

        private bool _isPressed;

        public bool IsPressed {
            get => _isPressed;
            set => Apply(value, ref _isPressed);
        }

        private DelegateCommand _clearCommand;

        public DelegateCommand ClearCommand => _clearCommand ?? (_clearCommand = new DelegateCommand(() => {
            Value = Keys.None;
            IsWaiting = false;
        }));

        ICommand ILocalKeyBindingInput.ClearCommand => ClearCommand;
    }
}