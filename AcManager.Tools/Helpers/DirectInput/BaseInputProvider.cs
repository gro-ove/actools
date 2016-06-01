using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.DirectInput {
    public abstract class BaseInputProvider<T> : Displayable, IInputProvider {
        public int Id { get; }

        protected BaseInputProvider(int id) {
            Id = id;
        }

        public string ShortName { get; protected set; }

        private T _value;

        public T Value {
            get { return _value; }
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
                OnValueChanged();
            }
        }

        protected virtual void OnValueChanged() {}
    }
}