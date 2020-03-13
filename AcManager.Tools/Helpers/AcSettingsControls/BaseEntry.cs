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
            set => Apply(value, ref _isWaiting, OnIsWaitingChanged);
        }

        protected virtual void OnIsWaitingChanged() { }

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

                OnInputArrived();
                if (Equals(value, _input)) {
                    if (IsWaiting) IsWaiting = false;
                    return;
                }

                _input = value;
                OnInputChanged(_input, value);

                IsWaiting = false;
                OnPropertyChanged();

                _clearCommand?.RaiseCanExecuteChanged();
            }
        }

        public virtual bool IsCompatibleWith([CanBeNull] T obj) {
            return obj != null;
        }

        public virtual void OnInputArrived() {}
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