using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.DirectInput {
    public abstract class BaseInputProvider<T> : Displayable, IInputProvider {
        public int Id { get; }

        protected BaseInputProvider(int id) {
            Id = id;
        }

        private bool _isVisible = true;

        public bool IsVisible {
            get => _isVisible;
            set {
                if (Equals(value, _isVisible)) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        public string ShortName { get; protected set; }

        private T _value;

        public T Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
                OnValueChanged();
            }
        }

        protected virtual void OnValueChanged() {}

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }
    }
}