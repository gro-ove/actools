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

        private bool _waiting;

        public bool Waiting {
            get { return _waiting; }
            set {
                if (Equals(value, _waiting)) return;
                _waiting = value;
                OnPropertyChanged();
            }
        }

        private T _input;

        public T Input {
            get { return _input; }
            set {
                if (Equals(value, _input)) {
                    if (Waiting) {
                        Waiting = false;
                    }

                    return;
                }

                OnInputChanged(_input, value);
                _input = value;
                Waiting = false;
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
            Waiting = false;
            Input = null;
        }
    }
}