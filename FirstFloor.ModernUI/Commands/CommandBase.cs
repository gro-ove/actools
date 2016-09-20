using System;
using System.Collections.Generic;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Commands {
    public abstract class CommandBase : ICommand {
        public readonly bool AlwaysCanExecute;
        public readonly bool IsAutomaticRequeryDisabled;

        protected CommandBase(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) {
            AlwaysCanExecute = alwaysCanExecute;
            IsAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        private List<WeakReference> _canExecuteChangedHandlers;

        public void RaiseCanExecuteChanged() {
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
        }

        protected abstract bool CanExecuteOverride(object parameter);

        protected abstract void ExecuteOverride(object parameter);

        bool ICommand.CanExecute(object parameter) {
            return AlwaysCanExecute || CanExecuteOverride(parameter);
        }

        void ICommand.Execute(object parameter) {
            ExecuteOverride(parameter);
        }

        public event EventHandler CanExecuteChanged {
            add {
                if (!AlwaysCanExecute) {
                    if (IsAutomaticRequeryDisabled) {
                        CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
                    } else {
                        CommandManager.RequerySuggested += value;
                    }
                }
            }
            remove {
                if (!AlwaysCanExecute) {
                    if (IsAutomaticRequeryDisabled) {
                        CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
                    } else {
                        CommandManager.RequerySuggested -= value;
                    }
                }
            }
        }
    }

    public abstract class CommandExt : CommandBase {
        protected CommandExt(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) : base(alwaysCanExecute, isAutomaticRequeryDisabled) { }

        protected sealed override void ExecuteOverride(object parameter) {
            ExecuteOverride();
        }

        protected sealed override bool CanExecuteOverride(object parameter) {
            return AlwaysCanExecute || CanExecuteOverride();
        }

        public bool CanExecute() {
            return AlwaysCanExecute || CanExecuteOverride();
        }

        public void Execute() {
            if (AlwaysCanExecute || CanExecuteOverride()) {
                ExecuteOverride();
            }
        }

        protected abstract bool CanExecuteOverride();

        protected abstract void ExecuteOverride();
    }

    public abstract class CommandExt<T> : CommandBase {
        protected CommandExt(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) : base(alwaysCanExecute, isAutomaticRequeryDisabled) {}

        protected sealed override void ExecuteOverride(object parameter) {
            if (parameter is T || (parameter == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))) {
                ExecuteOverride((T)parameter);
                return;
            }

            Logging.Error($"Invalid type: {parameter?.GetType().Name ?? "<NULL>"} (required: {typeof(T)})");
        }

        protected sealed override bool CanExecuteOverride(object parameter) {
            if (AlwaysCanExecute) return true;
            if (parameter is T || (parameter == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))) {
                return CanExecuteOverride((T)parameter);
            }

            Logging.Error($"Invalid type: {parameter?.GetType().Name ?? "<NULL>"} (required: {typeof(T)})");
            return false;
        }

        public bool CanExecute(T parameter) {
            return AlwaysCanExecute || CanExecuteOverride(parameter);
        }

        public void Execute(T parameter) {
            if (AlwaysCanExecute || CanExecuteOverride(parameter)) {
                ExecuteOverride(parameter);
            }
        }

        protected abstract bool CanExecuteOverride(T parameter);

        protected abstract void ExecuteOverride(T parameter);
    }
}