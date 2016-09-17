using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public class AsyncCommand : ICommandExt {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private readonly bool _isAutomaticRequeryDisabled;
        private List<WeakReference> _canExecuteChangedHandlers;
        private bool _inProcess;
        private readonly int _additionalDelay;
        
        public AsyncCommand([NotNull] Func<Task> execute, Func<bool> canExecute, int additionalDelay = 0, bool isAutomaticRequeryDisabled = false) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
            _additionalDelay = additionalDelay;
            _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        public AsyncCommand([NotNull] Func<Task> execute, int additionalDelay = 0, bool isAutomaticRequeryDisabled = false)
                : this(execute, null, additionalDelay, isAutomaticRequeryDisabled) {}

        public bool CanExecute() {
            return !_inProcess && _canExecute == null || _canExecute();
        }

        public async Task Execute() {
            try {
                _inProcess = true;
                RaiseCanExecuteChanged();

                await _execute();

                if (_additionalDelay != 0) {
                    await Task.Delay(_additionalDelay);
                }
            } finally {
                _inProcess = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() {
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
        }

        public event EventHandler CanExecuteChanged {
            add {
                if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested += value;
                CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
            }
            remove {
                if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested -= value;
                CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
            }
        }

        bool ICommand.CanExecute(object parameter) {
            return !_inProcess && (_canExecute == null || _canExecute());
        }

        void ICommand.Execute(object parameter) {
            Execute().Forget();
        }
    }

    public class AsyncCommand<T> : ICommandExt {
        private readonly Func<T, Task> _execute;
        private readonly Func<T, bool> _canExecute;
        private readonly bool _isAutomaticRequeryDisabled;
        private List<WeakReference> _canExecuteChangedHandlers;
        private bool _inProcess;
        private readonly int _additionalDelay;
        
        public AsyncCommand([NotNull] Func<T, Task> execute, Func<T, bool> canExecute = null, int additionalDelay = 0, bool isAutomaticRequeryDisabled = false) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
            _additionalDelay = additionalDelay;
            _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        public bool CanExecute(T parameter) {
            return !_inProcess && _canExecute == null || _canExecute(parameter);
        }

        public async Task Execute(T parameter) {
            try {
                _inProcess = true;
                RaiseCanExecuteChanged();

                await _execute(parameter);

                if (_additionalDelay != 0) {
                    await Task.Delay(_additionalDelay);
                }
            } finally {
                _inProcess = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() {
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
        }

        public event EventHandler CanExecuteChanged {
            add {
                if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested += value;
                CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
            }
            remove {
                if (!_isAutomaticRequeryDisabled) CommandManager.RequerySuggested -= value;
                CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
            }
        }

        bool ICommand.CanExecute(object parameter) {
            return !_inProcess && (_canExecute == null || parameter is T && _canExecute((T)parameter));
        }

        void ICommand.Execute(object parameter) {
            Execute((T)parameter).Forget();
        }
    }
}