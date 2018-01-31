using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public abstract class InputProviderBase<T> : Displayable, IInputProvider {
        public int Id { get; }

        public string DefaultDisplayName { get; private set; }
        public string DefaultShortName { get; private set; }

        protected InputProviderBase(int id) {
            Id = id;
        }

        public void SetDisplayParams(string displayName, bool isVisible) {
            IsVisible = isVisible;
            SetDisplayName(displayName);
        }

        protected abstract void SetDisplayName([CanBeNull] string displayName);

        private bool _isVisible = true;

        public bool IsVisible {
            get => _isVisible;
            set {
                if (Equals(value, _isVisible)) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        private string _shortName;

        public string ShortName {
            get => _shortName;
            protected set {
                if (value == _shortName) return;
                _shortName = value;
                OnPropertyChanged();
                if (DefaultShortName == null) {
                    DefaultShortName = value;
                }
            }
        }

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
            set {
                base.DisplayName = value;
                if (DefaultDisplayName == null) {
                    DefaultDisplayName = value;
                }
            }
        }
    }
}