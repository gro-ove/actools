using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public abstract class CommandBase : ICommand, INotifyPropertyChanged {
        public readonly bool AlwaysCanExecute;
        public readonly bool IsAutomaticRequeryDisabled;

        protected CommandBase(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) {
            AlwaysCanExecute = alwaysCanExecute;
            IsAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        private List<WeakReference> _canExecuteChangedHandlers;

        public void RaiseCanExecuteChanged() {
            OnPropertyChanged(nameof(IsAbleToExecute));
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
            if (IsAutomaticRequeryDisabled) {
                CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
            } else {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsAbleToExecute => AlwaysCanExecute || CanExecuteOverride(null);

        protected abstract bool CanExecuteOverride(object parameter);

        protected abstract void ExecuteOverride(object parameter);

        bool ICommand.CanExecute(object parameter) {
            return CanExecuteOverride(parameter);
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

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}