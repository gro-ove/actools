using System;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Commands {
    public abstract class CommandBase : ICommand {
        public event EventHandler CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) {
            return true;
        }
        
        public void Execute(object parameter) {
            if (!CanExecute(parameter)) return;
            OnExecute(parameter);
        }
        
        protected abstract void OnExecute(object parameter);
    }
}
