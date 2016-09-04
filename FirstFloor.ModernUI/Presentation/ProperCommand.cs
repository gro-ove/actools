using System;
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
}