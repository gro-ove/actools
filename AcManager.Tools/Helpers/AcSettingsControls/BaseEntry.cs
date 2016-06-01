using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public abstract class BaseEntry<T> : Displayable, IEntry where T : class, IInputProvider {
        public string Id { get; }

        protected BaseEntry(string id, string displayName) {
            Id = id;
            DisplayName = displayName;
        }

        public sealed override string DisplayName { get; set; }

        private WaitingFor _waitingFor;

        public WaitingFor WaitingFor {
            get { return _waitingFor; }
            set {
                if (Equals(value, _waitingFor)) return;
                _waitingFor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Waiting));
            }
        }

        public bool Waiting => _waitingFor != WaitingFor.None;

        private T _input;

        public T Input {
            get { return _input; }
            set {
                if (Equals(value, _input)) return;
                OnInputChanged(_input, value);
                _input = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnInputChanged(T oldValue, T newValue) {}

        public void Set(IniFile ini, T input) {
            Input = input;
            LoadFromIni(ini);
        }

        protected virtual void LoadFromIni(IniFile ini) {}

        public void Clear() {
            WaitingFor = WaitingFor.None;
            Input = null;
        }
    }
}