using System.Collections.Generic;
using System.Windows.Input;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public abstract class BaseEntry<T> : Displayable, IEntry where T : class, IInputProvider {
        public string Id { get; }

        protected BaseEntry([LocalizationRequired(false)] string id, string displayName) {
            Id = id;
            DisplayName = displayName;
        }

        public sealed override string DisplayName { get; set; }

        private bool _isWaiting;

        public bool IsWaiting {
            get => _isWaiting;
            set {
                if (Equals(value, _isWaiting)) return;
                _isWaiting = value;
                OnPropertyChanged();
            }
        }

        public virtual EntryLayer Layer => EntryLayer.Basic;

        [CanBeNull]
        private T _input;

        [CanBeNull]
        public T Input {
            get => _input;
            set {
                if (ControlsSettings.OptionDebugControlles) {
                    Logging.Debug($"Set: {value?.DisplayName} (ID={value?.Id})");
                }

                if (Equals(value, _input)) {
                    if (IsWaiting) IsWaiting = false;
                    return;
                }

                OnInputChanged(_input, value);

                _input = value;
                IsWaiting = false;
                OnPropertyChanged();

                _clearCommand?.RaiseCanExecuteChanged();
            }
        }

        public virtual bool IsCompatibleWith(T obj) {
            return obj != null;
        }

        protected virtual void OnInputChanged([CanBeNull] T oldValue, [CanBeNull] T newValue) {}

        public abstract void Save(IniFile ini);

        public abstract void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices);

        public void Clear() {
            IsWaiting = false;
            Input = null;
        }

        private CommandBase _clearCommand;

        public ICommand ClearCommand => _clearCommand ?? (_clearCommand = new DelegateCommand(Clear, () => Input != null));
    }
}