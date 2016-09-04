using System;
using System.Threading.Tasks;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class ProperAsyncCommand : ICommand {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _inProcess;
        private readonly int _additionalDelay;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="canExecute"></param>
        /// <param name="additionalDelay">In milliseconds, optional</param>
        public ProperAsyncCommand([NotNull] Func<object, Task> execute, Func<object, bool> canExecute = null, int additionalDelay = 0) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _additionalDelay = additionalDelay;
            _canExecute = canExecute ?? (o => true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProperCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        public ProperAsyncCommand([NotNull] Func<Task> execute, Func<bool> canExecute = null) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));

            _execute = o => execute();
            if (canExecute == null) {
                _canExecute = o => true;
            } else {
                _canExecute = o => canExecute();
            }
        }

        public ProperAsyncCommand(Func<object, Task> execute, int additionalDelay) : this(execute, null, additionalDelay) { }

        public ProperAsyncCommand(Func<Task> execute, int additionalDelay) : this(o => execute(), null, additionalDelay) { }

        public bool CanExecute(object parameter) {
            return !_inProcess && _canExecute(parameter);
        }

        public async void Execute(object parameter) {
            try {
                _inProcess = true;
                OnCanExecuteChanged();

                await _execute(parameter);

                if (_additionalDelay != 0) {
                    await Task.Delay(_additionalDelay);
                }
            } finally {
                _inProcess = false;
                OnCanExecuteChanged();
            }
        }

        public virtual void OnCanExecuteChanged() {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler CanExecuteChanged;
    }
}