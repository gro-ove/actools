using System;
using System.Threading.Tasks;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class ProperCommand : ICommand {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProperCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        public ProperCommand([NotNull] Action<object> execute, Func<object, bool> canExecute = null) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute ?? (o => true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProperCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        public ProperCommand([NotNull] Action execute, Func<bool> canExecute = null) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = o => execute();
            if (canExecute == null) {
                _canExecute = o => true;
            } else {
                _canExecute = o => canExecute();
            }
        }

        public bool CanExecute(object parameter) {
            return _canExecute(parameter);
        }

        public void Execute(object parameter) {
            _execute(parameter);
        }

        public virtual void OnCanExecuteChanged() {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler CanExecuteChanged;
    }

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

    /// <summary>
    /// The base implementation of a command.
    /// </summary>
    public abstract class CommandBase : ICommand {
        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Raises the <see cref="CanExecuteChanged" /> event.
        /// </summary>
        public virtual void OnCanExecuteChanged() {
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        public virtual bool CanExecute(object parameter) {
            return true;
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object parameter) {
            if (!CanExecute(parameter)) {
                return;
            }
            OnExecute(parameter);
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        protected abstract void OnExecute(object parameter);
    }
}
