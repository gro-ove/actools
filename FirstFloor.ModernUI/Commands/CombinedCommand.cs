using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;

namespace FirstFloor.ModernUI.Presentation {
    public class CombinedCommand : ICommandExt {
        private readonly ICommand _first;
        private readonly ICommand _second;
        private readonly bool _isAutomaticRequeryDisabled;
        private List<WeakReference> _canExecuteChangedHandlers;

        public CombinedCommand(ICommand first, ICommand second, bool isAutomaticRequeryDisabled = false) {
            _first = first;
            _second = second;
            _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
            WeakEventManager<ICommand, EventArgs>.AddHandler(first, nameof(ICommand.CanExecuteChanged), Handler);
            WeakEventManager<ICommand, EventArgs>.AddHandler(second, nameof(ICommand.CanExecuteChanged), Handler);
        }

        public void OnCanExecuteChanged() {
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

        private void Handler(object sender, EventArgs eventArgs) {
            OnCanExecuteChanged();
        }

        public void Execute(object parameter) {
            _first.Execute(parameter);
            _second.Execute(parameter);
        }

        public bool CanExecute(object parameter) {
            return _first.CanExecute(parameter) && _second.CanExecute(parameter);
        }
    }
}