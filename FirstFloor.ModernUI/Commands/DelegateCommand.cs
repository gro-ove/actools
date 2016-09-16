using System;
using System.Collections.Generic;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public class DelegateCommand : ICommandExt {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly bool _isAutomaticRequeryDisabled;
        private List<WeakReference> _canExecuteChangedHandlers;

        public static DelegateCommand Create(Action execute, Func<bool> canExecute = null, bool isAutomaticRequeryDisabled = false) {
            return new DelegateCommand(execute, canExecute, isAutomaticRequeryDisabled);
        }

        public DelegateCommand([NotNull] Action execute, Func<bool> canExecute = null, bool isAutomaticRequeryDisabled = false) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
            _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        public bool CanExecute() {
            return _canExecute == null || _canExecute();
        }

        public void Execute() {
            if (!CanExecute()) return;
            _execute?.Invoke();
        }

        // TODO: rename me to RaiseCanExecuteChanged
        public void OnCanExecuteChanged() {
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
        }
        
        public event EventHandler CanExecuteChanged {
            add {
                if (_canExecute != null) {
                    if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested += value;
                    CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
                }
            }
            remove {
                if (_canExecute != null) {
                    if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested -= value;
                    CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
                }
            }
        }

        bool ICommand.CanExecute(object parameter) {
            return CanExecute();
        }

        void ICommand.Execute(object parameter) {
            Execute();
        }
    }

    public class DelegateCommand<T> : ICommandExt {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        private readonly bool _isAutomaticRequeryDisabled;
        private List<WeakReference> _canExecuteChangedHandlers;

        public DelegateCommand([NotNull] Action<T> execute, Func<T, bool> canExecute = null, bool isAutomaticRequeryDisabled = false) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
            _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }
        
        public bool CanExecute(T parameter) {
            return _canExecute == null || _canExecute(parameter);
        }
        
        public void Execute(T parameter) {
            if (!CanExecute(parameter)) return;
            _execute?.Invoke(parameter);
        }
        
        public void OnCanExecuteChanged() {
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
        }

        public event EventHandler CanExecuteChanged {
            add {
                if (_canExecute != null) {
                    if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested += value;
                    CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
                }
            }
            remove {
                if (_canExecute != null) {
                    if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested -= value;
                    CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
                }
            }
        }

        bool ICommand.CanExecute(object parameter) {
            return _canExecute == null || parameter is T && _canExecute((T)parameter);
        }

        void ICommand.Execute(object parameter) {
            Execute((T)parameter);
        }
    }
}