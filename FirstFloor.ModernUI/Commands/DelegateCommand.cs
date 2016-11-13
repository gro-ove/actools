using System;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    // based on http://stackoverflow.com/a/1857619/4267982
    public class DelegateCommand : CommandExt {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public DelegateCommand([NotNull] Action execute, Func<bool> canExecute = null, bool isAutomaticRequeryDisabled = false)
                : base(canExecute == null, isAutomaticRequeryDisabled) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

        protected override bool CanExecuteOverride() {
            return _canExecute == null || _canExecute();
        }

        protected override void ExecuteOverride() {
            _execute.Invoke();
        }
    }

    public class DelegateCommand<T> : CommandExt<T> {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public DelegateCommand([NotNull] Action<T> execute, Func<T, bool> canExecute = null, bool isAutomaticRequeryDisabled = false)
                : base(canExecute == null, isAutomaticRequeryDisabled) {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

        protected override bool CanExecuteOverride(T parameter) {
            return _canExecute == null || _canExecute(parameter);
        }

        protected override void ExecuteOverride(T parameter) {
            _execute.Invoke(parameter);
        }
    }
}